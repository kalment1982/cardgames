using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using TractorGame.Core.AI;
using TractorGame.Core.AI.Bidding;
using TractorGame.Core.AI.V21;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Logging;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests.V30
{
    [Trait("Category", "SelfPlay")]
    [Trait("Category", "LongRunning")]
    public class V30VsV21AuditTests
    {
        [Fact]
        public void V30_Vs_V21_Audit_20Games()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var artifactRoot = TestPathHelper.ResolveFromRepoRoot("artifacts", "ruleai_v30_vs_v21_eval", timestamp);
            Directory.CreateDirectory(artifactRoot);

            var inMemory = new InMemoryLogSink();
            var sink = new CompositeLogSink(
                inMemory,
                new JsonLineLogSink(Path.Combine(artifactRoot, "raw"), "v30-vs-v21-audit"),
                new MarkdownReplayLogSink(Path.Combine(artifactRoot, "replay"), "v30-vs-v21-audit"),
                new AIDecisionBundleLogSink(Path.Combine(artifactRoot, "decision")));
            var logger = new CoreLogger(sink);

            var attemptSummaries = new List<MixedGameAuditSummary>();
            var finishedSummaries = new List<MixedGameAuditSummary>();
            for (var i = 0; i < 40 && finishedSummaries.Count < 20; i++)
            {
                bool v30OnEvenSeats = i % 2 == 0;
                var summary = PlaySingleGame(
                    seed: 23000 + i,
                    label: $"mixed20_g{i:D2}",
                    v30OnEvenSeats: v30OnEvenSeats,
                    logger: logger);
                attemptSummaries.Add(summary);
                if (summary.Finished)
                    finishedSummaries.Add(summary);
            }

            var entries = inMemory.Entries
                .OrderBy(entry => entry.RoundId, StringComparer.Ordinal)
                .ThenBy(entry => entry.Seq)
                .ToList();

            var summaryPath = Path.Combine(artifactRoot, "mixed20_summary.json");
            var perTrickPath = Path.Combine(artifactRoot, "mixed20_per_trick.md");

            File.WriteAllText(summaryPath, BuildSummaryJson(entries, finishedSummaries, attemptSummaries));
            File.WriteAllText(perTrickPath, BuildPerTrickMarkdown(entries, finishedSummaries));

            Assert.Equal(20, finishedSummaries.Count);
        }

        private static MixedGameAuditSummary PlaySingleGame(int seed, string label, bool v30OnEvenSeats, IGameLogger logger)
        {
            var roundId = $"mixed_{label}_{seed}";
            var game = new Game(seed, logger,
                sessionId: roundId,
                gameId: roundId,
                roundId: roundId);

            game.StartGame();
            RunAutoBidding(game, seed);

            var finalizeResult = game.FinalizeTrumpEx();
            if (!finalizeResult.Success)
                game.FinalizeTrump(PickTrumpSuit(seed));

            var config = new GameConfig
            {
                LevelRank = game.State.LevelRank,
                TrumpSuit = game.State.TrumpSuit
            };

            var strategy = AIStrategyParameters.CreatePreset(AIDifficulty.Hard);
            var players = new AIPlayer[4];
            var seatModes = new string[4];
            for (var i = 0; i < 4; i++)
            {
                bool useV30 = (i % 2 == 0) == v30OnEvenSeats;
                seatModes[i] = useV30 ? "V30" : "V21";
                players[i] = new AIPlayer(
                    config,
                    AIDifficulty.Hard,
                    seed: seed + i + 97,
                    strategyParameters: strategy,
                    decisionLogger: logger,
                    ruleAIOptions: new RuleAIOptions
                    {
                        UseRuleAIV30 = useV30,
                        UseRuleAIV21 = true,
                        EnableShadowCompare = false
                    });
            }

            var dealer = game.State.DealerIndex;
            var buryCards = players[dealer].BuryBottom(
                game.State.PlayerHands[dealer],
                AIRole.Dealer,
                game.BottomCardsSnapshot);
            if (buryCards.Count != 8)
                buryCards = game.State.PlayerHands[dealer].Take(8).ToList();

            var buryResult = game.BuryBottomEx(buryCards);
            if (!buryResult.Success)
            {
                buryCards = game.State.PlayerHands[dealer].Take(8).ToList();
                game.BuryBottomEx(buryCards);
            }

            var turnCounter = 0;
            var firstAttemptRejects = 0;
            var fallbackRecoveries = 0;
            string? unfinishedReason = null;

            while (game.State.Phase != GamePhase.Finished && turnCounter < 200)
            {
                turnCounter++;

                var playerIndex = game.State.CurrentPlayer;
                var hand = game.State.PlayerHands[playerIndex];
                if (hand.Count == 0)
                    break;

                var role = ResolveRole(playerIndex, game.State.DealerIndex);
                var currentWinningPlayer = ResolveCurrentWinningPlayer(game, config);
                var currentWinningCards = ResolveCurrentWinningCards(game, config);
                var trickScore = game.CurrentTrick.Sum(play => play.Cards.Sum(card => card.Score));
                var partnerWinning = currentWinningPlayer >= 0 && currentWinningPlayer % 2 == playerIndex % 2 && game.CurrentTrick.Count > 0;
                var knownBottom = playerIndex == dealer ? new List<Card>(game.State.BuriedCards) : new List<Card>();

                var logContext = new AIDecisionLogContext
                {
                    SessionId = game.SessionId,
                    GameId = game.GameId,
                    RoundId = game.RoundId,
                    TrickId = game.CurrentTrickId,
                    TurnId = $"turn_{turnCounter:D4}",
                    PlayerIndex = playerIndex,
                    Actor = $"player_{playerIndex}",
                    TrickIndex = game.CurrentTrickNo,
                    TurnIndex = turnCounter,
                    PlayPosition = game.CurrentTrick.Count + 1,
                    DealerIndex = game.State.DealerIndex,
                    CurrentWinningPlayer = currentWinningPlayer,
                    DefenderScore = game.State.DefenderScore,
                    BottomPoints = game.State.BuriedCards.Sum(card => card.Score),
                    TruthSnapshot = BuildTruthSnapshot(game, currentWinningCards)
                };

                List<Card> decision;
                if (game.CurrentTrick.Count == 0)
                {
                    decision = players[playerIndex].Lead(
                        hand,
                        role,
                        playerIndex,
                        opponentPositions: null,
                        knownBottomCards: knownBottom,
                        logContext: logContext);
                }
                else
                {
                    decision = players[playerIndex].Follow(
                        hand,
                        game.CurrentTrick[0].Cards,
                        currentWinningCards,
                        role,
                        partnerWinning,
                        trickScore,
                        logContext,
                        visibleBottomCards: knownBottom);
                }

                var playResult = game.PlayCardsEx(playerIndex, decision);
                if (playResult.Success)
                    continue;

                firstAttemptRejects++;
                var recovered = TryFallbackPlay(game, playerIndex, hand, config);
                if (recovered)
                {
                    fallbackRecoveries++;
                    continue;
                }

                unfinishedReason = $"fallback_failed_p{playerIndex}_{game.CurrentTrickId}";
                break;
            }

            if (game.State.Phase != GamePhase.Finished && unfinishedReason == null)
                unfinishedReason = turnCounter >= 200 ? "turn_guard_exceeded" : "unknown";

            var dealerParity = game.State.DealerIndex % 2;
            var winnerSide = game.State.DefenderScore >= 80 ? "defender" : "dealer";
            var winnerParity = winnerSide == "dealer"
                ? dealerParity
                : (dealerParity + 1) % 2;
            var v30Parity = v30OnEvenSeats ? 0 : 1;

            return new MixedGameAuditSummary
            {
                Label = label,
                Seed = seed,
                RoundId = roundId,
                Finished = game.State.Phase == GamePhase.Finished,
                UnfinishedReason = unfinishedReason,
                DealerIndex = game.State.DealerIndex,
                DefenderScore = game.State.DefenderScore,
                WinnerSide = winnerSide,
                WinnerParity = winnerParity,
                WinnerAiLine = winnerParity == v30Parity ? "V30" : "V21",
                V30SeatParity = v30Parity,
                SeatModes = seatModes,
                FirstAttemptRejects = firstAttemptRejects,
                FallbackRecoveries = fallbackRecoveries,
                TrumpSuit = game.State.TrumpSuit?.ToString() ?? "NoTrump",
                LevelRank = game.State.LevelRank.ToString()
            };
        }

        private static Dictionary<string, object?> BuildTruthSnapshot(Game game, IReadOnlyList<Card> currentWinningCards)
        {
            return new Dictionary<string, object?>
            {
                ["dealer_index"] = game.State.DealerIndex,
                ["defender_score"] = game.State.DefenderScore,
                ["trick_no"] = game.CurrentTrickNo,
                ["current_player"] = game.State.CurrentPlayer,
                ["current_trick"] = game.CurrentTrick
                    .Select(play => new Dictionary<string, object?>
                    {
                        ["player_index"] = play.PlayerIndex,
                        ["cards"] = play.Cards.Select(card => card.ToString()).ToList()
                    })
                    .Cast<object?>()
                    .ToList(),
                ["current_winning_cards"] = currentWinningCards.Select(card => card.ToString()).ToList(),
                ["hands_by_player"] = Enumerable.Range(0, 4)
                    .Select(playerIndex => new Dictionary<string, object?>
                    {
                        ["player_index"] = playerIndex,
                        ["hand"] = game.State.PlayerHands[playerIndex]
                            .Select(card => card.ToString())
                            .ToList()
                    })
                    .Cast<object?>()
                    .ToList(),
                ["cards_left_by_player"] = Enumerable.Range(0, 4)
                    .Select(playerIndex => game.State.PlayerHands[playerIndex].Count)
                    .Cast<object?>()
                    .ToList(),
                ["buried_cards"] = game.State.BuriedCards.Select(card => card.ToString()).ToList()
            };
        }

        private static string BuildSummaryJson(
            List<LogEntry> entries,
            List<MixedGameAuditSummary> summaries,
            List<MixedGameAuditSummary> attempts)
        {
            var leadDecisions = entries.Where(IsLeadDecision).ToList();
            var followDecisions = entries.Where(IsFollowDecision).ToList();

            var payload = new Dictionary<string, object?>
            {
                ["attempt_games_total"] = attempts.Count,
                ["total_games"] = summaries.Count,
                ["finished_games"] = summaries.Count(summary => summary.Finished),
                ["unfinished_games"] = attempts.Count(summary => !summary.Finished),
                ["v30_wins"] = summaries.Count(summary => string.Equals(summary.WinnerAiLine, "V30", StringComparison.Ordinal)),
                ["v21_wins"] = summaries.Count(summary => string.Equals(summary.WinnerAiLine, "V21", StringComparison.Ordinal)),
                ["first_attempt_rejects"] = attempts.Sum(summary => summary.FirstAttemptRejects),
                ["fallback_recoveries"] = attempts.Sum(summary => summary.FallbackRecoveries),
                ["terminal_unfinished_reasons"] = attempts
                    .Where(summary => !summary.Finished)
                    .GroupBy(summary => summary.UnfinishedReason ?? "unknown")
                    .ToDictionary(group => group.Key, group => group.Count()),
                ["lead_path_counts"] = CountByString(leadDecisions, "path"),
                ["follow_path_counts"] = CountByString(followDecisions, "path"),
                ["lead_intent_counts"] = CountByString(leadDecisions, "primary_intent"),
                ["follow_intent_counts"] = CountByString(followDecisions, "primary_intent"),
                ["play_reject_count"] = entries.Count(entry => string.Equals(entry.Event, "play.reject", StringComparison.Ordinal)),
                ["game_summaries"] = summaries.Select(summary => new Dictionary<string, object?>
                {
                    ["label"] = summary.Label,
                    ["seed"] = summary.Seed,
                    ["finished"] = summary.Finished,
                    ["unfinished_reason"] = summary.UnfinishedReason,
                    ["dealer_index"] = summary.DealerIndex,
                    ["defender_score"] = summary.DefenderScore,
                    ["winner_side"] = summary.WinnerSide,
                    ["winner_parity"] = summary.WinnerParity,
                    ["winner_ai_line"] = summary.WinnerAiLine,
                    ["v30_seat_parity"] = summary.V30SeatParity,
                    ["seat_modes"] = summary.SeatModes,
                    ["first_attempt_rejects"] = summary.FirstAttemptRejects,
                    ["fallback_recoveries"] = summary.FallbackRecoveries,
                    ["trump_suit"] = summary.TrumpSuit,
                    ["level_rank"] = summary.LevelRank
                }).ToList()
            };

            return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        }

        private static string BuildPerTrickMarkdown(List<LogEntry> entries, List<MixedGameAuditSummary> summaries)
        {
            var sb = new StringBuilder();
            var v30Wins = summaries.Count(summary => string.Equals(summary.WinnerAiLine, "V30", StringComparison.Ordinal));
            var v21Wins = summaries.Count(summary => string.Equals(summary.WinnerAiLine, "V21", StringComparison.Ordinal));
            var rejectCount = entries.Count(entry => string.Equals(entry.Event, "play.reject", StringComparison.Ordinal));
            sb.AppendLine("# RuleAI V30 vs V21 Audit 20");
            sb.AppendLine();
            sb.AppendLine($"- games: {summaries.Count}");
            sb.AppendLine($"- finished: {summaries.Count(summary => summary.Finished)}");
            sb.AppendLine($"- unfinished: {summaries.Count(summary => !summary.Finished)}");
            sb.AppendLine($"- V30 wins: {v30Wins}");
            sb.AppendLine($"- V21 wins: {v21Wins}");
            sb.AppendLine($"- play.reject: {rejectCount}");
            sb.AppendLine();

            foreach (var summary in summaries)
            {
                sb.AppendLine($"## game_{summary.Label}_{summary.Seed} (seed {summary.Seed})");
                sb.AppendLine($"- result: {(summary.Finished ? "finished" : "unfinished")} | winner={summary.WinnerAiLine} | winner_side={summary.WinnerSide} | defender_score={summary.DefenderScore} | rejects={summary.FirstAttemptRejects} | recoveries={summary.FallbackRecoveries}");
                sb.AppendLine($"- seats: p0={summary.SeatModes[0]}, p1={summary.SeatModes[1]}, p2={summary.SeatModes[2]}, p3={summary.SeatModes[3]}");
                sb.AppendLine();

                var trickIds = entries
                    .Where(entry => string.Equals(entry.RoundId, summary.RoundId, StringComparison.Ordinal))
                    .Where(entry => string.Equals(entry.Event, "ai.decision", StringComparison.Ordinal) ||
                                    string.Equals(entry.Event, "trick.finish", StringComparison.Ordinal))
                    .Select(entry => entry.TrickId)
                    .Where(trickId => !string.IsNullOrWhiteSpace(trickId))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(trickId => trickId, StringComparer.Ordinal)
                    .ToList();

                foreach (var trickId in trickIds)
                {
                    var trickFinish = entries.LastOrDefault(entry =>
                        string.Equals(entry.RoundId, summary.RoundId, StringComparison.Ordinal) &&
                        string.Equals(entry.TrickId, trickId, StringComparison.Ordinal) &&
                        string.Equals(entry.Event, "trick.finish", StringComparison.Ordinal));
                    var decisions = entries
                        .Where(entry => string.Equals(entry.RoundId, summary.RoundId, StringComparison.Ordinal) &&
                                        string.Equals(entry.TrickId, trickId, StringComparison.Ordinal) &&
                                        string.Equals(entry.Event, "ai.decision", StringComparison.Ordinal))
                        .OrderBy(entry => entry.Seq)
                        .ToList();

                    var winner = trickFinish != null ? ReadInt(trickFinish.Payload, "winner_index", -1) : -1;
                    var trickScore = trickFinish != null ? ReadInt(trickFinish.Payload, "trick_score", 0) : 0;
                    sb.AppendLine($"### {trickId} | winner={(winner >= 0 ? $"player_{winner}" : "unknown")} | trick_score={trickScore}");

                    foreach (var decision in decisions)
                    {
                        var turnText = decision.TurnId ?? $"seq_{decision.Seq:D4}";
                        var selectedCards = FormatCards(ReadCards(decision.Payload, "selected_cards"));
                        var selectedReason = ReadString(decision.Payload, "selected_reason");
                        var candidateId = ReadString(decision.Payload, "selected_candidate_id");
                        var triggeredRules = ReadStringList(decision.Payload, "triggered_rules");
                        var rulesText = triggeredRules.Count == 0 ? "-" : string.Join(",", triggeredRules);
                        int playerIndex = ReadInt(decision.Payload, "player_index", -1);
                        string aiLine = playerIndex >= 0 && playerIndex < summary.SeatModes.Length
                            ? summary.SeatModes[playerIndex]
                            : "?";
                        var decisionText =
                            $"- {turnText} | {decision.Actor}({aiLine}) | {ReadString(decision.Payload, "phase")} | {ReadString(decision.Payload, "role")} | {ReadString(decision.Payload, "path")} | {ReadString(decision.Payload, "primary_intent")} | {selectedCards} | {selectedReason}";
                        if (!string.IsNullOrWhiteSpace(candidateId))
                            decisionText += $" | candidate={candidateId}";
                        if (rulesText != "-")
                            decisionText += $" | rules={rulesText}";
                        sb.AppendLine(decisionText);
                    }

                    if (trickFinish != null)
                    {
                        var trickCards = ReadTrickFinishCards(trickFinish.Payload);
                        foreach (var play in trickCards)
                            sb.AppendLine($"  - play | player_{play.PlayerIndex} | {FormatCards(play.Cards)}");
                    }

                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private static bool IsLeadDecision(LogEntry entry)
        {
            return string.Equals(entry.Event, "ai.decision", StringComparison.Ordinal) &&
                   string.Equals(ReadString(entry.Payload, "phase"), "Lead", StringComparison.Ordinal);
        }

        private static bool IsFollowDecision(LogEntry entry)
        {
            return string.Equals(entry.Event, "ai.decision", StringComparison.Ordinal) &&
                   string.Equals(ReadString(entry.Payload, "phase"), "Follow", StringComparison.Ordinal);
        }

        private static Dictionary<string, int> CountByString(IEnumerable<LogEntry> entries, string key)
        {
            return entries
                .Select(entry => ReadString(entry.Payload, key))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .GroupBy(value => value!, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        }

        private static AIRole ResolveRole(int playerIndex, int dealerIndex)
        {
            if (playerIndex == dealerIndex)
                return AIRole.Dealer;

            return playerIndex % 2 == dealerIndex % 2
                ? AIRole.DealerPartner
                : AIRole.Opponent;
        }

        private static int ResolveCurrentWinningPlayer(Game game, GameConfig config)
        {
            if (game.CurrentTrick.Count == 0)
                return -1;

            var judge = new TrickJudge(config);
            return judge.DetermineWinner(game.CurrentTrick);
        }

        private static List<Card> ResolveCurrentWinningCards(Game game, GameConfig config)
        {
            if (game.CurrentTrick.Count == 0)
                return new List<Card>();

            var winner = ResolveCurrentWinningPlayer(game, config);
            return game.CurrentTrick
                .FirstOrDefault(play => play.PlayerIndex == winner)?.Cards
                ?.ToList() ?? new List<Card>(game.CurrentTrick[0].Cards);
        }

        private static void RunAutoBidding(Game game, int seed)
        {
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
                    LevelRank = game.State.LevelRank,
                    VisibleCards = new List<Card>(visibleHands[player]),
                    RoundIndex = step.PlayerCardCount - 1,
                    CurrentBidPriority = game.CurrentBidPriority,
                    CurrentBidPlayer = game.CurrentBidPlayer
                });

                var bidCards = bidDecision.AttemptCards;
                if (bidCards.Count == 0)
                    continue;

                var detail = bidDecision.ToLogDetail();
                var bidResult = game.BidTrumpEx(player, bidCards, detail);
                if (!bidResult.Success && bidCards.Count > 1)
                {
                    var single = new List<Card> { bidCards[0] };
                    if (game.CanBidTrumpEx(player, single).Success)
                        game.BidTrumpEx(player, single, detail);
                }
            }
        }

        private static Suit PickTrumpSuit(int seed)
        {
            return new[] { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond }[Math.Abs(seed) % 4];
        }

        private static bool TryFallbackPlay(Game game, int playerIndex, List<Card> hand, GameConfig config)
        {
            if (game.CurrentTrick.Count == 0)
            {
                foreach (var card in hand)
                {
                    if (game.PlayCardsEx(playerIndex, new List<Card> { card }).Success)
                        return true;
                }

                return false;
            }

            var leadCards = game.CurrentTrick[0].Cards;
            var need = leadCards.Count;
            var validator = new FollowValidator(config);
            var simple = BuildSimpleFollowFallback(hand, leadCards, config);
            if (simple.Count == need && validator.IsValidFollow(hand, leadCards, simple))
                return game.PlayCardsEx(playerIndex, simple).Success;

            if (TryFindValidCombination(hand, leadCards, config, out var exhaustive))
                return game.PlayCardsEx(playerIndex, exhaustive).Success;

            return false;
        }

        private static bool MatchesLeadCategory(Card card, Card leadCard, GameConfig config)
        {
            var leadCategory = config.GetCardCategory(leadCard);
            if (leadCategory == CardCategory.Trump)
                return config.IsTrump(card);

            return !config.IsTrump(card) && card.Suit == leadCard.Suit;
        }

        private static string ReadString(Dictionary<string, object?> payload, string key)
        {
            if (!payload.TryGetValue(key, out var value) || value == null)
                return string.Empty;

            return value switch
            {
                string text => text,
                JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonElement element => element.ToString(),
                _ => value.ToString() ?? string.Empty
            };
        }

        private static int ReadInt(Dictionary<string, object?> payload, string key, int fallback)
        {
            if (!payload.TryGetValue(key, out var value) || value == null)
                return fallback;

            return value switch
            {
                int i => i,
                long l => (int)l,
                JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var parsed) => parsed,
                _ when int.TryParse(value.ToString(), out var parsed) => parsed,
                _ => fallback
            };
        }

        private static List<string> ReadStringList(Dictionary<string, object?> payload, string key)
        {
            if (!payload.TryGetValue(key, out var value) || value == null)
                return new List<string>();

            var element = ToElement(value);
            if (element.ValueKind == JsonValueKind.Array)
            {
                return element
                    .EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Select(text => text!)
                    .ToList();
            }

            return value as List<string> ?? new List<string>();
        }

        private static List<string> ReadCards(Dictionary<string, object?> payload, string key)
        {
            if (!payload.TryGetValue(key, out var value) || value == null)
                return new List<string>();

            var element = ToElement(value);
            if (element.ValueKind == JsonValueKind.Array)
            {
                return element
                    .EnumerateArray()
                    .Select(ReadCardText)
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .ToList();
            }

            return new List<string>();
        }

        private static string ReadCardText(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
                return element.GetString() ?? string.Empty;

            if (element.ValueKind != JsonValueKind.Object)
                return element.ToString();

            if (element.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                return text.GetString() ?? string.Empty;

            return element.ToString();
        }

        private static List<TrickPlayLine> ReadTrickFinishCards(Dictionary<string, object?> payload)
        {
            if (!payload.TryGetValue("trick_cards", out var value) || value == null)
                return new List<TrickPlayLine>();

            var element = ToElement(value);
            if (element.ValueKind != JsonValueKind.Array)
                return new List<TrickPlayLine>();

            return element
                .EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .Select(item => new TrickPlayLine
                {
                    PlayerIndex = item.TryGetProperty("player_index", out var playerIndex) && playerIndex.TryGetInt32(out var parsedPlayer)
                        ? parsedPlayer
                        : -1,
                    Cards = item.TryGetProperty("cards", out var cards) && cards.ValueKind == JsonValueKind.Array
                        ? cards.EnumerateArray().Select(ReadCardText).Where(text => !string.IsNullOrWhiteSpace(text)).ToList()
                        : new List<string>()
                })
                .ToList();
        }

        private static JsonElement ToElement(object value)
        {
            if (value is JsonElement element)
                return element;

            return JsonSerializer.SerializeToElement(value);
        }

        private static string FormatCards(IEnumerable<string> cards)
        {
            var list = cards.Where(card => !string.IsNullOrWhiteSpace(card)).ToList();
            return list.Count == 0 ? "-" : string.Join(" ", list);
        }

        private static List<Card> BuildSimpleFollowFallback(List<Card> hand, List<Card> leadCards, GameConfig config)
        {
            var comparer = new CardComparer(config);
            var leadCard = leadCards[0];
            var need = leadCards.Count;
            var sameCategory = hand
                .Where(card => MatchesLeadCategory(card, leadCard, config))
                .OrderBy(card => card, comparer)
                .ToList();

            if (sameCategory.Count >= need)
                return sameCategory.Take(need).ToList();

            var result = new List<Card>(sameCategory);
            var remaining = hand
                .Except(result, CardIdentityComparer.Instance)
                .OrderBy(card => card, comparer)
                .ToList();
            result.AddRange(remaining.Take(need - result.Count));
            return result;
        }

        private static bool TryFindValidCombination(List<Card> hand, List<Card> leadCards, GameConfig config, out List<Card> result)
        {
            var validator = new FollowValidator(config);
            var need = leadCards.Count;
            var cards = hand.ToList();
            foreach (var combo in EnumerateCombinations(cards, need))
            {
                if (validator.IsValidFollow(hand, leadCards, combo))
                {
                    result = combo;
                    return true;
                }
            }

            result = new List<Card>();
            return false;
        }

        private static IEnumerable<List<Card>> EnumerateCombinations(List<Card> items, int choose)
        {
            var buffer = new List<Card>(choose);
            foreach (var combo in EnumerateCombinationsCore(items, choose, 0, buffer))
                yield return combo;
        }

        private static IEnumerable<List<Card>> EnumerateCombinationsCore(List<Card> items, int choose, int start, List<Card> buffer)
        {
            if (buffer.Count == choose)
            {
                yield return new List<Card>(buffer);
                yield break;
            }

            var remaining = choose - buffer.Count;
            for (var i = start; i <= items.Count - remaining; i++)
            {
                buffer.Add(items[i]);
                foreach (var combo in EnumerateCombinationsCore(items, choose, i + 1, buffer))
                    yield return combo;
                buffer.RemoveAt(buffer.Count - 1);
            }
        }

        private sealed class TrickPlayLine
        {
            public int PlayerIndex { get; init; }
            public List<string> Cards { get; init; } = new();
        }

        private sealed class MixedGameAuditSummary
        {
            public string Label { get; init; } = string.Empty;
            public int Seed { get; init; }
            public string RoundId { get; init; } = string.Empty;
            public bool Finished { get; init; }
            public string? UnfinishedReason { get; init; }
            public int DealerIndex { get; init; }
            public int DefenderScore { get; init; }
            public string WinnerSide { get; init; } = string.Empty;
            public int WinnerParity { get; init; }
            public string WinnerAiLine { get; init; } = string.Empty;
            public int V30SeatParity { get; init; }
            public string[] SeatModes { get; init; } = Array.Empty<string>();
            public int FirstAttemptRejects { get; init; }
            public int FallbackRecoveries { get; init; }
            public string TrumpSuit { get; init; } = string.Empty;
            public string LevelRank { get; init; } = string.Empty;
        }

        private sealed class CardIdentityComparer : IEqualityComparer<Card>
        {
            public static CardIdentityComparer Instance { get; } = new();

            public bool Equals(Card? x, Card? y)
            {
                if (ReferenceEquals(x, y))
                    return true;
                if (x is null || y is null)
                    return false;

                return x.Suit == y.Suit && x.Rank == y.Rank && x.Score == y.Score;
            }

            public int GetHashCode(Card obj)
            {
                return HashCode.Combine(obj.Suit, obj.Rank, obj.Score);
            }
        }
    }
}
