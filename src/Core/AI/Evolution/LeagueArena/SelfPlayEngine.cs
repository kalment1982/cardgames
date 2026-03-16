using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.AI.Bidding;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Logging;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

namespace TractorGame.Core.AI.Evolution.LeagueArena
{
    public sealed class SelfPlayEngine
    {
        private readonly EvolutionConfig _config;
        private readonly IGameLogger _decisionLogger;

        public SelfPlayEngine(EvolutionConfig config)
        {
            _config = config;
            _decisionLogger = new CoreLogger(new JsonLineLogSink(config.LogsPath, "evo-train"));
        }

        public SelfPlayAggregate Evaluate(
            AIStrategyParameters candidate,
            AIStrategyParameters opponent,
            int games,
            int seedBase,
            CancellationToken cancellationToken)
        {
            var bag = new ConcurrentBag<SingleGameOutcome>();
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, _config.MaxParallelism),
                CancellationToken = cancellationToken
            };

            Parallel.For(0, games, options, i =>
            {
                var seed = seedBase + i;
                var candidateOnEven = i % 2 == 0;
                var outcome = PlaySingleGame(seed, candidate, opponent, candidateOnEven, cancellationToken);
                bag.Add(outcome);
            });

            var aggregate = new SelfPlayAggregate();
            foreach (var item in bag)
                aggregate.Outcomes.Add(item);

            return aggregate;
        }

        private SingleGameOutcome PlaySingleGame(
            int seed,
            AIStrategyParameters candidateParameters,
            AIStrategyParameters opponentParameters,
            bool candidateOnEven,
            CancellationToken cancellationToken)
        {
            var outcome = new SingleGameOutcome();
            var game = new Game(seed, NullGameLogger.Instance,
                sessionId: $"evo_sess_g{_config.GenerationNumber}_{seed}",
                gameId: $"evo_game_g{_config.GenerationNumber}_{seed}",
                roundId: $"evo_round_g{_config.GenerationNumber}_{seed}");

            game.StartGame();
            RunDealingAndAutoBidding(game, seed);
            var finalizeResult = game.FinalizeTrumpEx();
            if (!finalizeResult.Success)
            {
                game.FinalizeTrump(PickTrumpSuit(seed));
            }

            var aiConfig = new GameConfig
            {
                LevelRank = game.State.LevelRank,
                TrumpSuit = game.State.TrumpSuit ?? Suit.Spade
            };

            var aiPlayers = BuildAiPlayers(aiConfig, candidateParameters, opponentParameters, candidateOnEven, seed, _decisionLogger);
            var candidateMask = Enumerable.Range(0, 4)
                .Select(index => candidateOnEven ? index % 2 == 0 : index % 2 == 1)
                .ToArray();
            var candidateParity = candidateOnEven ? 0 : 1;
            var candidateIsDealerSide = candidateParity == game.State.DealerIndex % 2;

            var dealer = game.State.DealerIndex;
            var buryCards = aiPlayers[dealer].BuryBottom(
                game.State.PlayerHands[dealer],
                AIRole.Dealer,
                game.BottomCardsSnapshot);
            if (buryCards.Count != 8)
                buryCards = game.State.PlayerHands[dealer].Take(8).ToList();

            var buryResult = game.BuryBottomEx(buryCards);
            if (!buryResult.Success)
            {
                var fallback = game.State.PlayerHands[dealer].Take(8).ToList();
                game.BuryBottomEx(fallback);
            }

            var candidateActions = new HashSet<string>(StringComparer.Ordinal);
            var opponentActions = new HashSet<string>(StringComparer.Ordinal);
            var turnGuard = 0;

            while (game.State.Phase != GamePhase.Finished && turnGuard < _config.MaxTurnsPerGame)
            {
                cancellationToken.ThrowIfCancellationRequested();
                turnGuard++;

                var playerIndex = game.State.CurrentPlayer;
                var hand = game.State.PlayerHands[playerIndex];
                if (hand.Count == 0)
                    break;

                var isCandidate = candidateMask[playerIndex];
                var isLead = game.CurrentTrick.Count == 0;
                var role = ResolveRole(playerIndex, game.State.DealerIndex);
                var handStrength = EvaluateHandStrength(hand, aiConfig);

                var stopwatch = Stopwatch.StartNew();
                var decision = SelectDecision(game, aiPlayers[playerIndex], hand, role, playerIndex, aiConfig);
                stopwatch.Stop();

                var decisionMs = stopwatch.Elapsed.TotalMilliseconds;
                var playResult = game.PlayCardsEx(playerIndex, decision);
                var firstAttemptLegal = playResult.Success;

                if (!firstAttemptLegal)
                {
                    if (isCandidate)
                        outcome.CandidateIllegalDecisions++;
                    else
                        outcome.OpponentIllegalDecisions++;

                    var recovered = TryFallbackPlay(game, playerIndex, hand, aiConfig);
                    if (!recovered)
                        break;
                }

                if (isCandidate)
                {
                    outcome.CandidateDecisions++;
                    outcome.CandidateLatenciesMs.Add(decisionMs);
                    candidateActions.Add(ActionSignature(decision));
                }
                else
                {
                    outcome.OpponentDecisions++;
                    outcome.OpponentLatenciesMs.Add(decisionMs);
                    opponentActions.Add(ActionSignature(decision));
                }

                LogAiDecision(game, playerIndex, decision, isLead, firstAttemptLegal, decisionMs, hand.Count, role, handStrength);
            }

            outcome.CandidateDistinctActions = candidateActions.Count;
            outcome.OpponentDistinctActions = opponentActions.Count;
            outcome.CandidateIsDealerSide = candidateIsDealerSide;
            outcome.CandidateWon = DidCandidateWin(game, candidateOnEven);
            return outcome;
        }

        private static void RunDealingAndAutoBidding(Game game, int seed)
        {
            var levelRank = game.State.LevelRank;
            var bidPolicy = new BidPolicy(seed + 5003);
            var visibleHands = new[]
            {
                new List<Card>(),
                new List<Card>(),
                new List<Card>(),
                new List<Card>()
            };

            while (!game.IsDealingComplete)
            {
                var dealResult = game.DealNextCardEx();
                if (!dealResult.Success)
                    break;

                var step = game.LastDealStep;
                if (step == null || step.IsBottomCard)
                    continue;

                var player = step.PlayerIndex;
                if (player < 0 || player >= visibleHands.Length)
                    continue;

                visibleHands[player].Add(step.Card);
                var bidDecision = bidPolicy.Decide(new BidPolicy.DecisionContext
                {
                    PlayerIndex = player,
                    DealerIndex = game.State.DealerIndex,
                    LevelRank = levelRank,
                    VisibleCards = new List<Card>(visibleHands[player]),
                    RoundIndex = step.PlayerCardCount - 1,
                    CurrentBidPriority = game.CurrentBidPriority,
                    CurrentBidPlayer = game.CurrentBidPlayer
                });
                var bidCards = bidDecision.AttemptCards;
                if (bidCards.Count == 0)
                    continue;

                var bidDetail = bidDecision.ToLogDetail();
                bidDetail["bid_attempt_mode"] = "policy";
                bidDetail["bid_attempt_count"] = bidCards.Count;
                var bidResult = game.BidTrumpEx(player, bidCards, bidDetail);
                if (bidResult.Success)
                    continue;

                if (bidCards.Count > 1)
                {
                    var single = new List<Card> { bidCards[0] };
                    if (game.CanBidTrumpEx(player, single).Success)
                    {
                        bidDetail["bid_attempt_mode"] = "fallback_single";
                        bidDetail["bid_attempt_count"] = 1;
                        game.BidTrumpEx(player, single, bidDetail);
                    }
                }
            }
        }

        private static AIPlayer[] BuildAiPlayers(
            GameConfig config,
            AIStrategyParameters candidateParameters,
            AIStrategyParameters opponentParameters,
            bool candidateOnEven,
            int seed,
            IGameLogger decisionLogger)
        {
            var players = new AIPlayer[4];
            for (var i = 0; i < 4; i++)
            {
                var isCandidate = candidateOnEven ? i % 2 == 0 : i % 2 == 1;
                var parameters = isCandidate ? candidateParameters : opponentParameters;
                players[i] = new AIPlayer(config, AIDifficulty.Hard, seed + i + 97, parameters, decisionLogger);
            }

            return players;
        }

        private static AIRole ResolveRole(int playerIndex, int dealerIndex)
        {
            if (playerIndex == dealerIndex)
                return AIRole.Dealer;

            return playerIndex % 2 == dealerIndex % 2
                ? AIRole.DealerPartner
                : AIRole.Opponent;
        }

        private static Suit PickTrumpSuit(int seed)
        {
            var suits = new[] { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond };
            var index = Math.Abs(seed) % suits.Length;
            return suits[index];
        }

        private static bool DidCandidateWin(Game game, bool candidateOnEven)
        {
            var defenderWon = game.State.DefenderScore >= 80;
            var candidateParity = candidateOnEven ? 0 : 1;
            var dealerParity = game.State.DealerIndex % 2;

            // Defender side is the opposite parity of dealer side.
            var candidateIsDealerSide = candidateParity == dealerParity;
            return candidateIsDealerSide ? !defenderWon : defenderWon;
        }

        private static List<Card> SelectDecision(
            Game game,
            AIPlayer ai,
            List<Card> hand,
            AIRole role,
            int playerIndex,
            GameConfig config)
        {
            var logContext = new AIDecisionLogContext
            {
                SessionId = game.SessionId,
                GameId = game.GameId,
                RoundId = game.RoundId,
                PlayerIndex = playerIndex,
                Actor = $"player_{playerIndex}"
            };

            if (game.CurrentTrick.Count == 0)
            {
                var otherPlayers = Enumerable.Range(0, 4)
                    .Where(i => i != playerIndex)
                    .ToList();
                var knownBottomCards = playerIndex == game.State.DealerIndex
                    ? new List<Card>(game.State.BuriedCards)
                    : null;
                return ai.Lead(hand, role, playerIndex, otherPlayers, knownBottomCards, logContext);
            }

            var leadCards = game.CurrentTrick[0].Cards;
            var currentWinningCards = leadCards;
            var partnerWinning = false;

            if (game.CurrentTrick.Count > 1)
            {
                var judge = new TrickJudge(config);
                var winner = judge.DetermineWinner(game.CurrentTrick);
                var winnerPlay = game.CurrentTrick.LastOrDefault(p => p.PlayerIndex == winner);
                if (winnerPlay != null)
                {
                    currentWinningCards = winnerPlay.Cards;
                    partnerWinning = winnerPlay.PlayerIndex % 2 == playerIndex % 2;
                }
            }

            var trickScore = game.CurrentTrick.Sum(play => play.Cards.Sum(card => card.Score));
            return ai.Follow(hand, leadCards, currentWinningCards, role, partnerWinning, trickScore, logContext);
        }

        private static bool TryFallbackPlay(Game game, int playerIndex, List<Card> hand, GameConfig config)
        {
            if (game.CurrentTrick.Count == 0)
            {
                foreach (var card in hand)
                {
                    var result = game.PlayCardsEx(playerIndex, new List<Card> { card });
                    if (result.Success)
                        return true;
                }

                return false;
            }

            var leadCards = game.CurrentTrick[0].Cards;
            var need = leadCards.Count;
            var followValidator = new FollowValidator(config);

            // Try deterministic fallback first.
            var simple = BuildSimpleFollowFallback(hand, leadCards, config);
            if (simple.Count == need && followValidator.IsValidFollow(hand, leadCards, simple))
                return game.PlayCardsEx(playerIndex, simple).Success;

            // Then randomized sampling.
            var rng = new Random(playerIndex * 193 + hand.Count * 7 + need);
            for (var attempt = 0; attempt < 200; attempt++)
            {
                var trial = hand.OrderBy(_ => rng.Next()).Take(need).ToList();
                if (!followValidator.IsValidFollow(hand, leadCards, trial))
                    continue;

                if (game.PlayCardsEx(playerIndex, trial).Success)
                    return true;
            }

            // Final fallback: bounded DFS search.
            if (TryFindValidCombination(hand, leadCards, config, out var exhaustive))
                return game.PlayCardsEx(playerIndex, exhaustive).Success;

            return false;
        }

        private static List<Card> BuildSimpleFollowFallback(List<Card> hand, List<Card> leadCards, GameConfig config)
        {
            var need = leadCards.Count;
            var leadCategory = config.GetCardCategory(leadCards[0]);
            var leadSuit = leadCards[0].Suit;
            var sameCategory = hand.Where(card => MatchesLeadCategory(card, leadSuit, leadCategory, config)).ToList();

            if (sameCategory.Count >= need)
                return sameCategory.Take(need).ToList();

            var result = new List<Card>(sameCategory);
            var remaining = new List<Card>(hand);
            foreach (var card in sameCategory)
                remaining.Remove(card);
            result.AddRange(remaining.Take(need - result.Count));
            return result;
        }

        private static bool MatchesLeadCategory(Card card, Suit leadSuit, CardCategory leadCategory, GameConfig config)
        {
            if (leadCategory == CardCategory.Trump)
                return config.IsTrump(card);

            return !config.IsTrump(card) && card.Suit == leadSuit;
        }

        private static bool TryFindValidCombination(
            List<Card> hand,
            List<Card> leadCards,
            GameConfig config,
            out List<Card> result)
        {
            var best = new List<Card>();
            var need = leadCards.Count;
            if (need <= 0 || need > hand.Count)
            {
                result = best;
                return false;
            }

            var validator = new FollowValidator(config);
            var current = new List<Card>(need);
            var checkedCount = 0;
            const int maxChecks = 10000;

            bool Dfs(int start)
            {
                if (checkedCount >= maxChecks)
                    return false;

                if (current.Count == need)
                {
                    checkedCount++;
                    if (!validator.IsValidFollow(hand, leadCards, current))
                        return false;

                    best = new List<Card>(current);
                    return true;
                }

                for (var i = start; i < hand.Count; i++)
                {
                    current.Add(hand[i]);
                    if (Dfs(i + 1))
                        return true;
                    current.RemoveAt(current.Count - 1);
                }

                return false;
            }

            var success = Dfs(0);
            result = best;
            return success;
        }

        private static string ActionSignature(List<Card> cards)
        {
            return string.Join(",", cards
                .Select(c => $"{c.Suit}-{c.Rank}")
                .OrderBy(x => x, StringComparer.Ordinal));
        }

        private void LogAiDecision(
            Game game,
            int playerIndex,
            List<Card> selectedCards,
            bool isLead,
            bool isLegal,
            double latencyMs,
            int cardsInHand,
            AIRole role,
            double handStrength)
        {
            var entry = new LogEntry
            {
                SchemaVersion = "1.2",
                Event = "ai.decision",
                Category = LogCategories.Decision,
                Level = LogLevels.Info,
                SessionId = game.SessionId,
                GameId = game.GameId,
                RoundId = game.RoundId,
                Actor = $"player_{playerIndex}",
                Payload = new Dictionary<string, object?>
                {
                    ["ai_player_index"] = playerIndex,
                    ["ai_difficulty"] = _config.EvaluationDifficulty.ToString(),
                    ["decision_type"] = isLead ? "lead" : "follow",
                    ["role"] = role.ToString(),
                    ["candidate_count"] = cardsInHand,
                    ["selected_cards"] = selectedCards.Select(c => new
                    {
                        suit = c.Suit.ToString(),
                        rank = c.Rank.ToString(),
                        score = c.Score
                    }).ToList()
                },
                Metrics = new Dictionary<string, double>
                {
                    ["decision_latency_ms"] = latencyMs,
                    ["is_legal"] = isLegal ? 1 : 0,
                    ["is_blunder"] = isLegal ? 0 : 1,
                    ["is_optimal"] = -1,
                    ["ai_difficulty_level"] = (double)_config.EvaluationDifficulty,
                    ["hand_strength"] = handStrength
                }
            };

            _decisionLogger.Log(entry);
        }

        private static double EvaluateHandStrength(List<Card> hand, GameConfig config)
        {
            if (hand == null || hand.Count == 0)
                return 0;

            var trumpCount = hand.Count(config.IsTrump);
            var pointCount = hand.Count(c => c.Score > 0);
            var highRankCount = hand.Count(card =>
                card.Rank == Rank.Ace || card.Rank == Rank.King || card.Rank == Rank.BigJoker || card.Rank == Rank.SmallJoker);

            var trumpRatio = (double)trumpCount / hand.Count;
            var pointRatio = (double)pointCount / hand.Count;
            var highRatio = (double)highRankCount / hand.Count;

            var score = 0.50 * trumpRatio + 0.30 * highRatio + 0.20 * pointRatio;
            return Math.Clamp(score, 0, 1);
        }
    }
}
