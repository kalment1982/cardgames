using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests.V21
{
    public class FollowCandidateGeneratorTests
    {
        [Fact]
        public void Generate_ProducesOnlyLegalFollowCandidates()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var builder = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, new CardMemory(config));
            var hand = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Three),
                new Card(Suit.Diamond, Rank.Three),
                new Card(Suit.Diamond, Rank.Eight),
                new Card(Suit.Diamond, Rank.Eight)
            };
            var lead = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Seven),
                new Card(Suit.Diamond, Rank.Seven)
            };
            var context = builder.BuildFollowContext(hand, lead, lead, AIRole.Opponent, partnerWinning: false);

            var candidates = new FollowCandidateGenerator(config).Generate(context);
            var validator = new FollowValidator(config);

            Assert.NotEmpty(candidates);
            Assert.All(candidates, candidate => Assert.True(validator.IsValidFollow(hand, lead, candidate)));
            Assert.Contains(candidates, candidate => candidate.Count == 2 && candidate.All(card => card.Rank == Rank.Three));
        }

        [Fact]
        public void Generate_WhenVoidAndTrumpCanOvercut_ProducesWinningTrumpCandidate()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Diamond };
            var builder = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, new CardMemory(config));
            var hand = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Seven),
                new Card(Suit.Diamond, Rank.Seven),
                new Card(Suit.Diamond, Rank.Queen),
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Joker, Rank.BigJoker),
                new Card(Suit.Spade, Rank.Two)
            };
            var lead = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Five)
            };
            var currentWinning = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Nine),
                new Card(Suit.Diamond, Rank.Ace)
            };
            var context = builder.BuildFollowContext(
                hand,
                lead,
                currentWinning,
                AIRole.DealerPartner,
                partnerWinning: false,
                trickScore: 15,
                cardsLeftMin: 7,
                playerIndex: 2,
                dealerIndex: 0);

            var candidates = new FollowCandidateGenerator(config).Generate(context);
            var validator = new FollowValidator(config);

            Assert.NotEmpty(candidates);
            Assert.All(candidates, candidate => Assert.True(validator.IsValidFollow(hand, lead, candidate)));
            Assert.Contains(candidates, candidate =>
                candidate.All(config.IsTrump) &&
                RuleAIUtility.CanBeatCards(config, currentWinning, candidate));
        }

        [Fact]
        public void Generate_WhenScoringTrumpSingleHasOpponentBehind_IncludesStableWinningSingles()
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

            Assert.Contains(candidates, candidate =>
                candidate.Count == 1 &&
                candidate[0].Suit == Suit.Club &&
                candidate[0].Rank == Rank.King);
            Assert.Contains(candidates, candidate =>
                candidate.Count == 1 &&
                candidate[0].Suit == Suit.Club &&
                candidate[0].Rank == Rank.Ten);
        }
    }
}
