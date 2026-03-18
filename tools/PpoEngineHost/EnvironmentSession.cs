using TractorGame.Core.AI;
using TractorGame.Core.AI.Bidding;
using TractorGame.Core.AI.V21;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Logging;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

namespace PpoEngineHost;

public class EnvironmentSession
{
    private readonly int _seed;
    private readonly HashSet<int> _ppoSeats;
    private readonly HashSet<int> _ruleAiSeats;

    private Game _game = null!;
    private GameConfig _config = null!;
    private AIPlayer?[] _rulePlayers = new AIPlayer?[4];
    private AIStrategyParameters? _ruleParameters;
    private RuleAIOptions? _ruleOptions;

    public EnvironmentSession(int seed, int[] ppoSeats, int[] ruleAiSeats)
    {
        _seed = seed;
        _ppoSeats = new HashSet<int>(ppoSeats);
        _ruleAiSeats = new HashSet<int>(ruleAiSeats);
    }

    public bool IsPpoSeat(int playerIndex) => _ppoSeats.Contains(playerIndex);

    public Game Game => _game;

    /// <summary>
    /// Export all legal actions for the current PPO player.
    /// Returns empty array if game is finished or current player is not a PPO seat.
    /// </summary>
    public object[] ExportLegalActions(int playerIndex)
    {
        if (_game.State.Phase != GamePhase.Playing)
            return Array.Empty<object>();

        if (playerIndex < 0 || !IsPpoSeat(playerIndex))
            return Array.Empty<object>();

        var teacherSelection = IsPpoSeat(playerIndex) ? SelectTeacherDecisionCards(playerIndex) : null;
        var mapped = GetMappedLegalActions(playerIndex, teacherSelection);
        return mapped.Select(m => m.action.ToSerializable(m.slot)).ToArray();
    }

    /// <summary>
    /// Export the RuleAI-selected action for the current PPO player without
    /// mutating game state. Returns null when unavailable.
    /// </summary>
    public object? ExportTeacherAction(int playerIndex)
    {
        if (_game.State.Phase != GamePhase.Playing)
            return null;

        if (playerIndex < 0 || !IsPpoSeat(playerIndex))
            return null;

        var (slot, action) = GetTeacherMappedAction(playerIndex);
        return action.ToSerializable(slot);
    }

    /// <summary>
    /// Build a state snapshot for the given PPO player seat.
    /// If playerIndex is negative or game is finished, uses the first PPO seat.
    /// </summary>
    public object BuildStateSnapshot(int playerIndex)
    {
        var seat = playerIndex >= 0 && _ppoSeats.Contains(playerIndex)
            ? playerIndex
            : _ppoSeats.FirstOrDefault();
        return StateSnapshotBuilder.Build(_game, seat);
    }

    /// <summary>
    /// Keep session config in sync with the game's internal state.
    /// </summary>
    private void SyncConfig()
    {
        _config.LevelRank = _game.State.LevelRank;
        _config.TrumpSuit = _game.State.TrumpSuit;
    }

    /// <summary>
    /// Reset: create game, deal, bid, bury, advance to first PPO decision point.
    /// </summary>
    public (bool done, int currentPlayer, TerminalResult? terminalResult) Reset()
    {
        _lastRecordedTrickNo = 0;

        _game = new Game(_seed, NullGameLogger.Instance,
            sessionId: $"ppo_host_sess_{_seed}",
            gameId: $"ppo_host_game_{_seed}",
            roundId: $"ppo_host_round_{_seed}");

        _game.StartGame();

        // Deal all cards + auto-bid
        RunDealingAndAutoBidding();

        // Finalize trump
        var finalizeResult = _game.FinalizeTrumpEx();
        if (!finalizeResult.Success)
            _game.FinalizeTrump(PickTrumpSuit(_seed));

        _config = new GameConfig
        {
            LevelRank = _game.State.LevelRank,
            TrumpSuit = _game.State.TrumpSuit
        };

        // Create RuleAI players for all seats.
        // PPO seats do not auto-play, but warm-start data collection can query
        // their RuleAI teacher actions through the same runtime path.
        _ruleParameters = ChampionLoader.LoadChampion();
        _ruleOptions = RuleAIOptions.Create(
            useRuleAIV21: true,
            enableShadowCompare: false,
            decisionTraceEnabled: false,
            decisionTraceIncludeTruthSnapshot: false,
            decisionTraceMaxCandidates: 0);

        _rulePlayers = new AIPlayer?[4];
        for (var i = 0; i < 4; i++)
        {
            _rulePlayers[i] = new AIPlayer(
                _config,
                AIDifficulty.Hard,
                _seed + i + 97,
                _ruleParameters,
                NullGameLogger.Instance,
                _ruleOptions);
        }

        // Bury bottom
        var dealer = _game.State.DealerIndex;
        List<Card> buryCards;
        if (_ruleAiSeats.Contains(dealer) && _rulePlayers[dealer] != null)
        {
            var dealerRole = ResolveRole(dealer, _game.State.DealerIndex);
            buryCards = _rulePlayers[dealer]!.BuryBottom(
                _game.State.PlayerHands[dealer], dealerRole, _game.BottomCardsSnapshot);
        }
        else
        {
            buryCards = SelectSimpleBuryCards(_game.State.PlayerHands[dealer], _config, 8);
        }

        if (buryCards.Count != 8)
            buryCards = SelectSimpleBuryCards(_game.State.PlayerHands[dealer], _config, 8);

        var buryResult = _game.BuryBottomEx(buryCards);
        if (!buryResult.Success)
        {
            var fallbackBury = SelectSimpleBuryCards(_game.State.PlayerHands[dealer], _config, 8);
            if (!_game.BuryBottomEx(fallbackBury).Success)
                throw new InvalidOperationException("Failed to bury bottom.");
        }

        // Now in Playing phase. Advance RuleAI seats until we hit a PPO decision point.
        return AdvanceToNextPpoDecisionPoint();
    }

    /// <summary>
    /// Step: execute action_slot, then advance RuleAI seats until next PPO decision point or game end.
    /// </summary>
    public (bool done, int currentPlayer, TerminalResult? terminalResult) Step(int actionSlot)
    {
        if (_game.State.Phase != GamePhase.Playing)
            throw new InvalidOperationException("Game is not in Playing phase.");

        var playerIndex = _game.State.CurrentPlayer;
        if (!IsPpoSeat(playerIndex))
            throw new InvalidOperationException($"Current player {playerIndex} is not a PPO seat.");

        SyncConfig();
        var action = ResolveActionForSlot(playerIndex, actionSlot);
        var cards = new List<Card>(action.Cards);

        var playResult = _game.PlayCardsEx(playerIndex, cards);
        if (!playResult.Success)
            throw new InvalidOperationException($"PlayCardsEx failed for player {playerIndex}: {playResult.ReasonCode}");

        // Record trick for RuleAI memory if trick just completed
        CheckAndRecordCompletedTrick();

        if (_game.State.Phase == GamePhase.Finished)
            return (true, -1, BuildTerminalResult());

        // Advance through RuleAI seats
        return AdvanceToNextPpoDecisionPoint();
    }

    private (bool done, int currentPlayer, TerminalResult? terminalResult) AdvanceToNextPpoDecisionPoint()
    {
        var guard = 0;
        while (_game.State.Phase == GamePhase.Playing && guard < 500)
        {
            guard++;
            var current = _game.State.CurrentPlayer;

            if (IsPpoSeat(current))
                return (false, current, null);

            // RuleAI plays automatically
            PlayRuleAiTurn(current);
            CheckAndRecordCompletedTrick();

            if (_game.State.Phase == GamePhase.Finished)
                return (true, -1, BuildTerminalResult());
        }

        if (guard >= 500)
            throw new InvalidOperationException("Turn guard exceeded while advancing to PPO decision point.");

        // Game finished during advancement
        return (true, -1, BuildTerminalResult());
    }

    private void PlayRuleAiTurn(int playerIndex)
    {
        var hand = _game.State.PlayerHands[playerIndex];
        if (hand.Count == 0) return;

        SyncConfig();
        List<Card> decision;
        var ai = _rulePlayers[playerIndex];

        if (ai != null)
        {
            decision = SelectRuleDecision(playerIndex, ai, hand);
        }
        else
        {
            // Fallback: use LegalPlayResolver
            if (!LegalPlayResolver.TryResolve(_game, playerIndex, _config, out decision!))
                decision = new List<Card> { hand[0] };
        }

        var playResult = _game.PlayCardsEx(playerIndex, decision);
        if (!playResult.Success)
        {
            // Fallback
            if (!LegalPlayResolver.TryResolve(_game, playerIndex, _config, out var legal))
                throw new InvalidOperationException($"No legal fallback for RuleAI player {playerIndex}.");

            var fallbackResult = _game.PlayCardsEx(playerIndex, legal);
            if (!fallbackResult.Success)
                throw new InvalidOperationException($"Fallback play still failed for RuleAI player {playerIndex}.");
        }
    }

    private List<Card> SelectRuleDecision(int playerIndex, AIPlayer ai, List<Card> hand)
    {
        var role = ResolveRole(playerIndex, _game.State.DealerIndex);
        var logContext = new AIDecisionLogContext
        {
            SessionId = _game.SessionId,
            GameId = _game.GameId,
            RoundId = _game.RoundId,
            PlayerIndex = playerIndex,
            DealerIndex = _game.State.DealerIndex,
            TrickIndex = _game.CurrentTrickNo,
            TurnIndex = _game.CurrentTurnNo,
            PlayPosition = _game.CurrentTrick.Count + 1,
            CurrentWinningPlayer = _game.CurrentTrick.Count > 0 ? DetermineCurrentWinner() : -1,
            DefenderScore = _game.State.DefenderScore,
            BottomPoints = _game.State.BuriedCards.Sum(c => c.Score),
            Actor = $"player_{playerIndex}"
        };

        if (_game.CurrentTrick.Count == 0)
        {
            var others = Enumerable.Range(0, 4).Where(i => i != playerIndex).ToList();
            var knownBottom = playerIndex == _game.State.DealerIndex
                ? new List<Card>(_game.State.BuriedCards)
                : null;
            return ai.Lead(hand, role, playerIndex, others, knownBottom, logContext);
        }

        var leadCards = _game.CurrentTrick[0].Cards;
        var currentWinningCards = leadCards;
        var partnerWinning = false;
        if (_game.CurrentTrick.Count > 1)
        {
            var winner = DetermineCurrentWinner();
            var winnerPlay = _game.CurrentTrick.LastOrDefault(p => p.PlayerIndex == winner);
            if (winnerPlay != null)
            {
                currentWinningCards = winnerPlay.Cards;
                partnerWinning = winnerPlay.PlayerIndex % 2 == playerIndex % 2;
            }
        }

        var trickScore = _game.CurrentTrick.Sum(p => p.Cards.Sum(c => c.Score));
        return ai.Follow(hand, leadCards, currentWinningCards, role, partnerWinning, trickScore, logContext);
    }

    private int _lastRecordedTrickNo;

    private List<(int slot, LegalAction action)> GetMappedLegalActions(int playerIndex, List<Card>? preferredCards = null)
    {
        SyncConfig();
        if (preferredCards == null && IsPpoSeat(playerIndex))
        {
            preferredCards = SelectTeacherDecisionCards(playerIndex);
        }

        var actions = LegalActionExporter.Export(_game, playerIndex);
        if (preferredCards is { Count: > 0 })
        {
            var preferredKey = BuildCandidateKey(preferredCards);
            if (!actions.Any(action => string.Equals(BuildCandidateKey(action.Cards), preferredKey, StringComparison.Ordinal)))
            {
                actions.Add(LegalActionExporter.CreateForCurrentState(_game, playerIndex, preferredCards));
            }
        }

        try
        {
            return ActionSlotMapper.MapAllActions(actions, _config);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.StartsWith("ACTION_SPACE_OVERFLOW:", StringComparison.Ordinal) ||
            ex.Message.StartsWith("ACTION_SLOT_CONFLICT:", StringComparison.Ordinal))
        {
            throw new PpoEngineProtocolException(ErrorCodes.ActionSpaceOverflow, ex.Message);
        }
    }

    private LegalAction ResolveActionForSlot(int playerIndex, int actionSlot)
    {
        if (actionSlot < 0 || actionSlot >= ActionSlotMapper.ActionDim)
        {
            throw new PpoEngineProtocolException(
                ErrorCodes.InvalidActionSlot,
                $"Action slot {actionSlot} is outside [0, {ActionSlotMapper.ActionDim - 1}].");
        }

        var mapped = GetMappedLegalActions(playerIndex);
        foreach (var (slot, action) in mapped)
        {
            if (slot == actionSlot)
                return action;
        }

        throw new PpoEngineProtocolException(
            ErrorCodes.ActionSlotNotLegal,
            $"Action slot {actionSlot} is not legal for player {playerIndex} in the current state.");
    }

    private (int slot, LegalAction action) GetTeacherMappedAction(int playerIndex)
    {
        var selectedCards = SelectTeacherDecisionCards(playerIndex);
        var selectedKey = BuildCandidateKey(selectedCards);
        var mappedActions = GetMappedLegalActions(playerIndex, selectedCards);
        foreach (var mapped in mappedActions)
        {
            if (string.Equals(BuildCandidateKey(mapped.action.Cards), selectedKey, StringComparison.Ordinal))
                return mapped;
        }

        var mappedKeys = string.Join(", ", mappedActions
            .Select(item => $"{item.slot}:{BuildCandidateKey(item.action.Cards)}"));

        throw new InvalidOperationException(
            $"Teacher action for player {playerIndex} not found in mapped legal actions. " +
            $"selected={selectedKey}; legal=[{mappedKeys}]");
    }

    private List<Card> SelectTeacherDecisionCards(int playerIndex)
    {
        var ai = _rulePlayers[playerIndex];
        if (ai == null)
            throw new InvalidOperationException($"Teacher RuleAI for player {playerIndex} is not initialized.");

        var hand = _game.State.PlayerHands[playerIndex];
        return SelectRuleDecision(playerIndex, ai, hand);
    }

    private void CheckAndRecordCompletedTrick()
    {
        if (_game.LastCompletedTrickNo <= _lastRecordedTrickNo) return;

        _lastRecordedTrickNo = _game.LastCompletedTrickNo;
        var completed = _game.LastCompletedTrick;
        foreach (var ai in _rulePlayers)
            ai?.RecordTrick(completed);
    }

    private int DetermineCurrentWinner()
    {
        if (_game.CurrentTrick.Count == 0) return -1;
        var judge = new TrickJudge(_config);
        return judge.DetermineWinner(_game.CurrentTrick);
    }

    private TerminalResult BuildTerminalResult()
    {
        var defenderScore = _game.State.DefenderScore;
        var dealerIndex = _game.State.DealerIndex;
        var levelMgr = new LevelManager();
        var levelResult = levelMgr.DetermineLevelChange(defenderScore, _game.State.LevelRank);

        var defenderWon = defenderScore >= 80;
        var winnerTeam = defenderWon ? "defender" : "dealer";

        // Determine if PPO team won
        // PPO seats share a parity; check if any PPO seat is on the dealer's team
        var ppoOnDealerTeam = _ppoSeats.Any(s => s % 2 == dealerIndex % 2);
        var ppoTeamWon = ppoOnDealerTeam ? !defenderWon : defenderWon;

        var ppoTeamScore = ppoOnDealerTeam
            ? Math.Max(0, 200 - defenderScore)
            : defenderScore;

        var ppoTeamLevelGain = ppoTeamWon ? levelResult.LevelChange : 0;

        var nextDealerIndex = levelMgr.DetermineNextDealerIndex(dealerIndex, levelResult.Winner);

        return new TerminalResult
        {
            WinnerTeam = winnerTeam,
            MyTeamWon = ppoTeamWon,
            MyTeamFinalScore = ppoTeamScore,
            MyTeamLevelGain = ppoTeamLevelGain,
            DefenderScore = defenderScore,
            NextDealer = nextDealerIndex
        };
    }

    // ─── Helpers (ported from PpoBridgeEval) ───

    private void RunDealingAndAutoBidding()
    {
        var levelRank = _game.State.LevelRank;
        var bidPolicy = new BidPolicy(_seed + 5003);
        var visibleHands = new[]
        {
            new List<Card>(), new List<Card>(),
            new List<Card>(), new List<Card>()
        };

        while (!_game.IsDealingComplete)
        {
            var dealResult = _game.DealNextCardEx();
            if (!dealResult.Success) break;

            var step = _game.LastDealStep;
            if (step == null || step.IsBottomCard) continue;

            var player = step.PlayerIndex;
            visibleHands[player].Add(step.Card);

            var bidDecision = bidPolicy.Decide(new BidPolicy.DecisionContext
            {
                PlayerIndex = player,
                DealerIndex = _game.State.DealerIndex,
                LevelRank = levelRank,
                VisibleCards = new List<Card>(visibleHands[player]),
                RoundIndex = step.PlayerCardCount - 1,
                CurrentBidPriority = _game.CurrentBidPriority,
                CurrentBidPlayer = _game.CurrentBidPlayer
            });

            var attempt = bidDecision.AttemptCards;
            if (attempt.Count == 0) continue;

            var bidDetail = bidDecision.ToLogDetail();
            bidDetail["bid_attempt_mode"] = "policy";
            bidDetail["bid_attempt_count"] = attempt.Count;
            if (_game.BidTrumpEx(player, attempt, bidDetail).Success) continue;

            if (attempt.Count > 1)
            {
                var single = new List<Card> { attempt[0] };
                if (_game.CanBidTrumpEx(player, single).Success)
                {
                    bidDetail["bid_attempt_mode"] = "fallback_single";
                    bidDetail["bid_attempt_count"] = 1;
                    _game.BidTrumpEx(player, single, bidDetail);
                }
            }
        }
    }

    private static Suit PickTrumpSuit(int seed)
    {
        var suits = new[] { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond };
        return suits[Math.Abs(seed) % suits.Length];
    }

    private static AIRole ResolveRole(int playerIndex, int dealerIndex)
    {
        if (playerIndex == dealerIndex) return AIRole.Dealer;
        return playerIndex % 2 == dealerIndex % 2 ? AIRole.DealerPartner : AIRole.Opponent;
    }

    private static List<Card> SelectSimpleBuryCards(List<Card> hand, GameConfig config, int count)
    {
        var comparer = new CardComparer(config);
        return hand
            .OrderBy(c => config.IsTrump(c) ? 1 : 0)
            .ThenBy(c => c.Score > 0 ? 1 : 0)
            .ThenBy(c => c, comparer)
            .Take(count)
            .ToList();
    }

    private static string BuildCandidateKey(IEnumerable<Card> cards)
    {
        return string.Join(",", cards
            .OrderBy(card => (int)card.Suit)
            .ThenBy(card => (int)card.Rank)
            .Select(card => $"{(int)card.Suit}-{(int)card.Rank}"));
    }
}
