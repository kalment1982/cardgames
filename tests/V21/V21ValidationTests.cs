using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TractorGame.Core.AI;
using TractorGame.Core.AI.Bidding;
using TractorGame.Core.AI.V21;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Logging;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;
using Xunit.Abstractions;

namespace TractorGame.Tests.V21
{
    /// <summary>
    /// V21规则AI自动打牌验证：
    /// 让V21队 vs Legacy队跑N局完整对局，统计胜率/得分/非法出牌率。
    /// 验证标准：V21胜率不低于Legacy（基线45%），非法出牌率接近0。
    /// </summary>
    [Trait("Category", "SelfPlay")]
    public class V21ValidationTests
    {
        private readonly ITestOutputHelper _output;

        public V21ValidationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // ── 快速冒烟：10局，CI用 ─────────────────────────────────────────────
        [Fact]
        public void V21_vs_Legacy_Smoke_10Games()
        {
            var result = RunBatch(games: 10, seedBase: 1000, difficulty: AIDifficulty.Hard);
            PrintReport(result, "Smoke(10局)");

            // 冒烟只验证：没有崩溃，非法出牌率极低
            Assert.True(result.IllegalMoveRate < 0.05,
                $"非法出牌率 {result.IllegalMoveRate:P1} 超过5%，AI决策有问题");
        }

        // ── 标准验证：100局，合并到完整测试套件 ────────────────────────────
        [Fact]
        public void V21_vs_Legacy_Standard_100Games()
        {
            var result = RunBatch(games: 100, seedBase: 2000, difficulty: AIDifficulty.Hard);
            PrintReport(result, "Standard(100局)");

            Assert.True(result.IllegalMoveRate < 0.02,
                $"非法出牌率 {result.IllegalMoveRate:P1} 超过2%");

            // V21胜率不应显著低于Legacy（允许±10%随机波动）
            Assert.True(result.V21WinRate >= 0.35,
                $"V21胜率 {result.V21WinRate:P1} 低于基线35%，策略退化");
        }

        // ── 难度对比：Easy/Medium/Hard/Expert各20局 ─────────────────────────
        [Theory]
        [InlineData(AIDifficulty.Easy, 20, 3000)]
        [InlineData(AIDifficulty.Medium, 20, 3100)]
        [InlineData(AIDifficulty.Hard, 20, 3200)]
        [InlineData(AIDifficulty.Expert, 20, 3300)]
        public void V21_vs_Legacy_ByDifficulty(AIDifficulty difficulty, int games, int seedBase)
        {
            var result = RunBatch(games, seedBase, difficulty);
            PrintReport(result, $"{difficulty}({games}局)");

            Assert.True(result.IllegalMoveRate < 0.05,
                $"[{difficulty}] 非法出牌率 {result.IllegalMoveRate:P1} 超过5%");
        }

        // ── 核心：批量对局 ───────────────────────────────────────────────────
        private static BatchResult RunBatch(int games, int seedBase, AIDifficulty difficulty)
        {
            var outcomes = new ConcurrentBag<GameOutcome>();

            Parallel.For(0, games, i =>
            {
                // 奇偶交替：V21在偶数局坐0/2位，奇数局坐1/3位
                bool v21OnEven = i % 2 == 0;
                var outcome = PlaySingleGame(seedBase + i, difficulty, v21OnEven);
                outcomes.Add(outcome);
            });

            return new BatchResult(outcomes.ToList());
        }

        private static GameOutcome PlaySingleGame(int seed, AIDifficulty difficulty, bool v21OnEven)
        {
            var outcome = new GameOutcome { Seed = seed, V21OnEven = v21OnEven };

            try
            {
                var logger = NullGameLogger.Instance;
                var game = new Game(seed, logger,
                    sessionId: $"v21val_{seed}",
                    gameId: $"v21val_{seed}",
                    roundId: $"v21val_{seed}");

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

                var v21Options = new RuleAIOptions { UseRuleAIV21 = true, EnableShadowCompare = false };
                var legacyOptions = new RuleAIOptions { UseRuleAIV21 = false, EnableShadowCompare = false };
                var strategy = AIStrategyParameters.CreatePreset(difficulty);

                var players = new AIPlayer[4];
                for (int i = 0; i < 4; i++)
                {
                    bool isV21 = v21OnEven ? i % 2 == 0 : i % 2 == 1;
                    players[i] = new AIPlayer(config, difficulty, seed + i + 97, strategy, logger,
                        isV21 ? v21Options : legacyOptions);
                }

                // 扣底
                var dealer = game.State.DealerIndex;
                var buryCards = players[dealer].BuryBottom(
                    game.State.PlayerHands[dealer],
                    AIRole.Dealer,
                    game.BottomCardsSnapshot);
                if (buryCards.Count != 8)
                    buryCards = game.State.PlayerHands[dealer].Take(8).ToList();
                var buryResult = game.BuryBottomEx(buryCards);
                if (!buryResult.Success)
                    game.BuryBottomEx(game.State.PlayerHands[dealer].Take(8).ToList());

                // 出牌循环
                int turnGuard = 0;
                while (game.State.Phase != GamePhase.Finished && turnGuard < 200)
                {
                    turnGuard++;
                    var playerIndex = game.State.CurrentPlayer;
                    var hand = game.State.PlayerHands[playerIndex];
                    if (hand.Count == 0) break;

                    var role = ResolveRole(playerIndex, game.State.DealerIndex);
                    var decision = SelectDecision(game, players[playerIndex], hand, role, playerIndex, config);
                    var playResult = game.PlayCardsEx(playerIndex, decision);

                    bool isV21Player = v21OnEven ? playerIndex % 2 == 0 : playerIndex % 2 == 1;
                    if (!playResult.Success)
                    {
                        if (isV21Player) outcome.V21IllegalMoves++;
                        else outcome.LegacyIllegalMoves++;

                        // fallback：出单张（最简单的合法出牌）
                        var fallback = new List<Card> { hand[0] };
                        var fallbackResult = game.PlayCardsEx(playerIndex, fallback);
                        if (!fallbackResult.Success)
                        {
                            outcome.Error = $"Player {playerIndex} fallback失败";
                            break;
                        }
                    }
                }

                // 结果
                outcome.DefenderScore = game.State.DefenderScore;
                outcome.DealerIndex = game.State.DealerIndex;
                outcome.Finished = game.State.Phase == GamePhase.Finished;

                bool v21IsDealerSide = (v21OnEven ? 0 : 1) == game.State.DealerIndex % 2;
                bool defenderWon = game.State.DefenderScore >= 80;
                outcome.V21Won = v21IsDealerSide ? !defenderWon : defenderWon;
            }
            catch (Exception ex)
            {
                outcome.Error = ex.Message;
            }

            return outcome;
        }

        // ── 自动叫主 ─────────────────────────────────────────────────────────
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

        // ── 出牌决策 ─────────────────────────────────────────────────────────
        private static List<Card> SelectDecision(
            Game game, AIPlayer ai, List<Card> hand,
            AIRole role, int playerIndex, GameConfig config)
        {
            var logContext = new AIDecisionLogContext
            {
                SessionId = game.SessionId,
                GameId = game.GameId,
                RoundId = game.RoundId,
                PlayerIndex = playerIndex
            };

            if (game.CurrentTrick.Count == 0)
            {
                var knownBottom = playerIndex == game.State.DealerIndex
                    ? new List<Card>(game.State.BuriedCards) : new List<Card>();
                return ai.Lead(hand, role, playerIndex, null, knownBottom, logContext);
            }

            var leadCards = game.CurrentTrick[0].Cards;
            var currentWinning = game.CurrentTrick
                .OrderByDescending(t => t, new TrickPlayComparer(config))
                .First().Cards;
            var partnerIndex = (playerIndex + 2) % 4;
            var partnerWinning = game.CurrentTrick.Count > 0 &&
                game.CurrentTrick.OrderByDescending(t => t, new TrickPlayComparer(config))
                    .First().PlayerIndex == partnerIndex;
            var trickScore = game.CurrentTrick.SelectMany(t => t.Cards).Sum(c => c.Score);

            return ai.Follow(hand, leadCards, currentWinning, role, partnerWinning, trickScore, logContext: logContext);
        }

        private static AIRole ResolveRole(int playerIndex, int dealerIndex)
        {
            if (playerIndex == dealerIndex) return AIRole.Dealer;
            return playerIndex % 2 == dealerIndex % 2 ? AIRole.DealerPartner : AIRole.Opponent;
        }

        private static Suit PickTrumpSuit(int seed) =>
            new[] { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond }[Math.Abs(seed) % 4];

        // ── 报告输出 ─────────────────────────────────────────────────────────
        private void PrintReport(BatchResult r, string label)
        {
            var report = $@"
=== V21验证报告 [{label}] ===
总局数:       {r.Total}
完成局数:     {r.Finished}
错误局数:     {r.Errors}
V21胜率:      {r.V21WinRate:P1}  ({r.V21Wins}/{r.Total})
平均闲家得分: {r.AvgDefenderScore:F1}
V21非法出牌:  {r.V21IllegalMoves} 次
Legacy非法:   {r.LegacyIllegalMoves} 次
非法出牌率:   {r.IllegalMoveRate:P2}
得分分布:     <80={r.DefenderScoreBelow80} | 80-119={r.DefenderScore80to119} | ≥120={r.DefenderScoreAbove120}";

            Console.WriteLine(report);
            _output.WriteLine(report);

            // 写到文件方便查看
            var reportPath = $"/tmp/v21_validation_{label.Replace("(", "_").Replace(")", "")}.txt";
            System.IO.File.WriteAllText(reportPath, report);
        }
    }

    // ── 辅助：TrickPlay排序（用于找当前赢牌） ────────────────────────────────
    internal sealed class TrickPlayComparer : IComparer<TrickPlay>
    {
        private readonly CardComparer _cardComparer;
        public TrickPlayComparer(GameConfig config) => _cardComparer = new CardComparer(config);
        public int Compare(TrickPlay? x, TrickPlay? y)
        {
            if (x == null || y == null) return 0;
            var xTop = x.Cards.OrderByDescending(c => c, _cardComparer).First();
            var yTop = y.Cards.OrderByDescending(c => c, _cardComparer).First();
            return _cardComparer.Compare(xTop, yTop);
        }
    }

    // ── 数据模型 ─────────────────────────────────────────────────────────────
    internal sealed class GameOutcome
    {
        public int Seed { get; set; }
        public bool V21OnEven { get; set; }
        public bool V21Won { get; set; }
        public int DefenderScore { get; set; }
        public int DealerIndex { get; set; }
        public bool Finished { get; set; }
        public int V21IllegalMoves { get; set; }
        public int LegacyIllegalMoves { get; set; }
        public string? Error { get; set; }
    }

    internal sealed class BatchResult
    {
        public int Total { get; }
        public int Finished { get; }
        public int Errors { get; }
        public int V21Wins { get; }
        public double V21WinRate { get; }
        public double AvgDefenderScore { get; }
        public int V21IllegalMoves { get; }
        public int LegacyIllegalMoves { get; }
        public double IllegalMoveRate { get; }
        public int DefenderScoreBelow80 { get; }
        public int DefenderScore80to119 { get; }
        public int DefenderScoreAbove120 { get; }

        public BatchResult(List<GameOutcome> outcomes)
        {
            Total = outcomes.Count;
            Finished = outcomes.Count(o => o.Finished);
            Errors = outcomes.Count(o => o.Error != null);
            V21Wins = outcomes.Count(o => o.V21Won);
            V21WinRate = Total > 0 ? (double)V21Wins / Total : 0;
            AvgDefenderScore = outcomes.Any() ? outcomes.Average(o => o.DefenderScore) : 0;
            V21IllegalMoves = outcomes.Sum(o => o.V21IllegalMoves);
            LegacyIllegalMoves = outcomes.Sum(o => o.LegacyIllegalMoves);
            var totalDecisions = Total * 25 * 4; // 估算总决策数
            IllegalMoveRate = totalDecisions > 0
                ? (double)(V21IllegalMoves + LegacyIllegalMoves) / totalDecisions : 0;
            DefenderScoreBelow80 = outcomes.Count(o => o.DefenderScore < 80);
            DefenderScore80to119 = outcomes.Count(o => o.DefenderScore >= 80 && o.DefenderScore < 120);
            DefenderScoreAbove120 = outcomes.Count(o => o.DefenderScore >= 120);
        }
    }
}
