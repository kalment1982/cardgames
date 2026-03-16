using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;
using TractorGame.Core.Logging;

namespace TractorGame.Core.Rules
{
    /// <summary>
    /// 出牌合法性校验器
    /// </summary>
    public class PlayValidator
    {
        private readonly GameConfig _config;

        public PlayValidator(GameConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// 检查出牌是否合法（首家出牌）
        /// </summary>
        public bool IsValidPlay(List<Card> hand, List<Card> cardsToPlay)
        {
            return IsValidPlayEx(hand, cardsToPlay).Success;
        }

        /// <summary>
        /// 检查出牌是否合法（首家出牌），返回失败原因。
        /// </summary>
        public OperationResult IsValidPlayEx(List<Card> hand, List<Card> cardsToPlay)
        {
            // 基础检查
            if (cardsToPlay == null || cardsToPlay.Count == 0)
                return OperationResult.Fail(ReasonCodes.PlayPatternInvalid);

            // 检查是否都在手牌中
            if (!AllCardsInHand(hand, cardsToPlay))
                return OperationResult.Fail(ReasonCodes.CardNotInHand);

            // 单张总是合法
            if (cardsToPlay.Count == 1)
                return OperationResult.Ok;

            // 检查牌型是否有效
            return ValidatePattern(cardsToPlay)
                ? OperationResult.Ok
                : OperationResult.Fail(ReasonCodes.PlayPatternInvalid);
        }

        /// <summary>
        /// 检查出牌是否合法（首家出牌），并验证甩牌
        /// </summary>
        public bool IsValidPlay(List<Card> hand, List<Card> cardsToPlay, List<List<Card>> otherHands)
        {
            return IsValidPlayEx(hand, cardsToPlay, otherHands).Success;
        }

        /// <summary>
        /// 检查出牌是否合法（首家出牌），并验证甩牌，返回失败原因。
        /// </summary>
        public OperationResult IsValidPlayEx(List<Card> hand, List<Card> cardsToPlay, List<List<Card>> otherHands)
        {
            // 基础检查
            var baseResult = IsValidPlayEx(hand, cardsToPlay);
            if (!baseResult.Success)
                return baseResult;

            // 单张、对子不需要额外验证
            if (cardsToPlay.Count == 1 || CardPattern.IsPair(cardsToPlay))
                return OperationResult.Ok;

            var pattern = new CardPattern(cardsToPlay, _config);
            if (pattern.IsTractor(cardsToPlay))
                return OperationResult.Ok;

            // 混合牌型（甩牌）需要验证是否能成功
            return ValidateThrowEx(cardsToPlay, otherHands);
        }

        /// <summary>
        /// 验证甩牌是否能成功
        /// </summary>
        private bool ValidateThrow(List<Card> throwCards, List<List<Card>> otherHands)
        {
            return ValidateThrowEx(throwCards, otherHands).Success;
        }

        private OperationResult ValidateThrowEx(List<Card> throwCards, List<List<Card>> otherHands)
        {
            // 检查是否同花色
            if (!IsSameSuitOrTrump(throwCards))
                return OperationResult.Fail(ReasonCodes.ThrowNotMax);

            // 如果没有提供其他玩家手牌，只能做基础验证
            if (otherHands == null || otherHands.Count == 0)
                return OperationResult.Ok;

            var throwValidator = new ThrowValidator(_config);
            var check = throwValidator.AnalyzeThrow(throwCards, otherHands);
            return check.Success
                ? OperationResult.Ok
                : OperationResult.Fail(ReasonCodes.ThrowNotMax, check.Detail);
        }

        /// <summary>
        /// 检查所有牌是否都在手牌中
        /// </summary>
        private bool AllCardsInHand(List<Card> hand, List<Card> cardsToPlay)
        {
            var handCopy = new List<Card>(hand);

            foreach (var card in cardsToPlay)
            {
                var found = handCopy.FirstOrDefault(c => c.Equals(card));
                if (found == null) return false;
                handCopy.Remove(found);
            }

            return true;
        }

        /// <summary>
        /// 验证牌型是否有效
        /// </summary>
        public bool ValidatePattern(List<Card> cards)
        {
            if (cards.Count == 1) return true; // 单张总是合法

            // 检查是否为对子
            if (cards.Count == 2)
                return CardPattern.IsPair(cards) || ValidateMixedPattern(cards);

            // 检查是否为拖拉机
            var pattern = new CardPattern(cards, _config);
            if (pattern.IsTractor(cards))
                return true;

            // 混合牌型（甩牌）- 需要进一步验证
            return ValidateMixedPattern(cards);
        }

        /// <summary>
        /// 验证混合牌型（甩牌）
        /// </summary>
        private bool ValidateMixedPattern(List<Card> cards)
        {
            // 混合牌型必须是同花色
            if (!IsSameSuitOrTrump(cards))
                return false;

            // 可以包含：拖拉机 + 对子 + 单张的组合
            // 这里简化处理：只要同花色就认为是合法的甩牌尝试
            return true;
        }

        /// <summary>
        /// 检查是否为同花色或同为主牌
        /// </summary>
        private bool IsSameSuitOrTrump(List<Card> cards)
        {
            bool allTrump = cards.All(c => _config.IsTrump(c));
            if (allTrump) return true;

            bool allSuit = cards.All(c => !_config.IsTrump(c));
            if (!allSuit) return false;

            // 都是副牌，检查是否同花色
            var firstSuit = cards[0].Suit;
            return cards.All(c => c.Suit == firstSuit);
        }
    }
}
