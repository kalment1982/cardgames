using System.Collections.Generic;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.AI.V30;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V30.Lead
{
    public class LeadConcreteLandingRegressionTests
    {
        [Fact]
        public void DecideLead_DealerStableSide_ShouldPreferHigherConcreteScorePair()
        {
            var config = new GameConfig
            {
                TrumpSuit = Suit.Spade,
                LevelRank = Rank.Two
            };

            var context = CreateContext(config, AIRole.Dealer, playerIndex: 0, trickIndex: 2, hand: new List<Card>
            {
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Diamond, Rank.Queen),
                new Card(Suit.Diamond, Rank.Ace),
                new Card(Suit.Spade, Rank.Three),
                new Card(Suit.Spade, Rank.Jack)
            });

            var decision = new PhaseDecision
            {
                Phase = PhaseKind.Lead,
                Intent = new ResolvedIntent
                {
                    PrimaryIntent = DecisionIntentKind.TakeLead
                },
                SelectedCards = new List<Card> { new Card(Suit.Diamond, Rank.Queen) },
                ScoredActions = new List<ScoredAction>
                {
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Diamond, Rank.Queen) },
                        Score = 36.4114,
                        ReasonCode = "lead_best_control"
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Heart, Rank.Nine), new Card(Suit.Heart, Rank.Nine) },
                        Score = 39.0,
                        ReasonCode = "lead_best_control"
                    }
                }
            };

            var engine = new RuleAIEngineV30(config, AIDifficulty.Hard, memory: null);
            var overlay = engine.DecideLead(context, decision);

            Assert.Equal("lead001.dealer_stable_side", overlay.SelectedCandidateId);
            Assert.Equal(2, overlay.SelectedCards.Count);
            Assert.All(overlay.SelectedCards, card => Assert.Equal(Suit.Heart, card.Suit));
            Assert.All(overlay.SelectedCards, card => Assert.Equal(Rank.Nine, card.Rank));
        }

        [Fact]
        public void DecideLead_ForceTrump_ShouldAvoidScoreBearingTrumpOutsidePressure()
        {
            var config = new GameConfig
            {
                TrumpSuit = Suit.Club,
                LevelRank = Rank.Two
            };

            var context = CreateContext(config, AIRole.Opponent, playerIndex: 1, trickIndex: 8, hand: new List<Card>
            {
                new Card(Suit.Club, Rank.King),
                new Card(Suit.Club, Rank.Five),
                new Card(Suit.Club, Rank.Nine),
                new Card(Suit.Diamond, Rank.Nine),
                new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Diamond, Rank.Seven),
                new Card(Suit.Spade, Rank.Four)
            });

            var decision = new PhaseDecision
            {
                Phase = PhaseKind.Lead,
                Intent = new ResolvedIntent
                {
                    PrimaryIntent = DecisionIntentKind.ForceTrump
                },
                SelectedCards = new List<Card> { new Card(Suit.Club, Rank.King) },
                ScoredActions = new List<ScoredAction>
                {
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Club, Rank.King) },
                        Score = 18.2,
                        ReasonCode = "force_trump",
                        Features = new Dictionary<string, double>
                        {
                            ["HighControlLossCost"] = 3.5,
                            ["TrumpConsumptionCost"] = 2.0
                        }
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Club, Rank.Five) },
                        Score = 15.4,
                        ReasonCode = "force_trump",
                        Features = new Dictionary<string, double>
                        {
                            ["HighControlLossCost"] = 0.2,
                            ["TrumpConsumptionCost"] = 0.5
                        }
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Club, Rank.Nine) },
                        Score = 14.6,
                        ReasonCode = "force_trump",
                        Features = new Dictionary<string, double>
                        {
                            ["HighControlLossCost"] = 0.4,
                            ["TrumpConsumptionCost"] = 0.7
                        }
                    }
                }
            };

            var engine = new RuleAIEngineV30(config, AIDifficulty.Hard, memory: null);
            var overlay = engine.DecideLead(context, decision);

            Assert.Equal("lead003.force_trump", overlay.SelectedCandidateId);
            Assert.Single(overlay.SelectedCards);
            Assert.Equal(Suit.Club, overlay.SelectedCards[0].Suit);
            Assert.Equal(Rank.Five, overlay.SelectedCards[0].Rank);
        }

        [Fact]
        public void DecideLead_BuildVoid_ShouldNotBreakWeakPairWhenStrongerPairExists()
        {
            var config = new GameConfig
            {
                TrumpSuit = Suit.Spade,
                LevelRank = Rank.Three
            };

            var context = CreateContext(config, AIRole.Dealer, playerIndex: 0, trickIndex: 1, hand: new List<Card>
            {
                new Card(Suit.Diamond, Rank.Two),
                new Card(Suit.Club, Rank.Seven),
                new Card(Suit.Club, Rank.Nine),
                new Card(Suit.Heart, Rank.Two),
                new Card(Suit.Club, Rank.Seven),
                new Card(Suit.Spade, Rank.Five),
                new Card(Suit.Club, Rank.Two)
            });
            context = context with
            {
                HandProfile = new HandProfile
                {
                    TrumpCount = 1,
                    HighTrumpCount = 0,
                    ScoreCardCount = 0,
                    SuitLengths = new Dictionary<Suit, int>
                    {
                        [Suit.Club] = 4,
                        [Suit.Diamond] = 1,
                        [Suit.Heart] = 1
                    },
                    PotentialVoidTargets = new List<Suit> { Suit.Club }
                }
            };

            var decision = new PhaseDecision
            {
                Phase = PhaseKind.Lead,
                Intent = new ResolvedIntent
                {
                    PrimaryIntent = DecisionIntentKind.TakeLead
                },
                SelectedCards = new List<Card> { new Card(Suit.Club, Rank.Seven) },
                ScoredActions = new List<ScoredAction>
                {
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Club, Rank.Seven) },
                        Score = 5.5297,
                        ReasonCode = "build_void",
                        Features = new Dictionary<string, double>
                        {
                            ["StructureBreakCost"] = 3.0
                        }
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Club, Rank.Seven), new Card(Suit.Club, Rank.Seven) },
                        Score = 14.736,
                        ReasonCode = "lead_best_control",
                        Features = new Dictionary<string, double>
                        {
                            ["StructureBreakCost"] = 0.5
                        }
                    }
                }
            };

            var engine = new RuleAIEngineV30(config, AIDifficulty.Hard, memory: null);
            var overlay = engine.DecideLead(context, decision);

            Assert.Equal(2, overlay.SelectedCards.Count);
            Assert.All(overlay.SelectedCards, card => Assert.Equal(Suit.Club, card.Suit));
            Assert.All(overlay.SelectedCards, card => Assert.Equal(Rank.Seven, card.Rank));
            Assert.NotEqual("lead009.build_void", overlay.SelectedCandidateId);
        }

        [Fact]
        public void DecideLead_SafeThrow_ShouldYieldWhenConcreteScoreIsMassivelyWorse()
        {
            var config = new GameConfig
            {
                TrumpSuit = Suit.Diamond,
                LevelRank = Rank.Two
            };

            var context = CreateContext(config, AIRole.Opponent, playerIndex: 1, trickIndex: 6, hand: new List<Card>
            {
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Club, Rank.Four)
            });

            var throwCards = new List<Card>
            {
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Heart, Rank.Queen)
            };
            var decision = new PhaseDecision
            {
                Phase = PhaseKind.Lead,
                Intent = new ResolvedIntent
                {
                    PrimaryIntent = DecisionIntentKind.TakeLead
                },
                SelectedCards = throwCards,
                ScoredActions = new List<ScoredAction>
                {
                    new ScoredAction
                    {
                        Cards = throwCards,
                        Score = -34.7119,
                        ReasonCode = "safe_throw"
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Spade, Rank.King) },
                        Score = 20.8547,
                        ReasonCode = "lead_best_control",
                        Features = new Dictionary<string, double>
                        {
                            ["StructureBreakCost"] = 0.2
                        }
                    }
                }
            };

            var engine = new RuleAIEngineV30(config, AIDifficulty.Hard, memory: null);
            var overlay = engine.DecideLead(context, decision);

            Assert.Single(overlay.SelectedCards);
            Assert.Equal(Suit.Spade, overlay.SelectedCards[0].Suit);
            Assert.Equal(Rank.King, overlay.SelectedCards[0].Rank);
            Assert.NotEqual("lead005.safe_throw.high", overlay.SelectedCandidateId);
        }

        private static RuleAIContext CreateContext(
            GameConfig config,
            AIRole role,
            int playerIndex,
            int trickIndex,
            List<Card> hand)
        {
            return new RuleAIContext
            {
                Phase = PhaseKind.Lead,
                Role = role,
                Difficulty = AIDifficulty.Hard,
                PlayerIndex = playerIndex,
                DealerIndex = role == AIRole.Dealer ? playerIndex : 0,
                MyHand = hand,
                GameConfig = config,
                RuleProfile = RuleProfile.FromConfig(config),
                DifficultyProfile = DifficultyProfile.From(AIDifficulty.Hard),
                StyleProfile = StyleProfile.Create(23),
                HandProfile = new HandProfile
                {
                    TrumpCount = hand.FindAll(config.IsTrump).Count,
                    HighTrumpCount = 0,
                    ScoreCardCount = hand.FindAll(card => card.Score > 0).Count,
                    SuitLengths = new Dictionary<Suit, int>
                    {
                        [Suit.Spade] = hand.FindAll(card => !config.IsTrump(card) && card.Suit == Suit.Spade).Count,
                        [Suit.Heart] = hand.FindAll(card => !config.IsTrump(card) && card.Suit == Suit.Heart).Count,
                        [Suit.Diamond] = hand.FindAll(card => !config.IsTrump(card) && card.Suit == Suit.Diamond).Count,
                        [Suit.Club] = hand.FindAll(card => !config.IsTrump(card) && card.Suit == Suit.Club).Count
                    }
                },
                MemorySnapshot = new MemorySnapshot(),
                InferenceSnapshot = new InferenceSnapshot(),
                DecisionFrame = new DecisionFrame
                {
                    PhaseKind = PhaseKind.Lead,
                    TrickIndex = trickIndex,
                    TurnIndex = trickIndex * 3,
                    PlayPosition = 1,
                    CardsLeftMin = hand.Count,
                    BottomRiskPressure = RiskLevel.Low,
                    DealerRetentionRisk = RiskLevel.Low,
                    BottomContestPressure = RiskLevel.Low
                }
            };
        }
    }
}
