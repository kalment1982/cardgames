using System.Collections.Generic;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.AI.V30;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V30
{
    public class RuleAIEngineV30Tests
    {
        [Fact]
        public void DecideLead_PrefersStableSideSuitRun_OverTrumpProbeInDealerEarlyWindow()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var memory = new CardMemory(config);
            var strategy = AIStrategyParameters.CreatePreset(AIDifficulty.Hard);
            var legacyBuilder = new RuleAIContextBuilder(config, AIDifficulty.Hard, strategy, memory, sessionStyleSeed: 1);
            var engine = new RuleAIEngineV30(config, AIDifficulty.Hard, memory);

            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Heart, Rank.Six),
                new Card(Suit.Diamond, Rank.Three),
                new Card(Suit.Club, Rank.Four)
            };

            var context = legacyBuilder.BuildLeadContext(
                hand,
                AIRole.Dealer,
                playerIndex: 0,
                dealerIndex: 0,
                visibleBottomCards: null,
                trickIndex: 1,
                turnIndex: 1,
                playPosition: 1,
                cardsLeftMin: hand.Count,
                currentWinningPlayer: -1,
                defenderScore: 0,
                bottomPoints: 0);

            var v21Decision = new PhaseDecision
            {
                Phase = PhaseKind.Lead,
                SelectedCards = new List<Card> { new Card(Suit.Heart, Rank.Six) },
                Intent = new ResolvedIntent { PrimaryIntent = DecisionIntentKind.ForceTrump },
                ScoredActions = new List<ScoredAction>
                {
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Heart, Rank.Six) },
                        Score = 8,
                        ReasonCode = "force_trump"
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Spade, Rank.Ace) },
                        Score = 7,
                        ReasonCode = "stable_side_suit"
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Diamond, Rank.Three) },
                        Score = 1,
                        ReasonCode = "probe"
                    }
                },
                Explanation = new DecisionExplanation
                {
                    Phase = PhaseKind.Lead,
                    PrimaryIntent = "ForceTrump",
                    SelectedReason = "force_trump"
                }
            };

            var overlay = engine.DecideLead(context, v21Decision, new AIDecisionLogContext
            {
                PlayerIndex = 0,
                DealerIndex = 0,
                TrickIndex = 1,
                TurnIndex = 1,
                RoundId = "round_v30_test"
            });

            Assert.Single(overlay.SelectedCards);
            Assert.Equal(Suit.Spade, overlay.SelectedCards[0].Suit);
            Assert.Equal(Rank.Ace, overlay.SelectedCards[0].Rank);
            Assert.Equal("StableSideSuitRun", overlay.PrimaryIntent);
            Assert.Equal("lead001.dealer_stable_side", overlay.SelectedCandidateId);
            Assert.Contains("Lead-001", overlay.TriggeredRules);
            Assert.NotNull(overlay.Bundle);
            Assert.Equal("Lead", overlay.Bundle!.Phase);
            Assert.Equal("v30_overlay_policy", overlay.Bundle.Mode);
            Assert.Equal("lead001.dealer_stable_side", overlay.Bundle.SelectedCandidateId);
        }
    }
}
