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
    }
}
