using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V21
{
    /// <summary>
    /// 生成埋底阶段的有限候选方案。
    /// </summary>
    public sealed class BuryCandidateGenerator
    {
        private readonly GameConfig _config;

        public BuryCandidateGenerator(GameConfig config)
        {
            _config = config;
        }

        public List<List<Card>> Generate(RuleAIContext context)
        {
            if (context.MyHand.Count < 8)
                return new List<List<Card>>();

            var comparer = new CardComparer(_config);
            var hand = context.MyHand;
            var candidates = new List<List<Card>>
            {
                SelectEight(hand, comparer, hand, pointWeight: 14, trumpWeight: 9, structureWeight: 8, voidWeight: 2),
                SelectEight(hand, comparer, hand, pointWeight: 12, trumpWeight: 14, structureWeight: 9, voidWeight: 2),
                SelectEight(hand, comparer, hand, pointWeight: 12, trumpWeight: 8, structureWeight: 14, voidWeight: 1),
                SelectEight(hand, comparer, hand, pointWeight: 9, trumpWeight: 7, structureWeight: 8, voidWeight: 8),
                SelectEight(hand, comparer, hand, pointWeight: 11, trumpWeight: 10, structureWeight: 10, voidWeight: 4)
            };

            return RuleAIUtility.DeduplicateCandidates(candidates)
                .Where(candidate => candidate.Count == 8)
                .ToList();
        }

        private List<Card> SelectEight(
            List<Card> hand,
            CardComparer comparer,
            List<Card> source,
            double pointWeight,
            double trumpWeight,
            double structureWeight,
            double voidWeight)
        {
            var ordered = source
                .Select(card => new
                {
                    Card = card,
                    Score = EvaluateCard(card, hand, comparer, pointWeight, trumpWeight, structureWeight, voidWeight)
                })
                .OrderBy(entry => entry.Score)
                .ThenBy(entry => entry.Card, comparer)
                .Take(8)
                .Select(entry => entry.Card)
                .ToList();

            return ordered;
        }

        private double EvaluateCard(
            Card card,
            List<Card> hand,
            CardComparer comparer,
            double pointWeight,
            double trumpWeight,
            double structureWeight,
            double voidWeight)
        {
            double score = 0;
            score += card.Score * pointWeight;
            if (_config.IsTrump(card))
                score += trumpWeight * 10;

            int structureLoss = RuleAIUtility.EstimateStructureLoss(_config, hand, new List<Card> { card }, comparer);
            score += structureLoss * structureWeight;

            if (!_config.IsTrump(card))
            {
                int sameSuitCount = hand.Count(candidate => !_config.IsTrump(candidate) && candidate.Suit == card.Suit);
                score -= sameSuitCount <= 3 ? voidWeight * 10 : 0;
            }

            score += RuleAIUtility.GetCardValue(card, _config) / 20.0;
            return score;
        }
    }
}
