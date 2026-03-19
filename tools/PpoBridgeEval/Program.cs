using System.Diagnostics;
using System.Text;
using System.Text.Json;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Logging;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

const int defaultGames = 50;
var games = defaultGames;
var checkpointPath = "rl_training/latest.pt";

for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--games" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedGames))
    {
        games = parsedGames;
        i++;
    }
    else if (args[i] == "--checkpoint" && i + 1 < args.Length)
    {
        checkpointPath = args[i + 1];
        i++;
    }
}

var root = Directory.GetCurrentDirectory();
var absoluteCheckpoint = Path.GetFullPath(Path.Combine(root, checkpointPath));
var absoluteBridgeScript = Path.GetFullPath(Path.Combine(root, "rl_training/bridge_server.py"));

if (!File.Exists(absoluteCheckpoint))
    throw new FileNotFoundException($"PPO checkpoint not found: {absoluteCheckpoint}");
if (!File.Exists(absoluteBridgeScript))
    throw new FileNotFoundException($"Bridge server not found: {absoluteBridgeScript}");

var ruleParameters = ChampionLoader.LoadChampion();
var ruleOptions = RuleAIOptions.Create(
    useRuleAIV21: true,
    enableShadowCompare: false,
    decisionTraceEnabled: false,
    decisionTraceIncludeTruthSnapshot: false,
    decisionTraceMaxCandidates: 0);

Console.WriteLine("PPO vs RuleAI V2.1 Evaluation");
Console.WriteLine($"Games: {games}");
Console.WriteLine($"Checkpoint: {absoluteCheckpoint}");
Console.WriteLine("Constraints:");
Console.WriteLine("- PPO checkpoint is trained on simplified Python environment.");
Console.WriteLine("- PPO bridge controls lead choice only within training-compatible single/pair action space.");
Console.WriteLine("- PPO follow path uses training-style deterministic follow, then formal-rule legality fallback if needed.");
Console.WriteLine("- Bidding uses existing auto bid flow; PPO dealer bury uses simple heuristic bury.");
Console.WriteLine();

using var bridge = new PpoBridgeClient(root, absoluteBridgeScript, absoluteCheckpoint);

var summary = new MatchSummary();
for (var gameIndex = 0; gameIndex < games; gameIndex++)
{
    var seed = 500000 + gameIndex * 17;
    var ppoOnEven = gameIndex % 2 == 0;
    var outcome = PlaySingleGame(seed, ppoOnEven, bridge, ruleParameters, ruleOptions);
    summary.Add(outcome);
    Console.WriteLine(
        $"Game {gameIndex + 1:D2}/{games}: winner={(outcome.PpoWon ? "PPO" : "RuleAI")}, " +
        $"ppoOn{(ppoOnEven ? "Even" : "Odd")}, dealer={outcome.DealerIndex}, " +
        $"defenderScore={outcome.DefenderScore}, ppoDealerSide={outcome.PpoDealerSide}");
}

Console.WriteLine();
Console.WriteLine("Summary");
Console.WriteLine($"PPO wins: {summary.PpoWins}/{summary.TotalGames} ({summary.PpoWinRate:P2})");
Console.WriteLine($"RuleAI wins: {summary.RuleWins}/{summary.TotalGames} ({summary.RuleWinRate:P2})");
Console.WriteLine($"PPO as dealer side: {summary.PpoDealerSideWins}/{summary.PpoDealerSideGames} ({summary.PpoDealerSideWinRate:P2})");
Console.WriteLine($"PPO as defender side: {summary.PpoDefenderSideWins}/{summary.PpoDefenderSideGames} ({summary.PpoDefenderSideWinRate:P2})");
Console.WriteLine($"Average defender score: {summary.AverageDefenderScore:F2}");

static SingleGameOutcome PlaySingleGame(
    int seed,
    bool ppoOnEven,
    PpoBridgeClient bridge,
    AIStrategyParameters ruleParameters,
    RuleAIOptions ruleOptions)
{
    var outcome = new SingleGameOutcome();
    var game = new Game(seed, NullGameLogger.Instance,
        sessionId: $"ppo_eval_sess_{seed}",
        gameId: $"ppo_eval_game_{seed}",
        roundId: $"ppo_eval_round_{seed}");

    game.StartGame();
    RunDealingAndAutoBidding(game, seed);
    var finalizeResult = game.FinalizeTrumpEx();
    if (!finalizeResult.Success)
        game.FinalizeTrump(PickTrumpSuit(seed));

    var config = new GameConfig
    {
        LevelRank = game.State.LevelRank,
        TrumpSuit = game.State.TrumpSuit
    };

    var rulePlayers = new AIPlayer[4];
    for (var i = 0; i < 4; i++)
    {
        if (IsPpoSeat(i, ppoOnEven))
            continue;

        rulePlayers[i] = new AIPlayer(
            config,
            AIDifficulty.Hard,
            seed + i + 97,
            ruleParameters,
            NullGameLogger.Instance,
            ruleOptions);
    }

    var dealer = game.State.DealerIndex;
    var dealerRole = ResolveRole(dealer, game.State.DealerIndex);
    var buryCards = IsPpoSeat(dealer, ppoOnEven)
        ? SelectSimpleBuryCards(game.State.PlayerHands[dealer], config, 8)
        : rulePlayers[dealer].BuryBottom(game.State.PlayerHands[dealer], dealerRole, game.BottomCardsSnapshot);
    if (buryCards.Count != 8)
        buryCards = SelectSimpleBuryCards(game.State.PlayerHands[dealer], config, 8);

    var buryResult = game.BuryBottomEx(buryCards);
    if (!buryResult.Success)
    {
        var fallbackBury = SelectSimpleBuryCards(game.State.PlayerHands[dealer], config, 8);
        if (!game.BuryBottomEx(fallbackBury).Success)
            throw new InvalidOperationException("Failed to bury bottom for evaluation game.");
    }

    var playedTricks = new List<List<TrickPlay>>();
    var recordedTrickNo = 0;
    var turnGuard = 0;

    while (game.State.Phase != GamePhase.Finished && turnGuard < 500)
    {
        turnGuard++;
        var playerIndex = game.State.CurrentPlayer;
        var hand = game.State.PlayerHands[playerIndex];
        if (hand.Count == 0)
            break;

        List<Card> decision;
        if (IsPpoSeat(playerIndex, ppoOnEven))
        {
            decision = game.CurrentTrick.Count == 0
                ? SelectPpoLead(game, config, playerIndex, hand, playedTricks, bridge)
                : SelectPpoFollow(game, config, playerIndex, hand);
        }
        else
        {
            decision = SelectRuleDecision(game, rulePlayers[playerIndex], config, playerIndex, hand);
        }

        var playResult = game.PlayCardsEx(playerIndex, decision);
        if (!playResult.Success)
        {
            if (!LegalPlayResolver.TryResolve(game, playerIndex, config, out var legal))
                throw new InvalidOperationException($"No legal fallback for player {playerIndex}.");

            var fallbackResult = game.PlayCardsEx(playerIndex, legal);
            if (!fallbackResult.Success)
                throw new InvalidOperationException($"Fallback play still failed for player {playerIndex}.");
        }

        if (game.LastCompletedTrickNo > recordedTrickNo)
        {
            recordedTrickNo = game.LastCompletedTrickNo;
            var completed = game.LastCompletedTrick;
            playedTricks.Add(CloneTrick(completed));

            foreach (var player in rulePlayers.Where(player => player != null))
                player.RecordTrick(completed);
        }
    }

    if (turnGuard >= 500)
        throw new InvalidOperationException("Turn guard reached during evaluation.");

    var ppoParity = ppoOnEven ? 0 : 1;
    var dealerParity = game.State.DealerIndex % 2;
    var ppoDealerSide = ppoParity == dealerParity;
    var defenderWon = game.State.DefenderScore >= 80;
    var ppoWon = ppoDealerSide ? !defenderWon : defenderWon;

    outcome.PpoWon = ppoWon;
    outcome.PpoDealerSide = ppoDealerSide;
    outcome.DealerIndex = game.State.DealerIndex;
    outcome.DefenderScore = game.State.DefenderScore;
    return outcome;
}

static List<Card> SelectRuleDecision(Game game, AIPlayer ai, GameConfig config, int playerIndex, List<Card> hand)
{
    var role = ResolveRole(playerIndex, game.State.DealerIndex);
    var logContext = new AIDecisionLogContext
    {
        SessionId = game.SessionId,
        GameId = game.GameId,
        RoundId = game.RoundId,
        PlayerIndex = playerIndex,
        DealerIndex = game.State.DealerIndex,
        TrickIndex = game.CurrentTrickNo,
        TurnIndex = game.CurrentTurnNo,
        PlayPosition = game.CurrentTrick.Count + 1,
        CurrentWinningPlayer = game.CurrentTrick.Count > 0 ? DetermineCurrentWinner(game, config) : -1,
        DefenderScore = game.State.DefenderScore,
        BottomPoints = game.State.BuriedCards.Sum(card => card.Score),
        Actor = $"player_{playerIndex}"
    };

    if (game.CurrentTrick.Count == 0)
    {
        var others = Enumerable.Range(0, 4).Where(index => index != playerIndex).ToList();
        var knownBottom = playerIndex == game.State.DealerIndex ? new List<Card>(game.State.BuriedCards) : null;
        return ai.Lead(hand, role, playerIndex, others, knownBottom, logContext);
    }

    var leadCards = game.CurrentTrick[0].Cards;
    var currentWinningCards = leadCards;
    var partnerWinning = false;
    if (game.CurrentTrick.Count > 1)
    {
        var winner = DetermineCurrentWinner(game, config);
        var winnerPlay = game.CurrentTrick.LastOrDefault(play => play.PlayerIndex == winner);
        if (winnerPlay != null)
        {
            currentWinningCards = winnerPlay.Cards;
            partnerWinning = winnerPlay.PlayerIndex % 2 == playerIndex % 2;
        }
    }

    var trickScore = game.CurrentTrick.Sum(play => play.Cards.Sum(card => card.Score));
    return ai.Follow(hand, leadCards, currentWinningCards, role, partnerWinning, trickScore, logContext);
}

static List<Card> SelectPpoLead(
    Game game,
    GameConfig config,
    int playerIndex,
    List<Card> hand,
    List<List<TrickPlay>> playedTricks,
    PpoBridgeClient bridge)
{
    var candidates = BuildPpoLeadCandidates(hand);
    if (candidates.Count == 0)
    {
        if (LegalPlayResolver.TryResolve(game, playerIndex, config, out var fallback))
            return fallback;
        return new List<Card> { hand[0] };
    }

    var payload = BuildStatePayload(game, playerIndex, hand, playedTricks);
    var actionIndex = bridge.SelectActionIndex(payload, playerIndex, candidates.Count);
    if (actionIndex < 0 || actionIndex >= candidates.Count)
        actionIndex = 0;

    var selected = candidates[actionIndex];
    var validator = new PlayValidator(config);
    if (validator.IsValidPlay(hand, selected))
        return selected;

    if (LegalPlayResolver.TryResolve(game, playerIndex, config, out var legal))
        return legal;

    return new List<Card> { hand[0] };
}

static List<Card> SelectPpoFollow(Game game, GameConfig config, int playerIndex, List<Card> hand)
{
    var leadCards = game.CurrentTrick[0].Cards;
    var selected = BuildTrainingStyleFollow(hand, leadCards);
    var validator = new FollowValidator(config);
    if (selected.Count == leadCards.Count && validator.IsValidFollow(hand, leadCards, selected))
        return selected;

    if (LegalPlayResolver.TryResolve(game, playerIndex, config, out var legal))
        return legal;

    throw new InvalidOperationException($"No valid PPO follow fallback for player {playerIndex}.");
}

static List<List<Card>> BuildPpoLeadCandidates(List<Card> hand)
{
    var candidates = new List<List<Card>>();
    var seenSingles = new HashSet<string>(StringComparer.Ordinal);
    foreach (var card in hand)
    {
        var key = BuildCardKey(card);
        if (seenSingles.Add(key))
            candidates.Add(new List<Card> { card });
    }

    var pairCounts = new Dictionary<string, int>(StringComparer.Ordinal);
    var pairCards = new Dictionary<string, List<Card>>(StringComparer.Ordinal);
    foreach (var card in hand)
    {
        var key = BuildCardKey(card);
        if (!pairCounts.ContainsKey(key))
        {
            pairCounts[key] = 0;
            pairCards[key] = new List<Card>();
        }

        pairCounts[key]++;
        pairCards[key].Add(card);
    }

    foreach (var entry in pairCounts)
    {
        if (entry.Value >= 2)
            candidates.Add(pairCards[entry.Key].Take(2).ToList());
    }

    return candidates;
}

static List<Card> BuildTrainingStyleFollow(List<Card> hand, List<Card> leadCards)
{
    var needCount = leadCards.Count;
    var leadSuit = leadCards[0].Suit;
    var sameSuit = hand.Where(card => card.Suit == leadSuit).ToList();

    if (sameSuit.Count >= needCount)
        return sameSuit.Take(needCount).ToList();

    var result = new List<Card>(sameSuit);
    var otherCards = hand.Where(card => card.Suit != leadSuit).ToList();
    result.AddRange(otherCards.Take(needCount - result.Count));
    return result;
}

static PpoStatePayload BuildStatePayload(Game game, int playerIndex, List<Card> hand, List<List<TrickPlay>> playedTricks)
{
    var hands = new List<List<CardPayload>>();
    for (var i = 0; i < 4; i++)
    {
        hands.Add(i == playerIndex
            ? hand.Select(ToCardPayload).ToList()
            : new List<CardPayload>());
    }

    return new PpoStatePayload(
        Dealer: game.State.DealerIndex,
        CurrentPlayer: playerIndex,
        TrumpSuit: game.State.TrumpSuit?.ToString(),
        LevelRank: game.State.LevelRank.ToString(),
        DealerScore: EstimateDealerScore(game.State.DefenderScore),
        DefenderScore: game.State.DefenderScore,
        TricksRemaining: Math.Max(0, 25 - playedTricks.Count),
        Hands: hands,
        PlayedTricks: playedTricks.Select(trick => trick.Select(ToPlayPayload).ToList()).ToList(),
        CurrentTrick: game.CurrentTrick.Select(ToPlayPayload).ToList());
}

static int EstimateDealerScore(int defenderScore)
{
    return Math.Max(0, 200 - defenderScore);
}

static PlayPayload ToPlayPayload(TrickPlay play)
{
    return new PlayPayload(play.PlayerIndex, play.Cards.Select(ToCardPayload).ToList());
}

static CardPayload ToCardPayload(Card card)
{
    return new CardPayload(card.Suit.ToString(), card.Rank.ToString());
}

static List<Card> SelectSimpleBuryCards(List<Card> hand, GameConfig config, int count)
{
    var comparer = new CardComparer(config);
    return hand
        .OrderBy(card => config.IsTrump(card) ? 1 : 0)
        .ThenBy(card => card.Score > 0 ? 1 : 0)
        .ThenBy(card => card, comparer)
        .Take(count)
        .ToList();
}

static bool IsPpoSeat(int playerIndex, bool ppoOnEven)
{
    return ppoOnEven ? playerIndex % 2 == 0 : playerIndex % 2 == 1;
}

static AIRole ResolveRole(int playerIndex, int dealerIndex)
{
    if (playerIndex == dealerIndex)
        return AIRole.Dealer;

    return playerIndex % 2 == dealerIndex % 2
        ? AIRole.DealerPartner
        : AIRole.Opponent;
}

static int DetermineCurrentWinner(Game game, GameConfig config)
{
    if (game.CurrentTrick.Count == 0)
        return -1;

    var judge = new TrickJudge(config);
    return judge.DetermineWinner(game.CurrentTrick);
}

static List<TrickPlay> CloneTrick(List<TrickPlay> trick)
{
    return trick
        .Select(play => new TrickPlay(play.PlayerIndex, new List<Card>(play.Cards)))
        .ToList();
}

static void RunDealingAndAutoBidding(Game game, int seed)
{
    var levelRank = game.State.LevelRank;
    var bidPolicy = new TractorGame.Core.AI.Bidding.BidPolicy(seed + 5003);
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
        visibleHands[player].Add(step.Card);
        var bidDecision = bidPolicy.Decide(new TractorGame.Core.AI.Bidding.BidPolicy.DecisionContext
        {
            PlayerIndex = player,
            DealerIndex = game.State.DealerIndex,
            LevelRank = levelRank,
            VisibleCards = new List<Card>(visibleHands[player]),
            RoundIndex = step.PlayerCardCount - 1,
            CurrentBidPriority = game.CurrentBidPriority,
            CurrentBidPlayer = game.CurrentBidPlayer
        });

        var attempt = bidDecision.AttemptCards;
        if (attempt.Count == 0)
            continue;

        var bidDetail = bidDecision.ToLogDetail();
        bidDetail["bid_attempt_mode"] = "policy";
        bidDetail["bid_attempt_count"] = attempt.Count;
        if (game.BidTrumpEx(player, attempt, bidDetail).Success)
            continue;

        if (attempt.Count > 1)
        {
            var single = new List<Card> { attempt[0] };
            if (game.CanBidTrumpEx(player, single).Success)
            {
                bidDetail["bid_attempt_mode"] = "fallback_single";
                bidDetail["bid_attempt_count"] = 1;
                game.BidTrumpEx(player, single, bidDetail);
            }
        }
    }
}

static Suit PickTrumpSuit(int seed)
{
    var suits = new[] { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond };
    return suits[Math.Abs(seed) % suits.Length];
}

static string BuildCardKey(Card card)
{
    return $"{card.Suit}-{card.Rank}";
}

sealed class MatchSummary
{
    public int TotalGames { get; private set; }
    public int PpoWins { get; private set; }
    public int RuleWins => TotalGames - PpoWins;
    public int PpoDealerSideGames { get; private set; }
    public int PpoDealerSideWins { get; private set; }
    public int PpoDefenderSideGames { get; private set; }
    public int PpoDefenderSideWins { get; private set; }
    public int DefenderScoreSum { get; private set; }

    public double PpoWinRate => TotalGames == 0 ? 0 : (double)PpoWins / TotalGames;
    public double RuleWinRate => TotalGames == 0 ? 0 : (double)RuleWins / TotalGames;
    public double PpoDealerSideWinRate => PpoDealerSideGames == 0 ? 0 : (double)PpoDealerSideWins / PpoDealerSideGames;
    public double PpoDefenderSideWinRate => PpoDefenderSideGames == 0 ? 0 : (double)PpoDefenderSideWins / PpoDefenderSideGames;
    public double AverageDefenderScore => TotalGames == 0 ? 0 : (double)DefenderScoreSum / TotalGames;

    public void Add(SingleGameOutcome outcome)
    {
        TotalGames++;
        DefenderScoreSum += outcome.DefenderScore;
        if (outcome.PpoWon)
            PpoWins++;

        if (outcome.PpoDealerSide)
        {
            PpoDealerSideGames++;
            if (outcome.PpoWon)
                PpoDealerSideWins++;
        }
        else
        {
            PpoDefenderSideGames++;
            if (outcome.PpoWon)
                PpoDefenderSideWins++;
        }
    }
}

sealed class SingleGameOutcome
{
    public bool PpoWon { get; set; }
    public bool PpoDealerSide { get; set; }
    public int DealerIndex { get; set; }
    public int DefenderScore { get; set; }
}

sealed class PpoBridgeClient : IDisposable
{
    private readonly Process _process;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object _gate = new();

    public PpoBridgeClient(string workingDirectory, string bridgeScript, string checkpointPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "python3",
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        startInfo.ArgumentList.Add("-u");
        startInfo.ArgumentList.Add(bridgeScript);
        startInfo.ArgumentList.Add("--checkpoint");
        startInfo.ArgumentList.Add(checkpointPath);

        _process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start PPO bridge process.");
        var readyLine = _process.StandardOutput.ReadLine();
        if (string.IsNullOrWhiteSpace(readyLine))
        {
            var stderr = _process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"PPO bridge failed to start. stderr: {stderr}");
        }

        var ready = JsonSerializer.Deserialize<BridgeResponse>(readyLine, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to parse PPO bridge ready message.");
        if (!ready.Ok)
            throw new InvalidOperationException($"PPO bridge startup error: {ready.Error}");
    }

    public int SelectActionIndex(PpoStatePayload state, int playerIndex, int legalActionCount)
    {
        lock (_gate)
        {
            var request = new BridgeRequest(
                Type: "select",
                PlayerIndex: playerIndex,
                LegalActionCount: legalActionCount,
                Deterministic: true,
                State: state);
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            _process.StandardInput.WriteLine(json);
            _process.StandardInput.Flush();

            var responseLine = _process.StandardOutput.ReadLine();
            if (string.IsNullOrWhiteSpace(responseLine))
            {
                var stderr = _process.StandardError.ReadToEnd();
                throw new InvalidOperationException($"PPO bridge returned empty response. stderr: {stderr}");
            }

            var response = JsonSerializer.Deserialize<BridgeResponse>(responseLine, _jsonOptions)
                ?? throw new InvalidOperationException("Failed to parse PPO bridge response.");
            if (!response.Ok)
                throw new InvalidOperationException($"PPO bridge error: {response.Error}");

            return response.ActionIndex ?? 0;
        }
    }

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited)
            {
                var json = JsonSerializer.Serialize(new { type = "close" }, _jsonOptions);
                _process.StandardInput.WriteLine(json);
                _process.StandardInput.Flush();
                if (!_process.WaitForExit(2000))
                    _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        finally
        {
            _process.Dispose();
        }
    }
}

record CardPayload(string Suit, string Rank);
record PlayPayload(int PlayerIndex, List<CardPayload> Cards);
record PpoStatePayload(
    int Dealer,
    int CurrentPlayer,
    string? TrumpSuit,
    string LevelRank,
    int DealerScore,
    int DefenderScore,
    int TricksRemaining,
    List<List<CardPayload>> Hands,
    List<List<PlayPayload>> PlayedTricks,
    List<PlayPayload> CurrentTrick);
record BridgeRequest(
    string Type,
    int PlayerIndex,
    int LegalActionCount,
    bool Deterministic,
    PpoStatePayload State);
sealed class BridgeResponse
{
    public bool Ok { get; set; }
    public string? Type { get; set; }
    public int? ActionIndex { get; set; }
    public string? Error { get; set; }
}
