using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V21
{
    public class DecisionBundleScenarioFactoryTests
    {
        private const string Trick0012Fixture =
            "tests/V21/Fixtures/decision_bundles/trick_0012_north_should_overcut.json";
        private const string LeadThrowFixture =
            "tests/V21/Fixtures/decision_bundles/trick_0004_north_should_lead_spade_throw.json";
        private const string FollowCheapCutFixture =
            "tests/V21/Fixtures/decision_bundles/trick_0004_north_should_cut_cheaply.json";

        [Fact]
        public void FromBundleFile_ParsesFollowScenarioContext()
        {
            var scenario = DecisionBundleScenarioFactory.FromBundleFile(Trick0012Fixture);

            Assert.Equal(ScenarioPhase.Follow, scenario.Phase);
            Assert.Equal(AIRole.DealerPartner, scenario.Role);
            Assert.Equal(AIDifficulty.Hard, scenario.Difficulty);
            Assert.Equal(Rank.Two, scenario.Config.LevelRank);
            Assert.Equal(Suit.Diamond, scenario.Config.TrumpSuit);
            Assert.Equal(2, scenario.PlayerIndex);
            Assert.Equal(0, scenario.DealerIndex);
            Assert.Equal(15, scenario.TrickScore);
            Assert.Equal(45, scenario.DefenderScore);
            Assert.Equal(10, scenario.BottomPoints);
            Assert.Equal(7, scenario.CardsLeftMin);
            Assert.Equal(7, scenario.Hand.Count);
            Assert.Equal(2, scenario.LeadCards.Count);
            Assert.Equal(2, scenario.CurrentWinningCards.Count);
        }

        [Fact]
        public void LoggedScenario_Trick0012_ShouldOvercutAndProtectScore()
        {
            var parsed = DecisionBundleScenarioFactory.FromBundleFile(Trick0012Fixture);
            var scenario = DecisionBundleScenarioFactory.FromBundleFile(
                Trick0012Fixture,
                name: "Follow_Trick0012_OvercutHighScore",
                description: "北家在15分墩应使用主牌毙回，不能直接垫掉红桃小牌。",
                expectations: new IScenarioExpectation[]
                {
                    new ExpectCount(2),
                    new ExpectIntent(DecisionIntentKind.TakeScore),
                    new ExpectAllTrump(parsed.Config),
                    new ExpectBeatCurrentWinner(parsed.Config, parsed.CurrentWinningCards),
                    new ExpectNotExactCards(new List<Card>
                    {
                        new Card(Suit.Heart, Rank.Nine),
                        new Card(Suit.Heart, Rank.Jack)
                    })
                });

            var result = ScenarioJudge.Run(scenario);

            Assert.True(result.Passed, result.ToString());
        }

        [Fact]
        public void LoggedScenario_LeadTrick0004_ShouldLeadMixedSpadeThrow()
        {
            var scenario = DecisionBundleScenarioFactory.FromBundleFile(LeadThrowFixture);
            var memory = DecisionBundleScenarioFactory.BuildMemoryFromBundleFile(LeadThrowFixture, scenario.Config);
            var context = new RuleAIContextBuilder(scenario.Config, scenario.Difficulty, null, memory).BuildLeadContext(
                scenario.Hand,
                scenario.Role,
                playerIndex: scenario.PlayerIndex,
                dealerIndex: scenario.DealerIndex,
                visibleBottomCards: scenario.VisibleBottomCards,
                trickIndex: scenario.TrickIndex,
                turnIndex: scenario.TurnIndex,
                playPosition: scenario.PlayPosition,
                cardsLeftMin: scenario.CardsLeftMin,
                currentWinningPlayer: scenario.CurrentWinningPlayer,
                currentTrickScore: scenario.TrickScore,
                defenderScore: scenario.DefenderScore,
                bottomPoints: scenario.BottomPoints);
            var policy = new LeadPolicy2(
                new LeadCandidateGenerator(scenario.Config, memory),
                new IntentResolver(scenario.Config),
                new ActionScorer(scenario.Config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Equal(3, decision.SelectedCards.Count);
            Assert.Equal(
                new[] { "♠A", "♠6", "♠6" }.OrderBy(text => text),
                decision.SelectedCards.Select(card => card.ToString()).OrderBy(text => text));
        }

        [Fact]
        public void LoggedScenario_FollowTrick0004_ShouldChooseCheapestWinningCut()
        {
            var parsed = DecisionBundleScenarioFactory.FromBundleFile(FollowCheapCutFixture);
            var scenario = DecisionBundleScenarioFactory.FromBundleFile(
                FollowCheapCutFixture,
                name: "Follow_Trick0004_CutCheaply",
                description: "北家在可稳稳赢回 10 分墩时，应优先选择不垫分的毙牌方案。",
                expectations: new IScenarioExpectation[]
                {
                    new ExpectCount(3),
                    new ExpectIntent(DecisionIntentKind.TakeScore),
                    new ExpectBeatCurrentWinner(parsed.Config, parsed.CurrentWinningCards),
                    new ExpectExactCards(new List<Card>
                    {
                        new Card(Suit.Joker, Rank.BigJoker),
                        new Card(Suit.Heart, Rank.Seven),
                        new Card(Suit.Spade, Rank.Queen)
                    })
                });

            var result = ScenarioJudge.Run(scenario);

            Assert.True(result.Passed, result.ToString());
        }
    }
}
