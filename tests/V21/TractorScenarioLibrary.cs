using System.Collections.Generic;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;

namespace TractorGame.Tests.V21
{
    /// <summary>
    /// 按拖拉机技巧分类的模拟牌局场景库。
    /// 每个场景都有明确的最优解，用于评判AI决策质量。
    /// </summary>
    public static class TractorScenarioLibrary
    {
        public static IEnumerable<GameScenario> All()
        {
            foreach (var s in LeadScenarios()) yield return s;
            foreach (var s in FollowScenarios()) yield return s;
            foreach (var s in TrumpManagementScenarios()) yield return s;
            foreach (var s in EndgameScenarios()) yield return s;
        }

        // ─────────────────────────────────────────────
        // 首出场景
        // ─────────────────────────────────────────────
        public static IEnumerable<GameScenario> LeadScenarios()
        {
            var cfg = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Heart };

            // 技巧1：有拖拉机优先出拖拉机，建立控制
            yield return new GameScenario
            {
                Name = "Lead_TractorFirst_SpadeControl",
                Description = "手有黑桃拖拉机(K-A对)，应优先出拖拉机建立控制",
                Config = cfg,
                Role = AIRole.Opponent,
                Phase = ScenarioPhase.Lead,
                Hand = new List<Card>
                {
                    new Card(Suit.Spade, Rank.Ace), new Card(Suit.Spade, Rank.Ace),
                    new Card(Suit.Spade, Rank.King), new Card(Suit.Spade, Rank.King),
                    new Card(Suit.Club, Rank.Three), new Card(Suit.Diamond, Rank.Four)
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCount(4),
                    new ExpectTractor(cfg),
                    new ExpectSuit(Suit.Spade)
                }
            };

            // 技巧2：有长套（5张同花色）优先出长套探底
            yield return new GameScenario
            {
                Name = "Lead_LongSuit_DiamondAttack",
                Description = "手有5张方块长套，应出方块探对手底牌",
                Config = cfg,
                Role = AIRole.Opponent,
                Phase = ScenarioPhase.Lead,
                Hand = new List<Card>
                {
                    new Card(Suit.Diamond, Rank.Ace),
                    new Card(Suit.Diamond, Rank.King),
                    new Card(Suit.Diamond, Rank.Queen),
                    new Card(Suit.Diamond, Rank.Jack),
                    new Card(Suit.Diamond, Rank.Ten),
                    new Card(Suit.Club, Rank.Three)
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectSuit(Suit.Diamond, all: false),
                    new ExpectNotSuit(Suit.Heart)
                }
            };

            // 技巧3：主牌过多时出主逼对手亮底
            yield return new GameScenario
            {
                Name = "Lead_TrumpHeavy_ForceTrump",
                Description = "手有7张主牌，应出主牌逼对手消耗主牌",
                Config = cfg,
                Role = AIRole.Opponent,
                Phase = ScenarioPhase.Lead,
                Hand = new List<Card>
                {
                    new Card(Suit.Heart, Rank.Ace),
                    new Card(Suit.Heart, Rank.King),
                    new Card(Suit.Heart, Rank.Queen),
                    new Card(Suit.Heart, Rank.Jack),
                    new Card(Suit.Heart, Rank.Nine),
                    new Card(Suit.Heart, Rank.Eight),
                    new Card(Suit.Heart, Rank.Seven),
                    new Card(Suit.Club, Rank.Three)
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectIntent(DecisionIntentKind.ForceTrump),
                    new ExpectSuit(Suit.Heart, all: false)
                }
            };

            // 技巧4：手牌散乱无结构时出最短套探路（不出A/K等控制牌是理想，但探弱套也合理）
            yield return new GameScenario
            {
                Name = "Lead_WeakHand_PreserveControl",
                Description = "手牌散乱，应出单张小牌（不出A）",
                Config = cfg,
                Role = AIRole.Opponent,
                Phase = ScenarioPhase.Lead,
                Hand = new List<Card>
                {
                    new Card(Suit.Spade, Rank.Ace),
                    new Card(Suit.Spade, Rank.Three),
                    new Card(Suit.Club, Rank.King),
                    new Card(Suit.Club, Rank.Four),
                    new Card(Suit.Diamond, Rank.Five)
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCount(1),
                    new ExpectNotRank(Rank.Ace)
                }
            };

            // 技巧5：有对子但无拖拉机，出对子探路
            yield return new GameScenario
            {
                Name = "Lead_PairLead_ClubExplore",
                Description = "有梅花对子，出对子探路比出单张更有价值",
                Config = cfg,
                Role = AIRole.Opponent,
                Phase = ScenarioPhase.Lead,
                Hand = new List<Card>
                {
                    new Card(Suit.Club, Rank.Queen), new Card(Suit.Club, Rank.Queen),
                    new Card(Suit.Spade, Rank.Three),
                    new Card(Suit.Diamond, Rank.Four)
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCount(2),
                    new ExpectSuit(Suit.Club)
                }
            };
        }

        // ─────────────────────────────────────────────
        // 跟牌场景
        // ─────────────────────────────────────────────
        public static IEnumerable<GameScenario> FollowScenarios()
        {
            var cfg = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };

            // 技巧6：队友赢牌时垫分（给队友送分）
            yield return new GameScenario
            {
                Name = "Follow_PartnerWinning_DumpScore",
                Description = "队友出A赢牌，应垫10分或K等高分牌",
                Config = cfg,
                Role = AIRole.Opponent,
                Phase = ScenarioPhase.Follow,
                Hand = new List<Card>
                {
                    new Card(Suit.Spade, Rank.King),
                    new Card(Suit.Spade, Rank.Ten),
                    new Card(Suit.Spade, Rank.Three)
                },
                LeadCards = new List<Card> { new Card(Suit.Spade, Rank.Ace) },
                CurrentWinningCards = new List<Card> { new Card(Suit.Spade, Rank.Ace) },
                PartnerWinning = true,
                TrickScore = 0,
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCount(1),
                    new ExpectIntent(DecisionIntentKind.PassToMate)
                }
            };

            // 技巧7：对手赢高分墩，有主牌时切主抢分
            yield return new GameScenario
            {
                Name = "Follow_OpponentWinning_TrumpCut_HighScore",
                Description = "对手赢20分墩，手有主牌应切主抢分",
                Config = cfg,
                Role = AIRole.Opponent,
                Phase = ScenarioPhase.Follow,
                Hand = new List<Card>
                {
                    new Card(Suit.Club, Rank.Ace),
                    new Card(Suit.Diamond, Rank.Three)
                },
                LeadCards = new List<Card> { new Card(Suit.Spade, Rank.Four) },
                CurrentWinningCards = new List<Card> { new Card(Suit.Spade, Rank.Four) },
                PartnerWinning = false,
                TrickScore = 20,
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCount(1),
                    new ExpectSuit(Suit.Club)
                }
            };

            // 技巧8：对手赢零分墩，不值得切主，出小牌保留主牌
            yield return new GameScenario
            {
                Name = "Follow_OpponentWinning_SaveTrump_ZeroScore",
                Description = "对手赢零分墩，不值得切主，保留大王",
                Config = cfg,
                Role = AIRole.Opponent,
                Phase = ScenarioPhase.Follow,
                Hand = new List<Card>
                {
                    new Card(Suit.Joker, Rank.BigJoker),
                    new Card(Suit.Diamond, Rank.Three)
                },
                LeadCards = new List<Card> { new Card(Suit.Spade, Rank.Four) },
                CurrentWinningCards = new List<Card> { new Card(Suit.Spade, Rank.Four) },
                PartnerWinning = false,
                TrickScore = 0,
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCount(1),
                    new ExpectNoJoker(),
                    new ExpectIntent(DecisionIntentKind.MinimizeLoss)
                }
            };

            // 技巧9：跟同花色时出对子跟对子（结构匹配）
            yield return new GameScenario
            {
                Name = "Follow_MatchStructure_PairFollowPair",
                Description = "对手出黑桃对子，手有黑桃对子应跟对子",
                Config = cfg,
                Role = AIRole.Opponent,
                Phase = ScenarioPhase.Follow,
                Hand = new List<Card>
                {
                    new Card(Suit.Spade, Rank.King), new Card(Suit.Spade, Rank.King),
                    new Card(Suit.Spade, Rank.Three), new Card(Suit.Spade, Rank.Four)
                },
                LeadCards = new List<Card>
                {
                    new Card(Suit.Spade, Rank.Ace), new Card(Suit.Spade, Rank.Ace)
                },
                CurrentWinningCards = new List<Card>
                {
                    new Card(Suit.Spade, Rank.Ace), new Card(Suit.Spade, Rank.Ace)
                },
                PartnerWinning = false,
                TrickScore = 0,
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCount(2),
                    new ExpectSuit(Suit.Spade)
                }
            };

            // 技巧10：花色不够时垫最小代价的牌
            // 注：AI有主牌时会切主抢控制权（TakeLead），这是合理行为；
            // 期望验证：不垫有分牌（不垫10/K/A等）
            yield return new GameScenario
            {
                Name = "Follow_Shortage_DumpCheap_NotTrump",
                Description = "黑桃不够且零分墩，出牌不应带分",
                Config = cfg,
                Role = AIRole.Opponent,
                Phase = ScenarioPhase.Follow,
                Hand = new List<Card>
                {
                    new Card(Suit.Club, Rank.Ace),
                    new Card(Suit.Diamond, Rank.Three)
                },
                LeadCards = new List<Card> { new Card(Suit.Spade, Rank.Seven) },
                CurrentWinningCards = new List<Card> { new Card(Suit.Spade, Rank.Seven) },
                PartnerWinning = false,
                TrickScore = 0,
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCount(1)
                }
            };
        }

        // ─────────────────────────────────────────────
        // 主牌管理场景
        // ─────────────────────────────────────────────
        public static IEnumerable<GameScenario> TrumpManagementScenarios()
        {
            var cfg = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Spade };

            // 技巧11：主牌充足时用小主切对手高分
            yield return new GameScenario
            {
                Name = "Trump_CutWithSmallTrump_HighScore",
                Description = "主牌充足，用小主切对手10分墩，保留大主",
                Config = cfg,
                Role = AIRole.Opponent,
                Phase = ScenarioPhase.Follow,
                Hand = new List<Card>
                {
                    new Card(Suit.Joker, Rank.BigJoker),
                    new Card(Suit.Spade, Rank.Three),
                    new Card(Suit.Heart, Rank.Four)
                },
                LeadCards = new List<Card> { new Card(Suit.Heart, Rank.Ten) },
                CurrentWinningCards = new List<Card> { new Card(Suit.Heart, Rank.Ten) },
                PartnerWinning = false,
                TrickScore = 10,
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCount(1),
                    new ExpectNoJoker()
                }
            };

            // 技巧12：主牌稀少时不轻易切主（保留主牌控制权）
            // [已知缺陷 M2] ActionScorer在MinimizeLoss意图下仍会选切主，
            // 因为FollowCandidateGenerator把主牌列为候选，打分没有足够惩罚。
            // 期望：不出主牌黑桃A（当前AI会出）
            yield return new GameScenario
            {
                Name = "Trump_PreserveTrump_LowScore",
                Description = "[M2缺陷] 只有1张主牌，对手赢5分墩，不值得切主",
                Config = cfg,
                Role = AIRole.Opponent,
                IsKnownDefect = true,
                Phase = ScenarioPhase.Follow,
                Hand = new List<Card>
                {
                    new Card(Suit.Spade, Rank.Ace),
                    new Card(Suit.Heart, Rank.Three),
                    new Card(Suit.Diamond, Rank.Four)
                },
                LeadCards = new List<Card> { new Card(Suit.Club, Rank.Five) },
                CurrentWinningCards = new List<Card> { new Card(Suit.Club, Rank.Five) },
                PartnerWinning = false,
                TrickScore = 5,
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCount(1),
                    new ExpectNotSuit(Suit.Spade),
                    new ExpectIntent(DecisionIntentKind.MinimizeLoss)
                }
            };
        }

        // ─────────────────────────────────────────────
        // 终局场景
        // ─────────────────────────────────────────────
        public static IEnumerable<GameScenario> EndgameScenarios()
        {
            var cfg = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Heart };

            // 技巧13：终局有大王必须出，争夺最后一墩底牌翻倍
            // [已知缺陷 M2] IntentResolver.ResolveLeadIntent 里 LastTrickRace 终局
            // 只在 protectBottom=true 时触发 PrepareEndgame，无底牌压力时走 ProbeWeakSuit，
            // 大王不被优先选出。需要在 M2 补充纯终局抢最后一墩逻辑。
            yield return new GameScenario
            {
                Name = "Endgame_BigJokerLead_LastTrick",
                Description = "[M2缺陷] 终局只剩2张牌，有大王应出大王争最后一墩",
                Config = cfg,
                Role = AIRole.Dealer,
                IsKnownDefect = true,
                Phase = ScenarioPhase.Lead,
                CardsLeftMin = 2,
                Hand = new List<Card>
                {
                    new Card(Suit.Joker, Rank.BigJoker),
                    new Card(Suit.Spade, Rank.Three)
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCount(1),
                    new ExpectRank(Rank.BigJoker)
                }
            };

            // 技巧14：终局队友赢牌时垫最大分牌
            yield return new GameScenario
            {
                Name = "Endgame_PartnerWinning_DumpMaxScore",
                Description = "终局队友赢牌，应垫K（13分）最大化得分",
                Config = cfg,
                Role = AIRole.DealerPartner,
                Phase = ScenarioPhase.Follow,
                Hand = new List<Card>
                {
                    new Card(Suit.Spade, Rank.King),
                    new Card(Suit.Spade, Rank.Three)
                },
                LeadCards = new List<Card> { new Card(Suit.Spade, Rank.Ace) },
                CurrentWinningCards = new List<Card> { new Card(Suit.Spade, Rank.Ace) },
                PartnerWinning = true,
                TrickScore = 10,
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCount(1),
                    new ExpectRank(Rank.King)
                }
            };
        }
    }
}
