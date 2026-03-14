"""
状态编码器 - 将游戏状态转换为神经网络输入
"""
import numpy as np
from typing import List, Tuple
from game_engine import GameState, Card, Suit, Rank, Role


class StateEncoder:
    """状态编码器"""

    def __init__(self):
        # 卡牌索引映射
        self.card_to_index = self._build_card_index()
        self.state_dim = 338  # 108*3 + 4 + 10

    def _build_card_index(self) -> dict:
        """构建卡牌到索引的映射"""
        index = 0
        card_map = {}

        # 大小王
        card_map[(Suit.JOKER, Rank.BIG_JOKER)] = index
        index += 1
        card_map[(Suit.JOKER, Rank.SMALL_JOKER)] = index
        index += 1

        # 普通牌
        for suit in [Suit.SPADE, Suit.HEART, Suit.CLUB, Suit.DIAMOND]:
            for rank in [Rank.ACE, Rank.KING, Rank.QUEEN, Rank.JACK,
                        Rank.TEN, Rank.NINE, Rank.EIGHT, Rank.SEVEN,
                        Rank.SIX, Rank.FIVE, Rank.FOUR, Rank.THREE, Rank.TWO]:
                card_map[(suit, rank)] = index
                index += 1

        return card_map

    def encode(self, state: GameState, player: int) -> np.ndarray:
        """编码游戏状态

        Args:
            state: 游戏状态
            player: 当前玩家

        Returns:
            338维特征向量
        """
        features = []

        # 1. 手牌特征（108维）
        hand_vector = self._encode_cards(state.hands[player])
        features.extend(hand_vector)

        # 2. 已出牌特征（108维）
        played_vector = self._encode_played_cards(state.played_tricks)
        features.extend(played_vector)

        # 3. 当前墩特征（108维）
        current_trick_vector = self._encode_current_trick(state.current_trick)
        features.extend(current_trick_vector)

        # 4. 角色特征（4维）
        role_vector = self._encode_role(state, player)
        features.extend(role_vector)

        # 5. 局面特征（10维）
        situation_vector = self._encode_situation(state, player)
        features.extend(situation_vector)

        return np.array(features, dtype=np.float32)

    def _encode_cards(self, cards: List[Card]) -> List[float]:
        """编码卡牌列表为108维向量"""
        vector = [0.0] * 108

        for card in cards:
            key = (card.suit, card.rank)
            if key in self.card_to_index:
                idx = self.card_to_index[key]
                vector[idx] += 1.0  # 计数（最多2张相同的牌）

        # 归一化（每张牌最多2张）
        return [v / 2.0 for v in vector]

    def _encode_played_cards(self, played_tricks: List[List[Tuple[int, List[Card]]]]) -> List[float]:
        """编码已出的牌"""
        all_played = []
        for trick in played_tricks:
            for _, cards in trick:
                all_played.extend(cards)

        return self._encode_cards(all_played)

    def _encode_current_trick(self, current_trick: List[Tuple[int, List[Card]]]) -> List[float]:
        """编码当前墩"""
        current_cards = []
        for _, cards in current_trick:
            current_cards.extend(cards)

        return self._encode_cards(current_cards)

    def _encode_role(self, state: GameState, player: int) -> List[float]:
        """编码角色特征"""
        is_dealer = 1.0 if player == state.dealer else 0.0
        is_dealer_partner = 1.0 if (player + 2) % 4 == state.dealer else 0.0
        is_defender = 1.0 if player not in [state.dealer, (state.dealer + 2) % 4] else 0.0
        is_my_turn = 1.0 if player == state.current_player else 0.0

        return [is_dealer, is_dealer_partner, is_defender, is_my_turn]

    def _encode_situation(self, state: GameState, player: int) -> List[float]:
        """编码局面特征"""
        # 分数（归一化到[0,1]）
        dealer_score_norm = state.dealer_score / 200.0
        defender_score_norm = state.defender_score / 200.0

        # 剩余墩数
        tricks_remaining_norm = state.tricks_remaining / 25.0

        # 当前墩信息
        my_team_winning = 0.0
        partner_winning = 0.0
        if len(state.current_trick) > 0:
            # 简化：假设第一家赢
            winner = state.current_trick[0][0]
            if winner in [state.dealer, (state.dealer + 2) % 4]:
                my_team_winning = 1.0 if player in [state.dealer, (state.dealer + 2) % 4] else 0.0
            else:
                my_team_winning = 1.0 if player not in [state.dealer, (state.dealer + 2) % 4] else 0.0

            # 队友是否赢
            partner = (player + 2) % 4
            partner_winning = 1.0 if winner == partner else 0.0

        # 是否先手
        is_leading = 1.0 if len(state.current_trick) == 0 else 0.0

        # 主花色编码（one-hot，4维）
        trump_encoding = [0.0] * 4
        if state.trump_suit:
            trump_encoding[state.trump_suit.value % 4] = 1.0

        return [
            dealer_score_norm,
            defender_score_norm,
            tricks_remaining_norm,
            my_team_winning,
            partner_winning,
            is_leading,
        ] + trump_encoding


class ActionEncoder:
    """动作编码器"""

    def __init__(self):
        self.card_to_index = StateEncoder()._build_card_index()

    def encode_action(self, action: List[Card]) -> np.ndarray:
        """编码动作为108维向量"""
        vector = [0.0] * 108

        for card in action:
            key = (card.suit, card.rank)
            if key in self.card_to_index:
                idx = self.card_to_index[key]
                vector[idx] += 1.0

        return np.array(vector, dtype=np.float32)

    def encode_action_mask(self, legal_actions: List[List[Card]]) -> np.ndarray:
        """编码合法动作掩码

        Returns:
            108维布尔向量，True表示该动作合法
        """
        mask = np.zeros(108, dtype=bool)

        for action in legal_actions:
            # 简化：只标记第一张牌的位置
            if len(action) > 0:
                card = action[0]
                key = (card.suit, card.rank)
                if key in self.card_to_index:
                    idx = self.card_to_index[key]
                    mask[idx] = True

        return mask

    def decode_action(self, action_index: int, legal_actions: List[List[Card]]) -> List[Card]:
        """从动作索引解码为实际动作

        简化版：action_index对应legal_actions的索引
        """
        if 0 <= action_index < len(legal_actions):
            return legal_actions[action_index]
        return legal_actions[0] if legal_actions else []


if __name__ == '__main__':
    # 测试
    from game_engine import TractorGame

    game = TractorGame(seed=42)
    game.state.phase = game_engine.GamePhase.PLAYING

    encoder = StateEncoder()
    state_vector = encoder.encode(game.state, player=0)

    print(f"状态向量维度: {len(state_vector)}")
    print(f"状态向量前10维: {state_vector[:10]}")

    # 测试动作编码
    action_encoder = ActionEncoder()
    actions = game.get_legal_actions(0)
    if actions:
        action_vector = action_encoder.encode_action(actions[0])
        print(f"动作向量维度: {len(action_vector)}")
        print(f"动作向量非零元素: {np.sum(action_vector > 0)}")
