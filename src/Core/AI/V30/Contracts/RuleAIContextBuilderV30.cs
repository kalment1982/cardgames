using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V30.Contracts
{
    /// <summary>
    /// V30 上下文组装器（Coordinator-A 最小实现）。
    /// </summary>
    public sealed class RuleAIContextBuilderV30
    {
        private const int TotalScorePoints = 200;
        private const int TotalScoreCardCount = 24;

        private readonly GameConfig _config;
        private readonly AIDifficulty _difficulty;
        private readonly CardMemory? _memory;
        private readonly V30FeatureFlags _featureFlags;
        private readonly HandProfileBuilderV30 _handProfileBuilder;
        private readonly MemorySnapshotBuilderV30 _memorySnapshotBuilder;

        public RuleAIContextBuilderV30(
            GameConfig config,
            AIDifficulty difficulty = AIDifficulty.Medium,
            CardMemory? memory = null,
            V30FeatureFlags? featureFlags = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _difficulty = difficulty;
            _memory = memory;
            _featureFlags = featureFlags ?? V30FeatureFlags.Default;
            _handProfileBuilder = new HandProfileBuilderV30(config);
            _memorySnapshotBuilder = new MemorySnapshotBuilderV30();
        }

        public RuleAIContextV30 BuildLeadContext(
            List<Card>? hand,
            AIRole role,
            int playerIndex = -1,
            int dealerIndex = -1,
            List<List<Card>>? legalActions = null,
            List<Card>? visibleBottomCards = null,
            int trickIndex = 0,
            int turnIndex = 0,
            int playPosition = 1,
            int cardsLeftMin = -1,
            int currentWinningPlayer = -1,
            int currentTrickScore = 0,
            int defenderScore = 0,
            int bottomPoints = 0)
        {
            var safeHand = EnsureHand(hand);
            var bottomCards = CloneCards(visibleBottomCards);
            int actualBottomPoints = ResolveBottomPoints(bottomPoints, bottomCards);

            var handProfile = _handProfileBuilder.Build(safeHand);
            var memorySnapshot = _memorySnapshotBuilder.Build(_memory, bottomCards);
            var score = ResolveScoreSnapshot(memorySnapshot);
            int estimatedBottomPoints = EstimateBottomPoints(role, actualBottomPoints, memorySnapshot, bottomCards);
            int remainingContestable = Math.Max(0, score.RemainingScoreTotal + estimatedBottomPoints);

            var frame = new DecisionFrameV30
            {
                PhaseKind = PhaseKindV30.Lead,
                TrickIndex = trickIndex,
                TurnIndex = turnIndex,
                PlayPosition = playPosition,
                CardsLeftMin = cardsLeftMin,
                CurrentWinningPlayer = currentWinningPlayer,
                CurrentTrickScore = currentTrickScore,
                DefenderScore = defenderScore,
                BottomPoints = actualBottomPoints,
                EstimatedBottomPoints = estimatedBottomPoints,
                PlayedScoreTotal = score.PlayedScoreTotal,
                RemainingScoreTotal = score.RemainingScoreTotal,
                RemainingScoreCards = score.RemainingScoreCards,
                RemainingContestableScore = remainingContestable,
                ScorePressure = ResolveScorePressure(defenderScore),
                EndgameLevel = ResolveEndgameLevel(cardsLeftMin),
                BottomRiskPressure = ResolveBottomRiskPressure(role, defenderScore, actualBottomPoints, remainingContestable),
                DealerRetentionRisk = ResolveDealerRetentionRisk(role, defenderScore, actualBottomPoints),
                BottomContestPressure = ResolveBottomContestPressure(role, defenderScore, estimatedBottomPoints),
                ProbabilityThreshold = _featureFlags.ProbabilityThreshold
            };

            return BuildContext(
                phase: PhaseKindV30.Lead,
                hand: safeHand,
                role: role,
                frame: frame,
                handProfile: handProfile,
                memorySnapshot: memorySnapshot,
                playerIndex: playerIndex,
                dealerIndex: dealerIndex,
                legalActions: legalActions,
                leadCards: null,
                currentWinningCards: null,
                visibleBottomCards: bottomCards);
        }

        public RuleAIContextV30 BuildFollowContext(
            List<Card>? hand,
            List<Card>? leadCards,
            List<Card>? currentWinningCards,
            AIRole role,
            bool partnerWinning,
            int trickScore = 0,
            int cardsLeftMin = -1,
            int playerIndex = -1,
            int dealerIndex = -1,
            List<List<Card>>? legalActions = null,
            List<Card>? visibleBottomCards = null,
            int trickIndex = 0,
            int turnIndex = 0,
            int playPosition = 0,
            int currentWinningPlayer = -1,
            int defenderScore = 0,
            int bottomPoints = 0)
        {
            var safeHand = EnsureHand(hand);
            if (_featureFlags.StrictContractValidation && (leadCards == null || leadCards.Count == 0))
                throw new ArgumentException("Follow context requires non-empty leadCards.", nameof(leadCards));

            var leadSnapshot = CloneCards(leadCards);
            var winningSnapshot = CloneCards(currentWinningCards != null && currentWinningCards.Count > 0
                ? currentWinningCards
                : leadSnapshot);
            var bottomCards = CloneCards(visibleBottomCards);
            int actualBottomPoints = ResolveBottomPoints(bottomPoints, bottomCards);

            var handProfile = _handProfileBuilder.Build(safeHand);
            var memorySnapshot = _memorySnapshotBuilder.Build(_memory, bottomCards);
            var score = ResolveScoreSnapshot(memorySnapshot);
            int estimatedBottomPoints = EstimateBottomPoints(role, actualBottomPoints, memorySnapshot, bottomCards);
            int remainingContestable = Math.Max(0, score.RemainingScoreTotal + estimatedBottomPoints);

            var frame = new DecisionFrameV30
            {
                PhaseKind = PhaseKindV30.Follow,
                TrickIndex = trickIndex,
                TurnIndex = turnIndex,
                PlayPosition = playPosition,
                CardsLeftMin = cardsLeftMin,
                CurrentWinningPlayer = currentWinningPlayer,
                PartnerWinning = partnerWinning,
                LeadCards = leadSnapshot,
                CurrentWinningCards = winningSnapshot,
                CurrentTrickScore = trickScore,
                DefenderScore = defenderScore,
                BottomPoints = actualBottomPoints,
                EstimatedBottomPoints = estimatedBottomPoints,
                PlayedScoreTotal = score.PlayedScoreTotal,
                RemainingScoreTotal = score.RemainingScoreTotal,
                RemainingScoreCards = score.RemainingScoreCards,
                RemainingContestableScore = remainingContestable,
                ScorePressure = ResolveScorePressure(defenderScore),
                EndgameLevel = ResolveEndgameLevel(cardsLeftMin),
                BottomRiskPressure = ResolveBottomRiskPressure(role, defenderScore, actualBottomPoints, remainingContestable),
                DealerRetentionRisk = ResolveDealerRetentionRisk(role, defenderScore, actualBottomPoints),
                BottomContestPressure = ResolveBottomContestPressure(role, defenderScore, estimatedBottomPoints),
                ProbabilityThreshold = _featureFlags.ProbabilityThreshold
            };

            return BuildContext(
                phase: PhaseKindV30.Follow,
                hand: safeHand,
                role: role,
                frame: frame,
                handProfile: handProfile,
                memorySnapshot: memorySnapshot,
                playerIndex: playerIndex,
                dealerIndex: dealerIndex,
                legalActions: legalActions,
                leadCards: leadSnapshot,
                currentWinningCards: winningSnapshot,
                visibleBottomCards: bottomCards);
        }

        public RuleAIContextV30 BuildBuryContext(
            List<Card>? hand,
            AIRole role = AIRole.Dealer,
            int playerIndex = -1,
            int dealerIndex = -1,
            List<List<Card>>? legalActions = null,
            List<Card>? visibleBottomCards = null,
            int defenderScore = 0,
            int cardsLeftMin = -1)
        {
            var safeHand = EnsureHand(hand);
            var bottomCards = CloneCards(visibleBottomCards);
            int actualBottomPoints = ResolveBottomPoints(0, bottomCards);

            var handProfile = _handProfileBuilder.Build(safeHand);
            var memorySnapshot = _memorySnapshotBuilder.Build(_memory, bottomCards);
            var score = ResolveScoreSnapshot(memorySnapshot);
            int estimatedBottomPoints = EstimateBottomPoints(role, actualBottomPoints, memorySnapshot, bottomCards);
            int remainingContestable = Math.Max(0, score.RemainingScoreTotal + estimatedBottomPoints);

            var frame = new DecisionFrameV30
            {
                PhaseKind = PhaseKindV30.BuryBottom,
                PlayPosition = 1,
                CardsLeftMin = cardsLeftMin,
                DefenderScore = defenderScore,
                BottomPoints = actualBottomPoints,
                EstimatedBottomPoints = estimatedBottomPoints,
                PlayedScoreTotal = score.PlayedScoreTotal,
                RemainingScoreTotal = score.RemainingScoreTotal,
                RemainingScoreCards = score.RemainingScoreCards,
                RemainingContestableScore = remainingContestable,
                ScorePressure = ResolveScorePressure(defenderScore),
                EndgameLevel = ResolveEndgameLevel(cardsLeftMin),
                BottomRiskPressure = ResolveBottomRiskPressure(role, defenderScore, actualBottomPoints, remainingContestable),
                DealerRetentionRisk = ResolveDealerRetentionRisk(role, defenderScore, actualBottomPoints),
                BottomContestPressure = ResolveBottomContestPressure(role, defenderScore, estimatedBottomPoints),
                ProbabilityThreshold = _featureFlags.ProbabilityThreshold
            };

            return BuildContext(
                phase: PhaseKindV30.BuryBottom,
                hand: safeHand,
                role: role,
                frame: frame,
                handProfile: handProfile,
                memorySnapshot: memorySnapshot,
                playerIndex: playerIndex,
                dealerIndex: dealerIndex,
                legalActions: legalActions,
                leadCards: null,
                currentWinningCards: null,
                visibleBottomCards: bottomCards);
        }

        private RuleAIContextV30 BuildContext(
            PhaseKindV30 phase,
            List<Card> hand,
            AIRole role,
            DecisionFrameV30 frame,
            HandProfileV30 handProfile,
            MemorySnapshotV30 memorySnapshot,
            int playerIndex,
            int dealerIndex,
            List<List<Card>>? legalActions,
            List<Card>? leadCards,
            List<Card>? currentWinningCards,
            List<Card>? visibleBottomCards)
        {
            return new RuleAIContextV30
            {
                Phase = phase,
                Role = role,
                Difficulty = _difficulty,
                PlayerIndex = playerIndex,
                DealerIndex = dealerIndex,
                MyHand = hand,
                LegalActions = CloneCandidateSets(legalActions),
                LeadCards = CloneCards(leadCards),
                CurrentWinningCards = CloneCards(currentWinningCards),
                VisibleBottomCards = CloneCards(visibleBottomCards),
                GameConfig = _config,
                FeatureFlags = _featureFlags,
                HandProfile = handProfile,
                MemorySnapshot = memorySnapshot,
                DecisionFrame = frame
            };
        }

        private List<Card> EnsureHand(List<Card>? hand)
        {
            if (_featureFlags.StrictContractValidation && hand == null)
                throw new ArgumentNullException(nameof(hand), "Context build requires non-null hand.");

            return hand == null ? new List<Card>() : new List<Card>(hand);
        }

        private int ResolveBottomPoints(int suppliedBottomPoints, List<Card> bottomCards)
        {
            if (suppliedBottomPoints > 0)
                return suppliedBottomPoints;

            if (bottomCards.Count == 0)
                return 0;

            return bottomCards.Sum(card => card.Score);
        }

        private (int PlayedScoreTotal, int RemainingScoreTotal, int RemainingScoreCards) ResolveScoreSnapshot(MemorySnapshotV30 snapshot)
        {
            int playedScoreTotal = snapshot.PlayedScoreTotal;
            int playedScoreCards = snapshot.PlayedScoreCardCount;

            int remainingScoreTotal = Math.Max(0, TotalScorePoints - playedScoreTotal);
            int remainingScoreCards = Math.Max(0, TotalScoreCardCount - playedScoreCards);

            return (playedScoreTotal, remainingScoreTotal, remainingScoreCards);
        }

        private int EstimateBottomPoints(AIRole role, int actualBottomPoints, MemorySnapshotV30 snapshot, List<Card> bottomCards)
        {
            if (bottomCards.Count > 0)
                return Math.Max(0, actualBottomPoints);

            if (role == AIRole.Dealer && actualBottomPoints > 0)
                return actualBottomPoints;

            int estimate = Math.Max(0, _featureFlags.DefaultBottomEstimatePoints);
            if (!_featureFlags.EnableBottomSignalBoost)
                return estimate;

            int boosted = estimate + ComputeBottomSignalBoost(snapshot);
            int upperBound = estimate + Math.Max(0, _featureFlags.BottomSignalBoostCap);
            return Math.Min(boosted, upperBound);
        }

        private int ComputeBottomSignalBoost(MemorySnapshotV30 snapshot)
        {
            var suits = new[] { "Spade", "Heart", "Club", "Diamond" };
            int fullVoidSuitCount = 0;
            foreach (var suit in suits)
            {
                int playersVoid = 0;
                for (int player = 0; player < 4; player++)
                {
                    if (snapshot.VoidSuitsByPlayer.TryGetValue(player, out var suitTexts) &&
                        suitTexts.Any(text => string.Equals(text, suit, StringComparison.OrdinalIgnoreCase)))
                    {
                        playersVoid++;
                    }
                }

                if (playersVoid == 4)
                    fullVoidSuitCount++;
            }

            if (fullVoidSuitCount <= 0)
                return 0;

            if (snapshot.PlayedScoreTotal >= TotalScorePoints)
                return 0;

            return fullVoidSuitCount >= 2 ? 15 : 10;
        }

        private static ScorePressureLevelV30 ResolveScorePressure(int defenderScore)
        {
            if (defenderScore >= 60) return ScorePressureLevelV30.Critical;
            if (defenderScore >= 40) return ScorePressureLevelV30.Tight;
            return ScorePressureLevelV30.Relaxed;
        }

        private static EndgameLevelV30 ResolveEndgameLevel(int cardsLeftMin)
        {
            if (cardsLeftMin < 0) return EndgameLevelV30.None;
            if (cardsLeftMin <= 2) return EndgameLevelV30.LastTrickRace;
            if (cardsLeftMin <= 6) return EndgameLevelV30.FinalThree;
            if (cardsLeftMin <= 12) return EndgameLevelV30.Late;
            return EndgameLevelV30.None;
        }

        private static RiskLevelV30 ResolveBottomRiskPressure(
            AIRole role,
            int defenderScore,
            int bottomPoints,
            int remainingContestableScore)
        {
            bool dealerSide = role == AIRole.Dealer || role == AIRole.DealerPartner;
            if (!dealerSide)
                return RiskLevelV30.None;

            if (defenderScore + bottomPoints * 2 >= 70)
                return RiskLevelV30.High;

            if (defenderScore + remainingContestableScore > 50 && bottomPoints >= 15)
                return RiskLevelV30.Medium;

            if (bottomPoints >= 11)
                return RiskLevelV30.Low;

            return RiskLevelV30.None;
        }

        private static RiskLevelV30 ResolveDealerRetentionRisk(AIRole role, int defenderScore, int bottomPoints)
        {
            bool dealerSide = role == AIRole.Dealer || role == AIRole.DealerPartner;
            if (!dealerSide)
                return RiskLevelV30.None;

            if (defenderScore + bottomPoints * 2 >= 70)
                return RiskLevelV30.High;

            if (defenderScore + bottomPoints >= 60)
                return RiskLevelV30.Medium;

            return RiskLevelV30.Low;
        }

        private static RiskLevelV30 ResolveBottomContestPressure(AIRole role, int defenderScore, int estimatedBottomPoints)
        {
            if (role != AIRole.Opponent)
                return RiskLevelV30.None;

            if (defenderScore + estimatedBottomPoints * 2 >= 80)
                return RiskLevelV30.High;

            if (defenderScore >= 60)
                return RiskLevelV30.Medium;

            if (defenderScore >= 50)
                return RiskLevelV30.Low;

            return RiskLevelV30.None;
        }

        private static List<Card> CloneCards(List<Card>? cards)
        {
            return cards == null ? new List<Card>() : new List<Card>(cards);
        }

        private static List<List<Card>> CloneCandidateSets(List<List<Card>>? candidates)
        {
            return candidates == null
                ? new List<List<Card>>()
                : candidates.Select(CloneCards).ToList();
        }
    }
}

