using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V30.Bottom;
using TractorGame.Core.AI.V30.Contracts;
using TractorGame.Core.AI.V30.Explain;
using TractorGame.Core.AI.V30.Lead;
using TractorGame.Core.AI.V30.Memory;
using TractorGame.Core.Models;
using TractorGame.Tests.V30.Specs;
using Xunit;

namespace TractorGame.Tests.V30.Regression
{
    public class V30RegressionSmokeTests
    {
        [Fact]
        public void SmokePlan_ShouldCoverCoreFrozenDomains()
        {
            var smokeCases = V30TestMatrixCatalog.Cases.Where(c => c.IsSmoke).ToList();

            Assert.True(smokeCases.Count >= 5);
            Assert.Contains(smokeCases, c => c.FrozenEntryId == "Lead-001");
            Assert.Contains(smokeCases, c => c.FrozenEntryId == "Lead-007");
            Assert.Contains(smokeCases, c => c.FrozenEntryId == "Bottom-001");
            Assert.Contains(smokeCases, c => c.FrozenEntryId == "Bottom-011");
            Assert.Contains(smokeCases, c => c.FrozenEntryId == "Mate-001");
            Assert.Contains(smokeCases, c => c.Module == "Explain");
        }

        [Fact]
        public void Smoke_Contracts_ContextPipeline_ShouldBeCallable()
        {
            var config = new GameConfig
            {
                TrumpSuit = Suit.Heart,
                LevelRank = Rank.Five
            };
            var memory = new CardMemory(config);
            var contextBuilder = new RuleAIContextBuilderV30(config, AIDifficulty.Hard, memory);
            var leadPolicy = new LeadPolicyV30();
            var bottomResolver = new BottomModeResolverV30();
            var threatAssessment = new ThreatAssessmentV30();
            var explainBuilder = new DecisionBundleBuilderV30();

            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Heart, Rank.Six),
                new Card(Suit.Diamond, Rank.Five),
                new Card(Suit.Club, Rank.Four)
            };
            var contract = contextBuilder.BuildLeadContext(
                hand,
                AIRole.Dealer,
                playerIndex: 0,
                dealerIndex: 0,
                trickIndex: 1,
                turnIndex: 0,
                cardsLeftMin: hand.Count,
                defenderScore: 39,
                bottomPoints: 15);

            var leadContext = new LeadContextV30
            {
                Role = LeadRoleV30.Dealer,
                TrickIndex = contract.DecisionFrame.TrickIndex,
                HasSafeThrowPlan = true,
                SafeThrowExpectedScore = 12,
                SafeThrowFutureValue = 7,
                HasStableSideSuitRun = true,
                StableSideSuitFutureValue = 8,
                HasLostSuitControl = false,
                HasTeamSideSuitRun = false,
                TeamSideSuitFutureValue = 0,
                KeyOpponentLikelyNotVoid = true,
                HasProfitableForceTrump = true,
                ForceTrumpFutureValue = 5,
                HasClearOwnFollowUpLine = true,
                MateHasPositiveTakeoverEvidence = false,
                HasFutureThrowPlan = false,
                HasForceTrumpForThrowPlan = false,
                HasVoidBuildPlan = false,
                HasProbePlan = true
            };
            var leadDecision = leadPolicy.Decide(leadContext);

            var bottomMode = bottomResolver.Resolve(new BottomModeInputV30
            {
                Role = contract.Role,
                DefenderScore = contract.DecisionFrame.DefenderScore,
                BottomPoints = contract.DecisionFrame.BottomPoints,
                RemainingContestableScore = contract.DecisionFrame.RemainingContestableScore,
                EstimatedBottomPoints = contract.DecisionFrame.EstimatedBottomPoints,
                BottomMultiplier = 2
            });

            var threat = threatAssessment.Evaluate(new ThreatAssessmentInputV30
            {
                CandidateCanBeatCurrentWinner = true,
                RemainingPlayers = new[]
                {
                    new RemainingPlayerThreatV30 { PlayerIndex = 1, IsTeammate = false, OvertakeProbability = 0.18 },
                    new RemainingPlayerThreatV30 { PlayerIndex = 2, IsTeammate = true, OvertakeProbability = 0.00 }
                }
            });

            var bundle = explainBuilder.Build(new DecisionExplainInputV30
            {
                Phase = "Lead",
                PrimaryIntent = leadDecision.Selected.Intent.ToString(),
                SecondaryIntent = bottomMode.OperationalMode.ToString(),
                TriggeredRules = leadDecision.Selected.TriggeredRules.ToList(),
                CandidateSummary = leadDecision.Candidates.Select(candidate => new DecisionCandidateV30
                {
                    Action = new List<string> { candidate.CandidateId },
                    Score = candidate.FutureValue,
                    ReasonCode = candidate.CandidateId
                }).ToList(),
                RejectedReasons = new[] { "smoke_check" },
                SelectedAction = new[] { leadDecision.Selected.CandidateId },
                SelectedReason = leadDecision.Selected.CandidateId,
                KnownFacts = new Dictionary<string, string> { ["phase"] = contract.Phase.ToString() },
                EstimatedFacts = new[]
                {
                    new EstimatedFactV30
                    {
                        Key = "opponent_overtake_risk",
                        Value = $"{threat.OpponentOvertakeRisk:0.00}",
                        Confidence = 0.70,
                        Evidence = "threat_assessment"
                    }
                },
                WinSecurity = threat.WinSecurity.ToString(),
                BottomMode = bottomMode.OperationalMode.ToString()
            });

            Assert.Equal(PhaseKindV30.Lead, contract.Phase);
            Assert.Equal(LeadDecisionIntentV30.SafeThrow, leadDecision.Selected.Intent);
            Assert.Equal(BottomOperationalModeV30.ProtectBottomAttention, bottomMode.OperationalMode);
            Assert.Equal(TractorGame.Core.AI.V30.Memory.WinSecurityLevelV30.StableWin, threat.WinSecurity);
            Assert.Equal("Lead", bundle.Phase);
            Assert.NotEmpty(bundle.SelectedAction);
        }

        [Fact]
        public void Smoke_LeadPriority_ShouldMatchFrozenOrdering()
        {
            var policy = new LeadPolicyV30();

            var highThrowContext = new LeadContextV30
            {
                Role = LeadRoleV30.Dealer,
                TrickIndex = 1,
                HasSafeThrowPlan = true,
                SafeThrowExpectedScore = 12,
                SafeThrowFutureValue = 6,
                HasStableSideSuitRun = true,
                StableSideSuitFutureValue = 8,
                KeyOpponentLikelyNotVoid = true,
                HasProbePlan = true
            };

            var highThrowDecision = policy.Decide(highThrowContext);
            Assert.Equal("lead005.safe_throw.high", highThrowDecision.Selected.CandidateId);
            Assert.Equal(LeadDecisionIntentV30.SafeThrow, highThrowDecision.Selected.Intent);
            Assert.Equal(1, highThrowDecision.Selected.PriorityTier);

            var lowThrowContext = new LeadContextV30
            {
                Role = LeadRoleV30.Dealer,
                TrickIndex = 1,
                HasSafeThrowPlan = true,
                SafeThrowExpectedScore = 0,
                SafeThrowFutureValue = 1,
                HasStableSideSuitRun = true,
                StableSideSuitFutureValue = 9,
                KeyOpponentLikelyNotVoid = true,
                HasProbePlan = true
            };

            var lowThrowDecision = policy.Decide(lowThrowContext);
            Assert.Equal("lead001.dealer_stable_side", lowThrowDecision.Selected.CandidateId);
            Assert.Equal(LeadDecisionIntentV30.StableSideSuitRun, lowThrowDecision.Selected.Intent);
        }

        [Fact]
        public void Smoke_BottomModeSwitch_ShouldTriggerAsExpected()
        {
            var estimator = new BottomScoreEstimatorV30();
            var resolver = new BottomModeResolverV30();
            var selector = new BottomPlanSelectorV30();
            var controlPolicy = new EndgameControlPolicyV30();

            int estimatedBottom = estimator.EstimateBottomPoints(knownBottomPoints: null);
            Assert.Equal(10, estimatedBottom);

            int escalatedBottom = estimator.EstimateBottomPoints(null, new[]
            {
                new BottomScoreSignalV30
                {
                    SignalType = BottomScoreSignalTypeV30.MultiSuitExhaustedScoreUnseen,
                    Confidence = 0.9
                }
            });
            Assert.True(escalatedBottom > estimatedBottom);

            var attentionMode = resolver.Resolve(new BottomModeInputV30
            {
                Role = AIRole.Dealer,
                DefenderScore = 39,
                BottomPoints = 15,
                RemainingContestableScore = 20,
                EstimatedBottomPoints = estimatedBottom,
                BottomMultiplier = 2
            });
            Assert.Equal(BottomOperationalModeV30.ProtectBottomAttention, attentionMode.OperationalMode);

            var strongMode = resolver.Resolve(new BottomModeInputV30
            {
                Role = AIRole.Dealer,
                DefenderScore = 55,
                BottomPoints = 10,
                RemainingContestableScore = 15,
                EstimatedBottomPoints = estimatedBottom,
                BottomMultiplier = 2
            });
            Assert.Equal(BottomOperationalModeV30.StrongProtectBottom, strongMode.OperationalMode);

            var plan = selector.Select(new BottomPlanInputV30
            {
                DefenderScore = 72,
                SingleBottomGainPoints = 10,
                DoubleBottomGainPoints = 20,
                SinglePlanStability = PlanStabilityV30.Stable,
                DoublePlanStability = PlanStabilityV30.Fragile
            });
            Assert.Equal(BottomPlanGoalV30.SingleBottomPreferred, plan.Goal);

            var jokerDecision = controlPolicy.DecideJokerOrder(new JokerControlInputV30
            {
                BigJokerUnplayedLikelyInRearOpponent = true,
                RearOpponentLikelyHasStrongerTrumpStructure = false,
                SmallJokerSecurity = WinSecurityTierV30.StableWin
            });
            Assert.False(jokerDecision.ShouldPlaySmallJokerFirst);

            var controlDecision = controlPolicy.ResolveTrumpControl(new EndgameControlInputV30
            {
                OperationalMode = BottomOperationalModeV30.StrongProtectBottom,
                CurrentTrickPoints = 10
            });
            Assert.True(controlDecision.FreezeTrumpResources);
            Assert.True(controlDecision.AllowConcedeLowPointTrick);
        }

        [Fact]
        public void Smoke_MemoryThreatAssessment_ShouldProvideWinSecurityLevel()
        {
            var inference = new InferenceEngineV30();
            var knowledge = inference.ObserveFollowAction(
                playerIndex: 1,
                ledSuit: Suit.Spade,
                playedCards: new[] { new Card(Suit.Heart, Rank.Ace) });
            Assert.True(knowledge.ConfirmedVoid);

            var snapshot = inference.BuildSnapshot(new[] { knowledge });
            Assert.True(snapshot.IsConfirmedVoid(1, Suit.Spade));

            var threatAssessment = new ThreatAssessmentV30();
            var stableThreat = threatAssessment.Evaluate(new ThreatAssessmentInputV30
            {
                CandidateCanBeatCurrentWinner = true,
                RemainingPlayers = new[]
                {
                    new RemainingPlayerThreatV30 { PlayerIndex = 2, IsTeammate = false, OvertakeProbability = 0.2 }
                }
            });
            Assert.Equal(TractorGame.Core.AI.V30.Memory.WinSecurityLevelV30.StableWin, stableThreat.WinSecurity);

            var lockThreat = threatAssessment.Evaluate(new ThreatAssessmentInputV30
            {
                CandidateCanBeatCurrentWinner = true,
                RemainingPlayers = new[]
                {
                    new RemainingPlayerThreatV30 { PlayerIndex = 2, IsTeammate = true, OvertakeProbability = 0.0 }
                }
            });
            Assert.Equal(TractorGame.Core.AI.V30.Memory.WinSecurityLevelV30.LockWin, lockThreat.WinSecurity);
        }

        [Fact]
        public void Smoke_ExplainBundle_ShouldContainRequiredFields()
        {
            var builder = new DecisionBundleBuilderV30();
            var bundle = builder.Build(new DecisionExplainInputV30
            {
                Phase = "Lead",
                PrimaryIntent = "SafeThrow",
                SecondaryIntent = "ProtectBottomAttention",
                TriggeredRules = new[] { "Lead-005", "Bottom-001" },
                CandidateSummary = new[]
                {
                    new DecisionCandidateV30
                    {
                        Action = new List<string> { "lead005.safe_throw.high" },
                        Score = 12,
                        ReasonCode = "lead005.safe_throw.high"
                    }
                },
                RejectedReasons = new[] { "lead004.low_value_probe" },
                SelectedAction = new[] { "lead005.safe_throw.high" },
                SelectedReason = "lead005.safe_throw.high",
                KnownFacts = new Dictionary<string, string> { ["confirmed_void"] = "false" },
                EstimatedFacts = new[]
                {
                    new EstimatedFactV30
                    {
                        Key = "estimated_bottom_points",
                        Value = "10",
                        Confidence = 0.7,
                        Evidence = "default_estimator"
                    }
                },
                WinSecurity = "stable_win",
                BottomMode = "protect_bottom_attention"
            });

            var json = builder.Serialize(bundle, indented: true);
            foreach (var field in V30TestMatrixCatalog.RequiredExplainFields)
            {
                Assert.Contains($"\"{field}\"", json);
            }

            Assert.NotEmpty(bundle.KnownFacts);
            Assert.NotEmpty(bundle.EstimatedFacts);
        }
    }
}
