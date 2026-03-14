"""
拖拉机游戏引擎 - Python实现
用于强化学习训练
"""
from enum import Enum
from dataclasses import dataclass
from typing import List, Tuple, Optional
import random


class Suit(Enum):
    """花色"""
    SPADE = 0    # 黑桃
    HEART = 1    # 红桃
    CLUB = 2     # 梅花
    DIAMOND = 3  # 方块
    JOKER = 4    # 王


class Rank(Enum):
    """点数"""
    TWO = 2
    THREE = 3
    FOUR = 4
    FIVE = 5
    SIX = 6
    SEVEN = 7
    EIGHT = 8
    NINE = 9
    TEN = 10
    JACK = 11
    QUEEN = 12
    KING = 13
    ACE = 14
    SMALL_JOKER = 15
    BIG_JOKER = 16


@dataclass
class Card:
    """牌"""
    suit: Suit
    rank: Rank

    def get_points(self) -> int:
        """获取分值"""
        if self.rank == Rank.FIVE:
            return 5
        if self.rank in [Rank.TEN, Rank.KING]:
            return 10
        return 0

    def __hash__(self):
        return hash((self.suit, self.rank))

    def __eq__(self, other):
        return self.suit == other.suit and self.rank == other.rank

    def __repr__(self):
        suit_symbols = {
            Suit.SPADE: '♠',
            Suit.HEART: '♥',
            Suit.CLUB: '♣',
            Suit.DIAMOND: '♦',
            Suit.JOKER: '🃏'
        }
        rank_names = {
            Rank.TWO: '2', Rank.THREE: '3', Rank.FOUR: '4',
            Rank.FIVE: '5', Rank.SIX: '6', Rank.SEVEN: '7',
            Rank.EIGHT: '8', Rank.NINE: '9', Rank.TEN: '10',
            Rank.JACK: 'J', Rank.QUEEN: 'Q', Rank.KING: 'K',
            Rank.ACE: 'A', Rank.SMALL_JOKER: '小王', Rank.BIG_JOKER: '大王'
        }
        return f"{suit_symbols[self.suit]}{rank_names[self.rank]}"


class GamePhase(Enum):
    """游戏阶段"""
    DEALING = 0      # 发牌
    BIDDING = 1      # 叫牌
    BURYING = 2      # 扣底
    PLAYING = 3      # 出牌
    FINISHED = 4     # 结束


class Role(Enum):
    """角色"""
    DEALER = 0           # 庄家
    DEALER_PARTNER = 1   # 庄家队友
    DEFENDER = 2         # 闲家


@dataclass
class GameState:
    """游戏状态"""
    phase: GamePhase
    current_player: int  # 当前玩家（0-3）
    dealer: int          # 庄家
    trump_suit: Optional[Suit]  # 主花色
    level_rank: Rank     # 当前级别

    hands: List[List[Card]]  # 4个玩家的手牌
    bottom: List[Card]       # 底牌

    current_trick: List[Tuple[int, List[Card]]]  # 当前墩：[(玩家, 牌)]
    played_tricks: List[List[Tuple[int, List[Card]]]]  # 已打的墩

    dealer_score: int = 0    # 庄家方得分
    defender_score: int = 0  # 闲家方得分

    tricks_remaining: int = 25  # 剩余墩数


class TractorGame:
    """拖拉机游戏引擎"""

    def __init__(self, seed: Optional[int] = None):
        self.seed = seed
        if seed is not None:
            random.seed(seed)

        self.state: Optional[GameState] = None
        self.reset()

    @property
    def current_player(self) -> int:
        """当前玩家"""
        return self.state.current_player if self.state else 0

    def reset(self) -> GameState:
        """重置游戏"""
        # 创建两副牌
        deck = self._create_deck()
        random.shuffle(deck)

        # 发牌：每人25张，底牌8张
        hands = [
            deck[0:25],
            deck[25:50],
            deck[50:75],
            deck[75:100]
        ]
        bottom = deck[100:108]

        # 初始化状态
        self.state = GameState(
            phase=GamePhase.BIDDING,
            current_player=0,
            dealer=0,  # 简化：固定玩家0为庄家
            trump_suit=Suit.SPADE,  # 简化：固定黑桃为主
            level_rank=Rank.TWO,    # 从2开始
            hands=hands,
            bottom=bottom,
            current_trick=[],
            played_tricks=[],
            tricks_remaining=25
        )

        return self.state

    def _create_deck(self) -> List[Card]:
        """创建两副牌"""
        deck = []

        # 两副普通牌
        for _ in range(2):
            for suit in [Suit.SPADE, Suit.HEART, Suit.CLUB, Suit.DIAMOND]:
                for rank in [Rank.TWO, Rank.THREE, Rank.FOUR, Rank.FIVE,
                            Rank.SIX, Rank.SEVEN, Rank.EIGHT, Rank.NINE,
                            Rank.TEN, Rank.JACK, Rank.QUEEN, Rank.KING, Rank.ACE]:
                    deck.append(Card(suit, rank))

            # 大小王
            deck.append(Card(Suit.JOKER, Rank.SMALL_JOKER))
            deck.append(Card(Suit.JOKER, Rank.BIG_JOKER))

        return deck

    def get_legal_actions(self, player: int) -> List[List[Card]]:
        """获取合法动作"""
        if self.state.phase != GamePhase.PLAYING:
            return []

        if player != self.state.current_player:
            return []

        hand = self.state.hands[player]

        # 如果是首家，可以出任意合法组合
        if len(self.state.current_trick) == 0:
            return self._get_lead_actions(hand)

        # 如果是跟牌，必须跟首引花色
        lead_cards = self.state.current_trick[0][1]
        return self._get_follow_actions(hand, lead_cards)

    def _get_lead_actions(self, hand: List[Card]) -> List[List[Card]]:
        """获取先手出牌动作"""
        actions = []

        # 简化版：只考虑单张和对子
        # 单张
        seen = set()
        for card in hand:
            card_key = (card.suit, card.rank)
            if card_key not in seen:
                actions.append([card])
                seen.add(card_key)

        # 对子
        card_counts = {}
        for card in hand:
            key = (card.suit, card.rank)
            card_counts[key] = card_counts.get(key, 0) + 1

        for (suit, rank), count in card_counts.items():
            if count >= 2:
                cards = [c for c in hand if c.suit == suit and c.rank == rank]
                actions.append(cards[:2])

        if actions:
            return actions
        if not hand:
            return []
        return [[hand[0]]]  # 至少返回一个动作

    def _get_follow_actions(self, hand: List[Card], lead_cards: List[Card]) -> List[List[Card]]:
        """获取跟牌动作"""
        # 简化版：必须出相同数量的牌
        need_count = len(lead_cards)
        lead_suit = lead_cards[0].suit

        if not hand:
            return []

        # 找同花色的牌
        same_suit = [c for c in hand if c.suit == lead_suit]

        if len(same_suit) >= need_count:
            # 有足够的同花色牌
            # 简化：返回前N张
            return [same_suit[:need_count]]
        else:
            # 不够，需要垫牌
            result = same_suit.copy()
            other_cards = [c for c in hand if c.suit != lead_suit]
            result.extend(other_cards[:need_count - len(same_suit)])
            return [result]

    def step(self, player: int, action: List[Card]) -> Tuple[GameState, float, bool]:
        """执行动作

        Returns:
            (new_state, reward, done)
        """
        if player != self.state.current_player:
            raise ValueError(f"Not player {player}'s turn")

        # 出牌
        self.state.current_trick.append((player, action))

        # 从手牌中移除
        for card in action:
            self.state.hands[player].remove(card)

        # 判断是否一墩结束
        if len(self.state.current_trick) == 4:
            reward = self._finish_trick()
            self.state.tricks_remaining -= 1

            # 判断游戏是否结束
            all_hands_empty = all(len(hand) == 0 for hand in self.state.hands)
            if self.state.tricks_remaining == 0 or all_hands_empty:
                self.state.phase = GamePhase.FINISHED
                final_reward = self._calculate_final_reward()
                return self.state, final_reward, True

            return self.state, reward, False
        else:
            # 下一个玩家
            self.state.current_player = (player + 1) % 4
            return self.state, 0.0, False

    def _finish_trick(self) -> float:
        """结束一墩，返回庄家方视角的即时奖励"""
        # 简化：第一家赢（实际需要比较牌力）
        winner = self.state.current_trick[0][0]

        # 计算分数
        points = 0
        for _, cards in self.state.current_trick:
            points += sum(c.get_points() for c in cards)

        # 更新分数
        if winner in [0, 2]:  # 庄家方（0和2）
            self.state.dealer_score += points
        else:  # 闲家方（1和3）
            self.state.defender_score += points

        # 保存已打的墩
        self.state.played_tricks.append(self.state.current_trick.copy())
        self.state.current_trick = []

        # 赢家先手
        self.state.current_player = winner

        # 即时奖励（庄家方视角，闲家方获胜时为负）
        reward = 0.5 + points * 0.05
        return reward if winner in [0, 2] else -reward

    def _calculate_final_reward(self) -> float:
        """计算最终奖励"""
        # 简化：庄家方得分>闲家方得分则胜利
        if self.state.dealer_score > self.state.defender_score:
            return 10.0  # 庄家方胜利
        else:
            return -10.0  # 闲家方胜利

    def get_role(self, player: int) -> Role:
        """获取玩家角色"""
        if player == self.state.dealer:
            return Role.DEALER
        elif (player + 2) % 4 == self.state.dealer:
            return Role.DEALER_PARTNER
        else:
            return Role.DEFENDER

    def is_terminal(self) -> bool:
        """是否游戏结束"""
        if self.state.phase == GamePhase.FINISHED:
            return True
        return all(len(hand) == 0 for hand in self.state.hands)


if __name__ == '__main__':
    # 测试
    game = TractorGame(seed=42)
    print(f"初始状态: {game.state.phase}")
    print(f"庄家: {game.state.dealer}")
    print(f"玩家0手牌数: {len(game.state.hands[0])}")

    # 简化测试：直接进入出牌阶段
    game.state.phase = GamePhase.PLAYING
    game.state.current_player = 0

    # 获取合法动作
    actions = game.get_legal_actions(0)
    print(f"玩家0合法动作数: {len(actions)}")
    print(f"第一个动作: {actions[0]}")

    # 执行动作
    state, reward, done = game.step(0, actions[0])
    print(f"执行后奖励: {reward}, 结束: {done}")
    print(f"当前玩家: {state.current_player}")
