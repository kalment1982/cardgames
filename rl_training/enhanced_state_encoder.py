"""
增强的状态编码器
包含：基础特征 + 对手手牌推断 + 历史出牌模式
"""
import numpy as np
from typing import List, Dict
from game_engine import GameState, Card, Suit, Rank


class EnhancedStateEncoder:
    """增强的状态编码器 - 412维特征"""

    def __init__(self):
        self.base_dim = 338
        self.opponent_belief_dim = 54
        self.play_pattern_dim = 20
        self.total_dim = self.base_dim + self.opponent_belief_dim + self.play_pattern_dim

    def encode_state(self, state: GameState, player: int) -> np.ndarray:
        """
        编码游戏状态为412维向量

        特征组成:
        - 基础特征 (338维): 手牌 + 已出牌 + 当前牌桌 + 角色 + 局势
        - 对手手牌推断 (54维): 推断对手可能的手牌分布
        - 历史出牌模式 (20维): 对手的出牌习惯和模式
        """
        # 1. 基础特征 (338维)
        base_features = self._encode_base_features(state, player)

        # 2. 对手手牌推断 (54维)
        opponent_belief = self._infer_opponent_cards(state, player)

        # 3. 历史出牌模式 (20维)
        play_patterns = self._encode_play_history(state, player)

        # 拼接所有特征
        return np.concatenate([base_features, opponent_belief, play_patterns])

    def _encode_base_features(self, state: GameState, player: int) -> np.ndarray:
        """编码基础特征 (338维)"""
        features = []

        # 1. 手牌编码 (108维)
        hand_encoding = self._encode_cards(state.hands[player])
        features.append(hand_encoding)

        # 2. 已出牌编码 (108维)
        played_cards = []
        for trick in state.played_tricks:
            for _, cards in trick:
                played_cards.extend(cards)
        played_encoding = self._encode_cards(played_cards)
        features.append(played_encoding)

        # 3. 当前牌桌编码 (108维)
        current_trick_cards = []
        for _, cards in state.current_trick:
            current_trick_cards.extend(cards)
        trick_encoding = self._encode_cards(current_trick_cards)
        features.append(trick_encoding)

        # 4. 角色特征 (4维)
        role_features = np.zeros(4)
        role_features[player] = 1.0  # one-hot编码当前玩家
        features.append(role_features)

        # 5. 局势特征 (10维)
        situation_features = self._encode_situation(state, player)
        features.append(situation_features)

        return np.concatenate(features)

    def _encode_cards(self, cards: List[Card]) -> np.ndarray:
        """编码牌列表为108维向量 (4花色 * 13点数 + 2王 = 54种牌 * 2副)"""
        encoding = np.zeros(108)

        for card in cards:
            idx = self._card_to_index(card)
            if idx < 108:
                encoding[idx] += 1.0

        return encoding

    def _card_to_index(self, card: Card) -> int:
        """将牌转换为索引 (0-107)"""
        if card.suit == Suit.JOKER:
            if card.rank == Rank.SMALL_JOKER:
                return 104  # 小王
            else:
                return 106  # 大王
        else:
            # 普通牌: suit * 26 + (rank - 2) * 2
            suit_offset = card.suit.value * 26
            rank_offset = (card.rank.value - 2) * 2
            return suit_offset + rank_offset

    def _encode_situation(self, state: GameState, player: int) -> np.ndarray:
        """编码局势特征 (10维)"""
        features = np.zeros(10)

        # 0: 是否庄家方
        features[0] = 1.0 if player in [state.dealer, (state.dealer + 2) % 4] else 0.0

        # 1: 当前轮次进度 (0-1)
        features[1] = len(state.played_tricks) / 13.0

        # 2: 手牌数量 (归一化)
        features[2] = len(state.hands[player]) / 27.0

        # 3: 是否首家出牌
        features[3] = 1.0 if len(state.current_trick) == 0 else 0.0

        # 4-7: 各玩家已出牌数量 (归一化)
        for p in range(4):
            played_count = 0
            for trick in state.played_tricks:
                for trick_player, cards in trick:
                    if trick_player == p:
                        played_count += len(cards)
            features[4 + p] = played_count / 27.0

        # 8: 主牌花色 (归一化)
        if state.trump_suit:
            features[8] = state.trump_suit.value / 4.0

        # 9: 主牌等级 (归一化)
        features[9] = state.level_rank.value / 14.0

        return features

    def _infer_opponent_cards(self, state: GameState, player: int) -> np.ndarray:
        """
        推断对手手牌分布 (54维)

        基于已出牌和当前牌桌，推断每种牌对手还可能有多少张
        """
        belief = np.zeros(54)

        # 统计每种牌已经出现的次数
        seen_cards = {}
        for trick in state.played_tricks:
            for _, cards in trick:
                for card in cards:
                    idx = self._card_to_simple_index(card)
                    seen_cards[idx] = seen_cards.get(idx, 0) + 1

        # 加上当前牌桌的牌
        for _, cards in state.current_trick:
            for card in cards:
                idx = self._card_to_simple_index(card)
                seen_cards[idx] = seen_cards.get(idx, 0) + 1

        # 加上自己的手牌
        my_cards = {}
        for card in state.hands[player]:
            idx = self._card_to_simple_index(card)
            my_cards[idx] = my_cards.get(idx, 0) + 1

        # 推断对手可能的牌
        for idx in range(54):
            total_count = 2  # 每种牌有2张
            seen_count = seen_cards.get(idx, 0)
            my_count = my_cards.get(idx, 0)

            # 对手可能有的数量 = 总数 - 已见 - 我的
            opponent_possible = max(0, total_count - seen_count - my_count)
            belief[idx] = opponent_possible / 2.0  # 归一化

        return belief

    def _card_to_simple_index(self, card: Card) -> int:
        """将牌转换为简单索引 (0-53, 不区分两副牌)"""
        if card.suit == Suit.JOKER:
            if card.rank == Rank.SMALL_JOKER:
                return 52  # 小王
            else:
                return 53  # 大王
        else:
            # 普通牌: suit * 13 + (rank - 2)
            return card.suit.value * 13 + (card.rank.value - 2)

    def _encode_play_history(self, state: GameState, player: int) -> np.ndarray:
        """
        编码历史出牌模式 (20维)

        分析对手的出牌习惯和策略倾向
        """
        patterns = np.zeros(20)

        if not state.played_tricks:
            return patterns

        # 分析每个对手的出牌模式
        opponents = [p for p in range(4) if p != player]

        for opp_idx, opp in enumerate(opponents):
            # 每个对手5个特征
            offset = opp_idx * 5

            # 统计该对手的出牌特征
            total_plays = 0
            big_card_plays = 0  # 出大牌次数
            point_card_plays = 0  # 出分牌次数
            trump_plays = 0  # 出主牌次数
            pair_plays = 0  # 出对子次数

            for trick in state.played_tricks:
                for trick_player, cards in trick:
                    if trick_player == opp:
                        total_plays += 1

                        # 检查是否出大牌 (A, K, Q, J)
                        for card in cards:
                            if card.rank.value >= 11:
                                big_card_plays += 1
                                break

                        # 检查是否出分牌 (5, 10, K)
                        for card in cards:
                            if card.rank in [Rank.FIVE, Rank.TEN, Rank.KING]:
                                point_card_plays += 1
                                break

                        # 检查是否出主牌
                        if state.trump_suit:
                            for card in cards:
                                if card.suit == state.trump_suit or card.rank.value == state.level_rank.value:
                                    trump_plays += 1
                                    break

                        # 检查是否出对子
                        if len(cards) >= 2:
                            pair_plays += 1

            # 归一化特征
            if total_plays > 0:
                patterns[offset + 0] = big_card_plays / total_plays
                patterns[offset + 1] = point_card_plays / total_plays
                patterns[offset + 2] = trump_plays / total_plays
                patterns[offset + 3] = pair_plays / total_plays
                patterns[offset + 4] = total_plays / len(state.played_tricks)

        return patterns


# 保持向后兼容
StateEncoder = EnhancedStateEncoder
