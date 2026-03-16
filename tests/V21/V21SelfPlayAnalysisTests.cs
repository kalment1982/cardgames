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
using Xunit;
using Xunit.Abstractions;

namespace TractorGame.Tests.V21
{
    /// <summary>
    /// V21自动对弈分析器：
    /// 1. 让V21自对弈N局，记录所有决策日志
    /// 2. 扫描日志，检测常见决策问题
    /// 3. 生成问题报告（按严重程度分类）
    /// </summary>
    [Trait("Category", "SelfPlay")]
    [Trait("Category", "LongRunning")]
    public class V21SelfPlayAnalysisTests
    {
        private readonly ITestOutputHelper _output;

        public V21SelfPlayAnalysisTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // ── 快速分析：10局自对弈 ─────────────────────────────────────────────
        [Fact]
        public void V21_SelfPlay_Analysis_10Games()
        {
            var result = RunSelfPlayWithAnalysis(games: 10, seedBase: 5000);
            PrintAnalysisReport(result);

            // 验证：严重问题数量应该很少
            Assert.True(result.CriticalIssues.Count < 5,
                $"发现 {result.CriticalIssues.Count} 个严重问题，超过阈值5");
        }

        // ── 标准分析：50局自对弈 ─────────────────────────────────────────────
        [Fact]
        public void V21_SelfPlay_Analysis_50Games()
        {
            var result = RunSelfPlayWithAnalysis(games: 50, seedBase: 6000);
            PrintAnalysisReport(result);

            Assert.True(result.CriticalIssues.Count < 10,
                $"发现 {result.CriticalIssues.Count} 个严重问题");
            Assert.True(result.MajorIssues.Count < 50,
                $"发现 {result.MajorIssues.Count} 个主要问题");
        }

        // ── 核心：自对弈 + 分析 ──────────────────────────────────────────────
        private static AnalysisResult RunSelfPlayWithAnalysis(int games, int seedBase)
        {
            var logSink = new InMemoryLogSink();
            var logger = new CoreLogger(logSink);

            // 跑N局V21自对弈
            for (int i = 0; i < games; i++)
            {
                PlaySingleGame(seedBase + i, logger);
            }

            // 分析日志
            var analyzer = new DecisionAnalyzer();
            return analyzer.Analyze(logSink.Entries.ToList());
        }

        private static void PlaySingleGame(int seed, IGameLogger logger)
        {
            var game = new Game(seed, NullGameLogger.Instance,
                sessionId: $"selfplay_{seed}",
                gameId: $"selfplay_{seed}",
                roundId: $"selfplay_{seed}");

            game.StartGame();
            RunAutoBidding(game, seed);

            var finalizeResult = game.FinalizeTrumpEx();
            if (!finalizeResult.Success)
                game.FinalizeTrump(PickTrumpSuit(seed));

            var config = new GameConfig
            {
                LevelRank = game.State.LevelRank,
                TrumpSuit = game.State.TrumpSuit ?? Suit.Spade
            };

            var v21Options = new RuleAIOptions { UseRuleAIV21 = true, EnableShadowCompare = false };
            var strategy = AIStrategyParameters.CreatePreset(AIDifficulty.Hard);

            var players = new AIPlayer[4];
            for (int i = 0; i < 4; i++)
                players[i] = new AIPlayer(config, AIDifficulty.Hard, seed + i + 97, strategy, logger, v21Options);

            // 扣底
            var dealer = game.State.DealerIndex;
            var buryCards = players[dealer].BuryBottom(
                game.State.PlayerHands[dealer], AIRole.Dealer, game.BottomCardsSnapshot);
            if (buryCards.Count != 8)
                buryCards = game.State.PlayerHands[dealer].Take(8).ToList();
            game.BuryBottomEx(buryCards);

            // 出牌循环
            int turnGuard = 0;
            while (game.State.Phase != GamePhase.Finished && turnGuard < 200)
            {
                turnGuard++;
                var playerIndex = game.State.CurrentPlayer;
                var hand = game.State.PlayerHands[playerIndex];
                if (hand.Count == 0) break;

                var role = ResolveRole(playerIndex, game.State.DealerIndex);
                var logContext = new AIDecisionLogContext
                {
                    SessionId = game.SessionId,
                    GameId = game.GameId,
                    RoundId = game.RoundId,
                    PlayerIndex = playerIndex
                };

                List<Card> decision;
                if (game.CurrentTrick.Count == 0)
                {
                    var knownBottom = playerIndex == dealer
                        ? new List<Card>(game.State.BuriedCards) : new List<Card>();
                    decision = players[playerIndex].Lead(hand, role, playerIndex, null, knownBottom, logContext);
                }
                else
                {
                    var leadCards = game.CurrentTrick[0].Cards;
                    var currentWinning = game.CurrentTrick
                        .OrderByDescending(t => t, new TrickPlayComparer(config))
                        .First().Cards;
                    var partnerIndex = (playerIndex + 2) % 4;
                    var partnerWinning = game.CurrentTrick.Count > 0 &&
                        game.CurrentTrick.OrderByDescending(t => t, new TrickPlayComparer(config))
                            .First().PlayerIndex == partnerIndex;
                    var trickScore = game.CurrentTrick.SelectMany(t => t.Cards).Sum(c => c.Score);

                    decision = players[playerIndex].Follow(hand, leadCards, currentWinning, role,
                        partnerWinning, trickScore, logContext);
                }

                var playResult = game.PlayCardsEx(playerIndex, decision);
                if (!playResult.Success)
                {
                    var fallback = new List<Card> { hand[0] };
                    game.PlayCardsEx(playerIndex, fallback);
                }
            }
        }

        private static AIRole ResolveRole(int playerIndex, int dealerIndex)
        {
            if (playerIndex == dealerIndex) return AIRole.Dealer;
            return playerIndex % 2 == dealerIndex % 2 ? AIRole.DealerPartner : AIRole.Opponent;
        }

        private static Suit PickTrumpSuit(int seed) =>
            new[] { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond }[Math.Abs(seed) % 4];

        // ── 自动叫主（复制自V21ValidationTests）────────────────────────────
        private static void RunAutoBidding(Game game, int seed)
        {
            var bidPolicy = new BidPolicy(seed + 5003);
            var visibleHands = new[] {
                new List<Card>(), new List<Card>(),
                new List<Card>(), new List<Card>()
            };

            while (!game.IsDealingComplete)
            {
                var dealResult = game.DealNextCardEx();
                if (!dealResult.Success) break;

                var step = game.LastDealStep;
                if (step == null || step.IsBottomCard) continue;

                var player = step.PlayerIndex;
                if (player < 0 || player >= 4) continue;

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
                if (bidCards.Count == 0) continue;
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

        // ── 报告输出 ─────────────────────────────────────────────────────────
        private void PrintAnalysisReport(AnalysisResult r)
        {
            var report = new StringBuilder();
            report.AppendLine("\n╔══════════════════════════════════════════════════════════════════╗");
            report.AppendLine("║              V21自对弈决策分析报告                               ║");
            report.AppendLine("╠══════════════════════════════════════════════════════════════════╣");
            report.AppendLine($"║  总决策数:     {r.TotalDecisions,6}                                        ║");
            report.AppendLine($"║  严重问题:     {r.CriticalIssues.Count,6}  ({r.CriticalRate:P2})                         ║");
            report.AppendLine($"║  主要问题:     {r.MajorIssues.Count,6}  ({r.MajorRate:P2})                         ║");
            report.AppendLine($"║  次要问题:     {r.MinorIssues.Count,6}  ({r.MinorRate:P2})                         ║");
            report.AppendLine("╚══════════════════════════════════════════════════════════════════╝");
            report.AppendLine();

            if (r.CriticalIssues.Count > 0)
            {
                report.AppendLine("🔴 严重问题（需立即修复）:");
                foreach (var issue in r.CriticalIssues.Take(10))
                    report.AppendLine($"  [{issue.Type}] {issue.Description}");
                if (r.CriticalIssues.Count > 10)
                    report.AppendLine($"  ... 还有 {r.CriticalIssues.Count - 10} 个");
                report.AppendLine();
            }

            if (r.MajorIssues.Count > 0)
            {
                report.AppendLine("🟡 主要问题（影响胜率）:");
                var grouped = r.MajorIssues.GroupBy(i => i.Type).OrderByDescending(g => g.Count());
                foreach (var group in grouped.Take(5))
                    report.AppendLine($"  [{group.Key}] {group.Count()} 次");
                report.AppendLine();
            }

            if (r.MinorIssues.Count > 0)
            {
                report.AppendLine("🟢 次要问题（可优化）:");
                var grouped = r.MinorIssues.GroupBy(i => i.Type).OrderByDescending(g => g.Count());
                foreach (var group in grouped.Take(5))
                    report.AppendLine($"  [{group.Key}] {group.Count()} 次");
                report.AppendLine();
            }

            report.AppendLine("═══════════════════════════════════════════════════════════════════");

            var reportText = report.ToString();
            Console.WriteLine(reportText);
            _output.WriteLine(reportText);

            // 写文件
            var path = "/tmp/v21_selfplay_analysis.txt";
            File.WriteAllText(path, reportText);
        }
    }

    // ── 规则引擎：DetectionRule + DecisionAnalyzer ───────────────────────────

    internal enum Severity { Minor, Major, Critical }

    /// <summary>
    /// 一条检测规则。Condition 返回 true 时触发，IssueBuilder 生成 Issue。
    /// </summary>
    internal sealed class DetectionRule
    {
        public string RuleId { get; init; } = string.Empty;
        public Severity Level { get; init; }
        /// <summary>判断是否触发。entry 是 ai.decision 日志条目。</summary>
        public Func<LogEntry, bool> Condition { get; init; } = _ => false;
        /// <summary>生成问题描述。只在 Condition 为 true 时调用。</summary>
        public Func<LogEntry, string> DescriptionBuilder { get; init; } = _ => string.Empty;
    }

    internal sealed class DecisionAnalyzer
    {
        private readonly List<DetectionRule> _rules;

        public DecisionAnalyzer() : this(DefaultRules.All) { }

        public DecisionAnalyzer(IEnumerable<DetectionRule> rules)
        {
            _rules = rules.ToList();
        }

        public AnalysisResult Analyze(List<LogEntry> entries)
        {
            var result = new AnalysisResult();
            var decisions = entries.Where(e => e.Event == "ai.decision").ToList();
            result.TotalDecisions = decisions.Count;

            foreach (var entry in decisions)
            {
                foreach (var rule in _rules)
                {
                    if (!rule.Condition(entry)) continue;

                    var issue = new Issue
                    {
                        Type = rule.RuleId,
                        Description = rule.DescriptionBuilder(entry),
                        Context = BuildContext(entry)
                    };

                    switch (rule.Level)
                    {
                        case Severity.Critical: result.CriticalIssues.Add(issue); break;
                        case Severity.Major:    result.MajorIssues.Add(issue);    break;
                        case Severity.Minor:    result.MinorIssues.Add(issue);    break;
                    }
                }
            }

            return result;
        }

        private static string BuildContext(LogEntry entry)
        {
            var p = entry.Payload;
            return $"[{entry.GameId}] Player{Get.Int(p, "player_index")} {Get.Str(p, "phase")} " +
                   $"Intent={Get.Str(p, "primary_intent")} Cards={Get.Str(p, "selected_cards")}";
        }
    }

    // ── 默认规则集 ────────────────────────────────────────────────────────────
    internal static class DefaultRules
    {
        public static IReadOnlyList<DetectionRule> All { get; } = new List<DetectionRule>
        {
            // ── Critical ────────────────────────────────────────────────────
            new DetectionRule
            {
                RuleId = "队友赢牌出王",
                Level = Severity.Critical,
                Condition = e =>
                {
                    var p = e.Payload;
                    if (!Get.Bool(p, "partner_winning")) return false;
                    if (Get.Str(p, "primary_intent") != "PassToMate") return false;
                    var cards = Get.Str(p, "selected_cards");
                    return cards?.Contains("BigJoker") == true || cards?.Contains("SmallJoker") == true;
                },
                DescriptionBuilder = e =>
                    $"队友赢牌时出了王牌: {Get.Str(e.Payload, "selected_cards")}"
            },

            new DetectionRule
            {
                RuleId = "零分墩切主",
                Level = Severity.Critical,
                Condition = e =>
                {
                    var p = e.Payload;
                    return Get.Int(p, "trick_score") == 0
                        && Get.Str(p, "phase") == "Follow"
                        && Get.Str(p, "selected_reason")?.Contains("TakeScore") == true;
                },
                DescriptionBuilder = _ => "零分墩时切主抢牌，浪费主牌"
            },

            // ── Major ────────────────────────────────────────────────────────
            new DetectionRule
            {
                RuleId = "高分墩不抢",
                Level = Severity.Major,
                Condition = e =>
                {
                    var p = e.Payload;
                    return Get.Int(p, "trick_score") >= 15
                        && Get.Str(p, "phase") == "Follow"
                        && !Get.Bool(p, "partner_winning")
                        && Get.Str(p, "primary_intent") == "MinimizeLoss";
                },
                DescriptionBuilder = e =>
                    $"{Get.Int(e.Payload, "trick_score")}分墩选择MinimizeLoss"
            },

            new DetectionRule
            {
                RuleId = "有拖拉机不出",
                Level = Severity.Major,
                Condition = e =>
                {
                    var p = e.Payload;
                    if (Get.Str(p, "phase") != "Lead") return false;
                    if (Get.Int(p, "candidate_count") <= 1) return false;
                    var tops = Get.StrList(p, "top_candidates");
                    if (tops == null || !tops.Any(c => c.Contains("Tractor"))) return false;
                    var selected = Get.Str(p, "selected_cards");
                    return selected != null && !selected.Contains(",");
                },
                DescriptionBuilder = _ => "候选中有拖拉机但出了单张"
            },

            // ── Minor ────────────────────────────────────────────────────────
            new DetectionRule
            {
                RuleId = "终局未出大牌",
                Level = Severity.Minor,
                Condition = e =>
                {
                    var p = e.Payload;
                    if (Get.Int(p, "cards_left_min") > 3) return false;
                    if (Get.Str(p, "phase") != "Lead") return false;
                    if (Get.Int(p, "trump_count") <= 0) return false;
                    var cards = Get.Str(p, "selected_cards");
                    return cards != null && !cards.Contains("Joker") && !cards.Contains("Ace");
                },
                DescriptionBuilder = e =>
                    $"终局剩{Get.Int(e.Payload, "cards_left_min")}张，未出大牌"
            },

            new DetectionRule
            {
                RuleId = "候选数过少",
                Level = Severity.Minor,
                Condition = e =>
                {
                    var p = e.Payload;
                    return Get.Int(p, "candidate_count") < 2
                        && Get.Str(p, "phase") == "Lead"
                        && Get.Int(p, "hand_count") > 5;
                },
                DescriptionBuilder = e =>
                    $"手牌{Get.Int(e.Payload, "hand_count")}张但只有{Get.Int(e.Payload, "candidate_count")}个候选"
            },

            new DetectionRule
            {
                RuleId = "决策耗时过长",
                Level = Severity.Minor,
                Condition = e =>
                    e.Metrics != null
                    && e.Metrics.ContainsKey("total_ms")
                    && e.Metrics["total_ms"] > 500.0,
                DescriptionBuilder = e =>
                    $"决策耗时 {e.Metrics!["total_ms"]:F0}ms"
            },
        };
    }

    // ── payload 读取辅助（静态，供规则 lambda 使用）────────────────────────────
    internal static class Get
    {
        public static string? Str(Dictionary<string, object?> d, string key) =>
            d.TryGetValue(key, out var v) ? v?.ToString() : null;

        public static int Int(Dictionary<string, object?> d, string key) =>
            d.TryGetValue(key, out var v) && v != null ? Convert.ToInt32(v) : 0;

        public static bool Bool(Dictionary<string, object?> d, string key) =>
            d.TryGetValue(key, out var v) && v != null && Convert.ToBoolean(v);

        public static List<string>? StrList(Dictionary<string, object?> d, string key)
        {
            if (!d.TryGetValue(key, out var v) || v == null) return null;
            if (v is JsonElement elem && elem.ValueKind == JsonValueKind.Array)
                return elem.EnumerateArray().Select(e => e.ToString()).ToList();
            if (v is List<string> list) return list;
            return null;
        }
    }

    // ── 数据模型 ─────────────────────────────────────────────────────────────
    internal sealed class AnalysisResult
    {
        public int TotalDecisions { get; set; }
        public List<Issue> CriticalIssues { get; } = new();
        public List<Issue> MajorIssues { get; } = new();
        public List<Issue> MinorIssues { get; } = new();

        public double CriticalRate => TotalDecisions > 0 ? (double)CriticalIssues.Count / TotalDecisions : 0;
        public double MajorRate => TotalDecisions > 0 ? (double)MajorIssues.Count / TotalDecisions : 0;
        public double MinorRate => TotalDecisions > 0 ? (double)MinorIssues.Count / TotalDecisions : 0;
    }

    internal sealed class Issue
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
    }
}
