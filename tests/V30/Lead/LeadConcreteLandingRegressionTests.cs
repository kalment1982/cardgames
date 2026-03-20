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
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Ten),
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
                        Cards = new List<Card> { new Card(Suit.Heart, Rank.Ten), new Card(Suit.Heart, Rank.Ten) },
                        Score = 39.0,
                        ReasonCode = "lead_best_control"
                    }
                }
            };

            var engine = new RuleAIEngineV30(config, AIDifficulty.Hard, memory: null);
            var overlay = engine.DecideLead(context, decision);

            Assert.Equal(2, overlay.SelectedCards.Count);
            Assert.All(overlay.SelectedCards, card => Assert.Equal(Suit.Heart, card.Suit));
            Assert.All(overlay.SelectedCards, card => Assert.Equal(Rank.Ten, card.Rank));
        }

        [Fact]
        public void DecideLead_DealerOpening_ShouldNotUseWeakLowPairAsStableSide()
        {
            var config = new GameConfig
            {
                TrumpSuit = Suit.Club,
                LevelRank = Rank.Two
            };

            var context = CreateContext(config, AIRole.Dealer, playerIndex: 0, trickIndex: 1, hand: new List<Card>
            {
                new Card(Suit.Heart, Rank.Three),
                new Card(Suit.Heart, Rank.Three),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Spade, Rank.Four),
                new Card(Suit.Diamond, Rank.Seven),
                new Card(Suit.Club, Rank.Jack),
                new Card(Suit.Club, Rank.Queen)
            });

            var decision = new PhaseDecision
            {
                Phase = PhaseKind.Lead,
                Intent = new ResolvedIntent { PrimaryIntent = DecisionIntentKind.TakeLead },
                SelectedCards = new List<Card> { new Card(Suit.Heart, Rank.Three), new Card(Suit.Heart, Rank.Three) },
                ScoredActions = new List<ScoredAction>
                {
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Heart, Rank.Three), new Card(Suit.Heart, Rank.Three) },
                        Score = 30.0,
                        ReasonCode = "lead_best_control"
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Spade, Rank.Four) },
                        Score = 20.0,
                        ReasonCode = "probe"
                    }
                }
            };

            var engine = new RuleAIEngineV30(config, AIDifficulty.Hard, memory: null);
            var overlay = engine.DecideLead(context, decision);

            Assert.Single(overlay.SelectedCards);
            Assert.Equal(Suit.Spade, overlay.SelectedCards[0].Suit);
            Assert.Equal(Rank.Four, overlay.SelectedCards[0].Rank);
        }

        [Fact]
        public void DecideLead_DealerOpening_ShouldNotUseBareKingAsStableSide()
        {
            var config = new GameConfig
            {
                TrumpSuit = Suit.Club,
                LevelRank = Rank.Two
            };

            var context = CreateContext(config, AIRole.Dealer, playerIndex: 0, trickIndex: 1, hand: new List<Card>
            {
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Heart, Rank.Four),
                new Card(Suit.Diamond, Rank.Seven),
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Club, Rank.Jack),
                new Card(Suit.Club, Rank.Queen)
            });

            var decision = new PhaseDecision
            {
                Phase = PhaseKind.Lead,
                Intent = new ResolvedIntent { PrimaryIntent = DecisionIntentKind.TakeLead },
                SelectedCards = new List<Card> { new Card(Suit.Spade, Rank.King) },
                ScoredActions = new List<ScoredAction>
                {
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Spade, Rank.King) },
                        Score = 25.0,
                        ReasonCode = "lead_best_control"
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Heart, Rank.Four) },
                        Score = 18.0,
                        ReasonCode = "probe"
                    }
                }
            };

            var engine = new RuleAIEngineV30(config, AIDifficulty.Hard, memory: null);
            var overlay = engine.DecideLead(context, decision);

            Assert.Single(overlay.SelectedCards);
            Assert.Equal(Suit.Heart, overlay.SelectedCards[0].Suit);
            Assert.Equal(Rank.Four, overlay.SelectedCards[0].Rank);
        }

        [Fact]
        public void DecideLead_DealerOpening_ProbeShouldAvoidWeakLowPairWhenSingleProbeExists()
        {
            var config = new GameConfig
            {
                TrumpSuit = Suit.Club,
                LevelRank = Rank.Two
            };

            var context = CreateContext(config, AIRole.Dealer, playerIndex: 0, trickIndex: 1, hand: new List<Card>
            {
                new Card(Suit.Heart, Rank.Three),
                new Card(Suit.Heart, Rank.Three),
                new Card(Suit.Spade, Rank.Four),
                new Card(Suit.Diamond, Rank.Seven),
                new Card(Suit.Club, Rank.Jack),
                new Card(Suit.Club, Rank.Queen)
            });

            var decision = new PhaseDecision
            {
                Phase = PhaseKind.Lead,
                Intent = new ResolvedIntent { PrimaryIntent = DecisionIntentKind.TakeLead },
                SelectedCards = new List<Card> { new Card(Suit.Heart, Rank.Three), new Card(Suit.Heart, Rank.Three) },
                ScoredActions = new List<ScoredAction>
                {
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Heart, Rank.Three), new Card(Suit.Heart, Rank.Three) },
                        Score = 18.0,
                        ReasonCode = "probe"
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Spade, Rank.Four) },
                        Score = 17.2,
                        ReasonCode = "probe"
                    }
                }
            };

            var engine = new RuleAIEngineV30(config, AIDifficulty.Hard, memory: null);
            var overlay = engine.DecideLead(context, decision);

            Assert.Single(overlay.SelectedCards);
            Assert.Equal(Suit.Spade, overlay.SelectedCards[0].Suit);
            Assert.Equal(Rank.Four, overlay.SelectedCards[0].Rank);
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

            Assert.Single(overlay.SelectedCards);
            Assert.Equal(Suit.Club, overlay.SelectedCards[0].Suit);
            Assert.Equal(Rank.Nine, overlay.SelectedCards[0].Rank);
            Assert.NotEqual(Rank.King, overlay.SelectedCards[0].Rank);
        }

        [Fact]
        public void DecideLead_ForceTrump_ShouldAvoidHighControlTrumpOutsidePressure()
        {
            var config = new GameConfig
            {
                TrumpSuit = Suit.Club,
                LevelRank = Rank.Two
            };

            var context = CreateContext(config, AIRole.Dealer, playerIndex: 0, trickIndex: 14, hand: new List<Card>
            {
                new Card(Suit.Club, Rank.Ace),
                new Card(Suit.Club, Rank.Five),
                new Card(Suit.Diamond, Rank.Nine),
                new Card(Suit.Diamond, Rank.Five),
                new Card(Suit.Heart, Rank.Queen)
            },
            cardsLeftMin: 12);

            var decision = new PhaseDecision
            {
                Phase = PhaseKind.Lead,
                Intent = new ResolvedIntent
                {
                    PrimaryIntent = DecisionIntentKind.ForceTrump
                },
                SelectedCards = new List<Card> { new Card(Suit.Club, Rank.Ace) },
                ScoredActions = new List<ScoredAction>
                {
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Club, Rank.Ace) },
                        Score = 22.0,
                        ReasonCode = "force_trump",
                        Features = new Dictionary<string, double>
                        {
                            ["HighControlLossCost"] = 2.0,
                            ["TrumpConsumptionCost"] = 1.0
                        }
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Diamond, Rank.Nine) },
                        Score = 21.5,
                        ReasonCode = "probe"
                    }
                }
            };

            var engine = new RuleAIEngineV30(config, AIDifficulty.Hard, memory: null);
            var overlay = engine.DecideLead(context, decision);

            Assert.Single(overlay.SelectedCards);
            Assert.NotEqual(Rank.Ace, overlay.SelectedCards[0].Rank);
        }

        [Fact]
        public void DecideLead_DealerSideEarly_ShouldNotForceTrumpWhenCheapSideProbeExists()
        {
            var config = new GameConfig
            {
                TrumpSuit = Suit.Heart,
                LevelRank = Rank.Two
            };

            var context = CreateContext(config, AIRole.Dealer, playerIndex: 0, trickIndex: 3, hand: new List<Card>
            {
                new Card(Suit.Heart, Rank.Three),
                new Card(Suit.Heart, Rank.Four),
                new Card(Suit.Spade, Rank.Seven),
                new Card(Suit.Diamond, Rank.Six),
                new Card(Suit.Club, Rank.Nine),
                new Card(Suit.Club, Rank.Queen)
            },
            cardsLeftMin: 12);

            var decision = new PhaseDecision
            {
                Phase = PhaseKind.Lead,
                Intent = new ResolvedIntent
                {
                    PrimaryIntent = DecisionIntentKind.ForceTrump
                },
                SelectedCards = new List<Card> { new Card(Suit.Heart, Rank.Three) },
                ScoredActions = new List<ScoredAction>
                {
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Heart, Rank.Three) },
                        Score = 16.0,
                        ReasonCode = "force_trump",
                        Features = new Dictionary<string, double>
                        {
                            ["HighControlLossCost"] = 0.0,
                            ["TrumpConsumptionCost"] = 0.0
                        }
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Spade, Rank.Seven) },
                        Score = 12.0,
                        ReasonCode = "probe",
                        Features = new Dictionary<string, double>
                        {
                            ["StructureBreakCost"] = 0.0,
                            ["HighControlLossCost"] = 0.0
                        }
                    }
                }
            };

            var engine = new RuleAIEngineV30(config, AIDifficulty.Hard, memory: null);
            var overlay = engine.DecideLead(context, decision);

            Assert.Single(overlay.SelectedCards);
            Assert.Equal(Suit.Spade, overlay.SelectedCards[0].Suit);
            Assert.Equal(Rank.Seven, overlay.SelectedCards[0].Rank);
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
            },
            handProfileOverride: new HandProfile
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
            });

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

            Assert.True(
                overlay.SelectedCards.Count == 2,
                $"{string.Join(" ", overlay.SelectedCards)} | {overlay.SelectedCandidateId} | {overlay.SelectedReason}");
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

        [Fact]
        public void DecideLead_ForceTrump_ShouldYieldToBetterNonTrumpPlanWhenTrumpEvIsWorse()
        {
            var config = new GameConfig
            {
                TrumpSuit = Suit.Club,
                LevelRank = Rank.Two
            };

            var context = CreateContext(config, AIRole.Dealer, playerIndex: 0, trickIndex: 10, hand: new List<Card>
            {
                new Card(Suit.Joker, Rank.SmallJoker),
                new Card(Suit.Club, Rank.Four),
                new Card(Suit.Club, Rank.Nine),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Diamond, Rank.King)
            },
            handProfileOverride: new HandProfile
            {
                TrumpCount = 5,
                HighTrumpCount = 1,
                JokerCount = 1,
                TrumpPairCount = 1,
                ScoreCardCount = 4,
                SuitLengths = new Dictionary<Suit, int>
                {
                    [Suit.Heart] = 3,
                    [Suit.Diamond] = 1
                }
            });

            var decision = new PhaseDecision
            {
                Phase = PhaseKind.Lead,
                Intent = new ResolvedIntent
                {
                    PrimaryIntent = DecisionIntentKind.ForceTrump
                },
                SelectedCards = new List<Card> { new Card(Suit.Joker, Rank.SmallJoker) },
                ScoredActions = new List<ScoredAction>
                {
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Joker, Rank.SmallJoker) },
                        Score = -5.0811,
                        ReasonCode = "lead003.force_trump",
                        Features = new Dictionary<string, double>
                        {
                            ["HighControlLossCost"] = 1.0,
                            ["TrumpConsumptionCost"] = 1.0,
                            ["StructureBreakCost"] = 32.0
                        }
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Heart, Rank.Ten), new Card(Suit.Heart, Rank.Ten) },
                        Score = 32.0408,
                        ReasonCode = "lead_best_control"
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Heart, Rank.Five) },
                        Score = 31.8745,
                        ReasonCode = "lead_best_control"
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Diamond, Rank.King) },
                        Score = 31.2206,
                        ReasonCode = "lead_best_control"
                    }
                }
            };

            var engine = new RuleAIEngineV30(config, AIDifficulty.Hard, memory: null);
            var overlay = engine.DecideLead(context, decision);

            Assert.DoesNotContain(overlay.SelectedCards, card => card.Suit == Suit.Joker && card.Rank == Rank.SmallJoker);
            Assert.DoesNotContain(overlay.SelectedCards, config.IsTrump);
            Assert.NotEqual("lead003.force_trump", overlay.SelectedCandidateId);
        }

        [Fact]
        public void DecideLead_Probe_ShouldNotBypassPositiveConcreteWithNegativeProbe()
        {
            var config = new GameConfig
            {
                TrumpSuit = Suit.Spade,
                LevelRank = Rank.Two
            };

            var context = CreateContext(config, AIRole.Dealer, playerIndex: 0, trickIndex: 18, hand: new List<Card>
            {
                new Card(Suit.Joker, Rank.SmallJoker),
                new Card(Suit.Joker, Rank.SmallJoker),
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Spade, Rank.Five),
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Ten)
            },
            handProfileOverride: new HandProfile
            {
                TrumpCount = 5,
                HighTrumpCount = 1,
                JokerCount = 2,
                TrumpPairCount = 2,
                ScoreCardCount = 3,
                SuitLengths = new Dictionary<Suit, int>
                {
                    [Suit.Heart] = 1
                }
            });

            var decision = new PhaseDecision
            {
                Phase = PhaseKind.Lead,
                Intent = new ResolvedIntent
                {
                    PrimaryIntent = DecisionIntentKind.TakeLead
                },
                SelectedCards = new List<Card>
                {
                    new Card(Suit.Joker, Rank.SmallJoker),
                    new Card(Suit.Joker, Rank.SmallJoker)
                },
                ScoredActions = new List<ScoredAction>
                {
                    new ScoredAction
                    {
                        Cards = new List<Card>
                        {
                            new Card(Suit.Joker, Rank.SmallJoker),
                            new Card(Suit.Joker, Rank.SmallJoker)
                        },
                        Score = -10.91,
                        ReasonCode = "lead004.low_value_probe"
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Spade, Rank.Five) },
                        Score = 30.69,
                        ReasonCode = "lead_best_control"
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Spade, Rank.Ten), new Card(Suit.Spade, Rank.Ten) },
                        Score = 29.72,
                        ReasonCode = "lead_best_control"
                    }
                }
            };

            var engine = new RuleAIEngineV30(config, AIDifficulty.Hard, memory: null);
            var overlay = engine.DecideLead(context, decision);

            Assert.DoesNotContain(overlay.SelectedCards, card => card.Suit == Suit.Joker && card.Rank == Rank.SmallJoker);
        }

        [Fact]
        public void DecideLead_Probe_DealerPartnerShouldUpgradeToHigherEvNonTrumpSingle()
        {
            var config = new GameConfig
            {
                TrumpSuit = Suit.Spade,
                LevelRank = Rank.Two
            };

            var context = CreateContext(config, AIRole.DealerPartner, playerIndex: 2, trickIndex: 10, hand: new List<Card>
            {
                new Card(Suit.Diamond, Rank.Seven),
                new Card(Suit.Diamond, Rank.King),
                new Card(Suit.Diamond, Rank.Five),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Club, Rank.Four),
                new Card(Suit.Club, Rank.Seven),
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Spade, Rank.Seven),
                new Card(Suit.Heart, Rank.Four),
                new Card(Suit.Diamond, Rank.Three),
                new Card(Suit.Club, Rank.Eight)
            });

            var decision = new PhaseDecision
            {
                Phase = PhaseKind.Lead,
                Intent = new ResolvedIntent
                {
                    PrimaryIntent = DecisionIntentKind.TakeLead
                },
                SelectedCards = new List<Card> { new Card(Suit.Diamond, Rank.Seven) },
                ScoredActions = new List<ScoredAction>
                {
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Heart, Rank.King) },
                        Score = 24.0333,
                        ReasonCode = "lead_best_control"
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Diamond, Rank.King) },
                        Score = 22.0995,
                        ReasonCode = "lead_best_control"
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Diamond, Rank.Five) },
                        Score = 21.2903,
                        ReasonCode = "lead_best_control"
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Diamond, Rank.Seven) },
                        Score = 20.6871,
                        ReasonCode = "lead004.low_value_probe"
                    }
                }
            };

            var engine = new RuleAIEngineV30(config, AIDifficulty.Hard, memory: null);
            var overlay = engine.DecideLead(context, decision);

            Assert.Single(overlay.SelectedCards);
            Assert.Equal(Rank.King, overlay.SelectedCards[0].Rank);
            Assert.DoesNotContain(overlay.SelectedCards, card => card.Rank == Rank.Seven);
        }

        [Fact]
        public void DecideLead_Probe_DealerSideShouldAvoidGenericPointProbeWhenCheapZeroProbeExists()
        {
            var config = new GameConfig
            {
                TrumpSuit = Suit.Spade,
                LevelRank = Rank.Two
            };

            var context = CreateContext(
                config,
                AIRole.Dealer,
                playerIndex: 0,
                trickIndex: 6,
                hand: new List<Card>
                {
                    new Card(Suit.Diamond, Rank.Ten),
                    new Card(Suit.Diamond, Rank.Four),
                    new Card(Suit.Club, Rank.Three),
                    new Card(Suit.Club, Rank.Eight),
                    new Card(Suit.Heart, Rank.Seven),
                    new Card(Suit.Spade, Rank.Nine),
                    new Card(Suit.Spade, Rank.Queen)
                });

            var decision = new PhaseDecision
            {
                Phase = PhaseKind.Lead,
                Intent = new ResolvedIntent
                {
                    PrimaryIntent = DecisionIntentKind.TakeLead
                },
                SelectedCards = new List<Card> { new Card(Suit.Diamond, Rank.Ten) },
                ScoredActions = new List<ScoredAction>
                {
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Diamond, Rank.Ten) },
                        Score = 18.2,
                        ReasonCode = "lead004.low_value_probe"
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Diamond, Rank.Four) },
                        Score = 17.9,
                        ReasonCode = "lead_best_control"
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Spade, Rank.Nine) },
                        Score = 17.7,
                        ReasonCode = "lead_best_control",
                        Features = new Dictionary<string, double>
                        {
                            ["TrumpConsumptionCost"] = 1.0
                        }
                    }
                }
            };

            var engine = new RuleAIEngineV30(config, AIDifficulty.Hard, memory: null);
            var overlay = engine.DecideLead(context, decision);

            Assert.Single(overlay.SelectedCards);
            Assert.Equal(Suit.Diamond, overlay.SelectedCards[0].Suit);
            Assert.Equal(Rank.Four, overlay.SelectedCards[0].Rank);
        }

        [Fact]
        public void DecideLead_Probe_CriticalDealerMayUpgradeToLowControlTrumpSingle()
        {
            var config = new GameConfig
            {
                TrumpSuit = Suit.Spade,
                LevelRank = Rank.Two
            };

            var context = CreateContext(
                config,
                AIRole.Dealer,
                playerIndex: 0,
                trickIndex: 12,
                hand: new List<Card>
                {
                    new Card(Suit.Heart, Rank.Five),
                    new Card(Suit.Spade, Rank.Five),
                    new Card(Suit.Spade, Rank.Ten),
                    new Card(Suit.Spade, Rank.Ten),
                    new Card(Suit.Joker, Rank.SmallJoker),
                    new Card(Suit.Joker, Rank.SmallJoker),
                    new Card(Suit.Heart, Rank.Three),
                    new Card(Suit.Diamond, Rank.Four),
                    new Card(Suit.Club, Rank.Six),
                    new Card(Suit.Club, Rank.Nine),
                    new Card(Suit.Diamond, Rank.Seven),
                    new Card(Suit.Heart, Rank.Eight),
                    new Card(Suit.Club, Rank.Queen)
                },
                scorePressure: ScorePressureLevel.Critical);

            var decision = new PhaseDecision
            {
                Phase = PhaseKind.Lead,
                Intent = new ResolvedIntent
                {
                    PrimaryIntent = DecisionIntentKind.TakeLead
                },
                SelectedCards = new List<Card> { new Card(Suit.Heart, Rank.Five) },
                ScoredActions = new List<ScoredAction>
                {
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Spade, Rank.Ten), new Card(Suit.Spade, Rank.Ten) },
                        Score = 29.0980,
                        ReasonCode = "lead_best_control",
                        Features = new Dictionary<string, double>
                        {
                            ["StructureBreakCost"] = 2.0,
                            ["TrumpConsumptionCost"] = 2.0
                        }
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Spade, Rank.Five) },
                        Score = 28.9317,
                        ReasonCode = "lead_best_control",
                        Features = new Dictionary<string, double>
                        {
                            ["TrumpConsumptionCost"] = 1.0
                        }
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Heart, Rank.Five) },
                        Score = 26.5288,
                        ReasonCode = "lead004.low_value_probe"
                    }
                }
            };

            var engine = new RuleAIEngineV30(config, AIDifficulty.Hard, memory: null);
            var overlay = engine.DecideLead(context, decision);

            Assert.Single(overlay.SelectedCards);
            Assert.Equal(Suit.Spade, overlay.SelectedCards[0].Suit);
            Assert.Equal(Rank.Five, overlay.SelectedCards[0].Rank);
        }

        [Fact]
        public void DecideLead_Probe_CriticalPressureShouldYieldToHigherConcreteScore()
        {
            var config = new GameConfig
            {
                TrumpSuit = Suit.Heart,
                LevelRank = Rank.Two
            };

            var context = CreateContext(
                config,
                AIRole.Dealer,
                playerIndex: 0,
                trickIndex: 15,
                hand: new List<Card>
                {
                    new Card(Suit.Heart, Rank.Ten),
                    new Card(Suit.Heart, Rank.Five),
                    new Card(Suit.Joker, Rank.SmallJoker),
                    new Card(Suit.Heart, Rank.Two),
                    new Card(Suit.Spade, Rank.Ten),
                    new Card(Suit.Spade, Rank.Five),
                    new Card(Suit.Heart, Rank.Eight),
                    new Card(Suit.Diamond, Rank.Two),
                    new Card(Suit.Club, Rank.Five)
                },
                scorePressure: ScorePressureLevel.Critical);

            var decision = new PhaseDecision
            {
                Phase = PhaseKind.Lead,
                Intent = new ResolvedIntent
                {
                    PrimaryIntent = DecisionIntentKind.TakeLead
                },
                SelectedCards = new List<Card> { new Card(Suit.Heart, Rank.Eight) },
                ScoredActions = new List<ScoredAction>
                {
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Spade, Rank.Ten) },
                        Score = 20.0198,
                        ReasonCode = "lead_best_control"
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Spade, Rank.Five) },
                        Score = 19.0087,
                        ReasonCode = "lead_best_control"
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Heart, Rank.Eight) },
                        Score = 15.6946,
                        ReasonCode = "lead004.low_value_probe"
                    }
                }
            };

            var engine = new RuleAIEngineV30(config, AIDifficulty.Hard, memory: null);
            var overlay = engine.DecideLead(context, decision);

            Assert.Single(overlay.SelectedCards);
            Assert.Equal(Suit.Spade, overlay.SelectedCards[0].Suit);
            Assert.NotEqual(Rank.Eight, overlay.SelectedCards[0].Rank);
        }

        [Fact]
        public void DecideLead_Probe_FinalThreeShouldYieldToPrepareEndgameControl()
        {
            var config = new GameConfig
            {
                TrumpSuit = Suit.Spade,
                LevelRank = Rank.Ten
            };

            var context = CreateContext(
                config,
                AIRole.Dealer,
                playerIndex: 0,
                trickIndex: 21,
                hand: new List<Card>
                {
                    new Card(Suit.Joker, Rank.SmallJoker),
                    new Card(Suit.Joker, Rank.SmallJoker),
                    new Card(Suit.Spade, Rank.Ten),
                    new Card(Suit.Spade, Rank.Ten)
                },
                scorePressure: ScorePressureLevel.Critical,
                endgameLevel: EndgameLevel.FinalThree,
                cardsLeftMin: 4);

            var decision = new PhaseDecision
            {
                Phase = PhaseKind.Lead,
                Intent = new ResolvedIntent
                {
                    PrimaryIntent = DecisionIntentKind.TakeLead
                },
                SelectedCards = new List<Card>
                {
                    new Card(Suit.Joker, Rank.SmallJoker),
                    new Card(Suit.Joker, Rank.SmallJoker)
                },
                ScoredActions = new List<ScoredAction>
                {
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Spade, Rank.Ten), new Card(Suit.Spade, Rank.Ten) },
                        Score = 31.7787,
                        ReasonCode = "prepare_endgame_preserve_control",
                        Features = new Dictionary<string, double>
                        {
                            ["StructureBreakCost"] = 2.0,
                            ["TrumpConsumptionCost"] = 2.0
                        }
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Joker, Rank.SmallJoker), new Card(Suit.Joker, Rank.SmallJoker) },
                        Score = 7.1537,
                        ReasonCode = "lead004.low_value_probe",
                        Features = new Dictionary<string, double>
                        {
                            ["StructureBreakCost"] = 2.0,
                            ["HighControlLossCost"] = 2.0,
                            ["TrumpConsumptionCost"] = 2.0
                        }
                    }
                }
            };

            var engine = new RuleAIEngineV30(config, AIDifficulty.Hard, memory: null);
            var overlay = engine.DecideLead(context, decision);

            Assert.Equal(2, overlay.SelectedCards.Count);
            Assert.All(overlay.SelectedCards, card => Assert.Equal(Suit.Spade, card.Suit));
            Assert.All(overlay.SelectedCards, card => Assert.Equal(Rank.Ten, card.Rank));
            Assert.NotEqual("lead004.low_value_probe", overlay.SelectedCandidateId);
        }

        [Fact]
        public void DecideLead_FinalThreeShouldRespectLegacyPrepareEndgameSelection()
        {
            var config = new GameConfig
            {
                TrumpSuit = Suit.Spade,
                LevelRank = Rank.Ten
            };

            var context = CreateContext(
                config,
                AIRole.Dealer,
                playerIndex: 0,
                trickIndex: 21,
                hand: new List<Card>
                {
                    new Card(Suit.Joker, Rank.SmallJoker),
                    new Card(Suit.Joker, Rank.SmallJoker),
                    new Card(Suit.Spade, Rank.Ten),
                    new Card(Suit.Spade, Rank.Ten)
                },
                scorePressure: ScorePressureLevel.Critical,
                endgameLevel: EndgameLevel.FinalThree,
                cardsLeftMin: 4);

            var decision = new PhaseDecision
            {
                Phase = PhaseKind.Lead,
                Intent = new ResolvedIntent
                {
                    PrimaryIntent = DecisionIntentKind.PrepareEndgame
                },
                SelectedCards = new List<Card>
                {
                    new Card(Suit.Spade, Rank.Ten),
                    new Card(Suit.Spade, Rank.Ten)
                },
                ScoredActions = new List<ScoredAction>
                {
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Spade, Rank.Ten), new Card(Suit.Spade, Rank.Ten) },
                        Score = 31.7787,
                        ReasonCode = "prepare_endgame_preserve_control",
                        Features = new Dictionary<string, double>
                        {
                            ["StructureBreakCost"] = 2.0,
                            ["TrumpConsumptionCost"] = 2.0
                        }
                    },
                    new ScoredAction
                    {
                        Cards = new List<Card> { new Card(Suit.Joker, Rank.SmallJoker), new Card(Suit.Joker, Rank.SmallJoker) },
                        Score = 7.1537,
                        ReasonCode = "lead004.low_value_probe",
                        Features = new Dictionary<string, double>
                        {
                            ["StructureBreakCost"] = 2.0,
                            ["HighControlLossCost"] = 2.0,
                            ["TrumpConsumptionCost"] = 2.0
                        }
                    }
                }
            };

            var engine = new RuleAIEngineV30(config, AIDifficulty.Hard, memory: null);
            var overlay = engine.DecideLead(context, decision);

            Assert.Equal(2, overlay.SelectedCards.Count);
            Assert.All(overlay.SelectedCards, card => Assert.Equal(Suit.Spade, card.Suit));
            Assert.All(overlay.SelectedCards, card => Assert.Equal(Rank.Ten, card.Rank));
            Assert.Equal("lead000.best_concrete_guard", overlay.SelectedCandidateId);
        }

        private static RuleAIContext CreateContext(
            GameConfig config,
            AIRole role,
            int playerIndex,
            int trickIndex,
            List<Card> hand,
            HandProfile? handProfileOverride = null,
            ScorePressureLevel scorePressure = ScorePressureLevel.Relaxed,
            EndgameLevel endgameLevel = EndgameLevel.None,
            int? cardsLeftMin = null)
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
                HandProfile = handProfileOverride ?? new HandProfile
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
                    CardsLeftMin = cardsLeftMin ?? hand.Count,
                    BottomRiskPressure = RiskLevel.Low,
                    DealerRetentionRisk = RiskLevel.Low,
                    BottomContestPressure = RiskLevel.Low,
                    ScorePressure = scorePressure,
                    EndgameLevel = endgameLevel
                }
            };
        }
    }
}
