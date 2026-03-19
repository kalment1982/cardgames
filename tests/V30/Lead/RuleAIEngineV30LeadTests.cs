using System.Collections.Generic;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.AI.V30;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V30.Lead
{
    public class RuleAIEngineV30LeadTests
    {
        [Fact]
        public void DecideLead_TeamSideSuitNeedsTopCardConfidence_AndFallsBackToBetterConcreteAction()
        {
            var config = new GameConfig
            {
                TrumpSuit = Suit.Spade,
                LevelRank = Rank.Two
            };

            var context = new RuleAIContext
            {
                Phase = PhaseKind.Lead,
                Role = AIRole.Opponent,
                Difficulty = AIDifficulty.Hard,
                PlayerIndex = 1,
                DealerIndex = 0,
                MyHand = new List<Card>
                {
                    new Card(Suit.Diamond, Rank.King),
                    new Card(Suit.Heart, Rank.Ten),
                    new Card(Suit.Spade, Rank.Three),
                    new Card(Suit.Spade, Rank.Jack),
                    new Card(Suit.Diamond, Rank.Four),
                    new Card(Suit.Heart, Rank.Five)
                },
                GameConfig = config,
                RuleProfile = RuleProfile.FromConfig(config),
                DifficultyProfile = DifficultyProfile.From(AIDifficulty.Hard),
                StyleProfile = StyleProfile.Create(17),
                HandProfile = new HandProfile
                {
                    TrumpCount = 2,
                    HighTrumpCount = 0,
                    ScoreCardCount = 3,
                    SuitLengths = new Dictionary<Suit, int>
                    {
                        [Suit.Diamond] = 2,
                        [Suit.Heart] = 2
                    }
                },
                MemorySnapshot = new MemorySnapshot(),
                InferenceSnapshot = new InferenceSnapshot
                {
                    MateHoldConfidence = new ProbabilityEstimate
                    {
                        Probability = 0.8,
                        Confidence = 0.9
                    }
                },
                DecisionFrame = new DecisionFrame
                {
                    PhaseKind = PhaseKind.Lead,
                    TrickIndex = 6,
                    TurnIndex = 21,
                    PlayPosition = 1,
                    CardsLeftMin = 16
                }
            };

            var decision = new PhaseDecision
            {
                Phase = PhaseKind.Lead,
                Intent = new ResolvedIntent
                {
                    PrimaryIntent = DecisionIntentKind.TakeLead
                },
                SelectedCards = new List<Card> { new Card(Suit.Diamond, Rank.King) },
                ScoredActions = new List<ScoredAction>
                {
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Diamond, Rank.King) },
                        Score = 23.4605,
                        ReasonCode = "lead_best_control",
                        Features = new Dictionary<string, double>
                        {
                            ["TrickWinValue"] = 0.6278,
                            ["TrickScoreSwing"] = 1
                        }
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Heart, Rank.Ten) },
                        Score = 25.0439,
                        ReasonCode = "lead_best_control",
                        Features = new Dictionary<string, double>
                        {
                            ["TrickWinValue"] = 0.6111,
                            ["TrickScoreSwing"] = 1
                        }
                    }
                }
            };

            var engine = new RuleAIEngineV30(config, AIDifficulty.Hard, memory: null);
            var overlay = engine.DecideLead(context, decision);

            Assert.Single(overlay.SelectedCards);
            Assert.Equal(Suit.Heart, overlay.SelectedCards[0].Suit);
            Assert.Equal(Rank.Ten, overlay.SelectedCards[0].Rank);
            Assert.DoesNotContain("Lead-006", overlay.TriggeredRules);
        }
    }
}
