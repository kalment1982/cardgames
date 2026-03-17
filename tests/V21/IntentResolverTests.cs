using System.Collections.Generic;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V21
{
    public class IntentResolverTests
    {
        [Fact]
        public void Resolve_FollowPartnerWinning_PrefersPassToMate()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var builder = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, new CardMemory(config));
            var context = builder.BuildFollowContext(
                new List<Card> { new Card(Suit.Spade, Rank.Three), new Card(Suit.Spade, Rank.Five) },
                new List<Card> { new Card(Suit.Spade, Rank.Ace) },
                new List<Card> { new Card(Suit.Spade, Rank.Ace) },
                AIRole.Opponent,
                partnerWinning: true,
                trickScore: 10);

            var intent = new IntentResolver(config).Resolve(context, new List<List<Card>>
            {
                new List<Card> { new Card(Suit.Spade, Rank.Three) }
            });

            Assert.Equal(DecisionIntentKind.PassToMate, intent.PrimaryIntent);
            Assert.Contains("avoid_overtake_mate", intent.VetoFlags);
        }

        [Fact]
        public void Resolve_LeadHighBottomRisk_PrefersProtectBottom()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var builder = new RuleAIContextBuilder(config, AIDifficulty.Expert, null, new CardMemory(config));
            var context = builder.BuildLeadContext(
                new List<Card> { new Card(Suit.Heart, Rank.Ace), new Card(Suit.Spade, Rank.Three) },
                AIRole.Dealer,
                playerIndex: 0,
                dealerIndex: 0,
                visibleBottomCards: new List<Card> { new Card(Suit.Heart, Rank.King), new Card(Suit.Club, Rank.Ten) },
                cardsLeftMin: 5,
                defenderScore: 75);

            var intent = new IntentResolver(config).Resolve(context, new List<List<Card>>
            {
                new List<Card> { new Card(Suit.Spade, Rank.Three) }
            });

            Assert.Contains(intent.PrimaryIntent, new[] { DecisionIntentKind.PrepareEndgame, DecisionIntentKind.ProtectBottom });
            Assert.Contains("high_bottom_risk", intent.RiskFlags);
        }

        [Fact]
        public void Resolve_FollowScoringTrickWithOpponentBehind_PromotesTakeScore()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var builder = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, new CardMemory(config));
            var hand = new List<Card>
            {
                new Card(Suit.Club, Rank.Eight),
                new Card(Suit.Club, Rank.Ten),
                new Card(Suit.Club, Rank.King),
                new Card(Suit.Club, Rank.Two),
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Diamond, Rank.Seven)
            };
            var lead = new List<Card> { new Card(Suit.Club, Rank.Five) };
            var currentWinning = new List<Card> { new Card(Suit.Club, Rank.Seven) };
            var context = builder.BuildFollowContext(
                hand,
                lead,
                currentWinning,
                AIRole.Opponent,
                partnerWinning: false,
                trickScore: 5,
                playerIndex: 3,
                dealerIndex: 0,
                trickIndex: 3,
                turnIndex: 11,
                playPosition: 3,
                currentWinningPlayer: 2);
            var candidates = new FollowCandidateGenerator(config).Generate(context);

            var intent = new IntentResolver(config).Resolve(context, candidates);

            Assert.Equal(DecisionIntentKind.TakeScore, intent.PrimaryIntent);
        }

        [Fact]
        public void Resolve_FollowPartnerWinningHighScoreWithLastOpponentBehind_PromotesTakeScore()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Diamond };
            var builder = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, new CardMemory(config));
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Spade, Rank.Five),
                new Card(Suit.Spade, Rank.Six),
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Spade, Rank.Jack),
                new Card(Suit.Diamond, Rank.Nine)
            };
            var lead = new List<Card> { new Card(Suit.Heart, Rank.Ten) };
            var currentWinning = new List<Card> { new Card(Suit.Heart, Rank.Ten) };
            var context = builder.BuildFollowContext(
                hand,
                lead,
                currentWinning,
                AIRole.Opponent,
                partnerWinning: true,
                trickScore: 10,
                playerIndex: 1,
                dealerIndex: 0,
                trickIndex: 15,
                turnIndex: 59,
                playPosition: 3,
                currentWinningPlayer: 3,
                cardsLeftMin: 8);
            var candidates = new FollowCandidateGenerator(config).Generate(context);

            var intent = new IntentResolver(config).Resolve(context, candidates);

            Assert.Equal(DecisionIntentKind.TakeScore, intent.PrimaryIntent);
            Assert.Contains(intent.Mode, new[] { "ProtectMateScoringTrick", "ReinforceMateFragileLead" });
        }
    }
}
