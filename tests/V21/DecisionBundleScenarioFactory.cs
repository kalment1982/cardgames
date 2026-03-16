using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TractorGame.Core.AI;
using TractorGame.Core.Models;

namespace TractorGame.Tests.V21
{
    /// <summary>
    /// 将 AI decision bundle JSON 还原为可直接喂给 ScenarioJudge 的场景。
    /// 用于把线上/自测日志快速沉淀成自动化回归用例。
    /// </summary>
    public static class DecisionBundleScenarioFactory
    {
        public static GameScenario FromBundleFile(
            string path,
            IEnumerable<IScenarioExpectation>? expectations = null,
            string? name = null,
            string? description = null,
            bool isKnownDefect = false)
        {
            var resolvedPath = ResolvePath(path);
            using var doc = JsonDocument.Parse(File.ReadAllText(resolvedPath));

            var root = doc.RootElement;
            var payload = root.GetProperty("payload");
            var bundle = payload.GetProperty("bundle");
            var meta = bundle.GetProperty("meta");
            var contextSnapshot = bundle.GetProperty("context_snapshot");
            var decisionFrame = contextSnapshot.TryGetProperty("decision_frame", out var frameElement)
                ? frameElement
                : bundle.TryGetProperty("decision_frame", out frameElement)
                ? frameElement
                : default;

            var phase = ParsePhase(ReadString(meta, "phase") ?? ReadString(contextSnapshot, "phase") ?? "Follow");
            var configElement = contextSnapshot.GetProperty("game_config");

            var scenario = new GameScenario
            {
                Name = name ?? ReadString(meta, "decision_trace_id") ?? Path.GetFileNameWithoutExtension(resolvedPath),
                Description = description ?? $"从 decision bundle 回放: {Path.GetFileName(resolvedPath)}",
                Config = new GameConfig
                {
                    LevelRank = ParseEnum<Rank>(ReadString(configElement, "level_rank") ?? nameof(Rank.Two)),
                    TrumpSuit = ParseNullableEnum<Suit>(ReadString(configElement, "trump_suit")),
                    ThrowFailPenalty = ReadInt(configElement, "throw_fail_penalty"),
                    EnableCounterBottom = ReadBool(configElement, "enable_counter_bottom")
                },
                Role = ParseEnum<AIRole>(ReadString(meta, "role") ?? nameof(AIRole.Opponent)),
                Difficulty = ParseEnum<AIDifficulty>(ReadString(meta, "difficulty") ?? nameof(AIDifficulty.Hard)),
                PlayerIndex = ReadInt(meta, "player_index", ReadInt(contextSnapshot, "player_index", -1)),
                DealerIndex = ReadInt(contextSnapshot, "dealer_index", ReadInt(decisionFrame, "dealer_index", -1)),
                Hand = ReadCards(contextSnapshot, "my_hand"),
                Phase = phase,
                LeadCards = ReadCards(contextSnapshot, "lead_cards"),
                CurrentWinningCards = ReadCards(contextSnapshot, "current_winning_cards"),
                PartnerWinning = ReadBool(contextSnapshot, "partner_winning", ReadBool(decisionFrame, "partner_winning")),
                TrickScore = ReadInt(contextSnapshot, "trick_score", ReadInt(decisionFrame, "current_trick_score")),
                TrickIndex = ReadInt(contextSnapshot, "trick_index", ReadInt(decisionFrame, "trick_index")),
                TurnIndex = ReadInt(contextSnapshot, "turn_index", ReadInt(decisionFrame, "turn_index")),
                PlayPosition = ReadInt(contextSnapshot, "play_position", ReadInt(decisionFrame, "play_position", 1)),
                CurrentWinningPlayer = ReadInt(contextSnapshot, "current_winning_player", ReadInt(decisionFrame, "current_winning_player", -1)),
                DefenderScore = ReadInt(decisionFrame, "defender_score"),
                BottomPoints = ReadInt(decisionFrame, "bottom_points"),
                CardsLeftMin = ReadInt(contextSnapshot, "cards_left_min", ReadInt(decisionFrame, "cards_left_min", -1)),
                VisibleBottomCards = ReadCards(contextSnapshot, "visible_bottom_cards"),
                IsKnownDefect = isKnownDefect,
                Expectations = expectations?.ToList() ?? new List<IScenarioExpectation>()
            };

            return scenario;
        }

        private static string ResolvePath(string path)
        {
            if (Path.IsPathRooted(path) && File.Exists(path))
                return path;

            var candidates = new List<string>();
            if (!Path.IsPathRooted(path))
            {
                candidates.Add(Path.GetFullPath(path, Directory.GetCurrentDirectory()));

                var root = FindRepositoryRoot();
                if (!string.IsNullOrWhiteSpace(root))
                    candidates.Add(Path.Combine(root!, path));
            }

            var resolved = candidates.FirstOrDefault(File.Exists);
            if (resolved != null)
                return resolved;

            throw new FileNotFoundException($"Decision bundle not found: {path}");
        }

        private static string? FindRepositoryRoot()
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    if (File.Exists(Path.Combine(dir.FullName, "TractorGame.csproj")))
                        return dir.FullName;
                    dir = dir.Parent;
                }
            }

            return null;
        }

        private static ScenarioPhase ParsePhase(string phase)
        {
            return phase switch
            {
                "Lead" => ScenarioPhase.Lead,
                "Follow" => ScenarioPhase.Follow,
                "BuryBottom" => ScenarioPhase.Bury,
                _ => throw new NotSupportedException($"Unsupported scenario phase: {phase}")
            };
        }

        private static T ParseEnum<T>(string value) where T : struct
        {
            if (Enum.TryParse<T>(value, ignoreCase: true, out var parsed))
                return parsed;

            throw new InvalidDataException($"Cannot parse {typeof(T).Name}: {value}");
        }

        private static T? ParseNullableEnum<T>(string? value) where T : struct
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return ParseEnum<T>(value);
        }

        private static string? ReadString(JsonElement element, string propertyName)
        {
            if (element.ValueKind == JsonValueKind.Undefined || !element.TryGetProperty(propertyName, out var property))
                return null;

            return property.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => property.GetString(),
                JsonValueKind.Number => property.ToString(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => property.ToString()
            };
        }

        private static int ReadInt(JsonElement element, string propertyName, int fallback = 0)
        {
            if (element.ValueKind == JsonValueKind.Undefined || !element.TryGetProperty(propertyName, out var property))
                return fallback;

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
                return value;

            if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out value))
                return value;

            return fallback;
        }

        private static bool ReadBool(JsonElement element, string propertyName, bool fallback = false)
        {
            if (element.ValueKind == JsonValueKind.Undefined || !element.TryGetProperty(propertyName, out var property))
                return fallback;

            if (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
                return property.GetBoolean();

            if (property.ValueKind == JsonValueKind.String && bool.TryParse(property.GetString(), out var value))
                return value;

            return fallback;
        }

        private static List<Card> ReadCards(JsonElement element, string propertyName)
        {
            if (element.ValueKind == JsonValueKind.Undefined || !element.TryGetProperty(propertyName, out var property))
                return new List<Card>();

            if (property.ValueKind != JsonValueKind.Array)
                return new List<Card>();

            return property.EnumerateArray()
                .Where(card => card.ValueKind == JsonValueKind.Object)
                .Select(ReadCard)
                .ToList();
        }

        private static Card ReadCard(JsonElement element)
        {
            var suit = ParseEnum<Suit>(ReadString(element, "suit") ?? nameof(Suit.Spade));
            var rank = ParseEnum<Rank>(ReadString(element, "rank") ?? nameof(Rank.Two));
            return new Card(suit, rank);
        }
    }
}
