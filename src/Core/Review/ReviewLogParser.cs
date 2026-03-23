using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TractorGame.Core.Review
{
    public sealed class ReviewLogParser
    {
        public IReadOnlyList<ReviewSessionDetail> ParseFile(string filePath, string sourceTag, string sourceLabel)
        {
            var sessions = new Dictionary<string, SessionBuilder>(StringComparer.Ordinal);

            foreach (var rawLine in File.ReadLines(filePath))
            {
                var line = rawLine?.Trim();
                if (string.IsNullOrWhiteSpace(line) || line[0] != '{')
                    continue;

                JsonDocument? doc = null;
                try
                {
                    doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    var eventName = ReadString(root, "event");
                    if (string.IsNullOrWhiteSpace(eventName))
                        continue;

                    var roundId = FirstNonEmpty(
                        ReadString(root, "round_id"),
                        ReadString(root, "game_id"),
                        ReadString(root, "session_id"));
                    if (string.IsNullOrWhiteSpace(roundId))
                        continue;

                    var session = GetOrCreateSession(sessions, roundId!, sourceTag, sourceLabel);
                    session.ObserveRoot(root);

                    if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
                        continue;

                    switch (eventName)
                    {
                        case "game.start":
                            session.DealerIndex = ReadInt(payload, "dealer_index", session.DealerIndex);
                            session.LevelRank = FirstNonEmpty(ReadString(payload, "level_rank"), session.LevelRank) ?? session.LevelRank;
                            session.MergePlayerAiLines(ReadPlayerAiLines(payload));
                            break;
                        case "trump.finalized":
                            session.TrumpSuit = FirstNonEmpty(ReadString(payload, "trump_suit"), session.TrumpSuit) ?? session.TrumpSuit;
                            if (session.BottomCards.Count == 0)
                                session.BottomCards = ReadCards(payload, "bottom_cards");
                            break;
                        case "bury.accept":
                            session.BottomCards = ReadCards(payload, "buried_cards");
                            if (session.BottomCards.Count == 0)
                                session.BottomCards = ReadCards(payload, "bottom_cards");
                            session.DealerIndex = ReadInt(payload, "dealer_index", session.DealerIndex);
                            break;
                        case "game.finish":
                            if (session.BottomCards.Count == 0)
                                session.BottomCards = ReadCards(payload, "bottom_cards");
                            break;
                        case "turn.start":
                            HandleTurnStart(root, payload, session);
                            break;
                        case "play.accept":
                            HandlePlayAccept(root, payload, session);
                            break;
                        case "trick.finish":
                            HandleTrickFinish(root, payload, session);
                            break;
                        case "ai.decision":
                            HandleAiDecision(root, payload, session);
                            break;
                        case "ai.bundle":
                            HandleAiBundle(root, payload, session);
                            break;
                    }
                }
                catch
                {
                    // Ignore malformed lines and continue building the rest of the session.
                }
                finally
                {
                    doc?.Dispose();
                }
            }

            return sessions.Values
                .Select(builder => builder.Build())
                .Where(session => session.Tricks.Count > 0)
                .OrderByDescending(session => session.Summary.StartedAtUtc)
                .ThenBy(session => session.Summary.RoundId, StringComparer.Ordinal)
                .ToList();
        }

        private static SessionBuilder GetOrCreateSession(
            IDictionary<string, SessionBuilder> sessions,
            string roundId,
            string sourceTag,
            string sourceLabel)
        {
            if (!sessions.TryGetValue(roundId, out var session))
            {
                session = new SessionBuilder(roundId, sourceTag, sourceLabel);
                sessions[roundId] = session;
            }

            return session;
        }

        private static void HandleTurnStart(JsonElement root, JsonElement payload, SessionBuilder session)
        {
            if (!ReadBool(payload, "is_lead"))
                return;

            int trickNo = ReadInt(payload, "trick_no");
            if (trickNo <= 0)
                trickNo = ParseTrickNo(ReadString(root, "trick_id"));
            if (trickNo <= 0)
                return;

            var trick = session.GetOrCreateTrick(trickNo, ReadString(root, "trick_id"));
            trick.LeadPlayer = ReadInt(payload, "lead_player", trick.LeadPlayer);
            trick.DefenderScoreBefore = ReadInt(payload, "defender_score", trick.DefenderScoreBefore);
            trick.HandsBefore = ParseHands(payload);

            session.DealerIndex = ReadInt(payload, "dealer_index", session.DealerIndex);
            session.LevelRank = FirstNonEmpty(ReadString(payload, "level_rank"), session.LevelRank) ?? session.LevelRank;
            session.TrumpSuit = FirstNonEmpty(ReadString(payload, "trump_suit"), session.TrumpSuit) ?? session.TrumpSuit;
        }

        private static void HandlePlayAccept(JsonElement root, JsonElement payload, SessionBuilder session)
        {
            int trickNo = ReadInt(payload, "trick_no");
            if (trickNo <= 0)
                trickNo = ReadInt(payload, "trickIndex");
            if (trickNo <= 0)
                trickNo = ParseTrickNo(ReadString(root, "trick_id"));
            if (trickNo <= 0)
                return;

            var trick = session.GetOrCreateTrick(trickNo, ReadString(root, "trick_id"));
            trick.LeadPlayer = ReadInt(payload, "lead_player", trick.LeadPlayer);
            if (trick.LeadPlayer < 0)
                trick.LeadPlayer = ReadInt(payload, "leadPlayer", trick.LeadPlayer);

            if (trick.HandsBefore.Count == 0 && ReadBool(payload, "is_lead"))
            {
                trick.HandsBefore = ParseHands(payload);
            }

            int playerIndex = ReadInt(payload, "player_index", -1);
            if (playerIndex < 0)
                playerIndex = ReadInt(payload, "playerIndex", -1);

            var cards = ReadCards(payload, "cards");
            if (playerIndex >= 0 && cards.Count > 0 && trick.Plays.All(play => play.PlayerIndex != playerIndex))
            {
                trick.Plays.Add(new ReviewPlay
                {
                    PlayerIndex = playerIndex,
                    Order = trick.Plays.Count + 1,
                    Cards = cards
                });
            }
        }

        private static void HandleTrickFinish(JsonElement root, JsonElement payload, SessionBuilder session)
        {
            int trickNo = ReadInt(payload, "trick_no");
            if (trickNo <= 0)
                trickNo = ReadInt(payload, "trickIndex");
            if (trickNo <= 0)
                trickNo = ParseTrickNo(ReadString(root, "trick_id"));
            if (trickNo <= 0)
                return;

            var trick = session.GetOrCreateTrick(trickNo, ReadString(root, "trick_id"));
            trick.WinnerIndex = ReadInt(payload, "winner_index", trick.WinnerIndex);
            if (trick.WinnerIndex < 0)
                trick.WinnerIndex = ReadInt(payload, "winner", trick.WinnerIndex);
            trick.WinnerReason = FirstNonEmpty(ParseWinnerReason(payload), trick.WinnerReason) ?? trick.WinnerReason;
            trick.TrickScore = ReadInt(payload, "trick_score", trick.TrickScore);
            if (trick.TrickScore == 0)
                trick.TrickScore = ReadInt(payload, "trickScore", trick.TrickScore);
            trick.DefenderScoreBefore = ReadInt(payload, "defender_score_before", trick.DefenderScoreBefore);
            if (trick.DefenderScoreBefore == 0)
                trick.DefenderScoreBefore = ReadInt(payload, "defenderScoreBefore", trick.DefenderScoreBefore);
            trick.DefenderScoreAfter = ReadInt(payload, "defender_score_after", trick.DefenderScoreAfter);
            if (trick.DefenderScoreAfter == 0)
                trick.DefenderScoreAfter = ReadInt(payload, "defenderScoreAfter", trick.DefenderScoreAfter);
            var plays = ParsePlays(payload);
            if (plays.Count > 0)
                trick.Plays = plays;

            if (trick.HandsBefore.Count == 0)
            {
                trick.HandsBefore = ParseHands(payload);
            }

            session.DefenderScore = trick.DefenderScoreAfter;
        }

        private static void HandleAiDecision(JsonElement root, JsonElement payload, SessionBuilder session)
        {
            var trickId = ReadString(root, "trick_id");
            int trickNo = ParseTrickNo(trickId);
            if (trickNo <= 0 || string.IsNullOrWhiteSpace(trickId))
                return;

            var trick = session.GetOrCreateTrick(trickNo, trickId);
            var decision = new ReviewDecision
            {
                DecisionTraceId = FirstNonEmpty(ReadString(payload, "decision_trace_id"), ReadString(root, "correlation_id")) ?? string.Empty,
                TurnId = FirstNonEmpty(ReadString(root, "turn_id"), ReadString(payload, "turn_id")) ?? string.Empty,
                PlayerIndex = ReadInt(payload, "player_index", -1),
                Actor = ReadString(root, "actor") ?? string.Empty,
                Phase = ReadString(payload, "phase") ?? ReadString(root, "phase") ?? string.Empty,
                Path = ReadString(payload, "path") ?? string.Empty,
                PhasePolicy = ReadString(payload, "phase_policy") ?? string.Empty,
                PrimaryIntent = ReadString(payload, "primary_intent") ?? string.Empty,
                SecondaryIntent = ReadString(payload, "secondary_intent") ?? string.Empty,
                SelectedReason = ReadString(payload, "selected_reason") ?? string.Empty,
                SelectedCandidateId = ReadString(payload, "selected_candidate_id") ?? string.Empty,
                PlayPosition = ReadInt(payload, "play_position", GuessPlayPositionFromTurnId(ReadString(root, "turn_id"))),
                TriggeredRules = ReadStringList(payload, "triggered_rules"),
                SelectedCards = ReadCards(payload, "selected_cards")
            };

            trick.Decisions.Add(decision);
            session.DecisionsByTraceId[decision.DecisionTraceId] = decision;
            session.DecisionsByTurnId[decision.TurnId] = decision;
        }

        private static void HandleAiBundle(JsonElement root, JsonElement payload, SessionBuilder session)
        {
            var traceId = FirstNonEmpty(ReadString(payload, "decision_trace_id"), ReadString(root, "correlation_id"));
            var turnId = ReadString(root, "turn_id");

            ReviewDecision? decision = null;
            if (!string.IsNullOrWhiteSpace(traceId))
                session.DecisionsByTraceId.TryGetValue(traceId!, out decision);

            if (decision == null && !string.IsNullOrWhiteSpace(turnId))
                session.DecisionsByTurnId.TryGetValue(turnId!, out decision);

            if (decision == null)
                return;

            if (payload.TryGetProperty("bundle", out var bundle) && bundle.ValueKind == JsonValueKind.Object)
                decision.Bundle = CloneElement(bundle);

            if (payload.TryGetProperty("bundle_v30", out var bundleV30) &&
                (bundleV30.ValueKind == JsonValueKind.Object || bundleV30.ValueKind == JsonValueKind.Array))
            {
                decision.BundleV30 = CloneElement(bundleV30);
            }

            if (session.BottomCards.Count == 0)
            {
                var bottomFromTruth = ReadCardsFromNested(payload, "bundle", "truth_snapshot", "buried_cards");
                if (bottomFromTruth.Count > 0)
                    session.BottomCards = bottomFromTruth;
            }

            if (session.BottomCards.Count == 0)
            {
                var bottomFromContext = ReadCardsFromNested(payload, "bundle", "context_snapshot", "visible_bottom_cards");
                if (bottomFromContext.Count > 0)
                    session.BottomCards = bottomFromContext;
            }
        }

        private static List<ReviewPlayerHand> ParseHands(JsonElement payload)
        {
            var result = new List<ReviewPlayerHand>();
            if (!TryReadArray(payload, out var hands, "hands_before_trick", "handsBeforeTrick"))
                return result;

            foreach (var item in hands.EnumerateArray())
            {
                var cards = ReadCards(item, "cards");
                result.Add(new ReviewPlayerHand
                {
                    PlayerIndex = ReadInt(item, "player_index", -1),
                    HandCount = ReadInt(item, "hand_count", cards.Count),
                    Cards = cards
                });
            }

            return result.OrderBy(hand => hand.PlayerIndex).ToList();
        }

        private static List<ReviewPlay> ParsePlays(JsonElement payload)
        {
            var result = new List<ReviewPlay>();
            if (!TryReadArray(payload, out var trickCards, "trick_cards", "plays"))
                return result;

            int order = 0;
            foreach (var item in trickCards.EnumerateArray())
            {
                order++;
                result.Add(new ReviewPlay
                {
                    PlayerIndex = ReadInt(item, "player_index", -1),
                    Order = order,
                    Cards = ReadCards(item, "cards")
                });
            }

            return result;
        }

        private static string? ParseWinnerReason(JsonElement payload)
        {
            if (!payload.TryGetProperty("winner_basis", out var winnerBasis) || winnerBasis.ValueKind != JsonValueKind.Object)
                return null;

            return ReadString(winnerBasis, "reason");
        }

        private static List<ReviewCard> ReadCards(JsonElement parent, string propertyName)
        {
            if (!parent.TryGetProperty(propertyName, out var cards) || cards.ValueKind != JsonValueKind.Array)
                return new List<ReviewCard>();

            return cards.EnumerateArray()
                .Select(ReadCard)
                .Where(card => !string.IsNullOrWhiteSpace(card.Text))
                .ToList();
        }

        private static List<ReviewCard> ReadCardsFromNested(JsonElement parent, string section, string subsection, string propertyName)
        {
            if (!parent.TryGetProperty(section, out var first) || first.ValueKind != JsonValueKind.Object)
                return new List<ReviewCard>();

            if (!first.TryGetProperty(subsection, out var second) || second.ValueKind != JsonValueKind.Object)
                return new List<ReviewCard>();

            return ReadCards(second, propertyName);
        }

        private static ReviewCard ReadCard(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                var text = element.GetString() ?? string.Empty;
                return new ReviewCard { Text = text };
            }

            return new ReviewCard
            {
                Suit = ReadString(element, "suit") ?? string.Empty,
                Rank = ReadString(element, "rank") ?? string.Empty,
                Score = ReadInt(element, "score"),
                Text = FirstNonEmpty(ReadString(element, "text"), element.ToString()) ?? string.Empty
            };
        }

        private static string? ReadString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
                return null;

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => value.ToString()
            };
        }

        private static int ReadInt(JsonElement element, string propertyName, int fallback = 0)
        {
            if (!element.TryGetProperty(propertyName, out var value))
                return fallback;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed))
                return parsed;

            return int.TryParse(value.ToString(), out parsed) ? parsed : fallback;
        }

        private static bool ReadBool(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
                return false;

            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => bool.TryParse(value.ToString(), out var parsed) && parsed
            };
        }

        private static bool TryReadArray(JsonElement element, out JsonElement array, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                if (element.TryGetProperty(propertyName, out array) && array.ValueKind == JsonValueKind.Array)
                    return true;
            }

            array = default;
            return false;
        }

        private static List<string> ReadStringList(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
                return new List<string>();

            return value.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .ToList();
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        private static int ParseTrickNo(string? trickId)
        {
            if (string.IsNullOrWhiteSpace(trickId))
                return 0;

            var digits = new string(trickId.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var trickNo) ? trickNo : 0;
        }

        private static int GuessPlayPositionFromTurnId(string? turnId)
        {
            if (string.IsNullOrWhiteSpace(turnId))
                return 0;

            var digits = new string(turnId.Where(char.IsDigit).ToArray());
            if (!int.TryParse(digits, out var turnNo) || turnNo <= 0)
                return 0;

            return ((turnNo - 1) % 4) + 1;
        }

        private static JsonElement CloneElement(JsonElement element)
        {
            using var doc = JsonDocument.Parse(element.GetRawText());
            return doc.RootElement.Clone();
        }

        private sealed class SessionBuilder
        {
            private readonly SortedDictionary<int, ReviewTrick> _tricks = new();

            public SessionBuilder(string roundId, string sourceTag, string sourceLabel)
            {
                Summary = new ReviewSessionSummary
                {
                    RoundId = roundId,
                    GameId = roundId,
                    SourceTag = sourceTag,
                    SourceLabel = sourceLabel
                };
            }

            public ReviewSessionSummary Summary { get; }
            public Dictionary<string, ReviewDecision> DecisionsByTraceId { get; } = new(StringComparer.Ordinal);
            public Dictionary<string, ReviewDecision> DecisionsByTurnId { get; } = new(StringComparer.Ordinal);
            public List<ReviewCard> BottomCards { get; set; } = new();
            public string LevelRank
            {
                get => Summary.LevelRank;
                set => Summary.LevelRank = value;
            }
            public string TrumpSuit
            {
                get => Summary.TrumpSuit;
                set => Summary.TrumpSuit = value;
            }
            public int DealerIndex
            {
                get => Summary.DealerIndex;
                set => Summary.DealerIndex = value;
            }
            public int DefenderScore
            {
                get => Summary.DefenderScore;
                set => Summary.DefenderScore = value;
            }

            public void ObserveRoot(JsonElement root)
            {
                Summary.GameId = FirstNonEmpty(ReadString(root, "game_id"), Summary.GameId) ?? Summary.GameId;
                Summary.StartedAtUtc = ResolveStartTime(root, Summary.StartedAtUtc);
            }

            public ReviewTrick GetOrCreateTrick(int trickNo, string? trickId)
            {
                if (!_tricks.TryGetValue(trickNo, out var trick))
                {
                    trick = new ReviewTrick
                    {
                        TrickNo = trickNo,
                        TrickId = string.IsNullOrWhiteSpace(trickId) ? $"trick_{trickNo:D4}" : trickId!
                    };
                    _tricks[trickNo] = trick;
                }

                return trick;
            }

            public ReviewSessionDetail Build()
            {
                Summary.TrickCount = _tricks.Count;
                Summary.SessionId = BuildSessionId(Summary.SourceTag, Summary.RoundId);
                var decisions = _tricks.Values.SelectMany(trick => trick.Decisions).ToList();
                Summary.AiLineSummary = BuildAiLineSummary(decisions);
                Summary.PlayerAiLines = MergePlayerAiLines(Summary.PlayerAiLines, BuildPlayerAiLines(decisions));

                return new ReviewSessionDetail
                {
                    Summary = Summary,
                    BottomCards = BottomCards,
                    Tricks = _tricks.Values.ToList()
                };
            }

            public void MergePlayerAiLines(IEnumerable<ReviewPlayerAiLine> playerAiLines)
            {
                Summary.PlayerAiLines = MergePlayerAiLines(Summary.PlayerAiLines, playerAiLines);
            }

            private static string BuildSessionId(string sourceTag, string roundId)
            {
                return Uri.EscapeDataString($"{sourceTag}|{roundId}");
            }

            private static string BuildAiLineSummary(IEnumerable<ReviewDecision> decisions)
            {
                int v30 = decisions.Count(decision => string.Equals(InferAiLine(decision), "V30", StringComparison.OrdinalIgnoreCase));
                int v21 = decisions.Count(decision => string.Equals(InferAiLine(decision), "V21", StringComparison.OrdinalIgnoreCase));

                if (v30 > 0 && v21 > 0)
                    return $"Mixed V30/V21 ({v30}/{v21})";
                if (v30 > 0)
                    return $"RuleAI V30 ({v30})";
                if (v21 > 0)
                    return $"RuleAI V21 ({v21})";

                return "Unknown";
            }

            private static List<ReviewPlayerAiLine> BuildPlayerAiLines(IEnumerable<ReviewDecision> decisions)
            {
                return decisions
                    .Where(decision => decision.PlayerIndex >= 0)
                    .GroupBy(decision => decision.PlayerIndex)
                    .Select(group =>
                    {
                        var aiLine = group
                            .Select(InferAiLine)
                            .Where(line => !string.IsNullOrWhiteSpace(line))
                            .GroupBy(line => line, StringComparer.OrdinalIgnoreCase)
                            .OrderByDescending(item => item.Count())
                            .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                            .Select(item => item.Key)
                            .FirstOrDefault() ?? "Unknown";

                        return new ReviewPlayerAiLine
                        {
                            PlayerIndex = group.Key,
                            AiLine = aiLine
                        };
                    })
                    .OrderBy(item => item.PlayerIndex)
                    .ToList();
            }

            private static List<ReviewPlayerAiLine> MergePlayerAiLines(
                IEnumerable<ReviewPlayerAiLine> preferred,
                IEnumerable<ReviewPlayerAiLine> fallback)
            {
                var merged = new Dictionary<int, ReviewPlayerAiLine>();

                foreach (var item in preferred.Concat(fallback))
                {
                    if (item.PlayerIndex < 0 || string.IsNullOrWhiteSpace(item.AiLine))
                        continue;

                    if (!merged.ContainsKey(item.PlayerIndex))
                    {
                        merged[item.PlayerIndex] = new ReviewPlayerAiLine
                        {
                            PlayerIndex = item.PlayerIndex,
                            AiLine = item.AiLine
                        };
                    }
                }

                return merged.Values
                    .OrderBy(item => item.PlayerIndex)
                    .ToList();
            }

            private static string InferAiLine(ReviewDecision decision)
            {
                if (decision.BundleV30.HasValue)
                    return "V30";

                if (!string.IsNullOrWhiteSpace(decision.Path))
                {
                    if (decision.Path.Contains("v30", StringComparison.OrdinalIgnoreCase))
                        return "V30";
                    if (decision.Path.Contains("v21", StringComparison.OrdinalIgnoreCase))
                        return "V21";
                    if (decision.Path.Contains("legacy", StringComparison.OrdinalIgnoreCase))
                        return "Legacy";
                }

                if (!string.IsNullOrWhiteSpace(decision.PhasePolicy))
                {
                    if (decision.PhasePolicy.Contains("v30", StringComparison.OrdinalIgnoreCase))
                        return "V30";
                    if (decision.PhasePolicy.Contains("v21", StringComparison.OrdinalIgnoreCase))
                        return "V21";
                }

                return "Unknown";
            }

            private static DateTime ResolveStartTime(JsonElement root, DateTime current)
            {
                var timestamp = ReadString(root, "ts_utc");
                if (!DateTime.TryParse(timestamp, out var parsed))
                    return current;

                if (current == default || parsed < current)
                    return parsed;

                return current;
            }
        }

        private static List<ReviewPlayerAiLine> ReadPlayerAiLines(JsonElement payload)
        {
            JsonElement array;
            if (!(payload.TryGetProperty("player_ai_lines", out array) && array.ValueKind == JsonValueKind.Array) &&
                !(payload.TryGetProperty("playerAiLines", out array) && array.ValueKind == JsonValueKind.Array))
            {
                return new List<ReviewPlayerAiLine>();
            }

            var result = new List<ReviewPlayerAiLine>();
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var playerIndex = ReadInt(item, "player_index", ReadInt(item, "playerIndex", -1));
                var aiLine = FirstNonEmpty(ReadString(item, "ai_line"), ReadString(item, "aiLine"));
                if (playerIndex < 0 || string.IsNullOrWhiteSpace(aiLine))
                    continue;

                result.Add(new ReviewPlayerAiLine
                {
                    PlayerIndex = playerIndex,
                    AiLine = aiLine!
                });
            }

            return result;
        }
    }
}
