using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests.V21
{
    public class LeadCandidateGeneratorTests
    {
        [Fact]
        public void Generate_IncludesStructuredLeadCandidates()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var builder = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, new CardMemory(config));
            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Spade, Rank.Seven),
                new Card(Suit.Spade, Rank.Seven),
                new Card(Suit.Heart, Rank.Ace)
            };
            var context = builder.BuildLeadContext(hand, AIRole.Opponent);

            var candidates = new LeadCandidateGenerator(config).Generate(context);

            Assert.Contains(candidates, candidate => candidate.Count == 6);
            Assert.Contains(candidates, candidate => candidate.Count == 2);
            Assert.Contains(candidates, candidate => candidate.Count == 1);
            Assert.Equal(candidates.Count, candidates.Select(BuildKey).Distinct().Count());
        }

        [Fact]
        public void Generate_IncludesDeterministicSafeThrow_InProtectBottomEndgame()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Diamond };
            var memory = new CardMemory(config);
            RecordPlayedCards(memory, BuildPlayedSpadesForSafeEndgame(config));
            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Spade, Rank.Seven),
                new Card(Suit.Spade, Rank.Four)
            };
            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, memory).BuildLeadContext(
                hand,
                AIRole.DealerPartner,
                playerIndex: 2,
                dealerIndex: 0,
                cardsLeftMin: 3,
                defenderScore: 80,
                bottomPoints: 20);

            var candidates = new LeadCandidateGenerator(config, memory).Generate(context);

            Assert.Contains(candidates, candidate => candidate.Count == 3 && candidate.All(card => card.Suit == Suit.Spade));
        }

        private static void RecordPlayedCards(CardMemory memory, List<Card> cards)
        {
            for (int index = 0; index < cards.Count; index += 4)
            {
                var trick = new List<TrickPlay>();
                var chunk = cards.Skip(index).Take(4).ToList();
                for (int offset = 0; offset < chunk.Count; offset++)
                {
                    trick.Add(new TrickPlay(offset, new List<Card> { chunk[offset] }));
                }

                memory.RecordTrick(trick);
            }
        }

        private static List<Card> BuildPlayedSpadesForSafeEndgame(GameConfig config)
        {
            var cards = new List<Card>();
            var ranks = new[]
            {
                Rank.Ace, Rank.King, Rank.Queen, Rank.Jack, Rank.Ten, Rank.Nine,
                Rank.Six, Rank.Five, Rank.Three
            };

            foreach (var rank in ranks)
            {
                cards.Add(new Card(Suit.Spade, rank));
                cards.Add(new Card(Suit.Spade, rank));
            }

            cards.Add(new Card(Suit.Spade, Rank.Eight));
            cards.Add(new Card(Suit.Spade, Rank.Seven));
            cards.Add(new Card(Suit.Spade, Rank.Four));

            Assert.All(cards, card => Assert.False(config.IsTrump(card)));
            return cards;
        }

        private static string BuildKey(List<Card> cards)
        {
            return string.Join(",", cards.Select(card => $"{(int)card.Suit}-{(int)card.Rank}"));
        }
    }
}
