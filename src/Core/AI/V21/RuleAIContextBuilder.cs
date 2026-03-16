using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V21
{
    /// <summary>
    /// 负责将公开信息、私有手牌、记牌快照和阶段信息组装成统一上下文。
    /// </summary>
    public sealed class RuleAIContextBuilder
    {
        private readonly GameConfig _config;
        private readonly AIDifficulty _difficulty;
        private readonly AIStrategyParameters _strategy;
        private readonly CardMemory? _memory;
        private readonly RuleProfile _ruleProfile;
        private readonly DifficultyProfile _difficultyProfile;
        private readonly StyleProfile _styleProfile;
        private readonly HandProfileBuilder _handProfileBuilder;
        private readonly MemorySnapshotBuilder _memorySnapshotBuilder;
        private readonly InferenceEngine _inferenceEngine;
        private readonly EndgamePolicy _endgamePolicy;

        public RuleAIContextBuilder(
            GameConfig config,
            AIDifficulty difficulty = AIDifficulty.Medium,
            AIStrategyParameters? strategy = null,
            CardMemory? memory = null,
            int sessionStyleSeed = 0)
        {
            _config = config;
            _difficulty = difficulty;
            _strategy = (strategy ?? AIStrategyParameters.CreatePreset(difficulty)).Normalize();
            _memory = memory;
            _ruleProfile = RuleProfile.FromConfig(config);
            _difficultyProfile = DifficultyProfile.From(difficulty, _strategy);
            _styleProfile = StyleProfile.Create(sessionStyleSeed);
            _handProfileBuilder = new HandProfileBuilder(config);
            _memorySnapshotBuilder = new MemorySnapshotBuilder();
            _inferenceEngine = new InferenceEngine(config);
            _endgamePolicy = new EndgamePolicy();
        }

        public RuleAIContext BuildBidContext(
            List<Card> visibleCards,
            AIRole role,
            int playerIndex = -1,
            int dealerIndex = -1,
            int roundIndex = 0,
            int currentBidPriority = -1,
            int currentBidPlayer = -1,
            List<List<Card>>? legalActions = null)
        {
            var hand = CloneCards(visibleCards);
            var frame = new DecisionFrame
            {
                PhaseKind = PhaseKind.Bid,
                PlayPosition = 1,
                CurrentWinningPlayer = currentBidPlayer
            };

            return BuildContext(
                PhaseKind.Bid,
                hand,
                role,
                frame,
                playerIndex,
                dealerIndex,
                legalActions,
                leadCards: null,
                currentWinningCards: null,
                visibleBottomCards: null,
                bidRoundIndex: roundIndex,
                currentBidPriority: currentBidPriority,
                currentBidPlayer: currentBidPlayer);
        }

        public RuleAIContext BuildBuryContext(
            List<Card> hand,
            AIRole role = AIRole.Dealer,
            int playerIndex = -1,
            int dealerIndex = -1,
            List<List<Card>>? legalActions = null,
            List<Card>? visibleBottomCards = null,
            int defenderScore = 0,
            int cardsLeftMin = -1)
        {
            var bottomCards = CloneCards(visibleBottomCards);
            var scorePressure = _endgamePolicy.ResolveScorePressure(defenderScore);
            var bottomPoints = bottomCards.Sum(card => card.Score);
            var frame = new DecisionFrame
            {
                PhaseKind = PhaseKind.BuryBottom,
                PlayPosition = 1,
                CardsLeftMin = cardsLeftMin,
                DefenderScore = defenderScore,
                BottomPoints = bottomPoints,
                ScorePressure = scorePressure,
                EndgameLevel = _endgamePolicy.ResolveEndgameLevel(cardsLeftMin),
                BottomRiskPressure = _endgamePolicy.ResolveBottomRisk(role, bottomPoints, cardsLeftMin, scorePressure),
                DealerRetentionRisk = _endgamePolicy.ResolveDealerRetentionRisk(role, defenderScore, bottomPoints, cardsLeftMin)
            };

            return BuildContext(
                PhaseKind.BuryBottom,
                CloneCards(hand),
                role,
                frame,
                playerIndex,
                dealerIndex,
                legalActions,
                leadCards: null,
                currentWinningCards: null,
                visibleBottomCards: bottomCards);
        }

        public RuleAIContext BuildLeadContext(
            List<Card> hand,
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
            var bottomCards = CloneCards(visibleBottomCards);
            if (bottomCards.Count > 0 && bottomPoints == 0)
                bottomPoints = bottomCards.Sum(card => card.Score);

            var scorePressure = _endgamePolicy.ResolveScorePressure(defenderScore);
            var frame = new DecisionFrame
            {
                PhaseKind = PhaseKind.Lead,
                TrickIndex = trickIndex,
                TurnIndex = turnIndex,
                PlayPosition = playPosition,
                CardsLeftMin = cardsLeftMin,
                CurrentWinningPlayer = currentWinningPlayer,
                CurrentTrickScore = currentTrickScore,
                DefenderScore = defenderScore,
                BottomPoints = bottomPoints,
                ScorePressure = scorePressure,
                EndgameLevel = _endgamePolicy.ResolveEndgameLevel(cardsLeftMin),
                BottomRiskPressure = _endgamePolicy.ResolveBottomRisk(role, bottomPoints, cardsLeftMin, scorePressure),
                DealerRetentionRisk = _endgamePolicy.ResolveDealerRetentionRisk(role, defenderScore, bottomPoints, cardsLeftMin)
            };

            return BuildContext(
                PhaseKind.Lead,
                CloneCards(hand),
                role,
                frame,
                playerIndex,
                dealerIndex,
                legalActions,
                leadCards: null,
                currentWinningCards: null,
                visibleBottomCards: bottomCards);
        }

        public RuleAIContext BuildFollowContext(
            List<Card> hand,
            List<Card> leadCards,
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
            var leadSnapshot = CloneCards(leadCards);
            var winningSnapshot = CloneCards(currentWinningCards != null && currentWinningCards.Count > 0
                ? currentWinningCards
                : leadCards);
            var bottomCards = CloneCards(visibleBottomCards);
            if (bottomCards.Count > 0 && bottomPoints == 0)
                bottomPoints = bottomCards.Sum(card => card.Score);

            var scorePressure = _endgamePolicy.ResolveScorePressure(defenderScore);
            var frame = new DecisionFrame
            {
                PhaseKind = PhaseKind.Follow,
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
                BottomPoints = bottomPoints,
                ScorePressure = scorePressure,
                EndgameLevel = _endgamePolicy.ResolveEndgameLevel(cardsLeftMin),
                BottomRiskPressure = _endgamePolicy.ResolveBottomRisk(role, bottomPoints, cardsLeftMin, scorePressure),
                DealerRetentionRisk = _endgamePolicy.ResolveDealerRetentionRisk(role, defenderScore, bottomPoints, cardsLeftMin)
            };

            return BuildContext(
                PhaseKind.Follow,
                CloneCards(hand),
                role,
                frame,
                playerIndex,
                dealerIndex,
                legalActions,
                leadSnapshot,
                winningSnapshot,
                bottomCards);
        }

        private RuleAIContext BuildContext(
            PhaseKind phase,
            List<Card> hand,
            AIRole role,
            DecisionFrame frame,
            int playerIndex,
            int dealerIndex,
            List<List<Card>>? legalActions,
            List<Card>? leadCards,
            List<Card>? currentWinningCards,
            List<Card>? visibleBottomCards,
            int bidRoundIndex = 0,
            int currentBidPriority = -1,
            int currentBidPlayer = -1)
        {
            var bottomCards = CloneCards(visibleBottomCards);
            var handProfile = _handProfileBuilder.Build(hand);
            var memorySnapshot = _difficultyProfile.MemoryEnabled && _memory != null
                ? _memorySnapshotBuilder.Build(_memory, bottomCards)
                : new MemorySnapshot();
            var inferenceSnapshot = _difficultyProfile.InferenceDepth > 0
                ? _inferenceEngine.Build(_memory, hand, playerIndex, new[] { 0, 1, 2, 3 }, frame.CardsLeftMin, bottomCards)
                : new InferenceSnapshot();

            return new RuleAIContext
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
                VisibleBottomCards = bottomCards,
                GameConfig = _config,
                RuleProfile = _ruleProfile,
                DifficultyProfile = _difficultyProfile,
                StyleProfile = _styleProfile,
                HandProfile = handProfile,
                MemorySnapshot = memorySnapshot,
                InferenceSnapshot = inferenceSnapshot,
                DecisionFrame = frame,
                BidRoundIndex = bidRoundIndex,
                CurrentBidPriority = currentBidPriority,
                CurrentBidPlayer = currentBidPlayer
            };
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
