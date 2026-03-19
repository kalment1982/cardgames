using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI.V21;
using TractorGame.Core.AI.V30.Follow;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V30.Follow
{
    public class FollowCandidateOverlayV30Tests
    {
        [Fact]
        public void BuildAndRank_PartnerWinning_AvoidsOvertakeByDefault()
        {
            var config = FollowOverlayTestHelper.CreateConfig();
            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.Three)
            };
            var lead = new List<Card> { new Card(Suit.Spade, Rank.Nine) };
            var currentWinning = new List<Card> { new Card(Suit.Spade, Rank.King) };
            var context = FollowOverlayTestHelper.BuildFollowContext(config, hand, lead, currentWinning, partnerWinning: true, trickScore: 0);

            var overlay = new FollowCandidateOverlayV30();
            var ranked = overlay.BuildAndRank(context, new[]
            {
                new List<Card> { new Card(Suit.Spade, Rank.Ace) },
                new List<Card> { new Card(Suit.Spade, Rank.Three) }
            });

            Assert.Equal(Rank.Three, ranked[0].Cards[0].Rank);
            Assert.False(ranked[0].CanBeatCurrentWinner);
        }

        [Fact]
        public void BuildAndRank_PartnerWinning_OnScoringTrick_PrefersPassingPoints()
        {
            var config = FollowOverlayTestHelper.CreateConfig();
            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Three)
            };
            var lead = new List<Card> { new Card(Suit.Spade, Rank.Ace) };
            var currentWinning = new List<Card> { new Card(Suit.Spade, Rank.Ace) };
            var context = FollowOverlayTestHelper.BuildFollowContext(config, hand, lead, currentWinning, partnerWinning: true, trickScore: 10);
            context = FollowOverlayTestHelper.BuildFollowContext(
                config,
                hand,
                lead,
                currentWinning,
                partnerWinning: true,
                trickScore: 10,
                playPosition: 4,
                currentWinningPlayer: 2);

            var overlay = new FollowCandidateOverlayV30();
            var ranked = overlay.BuildAndRank(context, new[]
            {
                new List<Card> { new Card(Suit.Spade, Rank.Three) },
                new List<Card> { new Card(Suit.Spade, Rank.Ten) }
            });

            Assert.Equal(Rank.Ten, ranked[0].Cards[0].Rank);
            Assert.Equal(10, ranked[0].CandidatePoints);
        }

        [Fact]
        public void BuildAndRank_NotPartnerWinning_OnScoringTrick_PrefersWinningCandidate()
        {
            var config = FollowOverlayTestHelper.CreateConfig();
            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Spade, Rank.Three)
            };
            var lead = new List<Card> { new Card(Suit.Spade, Rank.Nine) };
            var currentWinning = new List<Card> { new Card(Suit.Spade, Rank.Jack) };
            var context = FollowOverlayTestHelper.BuildFollowContext(config, hand, lead, currentWinning, partnerWinning: false, trickScore: 10);

            var overlay = new FollowCandidateOverlayV30();
            var ranked = overlay.BuildAndRank(context, new[]
            {
                new List<Card> { new Card(Suit.Spade, Rank.Three) },
                new List<Card> { new Card(Suit.Spade, Rank.Queen) }
            });

            Assert.Equal(Rank.Queen, ranked[0].Cards[0].Rank);
            Assert.True(ranked[0].CanBeatCurrentWinner);
        }

        [Fact]
        public void BuildAndRank_TakeScoreSameSecurityAndCost_PrefersKeepingPointCard()
        {
            var config = new GameConfig
            {
                TrumpSuit = Suit.Heart,
                LevelRank = Rank.Five
            };
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Queen)
            };
            var lead = new List<Card> { new Card(Suit.Club, Rank.Queen) };
            var currentWinning = new List<Card> { new Card(Suit.Club, Rank.King) };
            var context = FollowOverlayTestHelper.BuildFollowContext(
                config,
                hand,
                lead,
                currentWinning,
                partnerWinning: false,
                trickScore: 10,
                playPosition: 3,
                currentWinningPlayer: 1);

            var overlay = new FollowCandidateOverlayV30();
            var ranked = overlay.BuildAndRank(context, new[]
            {
                new List<Card> { new Card(Suit.Heart, Rank.Ten) },
                new List<Card> { new Card(Suit.Heart, Rank.Queen) }
            });

            Assert.Equal(Rank.Queen, ranked[0].Cards[0].Rank);
            Assert.Equal(0, ranked[0].CandidatePoints);
            Assert.True(ranked[0].CanBeatCurrentWinner);
        }

        [Fact]
        public void BuildAndRank_ZeroPointTrick_AvoidsHighCostWinWhenCheapLossExists()
        {
            var config = new GameConfig
            {
                TrumpSuit = Suit.Spade,
                LevelRank = Rank.Two
            };
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Seven),
                new Card(Suit.Heart, Rank.Two)
            };
            var lead = new List<Card> { new Card(Suit.Heart, Rank.Queen) };
            var currentWinning = new List<Card> { new Card(Suit.Heart, Rank.Queen) };
            var context = FollowOverlayTestHelper.BuildFollowContext(
                config,
                hand,
                lead,
                currentWinning,
                partnerWinning: false,
                trickScore: 0,
                playPosition: 2,
                currentWinningPlayer: 3);

            var overlay = new FollowCandidateOverlayV30();
            var ranked = overlay.BuildAndRank(context, new[]
            {
                new List<Card> { new Card(Suit.Heart, Rank.Two) },
                new List<Card> { new Card(Suit.Heart, Rank.Jack) }
            });

            Assert.Equal(Rank.Jack, ranked[0].Cards[0].Rank);
            Assert.False(ranked[0].CanBeatCurrentWinner);
        }

        [Fact]
        public void BuildAndRank_MinimizeLoss_PrefersLowerPointDumpWhenStillLosing()
        {
            var config = FollowOverlayTestHelper.CreateConfig();
            var hand = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Five),
                new Card(Suit.Club, Rank.Ten),
                new Card(Suit.Club, Rank.Queen),
                new Card(Suit.Club, Rank.Six),
                new Card(Suit.Club, Rank.Three)
            };
            var lead = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Nine),
                new Card(Suit.Diamond, Rank.Nine),
                new Card(Suit.Diamond, Rank.Eight),
                new Card(Suit.Diamond, Rank.Eight)
            };
            var currentWinning = new List<Card>(lead);
            var context = FollowOverlayTestHelper.BuildFollowContext(
                config,
                hand,
                lead,
                currentWinning,
                partnerWinning: false,
                trickScore: 10);

            var overlay = new FollowCandidateOverlayV30();
            var ranked = overlay.BuildAndRank(context, new[]
            {
                new List<Card>
                {
                    new Card(Suit.Diamond, Rank.Five),
                    new Card(Suit.Club, Rank.Three),
                    new Card(Suit.Club, Rank.Six),
                    new Card(Suit.Club, Rank.Ten)
                },
                new List<Card>
                {
                    new Card(Suit.Diamond, Rank.Five),
                    new Card(Suit.Club, Rank.Three),
                    new Card(Suit.Club, Rank.Six),
                    new Card(Suit.Club, Rank.Queen)
                }
            });

            Assert.Equal(5, ranked[0].CandidatePoints);
            Assert.Equal(Rank.Queen, ranked[0].Cards[3].Rank);
            Assert.False(ranked[0].CanBeatCurrentWinner);
        }

        [Fact]
        public void BuildAndRank_PartnerWinning_WithRearThreat_AvoidsSendingPoints()
        {
            var config = FollowOverlayTestHelper.CreateConfig();
            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Three)
            };
            var lead = new List<Card> { new Card(Suit.Spade, Rank.Nine) };
            var currentWinning = new List<Card> { new Card(Suit.Spade, Rank.Jack) };
            var context = FollowOverlayTestHelper.BuildFollowContext(
                config,
                hand,
                lead,
                currentWinning,
                partnerWinning: true,
                trickScore: 10,
                playPosition: 2,
                currentWinningPlayer: 2,
                inferenceSnapshot: new InferenceSnapshot
                {
                    HighTrumpRiskByPlayer = new Dictionary<int, RiskEstimate>
                    {
                        [1] = new RiskEstimate { Level = RiskLevel.High, Confidence = 0.8 }
                    }
                });

            var overlay = new FollowCandidateOverlayV30();
            var ranked = overlay.BuildAndRank(context, new[]
            {
                new List<Card> { new Card(Suit.Spade, Rank.Ten) },
                new List<Card> { new Card(Suit.Spade, Rank.Three) }
            });

            Assert.Equal(Rank.Three, ranked[0].Cards[0].Rank);
            Assert.Equal(0, ranked[0].CandidatePoints);
            Assert.Contains("rear_threat", ranked[0].Reason);
        }

        [Fact]
        public void BuildAndRank_AssignsSecurityLevels()
        {
            var config = FollowOverlayTestHelper.CreateConfig();
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Six),
                new Card(Suit.Spade, Rank.Five),
                new Card(Suit.Joker, Rank.BigJoker)
            };
            var lead = new List<Card> { new Card(Suit.Heart, Rank.Six) };
            var currentWinning = new List<Card> { new Card(Suit.Heart, Rank.Six) };
            var context = FollowOverlayTestHelper.BuildFollowContext(config, hand, lead, currentWinning, partnerWinning: false, trickScore: 5);

            var overlay = new FollowCandidateOverlayV30();
            var ranked = overlay.BuildAndRank(context, new[]
            {
                new List<Card> { new Card(Suit.Spade, Rank.Five) },
                new List<Card> { new Card(Suit.Joker, Rank.BigJoker) }
            });

            Assert.Contains(ranked, candidate => candidate.Security == FollowWinSecurityV30.Lock);
            Assert.Contains(ranked, candidate => candidate.Security == FollowWinSecurityV30.Stable);
        }

        [Fact]
        public void ResolveIntent_MatchesPartnerAndWinningSignals()
        {
            var overlay = new FollowCandidateOverlayV30();

            var partnerContext = FollowOverlayTestHelper.BuildFollowContext(
                FollowOverlayTestHelper.CreateConfig(),
                new List<Card> { new Card(Suit.Spade, Rank.Three) },
                new List<Card> { new Card(Suit.Spade, Rank.Nine) },
                new List<Card> { new Card(Suit.Spade, Rank.King) },
                partnerWinning: true,
                trickScore: 10);

            var takeScoreContext = FollowOverlayTestHelper.BuildFollowContext(
                FollowOverlayTestHelper.CreateConfig(),
                new List<Card> { new Card(Suit.Spade, Rank.Ace) },
                new List<Card> { new Card(Suit.Spade, Rank.Nine) },
                new List<Card> { new Card(Suit.Spade, Rank.King) },
                partnerWinning: false,
                trickScore: 5);

            var passIntent = overlay.ResolveIntent(partnerContext, new List<FollowCandidateViewV30>
            {
                new FollowCandidateViewV30 { CanBeatCurrentWinner = false }
            });
            var takeIntent = overlay.ResolveIntent(takeScoreContext, new List<FollowCandidateViewV30>
            {
                new FollowCandidateViewV30 { CanBeatCurrentWinner = true }
            });

            Assert.Equal(FollowOverlayIntentV30.PassToMate, passIntent);
            Assert.Equal(FollowOverlayIntentV30.TakeScore, takeIntent);
        }

        [Fact]
        public void ResolveIntent_UsesTopRankedCandidateInsteadOfAnyWinningCandidate()
        {
            var overlay = new FollowCandidateOverlayV30();
            var context = FollowOverlayTestHelper.BuildFollowContext(
                FollowOverlayTestHelper.CreateConfig(),
                new List<Card> { new Card(Suit.Spade, Rank.Ace) },
                new List<Card> { new Card(Suit.Spade, Rank.Nine) },
                new List<Card> { new Card(Suit.Spade, Rank.King) },
                partnerWinning: false,
                trickScore: 0);

            var intent = overlay.ResolveIntent(context, new List<FollowCandidateViewV30>
            {
                new FollowCandidateViewV30 { CanBeatCurrentWinner = false, OverlayScore = 10 },
                new FollowCandidateViewV30 { CanBeatCurrentWinner = true, OverlayScore = 5 }
            });

            Assert.Equal(FollowOverlayIntentV30.MinimizeLoss, intent);
        }
    }
}
