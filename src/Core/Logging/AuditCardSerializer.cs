using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TractorGame.Core.Models;

namespace TractorGame.Core.Logging
{
    public static class AuditCardSerializer
    {
        public static List<Dictionary<string, object?>> SerializeCards(
            IEnumerable<Card> cards,
            GameConfig? config = null)
        {
            return cards.Select(card => SerializeCard(card, config)).ToList();
        }

        public static Dictionary<string, object?> SerializeCard(Card card, GameConfig? config = null)
        {
            bool isTrump = config?.IsTrump(card) ?? false;
            bool isLevelCard = config != null && card.Rank == config.LevelRank;

            return new Dictionary<string, object?>
            {
                ["card_instance_id"] = $"{card.Suit}-{card.Rank}-{RuntimeHelpers.GetHashCode(card):x}",
                ["suit"] = card.Suit.ToString(),
                ["rank"] = card.Rank.ToString(),
                ["score"] = card.Score,
                ["text"] = card.ToString(),
                ["is_joker"] = card.IsJoker,
                ["is_level_card"] = isLevelCard,
                ["is_score_card"] = card.IsScoreCard,
                ["is_trump"] = isTrump,
                ["effective_suit"] = isTrump ? "Trump" : card.Suit.ToString(),
                ["card_category"] = config != null ? config.GetCardCategory(card).ToString() : null,
                ["trump_reason"] = GetTrumpReason(card, config)
            };
        }

        private static string? GetTrumpReason(Card card, GameConfig? config)
        {
            if (config == null)
                return null;

            if (card.IsJoker)
                return "joker";

            if (card.Rank == config.LevelRank)
                return "level_rank";

            if (config.TrumpSuit.HasValue && card.Suit == config.TrumpSuit.Value)
                return "trump_suit";

            return null;
        }
    }
}
