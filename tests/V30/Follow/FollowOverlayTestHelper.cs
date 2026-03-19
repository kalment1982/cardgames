using System.Collections.Generic;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;

namespace TractorGame.Tests.V30.Follow
{
    internal static class FollowOverlayTestHelper
    {
        public static RuleAIContext BuildFollowContext(
            GameConfig config,
            List<Card> hand,
            List<Card> lead,
            List<Card> currentWinning,
            bool partnerWinning,
            int trickScore,
            List<List<Card>>? legalActions = null,
            int playerIndex = 0,
            int dealerIndex = 2,
            int playPosition = 3,
            int currentWinningPlayer = 1,
            InferenceSnapshot? inferenceSnapshot = null)
        {
            var memory = new CardMemory(config);
            var builder = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, memory);
            var context = builder.BuildFollowContext(
                hand,
                lead,
                currentWinning,
                AIRole.Opponent,
                partnerWinning,
                trickScore,
                legalActions: legalActions,
                trickIndex: 4,
                turnIndex: 16,
                playPosition: playPosition,
                currentWinningPlayer: currentWinningPlayer,
                playerIndex: playerIndex,
                dealerIndex: dealerIndex,
                cardsLeftMin: hand.Count);

            if (inferenceSnapshot == null)
                return context;

            return new RuleAIContext
            {
                Phase = context.Phase,
                Role = context.Role,
                Difficulty = context.Difficulty,
                PlayerIndex = context.PlayerIndex,
                DealerIndex = context.DealerIndex,
                MyHand = context.MyHand,
                LegalActions = context.LegalActions,
                LeadCards = context.LeadCards,
                CurrentWinningCards = context.CurrentWinningCards,
                VisibleBottomCards = context.VisibleBottomCards,
                GameConfig = context.GameConfig,
                RuleProfile = context.RuleProfile,
                DifficultyProfile = context.DifficultyProfile,
                StyleProfile = context.StyleProfile,
                HandProfile = context.HandProfile,
                MemorySnapshot = context.MemorySnapshot,
                InferenceSnapshot = inferenceSnapshot,
                DecisionFrame = context.DecisionFrame,
                BidRoundIndex = context.BidRoundIndex,
                CurrentBidPriority = context.CurrentBidPriority,
                CurrentBidPlayer = context.CurrentBidPlayer
            };
        }

        public static GameConfig CreateConfig()
        {
            return new GameConfig
            {
                TrumpSuit = Suit.Heart,
                LevelRank = Rank.Five
            };
        }
    }
}
