using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V30.Memory
{
    public sealed class SuitKnowledgeV30
    {
        public int PlayerIndex { get; init; }
        public Suit Suit { get; init; }
        public bool ConfirmedVoid { get; init; }
        public double ProbabilityHasSuit { get; init; }

        public SuitKnowledgeStateV30 State
        {
            get
            {
                if (ConfirmedVoid)
                    return SuitKnowledgeStateV30.ConfirmedVoid;

                if (ProbabilityHasSuit >= InferenceEngineV30.ProbabilityHasSuitThreshold)
                    return SuitKnowledgeStateV30.ProbablyHasSuit;

                if (ProbabilityHasSuit <= InferenceEngineV30.ProbabilityVoidThreshold)
                    return SuitKnowledgeStateV30.ProbablyVoid;

                return SuitKnowledgeStateV30.Unknown;
            }
        }
    }

    public sealed class InferenceSnapshotV30
    {
        private readonly Dictionary<(int PlayerIndex, Suit Suit), SuitKnowledgeV30> _knowledgeByPlayerAndSuit;

        public InferenceSnapshotV30(IEnumerable<SuitKnowledgeV30> suitKnowledge)
        {
            _knowledgeByPlayerAndSuit = suitKnowledge?
                .ToDictionary(item => (item.PlayerIndex, item.Suit), item => item)
                ?? new Dictionary<(int PlayerIndex, Suit Suit), SuitKnowledgeV30>();
        }

        public SuitKnowledgeV30 GetSuitKnowledge(int playerIndex, Suit suit)
        {
            if (_knowledgeByPlayerAndSuit.TryGetValue((playerIndex, suit), out var knowledge))
                return knowledge;

            return new SuitKnowledgeV30
            {
                PlayerIndex = playerIndex,
                Suit = suit,
                ConfirmedVoid = false,
                ProbabilityHasSuit = 0.5
            };
        }

        public bool IsConfirmedVoid(int playerIndex, Suit suit)
            => GetSuitKnowledge(playerIndex, suit).State == SuitKnowledgeStateV30.ConfirmedVoid;

        public bool IsProbablyHasSuit(int playerIndex, Suit suit)
            => GetSuitKnowledge(playerIndex, suit).State == SuitKnowledgeStateV30.ProbablyHasSuit;

        public bool IsProbablyVoid(int playerIndex, Suit suit)
            => GetSuitKnowledge(playerIndex, suit).State == SuitKnowledgeStateV30.ProbablyVoid;
    }

    public sealed class InferenceEngineV30
    {
        public const double ProbabilityHasSuitThreshold = 0.70;
        public const double ProbabilityVoidThreshold = 0.30;

        public SuitKnowledgeV30 BuildSuitKnowledge(
            int playerIndex,
            Suit suit,
            bool confirmedVoid,
            double probabilityHasSuit)
        {
            return new SuitKnowledgeV30
            {
                PlayerIndex = playerIndex,
                Suit = suit,
                ConfirmedVoid = confirmedVoid,
                ProbabilityHasSuit = ClampProbability(probabilityHasSuit)
            };
        }

        public SuitKnowledgeV30 ObserveFollowAction(
            int playerIndex,
            Suit ledSuit,
            IReadOnlyList<Card> playedCards,
            double fallbackProbabilityHasSuit = 0.50)
        {
            bool followedSuit = playedCards.Any(card => card.Suit == ledSuit);
            if (!followedSuit)
            {
                return BuildSuitKnowledge(
                    playerIndex,
                    ledSuit,
                    confirmedVoid: true,
                    probabilityHasSuit: 0.0);
            }

            // 跟出该门后，至少可判定当前轮次不是绝门。
            return BuildSuitKnowledge(
                playerIndex,
                ledSuit,
                confirmedVoid: false,
                probabilityHasSuit: Math.Max(ProbabilityHasSuitThreshold, fallbackProbabilityHasSuit));
        }

        public InferenceSnapshotV30 BuildSnapshot(IEnumerable<SuitKnowledgeV30> knowledge)
            => new InferenceSnapshotV30(knowledge);

        private static double ClampProbability(double value)
            => Math.Max(0.0, Math.Min(1.0, value));
    }
}
