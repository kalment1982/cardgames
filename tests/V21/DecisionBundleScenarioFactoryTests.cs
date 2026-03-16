using System.Collections.Generic;
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
    }
}
