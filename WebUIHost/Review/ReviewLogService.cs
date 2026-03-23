using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace WebUIHost.Review;

public sealed class ReviewLogService
{
    private const int RecentIndexFileLimit = 8;
    private const int RecentIndexFallbackFileLimit = 12;

    private readonly string _repoRoot;
    private readonly object _sync = new();
    private ReviewIndexCache? _indexCache;
    private ReviewIndexCache? _recentIndexCache;

    public ReviewLogService(string repoRoot)
    {
        _repoRoot = repoRoot;
    }

    public ReviewSessionsResponse GetSessions(int? limit = null)
    {
        var index = ShouldUseRecentIndex(limit)
            ? GetOrBuildRecentIndex()
            : GetOrBuildIndex();
        var sessions = index.Sessions;
        if (limit.HasValue && limit.Value > 0)
            sessions = sessions.Take(limit.Value).ToList();

        return new ReviewSessionsResponse
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Total = index.Sessions.Count,
            Sessions = sessions
        };
    }

    public ReviewSessionDetailResponse? GetSessionDetail(string sessionId, out string? error)
    {
        error = null;
        if (!ReviewSessionIdCodec.TryDecode(sessionId, out var sourceTag, out var roundId))
        {
            error = "Invalid sessionId.";
            return null;
        }

        if (!TryGetAggregate(sessionId, out var aggregate))
        {
            error = "Session not found.";
            return null;
        }

        // Safety check in case caller provides mismatched decoded id.
        if (!string.Equals(aggregate.SourceTag, sourceTag, StringComparison.Ordinal) ||
            !string.Equals(aggregate.RoundId, roundId, StringComparison.Ordinal))
        {
            error = "Session id does not match indexed session.";
            return null;
        }

        var warnings = new List<string>();
        var trickBuilders = new Dictionary<int, TrickBuilder>();
        var bottomCards = new List<string>();

        foreach (var filePath in aggregate.SourceFiles.OrderBy(path => path, StringComparer.Ordinal))
        {
            if (!File.Exists(filePath))
                continue;

            int lineNo = 0;
            foreach (var rawLine in File.ReadLines(filePath))
            {
                lineNo++;
                var line = NormalizeLine(rawLine);
                if (string.IsNullOrWhiteSpace(line) || line[0] != '{')
                    continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    var eventRoundId = ReadString(root, "round_id");
                    if (!string.Equals(eventRoundId, roundId, StringComparison.Ordinal))
                        continue;

                    var eventName = ReadString(root, "event");
                    if (string.IsNullOrWhiteSpace(eventName))
                        continue;

                    string? trickId = ReadString(root, "trick_id");
                    string? turnId = ReadString(root, "turn_id");
                    long seq = ReadLong(root, "seq");
                    int trickNo = 0;

                    root.TryGetProperty("payload", out var payload);

                    if (string.Equals(eventName, "turn.start", StringComparison.Ordinal))
                    {
                        trickNo = NormalizeTrickNo(ReadInt(payload, "trick_no", -1));
                        if (trickNo <= 0)
                            trickNo = ParseTrickNo(trickId);

                        if (trickNo > 0)
                        {
                            var trick = EnsureTrick(trickBuilders, trickNo, trickId);
                            trick.LeadPlayer = ResolveInt(trick.LeadPlayer, ReadInt(payload, "lead_player", -1));
                            if (ReadBool(payload, "is_lead"))
                            {
                                var parsedHands = ParseHandsBefore(payload);
                                if (parsedHands.Count > 0)
                                    trick.HandsBefore = parsedHands;
                            }
                        }

                        continue;
                    }

                    if (string.Equals(eventName, "play.accept", StringComparison.Ordinal))
                    {
                        trickNo = ReadTrickNo(payload);
                        if (trickNo <= 0)
                            trickNo = ParseTrickNo(trickId);
                        if (trickNo > 0)
                        {
                            var trick = EnsureTrick(trickBuilders, trickNo, trickId);
                            trick.LeadPlayer = ResolveInt(
                                trick.LeadPlayer,
                                ReadInt(payload, "lead_player", -1),
                                ReadInt(payload, "leadPlayer", -1));
                            if (trick.HandsBefore.Count == 0 && ReadBool(payload, "is_lead"))
                            {
                                var parsedHands = ParseHandsBefore(payload);
                                if (parsedHands.Count > 0)
                                    trick.HandsBefore = parsedHands;
                            }

                            if (!trick.HasFinalPlays)
                            {
                                var playerIndex = ReadInt(payload, "player_index", -1);
                                if (playerIndex < 0)
                                    playerIndex = ReadInt(payload, "playerIndex", -1);
                                var cards = ParseCardTexts(payload, "cards");
                                if (playerIndex >= 0 && cards.Count > 0)
                                {
                                    trick.Plays.Add(new ReviewPlay
                                    {
                                        PlayOrder = trick.Plays.Count + 1,
                                        PlayerIndex = playerIndex,
                                        Cards = cards
                                    });
                                }
                            }
                        }

                        continue;
                    }

                    if (string.Equals(eventName, "trick.finish", StringComparison.Ordinal))
                    {
                        trickNo = ReadTrickNo(payload);
                        if (trickNo <= 0)
                            trickNo = ParseTrickNo(trickId);
                        if (trickNo <= 0)
                            continue;

                        var trick = EnsureTrick(trickBuilders, trickNo, trickId);
                        trick.WinnerIndex = ResolveInt(
                            trick.WinnerIndex,
                            ReadInt(payload, "winner_index", -1),
                            ReadInt(payload, "winner", -1));
                        trick.TrickScore = ResolveInt(
                            trick.TrickScore,
                            ReadInt(payload, "trick_score"),
                            ReadInt(payload, "trickScore"));
                        trick.DefenderScoreBefore = ResolveInt(
                            trick.DefenderScoreBefore,
                            ReadInt(payload, "defender_score_before"),
                            ReadInt(payload, "defenderScoreBefore"));
                        trick.DefenderScoreAfter = ResolveInt(
                            trick.DefenderScoreAfter,
                            ReadInt(payload, "defender_score_after"),
                            ReadInt(payload, "defenderScoreAfter"));
                        trick.WinnerReason ??= FirstNonEmpty(
                            ReadString(payload, "winner_basis", "reason"),
                            ReadString(payload, "reason"),
                            ReadString(payload, "type"),
                            ReadString(payload, "raw_type"));
                        trick.LeadPlayer = ResolveInt(trick.LeadPlayer, ReadInt(payload, "winner_basis", "lead_player_index", -1));

                        var trickCards = ParseTrickCards(payload);
                        if (trickCards.Count > 0)
                        {
                            trick.Plays = trickCards;
                            trick.HasFinalPlays = true;
                        }

                        if (trick.HandsBefore.Count == 0)
                        {
                            var parsedHands = ParseHandsBefore(payload);
                            if (parsedHands.Count > 0)
                                trick.HandsBefore = parsedHands;
                        }

                        continue;
                    }

                    if (string.Equals(eventName, "bury.accept", StringComparison.Ordinal))
                    {
                        var buried = FirstNonEmptyList(
                            ParseCardTexts(payload, "buried_cards"),
                            ParseCardTexts(payload, "bottom_cards"));
                        if (buried.Count > 0)
                            bottomCards = buried;
                        continue;
                    }

                    if (string.Equals(eventName, "trump.finalized", StringComparison.Ordinal) ||
                        string.Equals(eventName, "game.finish", StringComparison.Ordinal))
                    {
                        if (bottomCards.Count == 0)
                        {
                            var resolved = ParseCardTexts(payload, "bottom_cards");
                            if (resolved.Count > 0)
                                bottomCards = resolved;
                        }
                        continue;
                    }

                    if (string.Equals(eventName, "ai.decision", StringComparison.Ordinal))
                    {
                        trickNo = ParseTrickNo(trickId);
                        if (trickNo <= 0)
                            continue;

                        var bundle = ReadClonedElement(payload, "bundle");
                        var bundleV30 = ReadClonedElement(payload, "bundle_v30");
                        var selectedCards = FirstNonEmptyList(
                            ParseCardTexts(payload, "selected_cards"),
                            ParseCardTextsPath(payload, "bundle", "selected_action", "cards"),
                            ParseCardTextsPath(payload, "bundle_v30", "selected_cards"));
                        var triggeredRules = FirstNonEmptyList(
                            ParseStringArray(payload, "triggered_rules"),
                            ParseStringArrayPath(payload, "bundle_v30", "triggered_rules"),
                            ParseStringArrayPath(payload, "bundle", "intent_snapshot", "tags"));

                        var trick = EnsureTrick(trickBuilders, trickNo, trickId);
                        var decision = new ReviewDecision
                        {
                            DecisionTraceId = FirstNonEmpty(ReadString(payload, "decision_trace_id"), ReadString(root, "correlation_id")),
                            TurnId = FirstNonEmpty(
                                turnId,
                                ReadString(payload, "turn_id"),
                                ReadPathString(payload, "bundle", "meta", "turn_id"),
                                ReadPathString(payload, "bundle_v30", "turn_id")),
                            PlayPosition = ResolvePlayPosition(
                                ReadInt(payload, "play_position"),
                                ReadPathInt(payload, 0, "bundle", "context_snapshot", "play_position"),
                                ReadPathInt(payload, 0, "bundle_v30", "context_snapshot", "play_position")),
                            PlayerIndex = ResolveInt(
                                ReadInt(payload, "player_index", -1),
                                ReadPathInt(payload, -1, "bundle", "meta", "player_index"),
                                ReadPathInt(payload, -1, "bundle_v30", "player_index")),
                            AiLine = InferAiLine(
                                FirstNonEmpty(
                                    ReadString(payload, "path"),
                                    ReadPathString(payload, "bundle", "meta", "path"),
                                    ReadPathString(payload, "bundle", "algorithm_trace", "path"),
                                    ReadPathString(payload, "bundle_v30", "path")),
                                FirstNonEmpty(
                                    ReadString(payload, "phase_policy"),
                                    ReadPathString(payload, "bundle", "intent_snapshot", "phase_policy"),
                                    ReadPathString(payload, "bundle_v30", "phase_policy")),
                                bundleV30.HasValue && bundleV30.Value.ValueKind != JsonValueKind.Undefined && bundleV30.Value.ValueKind != JsonValueKind.Null),
                            Phase = FirstNonEmpty(
                                ReadString(payload, "phase"),
                                ReadString(root, "phase"),
                                ReadPathString(payload, "bundle", "meta", "phase"),
                                ReadPathString(payload, "bundle_v30", "phase")),
                            Path = FirstNonEmpty(
                                ReadString(payload, "path"),
                                ReadPathString(payload, "bundle", "meta", "path"),
                                ReadPathString(payload, "bundle", "algorithm_trace", "path"),
                                ReadPathString(payload, "bundle_v30", "path")),
                            PhasePolicy = FirstNonEmpty(
                                ReadString(payload, "phase_policy"),
                                ReadPathString(payload, "bundle", "intent_snapshot", "phase_policy"),
                                ReadPathString(payload, "bundle_v30", "phase_policy")),
                            PrimaryIntent = FirstNonEmpty(
                                ReadString(payload, "primary_intent"),
                                ReadPathString(payload, "bundle", "intent_snapshot", "primary_intent"),
                                ReadPathString(payload, "bundle_v30", "primary_intent")),
                            SecondaryIntent = FirstNonEmpty(
                                ReadString(payload, "secondary_intent"),
                                ReadPathString(payload, "bundle", "intent_snapshot", "secondary_intent"),
                                ReadPathString(payload, "bundle_v30", "secondary_intent")),
                            SelectedReason = FirstNonEmpty(
                                ReadString(payload, "selected_reason"),
                                ReadPathString(payload, "bundle", "intent_snapshot", "selected_reason"),
                                ReadPathString(payload, "bundle_v30", "selected_reason")),
                            SelectedCandidateId = FirstNonEmpty(
                                ReadString(payload, "selected_candidate_id"),
                                ReadPathString(payload, "bundle_v30", "selected_candidate_id")),
                            TriggeredRules = triggeredRules,
                            SelectedCards = selectedCards,
                            Bundle = bundle,
                            BundleV30 = bundleV30
                        };
                        trick.UpsertDecision(decision, seq);
                        continue;
                    }

                    if (string.Equals(eventName, "ai.bundle", StringComparison.Ordinal))
                    {
                        trickNo = ParseTrickNo(trickId);
                        if (trickNo <= 0)
                            trickNo = NormalizeTrickNo(ReadPathInt(payload, 0, "bundle", "context_snapshot", "trick_index"));
                        if (trickNo <= 0)
                            trickNo = NormalizeTrickNo(ReadPathInt(payload, 0, "bundle_v30", "context_snapshot", "trick_index"));
                        if (trickNo <= 0)
                            continue;

                        var bundle = ReadClonedElement(payload, "bundle");
                        var bundleV30 = ReadClonedElement(payload, "bundle_v30");
                        var selectedCards = FirstNonEmptyList(
                            ParseCardTexts(payload, "selected_cards"),
                            ParseCardTextsPath(payload, "bundle", "selected_action", "cards"),
                            ParseCardTextsPath(payload, "bundle_v30", "selected_cards"));
                        var triggeredRules = FirstNonEmptyList(
                            ParseStringArray(payload, "triggered_rules"),
                            ParseStringArrayPath(payload, "bundle_v30", "triggered_rules"),
                            ParseStringArrayPath(payload, "bundle", "intent_snapshot", "tags"));

                        var trick = EnsureTrick(trickBuilders, trickNo, trickId);
                        var decision = new ReviewDecision
                        {
                            DecisionTraceId = FirstNonEmpty(ReadString(payload, "decision_trace_id"), ReadString(root, "correlation_id")),
                            TurnId = FirstNonEmpty(
                                turnId,
                                ReadString(payload, "turn_id"),
                                ReadPathString(payload, "bundle", "meta", "turn_id"),
                                ReadPathString(payload, "bundle_v30", "turn_id")),
                            PlayPosition = ResolvePlayPosition(
                                ReadInt(payload, "play_position"),
                                ReadPathInt(payload, 0, "bundle", "context_snapshot", "play_position"),
                                ReadPathInt(payload, 0, "bundle_v30", "context_snapshot", "play_position")),
                            PlayerIndex = ResolveInt(
                                ReadInt(payload, "player_index", -1),
                                ReadPathInt(payload, -1, "bundle", "meta", "player_index"),
                                ReadPathInt(payload, -1, "bundle_v30", "player_index")),
                            AiLine = InferAiLine(
                                FirstNonEmpty(
                                    ReadString(payload, "path"),
                                    ReadPathString(payload, "bundle", "meta", "path"),
                                    ReadPathString(payload, "bundle", "algorithm_trace", "path"),
                                    ReadPathString(payload, "bundle_v30", "path")),
                                FirstNonEmpty(
                                    ReadString(payload, "phase_policy"),
                                    ReadPathString(payload, "bundle", "intent_snapshot", "phase_policy"),
                                    ReadPathString(payload, "bundle_v30", "phase_policy")),
                                bundleV30.HasValue && bundleV30.Value.ValueKind != JsonValueKind.Undefined && bundleV30.Value.ValueKind != JsonValueKind.Null),
                            Phase = FirstNonEmpty(
                                ReadString(payload, "phase"),
                                ReadString(root, "phase"),
                                ReadPathString(payload, "bundle", "meta", "phase"),
                                ReadPathString(payload, "bundle_v30", "phase")),
                            Path = FirstNonEmpty(
                                ReadString(payload, "path"),
                                ReadPathString(payload, "bundle", "meta", "path"),
                                ReadPathString(payload, "bundle", "algorithm_trace", "path"),
                                ReadPathString(payload, "bundle_v30", "path")),
                            PhasePolicy = FirstNonEmpty(
                                ReadString(payload, "phase_policy"),
                                ReadPathString(payload, "bundle", "intent_snapshot", "phase_policy"),
                                ReadPathString(payload, "bundle_v30", "phase_policy")),
                            PrimaryIntent = FirstNonEmpty(
                                ReadString(payload, "primary_intent"),
                                ReadPathString(payload, "bundle", "intent_snapshot", "primary_intent"),
                                ReadPathString(payload, "bundle_v30", "primary_intent")),
                            SecondaryIntent = FirstNonEmpty(
                                ReadString(payload, "secondary_intent"),
                                ReadPathString(payload, "bundle", "intent_snapshot", "secondary_intent"),
                                ReadPathString(payload, "bundle_v30", "secondary_intent")),
                            SelectedReason = FirstNonEmpty(
                                ReadString(payload, "selected_reason"),
                                ReadPathString(payload, "bundle", "intent_snapshot", "selected_reason"),
                                ReadPathString(payload, "bundle_v30", "selected_reason")),
                            SelectedCandidateId = FirstNonEmpty(
                                ReadString(payload, "selected_candidate_id"),
                                ReadPathString(payload, "bundle_v30", "selected_candidate_id")),
                            TriggeredRules = triggeredRules,
                            SelectedCards = selectedCards,
                            Bundle = bundle,
                            BundleV30 = bundleV30
                        };
                        trick.UpsertDecision(decision, seq);

                        if (bottomCards.Count == 0)
                        {
                            bottomCards = FirstNonEmptyList(
                                ParseCardTextsPath(payload, "bundle", "truth_snapshot", "buried_cards"),
                                ParseCardTextsPath(payload, "bundle", "truth_snapshot", "bottom_cards"),
                                ParseCardTextsPath(payload, "bundle", "context_snapshot", "visible_bottom_cards"),
                                ParseCardTextsPath(payload, "bundle_v30", "truth_snapshot", "buried_cards"),
                                ParseCardTextsPath(payload, "bundle_v30", "truth_snapshot", "bottom_cards"),
                                ParseCardTextsPath(payload, "bundle_v30", "context_snapshot", "visible_bottom_cards"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"ignore malformed line: {Path.GetFileName(filePath)}:{lineNo} ({ex.GetType().Name})");
                }
            }
        }

        var tricks = trickBuilders.Values
            .OrderBy(item => item.TrickNo)
            .Where(item => item.WinnerIndex >= 0 || item.HasFinalPlays || item.Plays.Count >= 4)
            .Select(item => item.ToDetail())
            .ToList();

        return new ReviewSessionDetailResponse
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Summary = aggregate.ToSummary(),
            BottomCards = bottomCards,
            Tricks = tricks,
            Warnings = warnings
        };
    }

    private ReviewIndexCache GetOrBuildIndex()
    {
        var files = CollectRawFiles(recentOnly: false, RecentIndexFileLimit);
        var stamp = "full:" + BuildStamp(files);

        lock (_sync)
        {
            if (_indexCache != null && string.Equals(_indexCache.Stamp, stamp, StringComparison.Ordinal))
                return _indexCache;
        }

        var cache = BuildIndex(files, stamp);
        lock (_sync)
        {
            _indexCache = cache;
            return cache;
        }
    }

    private ReviewIndexCache GetOrBuildRecentIndex()
    {
        var files = CollectRawFiles(recentOnly: true, RecentIndexFileLimit);
        var stamp = "recent:" + BuildStamp(files);

        lock (_sync)
        {
            if (_recentIndexCache != null && string.Equals(_recentIndexCache.Stamp, stamp, StringComparison.Ordinal))
                return _recentIndexCache;
        }

        var cache = BuildIndex(files, stamp);
        if (cache.Sessions.Count == 0)
        {
            var fallbackFiles = CollectRawFiles(recentOnly: true, RecentIndexFallbackFileLimit);
            var fallbackStamp = "recent:" + BuildStamp(fallbackFiles);
            cache = BuildIndex(fallbackFiles, fallbackStamp);
            stamp = fallbackStamp;
        }

        lock (_sync)
        {
            _recentIndexCache = cache;
            return cache;
        }
    }

    private ReviewIndexCache BuildIndex(List<SourceRawFile> files, string stamp)
    {
        var byComposite = new Dictionary<string, SessionAggregate>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            if (!File.Exists(file.Path))
                continue;

            foreach (var rawLine in File.ReadLines(file.Path))
            {
                var line = NormalizeLine(rawLine);
                if (string.IsNullOrWhiteSpace(line) || line[0] != '{')
                    continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    var roundId = ReadString(root, "round_id");
                    if (string.IsNullOrWhiteSpace(roundId))
                        continue;

                    var composite = $"{file.SourceTag}\n{roundId}";
                    if (!byComposite.TryGetValue(composite, out var aggregate))
                    {
                        aggregate = new SessionAggregate
                        {
                            SourceTag = file.SourceTag,
                            SourceLabel = file.SourceLabel,
                            RoundId = roundId
                        };
                        byComposite[composite] = aggregate;
                    }

                    aggregate.SourceFiles.Add(file.Path);
                    aggregate.GameId ??= ReadString(root, "game_id");

                    var ts = ParseUtc(ReadString(root, "ts_utc"));
                    if (ts.HasValue && (!aggregate.StartedAtUtc.HasValue || ts.Value < aggregate.StartedAtUtc.Value))
                        aggregate.StartedAtUtc = ts;

                    var eventName = ReadString(root, "event");
                    root.TryGetProperty("payload", out var payload);

                    if (string.Equals(eventName, "game.start", StringComparison.Ordinal))
                    {
                        aggregate.DealerIndex = ResolveInt(aggregate.DealerIndex, ReadInt(payload, "dealer_index", -1));
                        aggregate.LevelRank ??= ReadString(payload, "level_rank");
                        foreach (var item in ParsePlayerAiLines(payload))
                            aggregate.AddPlayerAiLine(item.PlayerIndex, item.AiLine);
                        continue;
                    }

                    if (string.Equals(eventName, "turn.start", StringComparison.Ordinal))
                    {
                        aggregate.DealerIndex = ResolveInt(aggregate.DealerIndex, ReadInt(payload, "dealer_index", -1));
                        aggregate.LevelRank ??= ReadString(payload, "level_rank");
                        aggregate.TrumpSuit ??= ReadString(payload, "trump_suit");
                        aggregate.DefenderScore = Math.Max(aggregate.DefenderScore, ReadInt(payload, "defender_score"));
                        continue;
                    }

                    if (string.Equals(eventName, "trump.finalized", StringComparison.Ordinal))
                    {
                        aggregate.TrumpSuit ??= ReadString(payload, "trump_suit");
                        continue;
                    }

                    if (string.Equals(eventName, "trick.finish", StringComparison.Ordinal))
                    {
                        var trickId = ReadString(root, "trick_id");
                        if (!string.IsNullOrWhiteSpace(trickId))
                            aggregate.TrickIds.Add(trickId);
                        else
                        {
                            var trickNo = ReadTrickNo(payload);
                            if (trickNo > 0)
                                aggregate.TrickIds.Add($"trick_{trickNo:D4}");
                        }

                        aggregate.DefenderScore = Math.Max(
                            aggregate.DefenderScore,
                            Math.Max(
                                ReadInt(payload, "defender_score_after"),
                                ReadInt(payload, "defenderScoreAfter")));
                        aggregate.LevelRank ??= ReadString(payload, "level_rank");
                        aggregate.TrumpSuit ??= ReadString(payload, "trump_suit");
                        continue;
                    }

                    if (string.Equals(eventName, "ai.decision", StringComparison.Ordinal))
                    {
                        var path = ReadString(payload, "path");
                        if (!string.IsNullOrWhiteSpace(path))
                            aggregate.AddPath(path!);

                        var aiLine = InferAiLine(
                            path,
                            ReadString(payload, "phase_policy"),
                            hasBundleV30: false);
                        var playerIndex = ResolveInt(
                            ReadInt(payload, "player_index", -1),
                            ReadInt(payload, "playerIndex", -1));
                        aggregate.AddPlayerAiLine(playerIndex, aiLine);
                        continue;
                    }

                    if (string.Equals(eventName, "ai.bundle", StringComparison.Ordinal))
                    {
                        var path = FirstNonEmpty(
                            ReadString(payload, "path"),
                            ReadPathString(payload, "bundle", "meta", "path"),
                            ReadPathString(payload, "bundle", "algorithm_trace", "path"),
                            ReadPathString(payload, "bundle_v30", "path"));
                        if (!string.IsNullOrWhiteSpace(path))
                            aggregate.AddPath(path!);

                        var hasBundleV30 = payload.TryGetProperty("bundle_v30", out var bundleV30)
                            && bundleV30.ValueKind != JsonValueKind.Null
                            && bundleV30.ValueKind != JsonValueKind.Undefined;
                        var aiLine = InferAiLine(
                            path,
                            FirstNonEmpty(
                                ReadString(payload, "phase_policy"),
                                ReadPathString(payload, "bundle", "meta", "phase_policy")),
                            hasBundleV30);
                        var playerIndex = ResolveInt(
                            ReadInt(payload, "player_index", -1),
                            ReadInt(payload, "playerIndex", -1),
                            ReadPathInt(payload, -1, "bundle", "meta", "player_index"),
                            ReadPathInt(payload, -1, "bundle_v30", "player_index"));
                        aggregate.AddPlayerAiLine(playerIndex, aiLine);
                    }
                }
                catch
                {
                    // Ignore malformed rows in index scan.
                }
            }
        }

        var aggregates = byComposite.Values
            .Select(aggregate =>
            {
                aggregate.SessionId = ReviewSessionIdCodec.Encode(aggregate.SourceTag, aggregate.RoundId);
                return aggregate;
            })
            .ToList();

        aggregates = aggregates
            .Where(aggregate => aggregate.TrickIds.Count > 0)
            .ToList();

        var sessions = aggregates
            .Select(aggregate => aggregate.ToSummary())
            .OrderByDescending(item => item.StartedAtUtc ?? DateTime.MinValue)
            .ThenBy(item => item.RoundId, StringComparer.Ordinal)
            .ToList();

        var bySessionId = aggregates.ToDictionary(item => item.SessionId, StringComparer.Ordinal);
        return new ReviewIndexCache
        {
            Stamp = stamp,
            Sessions = sessions,
            AggregatesBySessionId = bySessionId
        };
    }

    private List<SourceRawFile> CollectRawFiles(bool recentOnly, int recentFileLimit)
    {
        var files = new List<SourceRawFile>();

        var logsRawRoot = Path.Combine(_repoRoot, "logs", "raw");
        if (Directory.Exists(logsRawRoot))
        {
            foreach (var file in Directory.EnumerateFiles(logsRawRoot, "*.jsonl", SearchOption.AllDirectories))
            {
                files.Add(CreateSourceRawFile(file, "logs_raw", "logs/raw"));
            }
        }

        var artifactsRoot = Path.Combine(_repoRoot, "artifacts");
        if (Directory.Exists(artifactsRoot))
        {
            foreach (var rawDir in Directory.EnumerateDirectories(artifactsRoot, "raw", SearchOption.AllDirectories))
            {
                var parent = Directory.GetParent(rawDir)?.FullName;
                if (string.IsNullOrWhiteSpace(parent))
                    continue;

                var sourceTag = Path.GetRelativePath(_repoRoot, parent).Replace('\\', '/');
                var sourceLabel = sourceTag + "/raw";
                foreach (var file in Directory.EnumerateFiles(rawDir, "*.jsonl", SearchOption.AllDirectories))
                {
                    files.Add(CreateSourceRawFile(file, sourceTag, sourceLabel));
                }
            }
        }

        var ordered = files
            .GroupBy(item => item.Path, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        if (recentOnly)
        {
            var liveOnly = ordered
                .Where(item => string.Equals(item.SourceTag, "logs_raw", StringComparison.Ordinal))
                .OrderByDescending(item => item.LastWriteUtc)
                .ThenByDescending(item => item.Length)
                .ThenBy(item => item.Path, StringComparer.Ordinal)
                .Take(Math.Max(1, recentFileLimit))
                .ToList();

            if (liveOnly.Count > 0)
                return liveOnly;

            return ordered
                .OrderByDescending(item => item.LastWriteUtc)
                .ThenByDescending(item => item.Length)
                .ThenBy(item => item.Path, StringComparer.Ordinal)
                .Take(Math.Max(1, recentFileLimit))
                .ToList();
        }

        return ordered
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .ToList();
    }

    private static string BuildStamp(List<SourceRawFile> files)
    {
        long count = files.Count;
        long maxWriteTicks = 0;
        long totalLength = 0;

        foreach (var file in files)
        {
            totalLength += file.Length;
            var ticks = file.LastWriteUtc.Ticks;
            if (ticks > maxWriteTicks)
                maxWriteTicks = ticks;
        }

        return $"{count}:{totalLength}:{maxWriteTicks}";
    }

    private bool TryGetAggregate(string sessionId, out SessionAggregate aggregate)
    {
        var recentIndex = GetOrBuildRecentIndex();
        if (recentIndex.AggregatesBySessionId.TryGetValue(sessionId, out aggregate!))
            return true;

        var fullIndex = GetOrBuildIndex();
        return fullIndex.AggregatesBySessionId.TryGetValue(sessionId, out aggregate!);
    }

    private static bool ShouldUseRecentIndex(int? limit)
    {
        return !limit.HasValue || limit.Value <= 20;
    }

    private static SourceRawFile CreateSourceRawFile(string filePath, string sourceTag, string sourceLabel)
    {
        try
        {
            var info = new FileInfo(filePath);
            return new SourceRawFile
            {
                Path = filePath,
                SourceTag = sourceTag,
                SourceLabel = sourceLabel,
                LastWriteUtc = info.Exists ? info.LastWriteTimeUtc : DateTime.MinValue,
                Length = info.Exists ? info.Length : 0L
            };
        }
        catch
        {
            return new SourceRawFile
            {
                Path = filePath,
                SourceTag = sourceTag,
                SourceLabel = sourceLabel,
                LastWriteUtc = DateTime.MinValue,
                Length = 0L
            };
        }
    }

    private static TrickBuilder EnsureTrick(IDictionary<int, TrickBuilder> tricks, int trickNo, string? trickId)
    {
        if (!tricks.TryGetValue(trickNo, out var trick))
        {
            trick = new TrickBuilder { TrickNo = trickNo };
            tricks[trickNo] = trick;
        }

        if (!string.IsNullOrWhiteSpace(trickId))
            trick.TrickId = trickId!;

        return trick;
    }

    private static string NormalizeLine(string line)
    {
        if (string.IsNullOrEmpty(line))
            return string.Empty;

        var trimmed = line.Trim();
        if (trimmed.Length == 0)
            return trimmed;

        if (trimmed[0] == '\uFEFF')
            trimmed = trimmed[1..];

        return trimmed;
    }

    private static List<ReviewPlayerHand> ParseHandsBefore(JsonElement payload)
    {
        if (!(payload.TryGetProperty("hands_before_trick", out var array) && array.ValueKind == JsonValueKind.Array) &&
            !(payload.TryGetProperty("handsBeforeTrick", out array) && array.ValueKind == JsonValueKind.Array))
            return new List<ReviewPlayerHand>();

        var result = new List<ReviewPlayerHand>();
        foreach (var item in array.EnumerateArray())
        {
            var cards = ParseCardTexts(item, "cards");
            result.Add(new ReviewPlayerHand
            {
                PlayerIndex = ReadInt(item, "player_index", -1),
                HandCount = ReadInt(item, "hand_count", cards.Count),
                Cards = cards
            });
        }

        return result.OrderBy(hand => hand.PlayerIndex).ToList();
    }

    private static List<ReviewPlayerAiLine> ParsePlayerAiLines(JsonElement payload)
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

            var playerIndex = ResolveInt(
                ReadInt(item, "player_index", -1),
                ReadInt(item, "playerIndex", -1));
            var aiLine = FirstNonEmpty(
                ReadString(item, "ai_line"),
                ReadString(item, "aiLine"));
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

    private static List<ReviewPlay> ParseTrickCards(JsonElement payload)
    {
        JsonElement array;
        if (payload.TryGetProperty("trick_cards", out var trickCards) && trickCards.ValueKind == JsonValueKind.Array)
        {
            array = trickCards;
        }
        else if (payload.TryGetProperty("plays", out var plays) && plays.ValueKind == JsonValueKind.Array)
        {
            array = plays;
        }
        else
        {
            return new List<ReviewPlay>();
        }

        var result = new List<ReviewPlay>();
        int order = 1;
        foreach (var item in array.EnumerateArray())
        {
            result.Add(new ReviewPlay
            {
                PlayOrder = order++,
                PlayerIndex = ResolveInt(
                    ReadInt(item, "player_index", -1),
                    ReadInt(item, "playerIndex", -1)),
                Cards = ParseCardTexts(item, "cards")
            });
        }

        return result;
    }

    private static int ReadTrickNo(JsonElement payload)
    {
        return ResolveInt(
            NormalizeTrickNo(ReadInt(payload, "trick_no", -1)),
            NormalizeTrickNo(ReadInt(payload, "trickIndex", -1)),
            NormalizeTrickNo(ReadInt(payload, "trick_index", -1)));
    }

    private static List<string> ParseCardTexts(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var cardsElement) || cardsElement.ValueKind != JsonValueKind.Array)
            return new List<string>();

        var result = new List<string>();
        foreach (var card in cardsElement.EnumerateArray())
        {
            if (card.ValueKind == JsonValueKind.String)
            {
                var text = card.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    result.Add(text!);
                continue;
            }

            if (card.ValueKind == JsonValueKind.Object)
            {
                var text = ReadString(card, "text") ?? card.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                    result.Add(text);
            }
        }

        return result;
    }

    private static List<string> ParseStringArray(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.Array)
            return new List<string>();

        var result = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            var text = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
            if (!string.IsNullOrWhiteSpace(text))
                result.Add(text!);
        }

        return result;
    }

    private static List<string> ParseStringArrayPath(JsonElement parent, params string[] path)
    {
        var element = ReadPathElement(parent, path);
        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Array)
            return new List<string>();

        var result = new List<string>();
        foreach (var item in element.Value.EnumerateArray())
        {
            var text = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
            if (!string.IsNullOrWhiteSpace(text))
                result.Add(text!);
        }

        return result;
    }

    private static List<string> ParseCardTextsPath(JsonElement parent, params string[] path)
    {
        var element = ReadPathElement(parent, path);
        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Array)
            return new List<string>();

        var result = new List<string>();
        foreach (var card in element.Value.EnumerateArray())
        {
            if (card.ValueKind == JsonValueKind.String)
            {
                var text = card.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    result.Add(text!);
                continue;
            }

            if (card.ValueKind == JsonValueKind.Object)
            {
                var text = ReadString(card, "text") ?? card.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                    result.Add(text);
            }
        }

        return result;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string InferAiLine(string? path, string? phasePolicy, bool hasBundleV30)
    {
        if (hasBundleV30)
            return "V30";

        if (!string.IsNullOrWhiteSpace(path))
        {
            if (path.Contains("v30", StringComparison.OrdinalIgnoreCase))
                return "V30";
            if (path.Contains("v21", StringComparison.OrdinalIgnoreCase))
                return "V21";
            if (path.Contains("legacy", StringComparison.OrdinalIgnoreCase))
                return "Legacy";
        }

        if (!string.IsNullOrWhiteSpace(phasePolicy))
        {
            if (phasePolicy.Contains("v30", StringComparison.OrdinalIgnoreCase))
                return "V30";
            if (phasePolicy.Contains("v21", StringComparison.OrdinalIgnoreCase))
                return "V21";
        }

        return "Unknown";
    }

    private static List<string> FirstNonEmptyList(params List<string>[] values)
    {
        foreach (var value in values)
        {
            if (value.Count > 0)
                return value;
        }

        return new List<string>();
    }

    private static int ParseTrickNo(string? trickId)
    {
        if (string.IsNullOrWhiteSpace(trickId))
            return 0;

        const string prefix = "trick_";
        if (!trickId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return 0;

        var token = trickId[prefix.Length..];
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? NormalizeTrickNo(parsed)
            : 0;
    }

    private static int NormalizeTrickNo(int trickNo)
    {
        if (trickNo == 0)
            return 1;

        return trickNo;
    }

    private static int ParseTurnNo(string? turnId)
    {
        if (string.IsNullOrWhiteSpace(turnId))
            return int.MaxValue;

        const string prefix = "turn_";
        if (!turnId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return int.MaxValue;

        var token = turnId[prefix.Length..];
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : int.MaxValue;
    }

    private static DateTime? ParseUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return null;
        }

        return parsed;
    }

    private static int ResolveInt(int current, int candidate)
    {
        if (candidate < 0)
            return current;

        return current < 0 ? candidate : current;
    }

    private static int ResolveInt(params int[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate >= 0)
                return candidate;
        }

        return -1;
    }

    private static int ResolvePlayPosition(params int[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate > 0)
                return candidate;
        }

        return 0;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
            return null;

        if (value.ValueKind == JsonValueKind.String)
            return value.GetString();

        return value.ToString();
    }

    private static string? ReadString(JsonElement element, string objectPropertyName, string nestedPropertyName)
    {
        if (!element.TryGetProperty(objectPropertyName, out var nested) || nested.ValueKind != JsonValueKind.Object)
            return null;

        return ReadString(nested, nestedPropertyName);
    }

    private static int ReadInt(JsonElement element, string propertyName, int fallback = 0)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return fallback;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;

        return int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static int ReadInt(JsonElement element, string objectPropertyName, string nestedPropertyName, int fallback = 0)
    {
        if (!element.TryGetProperty(objectPropertyName, out var nested) || nested.ValueKind != JsonValueKind.Object)
            return fallback;

        return ReadInt(nested, nestedPropertyName, fallback);
    }

    private static long ReadLong(JsonElement element, string propertyName, long fallback = 0)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return fallback;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            return number;

        return long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static bool ReadBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return false;

        if (value.ValueKind == JsonValueKind.True)
            return true;
        if (value.ValueKind == JsonValueKind.False)
            return false;

        return bool.TryParse(value.ToString(), out var parsed) && parsed;
    }

    private static string? ReadPathString(JsonElement element, params string[] path)
    {
        var nested = ReadPathElement(element, path);
        if (!nested.HasValue)
            return null;

        var value = nested.Value;
        if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
            return null;

        if (value.ValueKind == JsonValueKind.String)
            return value.GetString();

        return value.ToString();
    }

    private static int ReadPathInt(JsonElement element, int fallback, params string[] path)
    {
        var nested = ReadPathElement(element, path);
        if (!nested.HasValue)
            return fallback;

        var value = nested.Value;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;

        return int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static JsonElement? ReadPathElement(JsonElement element, params string[] path)
    {
        if (path.Length == 0)
            return null;

        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                return null;
        }

        if (current.ValueKind == JsonValueKind.Null || current.ValueKind == JsonValueKind.Undefined)
            return null;

        return current;
    }

    private static JsonElement? ReadClonedElement(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
            return null;
        return value.Clone();
    }

    private sealed class ReviewIndexCache
    {
        public string Stamp { get; init; } = string.Empty;
        public List<ReviewSessionSummary> Sessions { get; init; } = new();
        public Dictionary<string, SessionAggregate> AggregatesBySessionId { get; init; } = new(StringComparer.Ordinal);
    }

    private sealed class SourceRawFile
    {
        public string Path { get; init; } = string.Empty;
        public string SourceTag { get; init; } = string.Empty;
        public string SourceLabel { get; init; } = string.Empty;
        public DateTime LastWriteUtc { get; init; }
        public long Length { get; init; }
    }

    private sealed class SessionAggregate
    {
        public string SessionId { get; set; } = string.Empty;
        public string SourceTag { get; init; } = string.Empty;
        public string SourceLabel { get; init; } = string.Empty;
        public string RoundId { get; init; } = string.Empty;
        public string? GameId { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public int DealerIndex { get; set; } = -1;
        public string? LevelRank { get; set; }
        public string? TrumpSuit { get; set; }
        public int DefenderScore { get; set; }
        public HashSet<string> TrickIds { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, int> PathCounts { get; } = new(StringComparer.Ordinal);
        public Dictionary<int, Dictionary<string, int>> PlayerAiCounts { get; } = new();
        public HashSet<string> SourceFiles { get; } = new(StringComparer.Ordinal);

        public void AddPath(string path)
        {
            PathCounts[path] = PathCounts.TryGetValue(path, out var count) ? count + 1 : 1;
        }

        public void AddPlayerAiLine(int playerIndex, string aiLine)
        {
            if (playerIndex < 0 || string.IsNullOrWhiteSpace(aiLine))
                return;

            if (!PlayerAiCounts.TryGetValue(playerIndex, out var byLine))
            {
                byLine = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                PlayerAiCounts[playerIndex] = byLine;
            }

            byLine[aiLine] = byLine.TryGetValue(aiLine, out var count) ? count + 1 : 1;
        }

        public ReviewSessionSummary ToSummary()
        {
            int v30 = 0;
            int v21 = 0;
            int legacy = 0;
            int other = 0;

            foreach (var pair in PathCounts)
            {
                var key = pair.Key;
                var count = pair.Value;
                if (key.Contains("v30", StringComparison.OrdinalIgnoreCase))
                    v30 += count;
                else if (key.Contains("v21", StringComparison.OrdinalIgnoreCase))
                    v21 += count;
                else if (key.Contains("legacy", StringComparison.OrdinalIgnoreCase))
                    legacy += count;
                else
                    other += count;
            }

            var summaryTokens = new List<string>(4);
            if (v30 > 0)
                summaryTokens.Add($"V30:{v30}");
            if (v21 > 0)
                summaryTokens.Add($"V21:{v21}");
            if (legacy > 0)
                summaryTokens.Add($"Legacy:{legacy}");
            if (other > 0)
                summaryTokens.Add($"Other:{other}");

            var playerAiLines = PlayerAiCounts
                .OrderBy(pair => pair.Key)
                .Select(pair =>
                {
                    var topLine = pair.Value
                        .OrderByDescending(item => item.Value)
                        .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                        .FirstOrDefault()
                        .Key ?? "Unknown";

                    return new ReviewPlayerAiLine
                    {
                        PlayerIndex = pair.Key,
                        AiLine = topLine
                    };
                })
                .ToList();

            return new ReviewSessionSummary
            {
                SessionId = SessionId,
                RoundId = RoundId,
                GameId = GameId,
                SourceTag = SourceTag,
                SourceLabel = SourceLabel,
                StartedAtUtc = StartedAtUtc,
                DealerIndex = DealerIndex,
                LevelRank = LevelRank,
                TrumpSuit = TrumpSuit,
                DefenderScore = DefenderScore,
                TrickCount = TrickIds.Count,
                AiLineSummary = summaryTokens.Count > 0 ? string.Join(" | ", summaryTokens) : "None",
                AiLineBreakdown = new ReviewAiLineBreakdown
                {
                    V30Decisions = v30,
                    V21Decisions = v21,
                    LegacyDecisions = legacy,
                    OtherDecisions = other,
                    PathCounts = new Dictionary<string, int>(PathCounts, StringComparer.Ordinal)
                },
                PlayerAiLines = playerAiLines
            };
        }
    }

    private sealed class TrickBuilder
    {
        private readonly Dictionary<string, DecisionRow> _decisionByKey = new(StringComparer.Ordinal);
        private readonly List<DecisionRow> _decisionRows = new();

        public int TrickNo { get; init; }
        public string TrickId { get; set; } = string.Empty;
        public int LeadPlayer { get; set; } = -1;
        public int WinnerIndex { get; set; } = -1;
        public string? WinnerReason { get; set; }
        public int TrickScore { get; set; }
        public int DefenderScoreBefore { get; set; }
        public int DefenderScoreAfter { get; set; }
        public List<ReviewPlayerHand> HandsBefore { get; set; } = new();
        public List<ReviewPlay> Plays { get; set; } = new();
        public bool HasFinalPlays { get; set; }

        public void UpsertDecision(ReviewDecision incoming, long seq)
        {
            var key = BuildDecisionKey(incoming.DecisionTraceId, incoming.TurnId, incoming.PlayerIndex);
            if (!string.IsNullOrWhiteSpace(key) && _decisionByKey.TryGetValue(key, out var existing))
            {
                MergeDecision(existing.Decision, incoming);
                if (seq < existing.Seq)
                    existing.Seq = seq;
                return;
            }

            var row = new DecisionRow
            {
                Decision = incoming,
                Seq = seq
            };
            _decisionRows.Add(row);
            if (!string.IsNullOrWhiteSpace(key))
                _decisionByKey[key] = row;
        }

        public ReviewTrickDetail ToDetail()
        {
            var orderedDecisions = _decisionRows
                .OrderBy(row => ParseTurnNo(row.Decision.TurnId))
                .ThenBy(row => row.Decision.PlayPosition <= 0 ? int.MaxValue : row.Decision.PlayPosition)
                .ThenBy(row => row.Seq)
                .Select(row => row.Decision)
                .ToList();

            var trickId = !string.IsNullOrWhiteSpace(TrickId)
                ? TrickId
                : $"trick_{TrickNo:D4}";

            return new ReviewTrickDetail
            {
                TrickNo = TrickNo,
                TrickId = trickId,
                LeadPlayer = LeadPlayer,
                WinnerIndex = WinnerIndex,
                WinnerReason = WinnerReason,
                TrickScore = TrickScore,
                DefenderScoreBefore = DefenderScoreBefore,
                DefenderScoreAfter = DefenderScoreAfter,
                HandsBefore = HandsBefore.OrderBy(hand => hand.PlayerIndex).ToList(),
                Plays = Plays,
                Decisions = orderedDecisions
            };
        }

        private static string? BuildDecisionKey(string? decisionTraceId, string? turnId, int playerIndex)
        {
            if (!string.IsNullOrWhiteSpace(decisionTraceId))
                return $"trace:{decisionTraceId}";

            if (string.IsNullOrWhiteSpace(turnId) || playerIndex < 0)
                return null;

            return $"turn:{turnId}|{playerIndex}";
        }

        private static void MergeDecision(ReviewDecision target, ReviewDecision incoming)
        {
            target.DecisionTraceId = MergeText(target.DecisionTraceId, incoming.DecisionTraceId);
            target.TurnId = MergeText(target.TurnId, incoming.TurnId);
            if (target.PlayPosition <= 0 && incoming.PlayPosition > 0)
                target.PlayPosition = incoming.PlayPosition;
            target.PlayerIndex = ResolveInt(target.PlayerIndex, incoming.PlayerIndex);
            target.AiLine = MergeText(target.AiLine, incoming.AiLine);
            target.Phase = MergeText(target.Phase, incoming.Phase);
            target.Path = MergeText(target.Path, incoming.Path);
            target.PhasePolicy = MergeText(target.PhasePolicy, incoming.PhasePolicy);
            target.PrimaryIntent = MergeText(target.PrimaryIntent, incoming.PrimaryIntent);
            target.SecondaryIntent = MergeText(target.SecondaryIntent, incoming.SecondaryIntent);
            target.SelectedReason = MergeText(target.SelectedReason, incoming.SelectedReason);
            target.SelectedCandidateId = MergeText(target.SelectedCandidateId, incoming.SelectedCandidateId);
            if (target.TriggeredRules.Count == 0 && incoming.TriggeredRules.Count > 0)
                target.TriggeredRules = incoming.TriggeredRules;
            if (target.SelectedCards.Count == 0 && incoming.SelectedCards.Count > 0)
                target.SelectedCards = incoming.SelectedCards;
            target.Bundle ??= incoming.Bundle;
            target.BundleV30 ??= incoming.BundleV30;
        }

        private static string? MergeText(string? current, string? incoming)
        {
            if (!string.IsNullOrWhiteSpace(current))
                return current;
            if (!string.IsNullOrWhiteSpace(incoming))
                return incoming;
            return current;
        }

        private sealed class DecisionRow
        {
            public ReviewDecision Decision { get; init; } = new();
            public long Seq { get; set; }
        }
    }
}

internal static class ReviewSessionIdCodec
{
    public static string Encode(string sourceTag, string roundId)
    {
        var raw = $"{sourceTag}\n{roundId}";
        var bytes = Encoding.UTF8.GetBytes(raw);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryDecode(string sessionId, out string sourceTag, out string roundId)
    {
        sourceTag = string.Empty;
        roundId = string.Empty;
        if (string.IsNullOrWhiteSpace(sessionId))
            return false;

        var encoded = sessionId.Replace('-', '+').Replace('_', '/');
        var padding = encoded.Length % 4;
        if (padding > 0)
            encoded = encoded.PadRight(encoded.Length + (4 - padding), '=');

        try
        {
            var bytes = Convert.FromBase64String(encoded);
            var raw = Encoding.UTF8.GetString(bytes);
            var splitIndex = raw.IndexOf('\n');
            if (splitIndex <= 0 || splitIndex >= raw.Length - 1)
                return false;

            sourceTag = raw[..splitIndex];
            roundId = raw[(splitIndex + 1)..];
            return !string.IsNullOrWhiteSpace(sourceTag) && !string.IsNullOrWhiteSpace(roundId);
        }
        catch
        {
            return false;
        }
    }
}
