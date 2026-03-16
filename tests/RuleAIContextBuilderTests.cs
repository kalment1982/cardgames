using System.Collections.Generic;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests
{
    public class RuleAIContextBuilderTests
    {
        [Fact]
        public void BuildLeadContext_PopulatesProfilesAndFrame()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var memory = new CardMemory(config);
            var builder = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, memory, sessionStyleSeed: 17);
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Club, Rank.Three)
            };

            var context = builder.BuildLeadContext(
                hand,
                AIRole.Dealer,
                playerIndex: 0,
                dealerIndex: 0,
                cardsLeftMin: 7,
                defenderScore: 55);

            Assert.Equal(PhaseKind.Lead, context.Phase);
            Assert.Equal(AIRole.Dealer, context.Role);
            Assert.Equal(AIDifficulty.Hard, context.Difficulty);
            Assert.Equal(4, context.HandCount);
            Assert.Equal(2, context.TrumpCount);
            Assert.Equal(2, context.PointCardCount);
            Assert.True(context.IsDealerSide);
            Assert.Equal(ScorePressureLevel.Tight, context.DecisionFrame.ScorePressure);
            Assert.Equal("Standard80", context.RuleProfile.Mode);
            Assert.Equal(17, context.StyleProfile.SessionStyleSeed);
            Assert.NotSame(hand, context.MyHand);
        }

        [Fact]
        public void BuildFollowContext_FallsBackToLeadCards_WhenCurrentWinnerMissing()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade };
            var builder = new RuleAIContextBuilder(config, AIDifficulty.Medium, null, new CardMemory(config));
            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Three),
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Diamond, Rank.King)
            };
            var leadCards = new List<Card>
            {
                new Card(Suit.Club, Rank.Ten),
                new Card(Suit.Club, Rank.Nine)
            };

            var context = builder.BuildFollowContext(
                hand,
                leadCards,
                null,
                AIRole.Opponent,
                partnerWinning: true,
                trickScore: 10,
                cardsLeftMin: 3);

            Assert.Equal(PhaseKind.Follow, context.Phase);
            Assert.Equal(leadCards.Count, context.CurrentWinningCards.Count);
            Assert.Equal(leadCards[0], context.CurrentWinningCards[0]);
            Assert.True(context.PartnerWinning);
            Assert.Equal(3, context.CardsLeftMin);
            Assert.Equal(10, context.TrickScore);
        }

        [Fact]
        public void BuildBuryContext_ComputesBottomRisk()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var builder = new RuleAIContextBuilder(config, AIDifficulty.Expert, null, new CardMemory(config));
            var hand = new List<Card>();
            for (int i = 0; i < 33; i++)
                hand.Add(new Card(Suit.Heart, Rank.Three));

            var bottom = new List<Card>
            {
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Club, Rank.Ten)
            };

            var context = builder.BuildBuryContext(
                hand,
                AIRole.Dealer,
                playerIndex: 0,
                dealerIndex: 0,
                visibleBottomCards: bottom,
                defenderScore: 72,
                cardsLeftMin: 5);

            Assert.Equal(PhaseKind.BuryBottom, context.Phase);
            Assert.Equal(20, context.DecisionFrame.BottomPoints);
            Assert.Equal(RiskLevel.High, context.DecisionFrame.BottomRiskPressure);
            Assert.Equal(RiskLevel.High, context.DecisionFrame.DealerRetentionRisk);
            Assert.Equal(EndgameLevel.FinalThree, context.DecisionFrame.EndgameLevel);
        }

        [Fact]
        public void BuildBidContext_PopulatesBidFields()
        {
            var config = new GameConfig { LevelRank = Rank.Five };
            var builder = new RuleAIContextBuilder(config);
            var visible = new List<Card>
            {
                new Card(Suit.Spade, Rank.Five),
                new Card(Suit.Spade, Rank.Ace)
            };

            var context = builder.BuildBidContext(
                visible,
                AIRole.Opponent,
                playerIndex: 1,
                dealerIndex: 0,
                roundIndex: 6,
                currentBidPriority: 0,
                currentBidPlayer: 3);

            Assert.Equal(PhaseKind.Bid, context.Phase);
            Assert.Equal(6, context.BidRoundIndex);
            Assert.Equal(0, context.CurrentBidPriority);
            Assert.Equal(3, context.CurrentBidPlayer);
            Assert.Equal(PhaseKind.Bid, context.DecisionFrame.PhaseKind);
        }
    }
}
