"""
StateEncoder — Encode state_snapshot JSON into a fixed-size observation vector.

Follows the Phase-1 encoding spec from ``doc/AI共享决策输入编码定义_v1.0.md``.
Total observation dimension: 382.
"""
import logging
from typing import Optional

import numpy as np

logger = logging.getLogger(__name__)

# ── Card face index mapping (matches ActionSlotMapper.CardFaceIndex) ──
# BigJoker=0, SmallJoker=1,
# ♠A..♠2 = 2..14, ♥A..♥2 = 15..27, ♣A..♣2 = 28..40, ♦A..♦2 = 41..53

_SUIT_OFFSET = {
    "Spade": 0,
    "Heart": 13,
    "Club": 26,
    "Diamond": 39,
}

# Rank string -> offset within a suit (A=0, K=1, ..., 2=12)
_RANK_OFFSET = {
    "Ace": 0, "King": 1, "Queen": 2, "Jack": 3, "Ten": 4,
    "Nine": 5, "Eight": 6, "Seven": 7, "Six": 8, "Five": 9,
    "Four": 10, "Three": 11, "Two": 12,
}

# Role string -> one-hot index
_ROLE_INDEX = {"dealer": 0, "dealer_partner": 1, "defender": 2}

# Trump suit string -> one-hot index (5 classes, last is NoTrump)
_TRUMP_SUIT_INDEX = {
    "Spade": 0, "Heart": 1, "Club": 2, "Diamond": 3, "NoTrump": 4,
}

# Level rank string -> one-hot index (13 classes: A=0 .. 2=12)
_LEVEL_RANK_INDEX = _RANK_OFFSET  # same mapping

# Suit string -> index for per-suit vectors
_SUIT_INDEX = {"Spade": 0, "Heart": 1, "Club": 2, "Diamond": 3}

# Score values by rank
_RANK_SCORE = {"Five": 5, "Ten": 10, "King": 10}


def card_face_index(card: dict) -> int:
    """Map a card dict ``{"suit": ..., "rank": ...}`` to 0-53 face index.

    Matches ``ActionSlotMapper.CardFaceIndex`` in C#.
    """
    rank = card["rank"]
    suit = card["suit"]
    if rank == "BigJoker":
        return 0
    if rank == "SmallJoker":
        return 1
    return 2 + _SUIT_OFFSET[suit] + _RANK_OFFSET[rank]


def encode_card_count_108(cards: list[dict]) -> np.ndarray:
    """Encode a list of card dicts into a 108-dim vector.

    Each card face (54 faces) occupies 2 slots (two decks).
    Slot ``face_index * 2`` is the first copy, ``face_index * 2 + 1`` the second.
    Value is 0 or 1.
    """
    vec = np.zeros(108, dtype=np.float32)
    # Track how many of each face we've seen so far
    seen = [0] * 54
    for c in cards:
        fi = card_face_index(c)
        copy = seen[fi]
        if copy < 2:
            vec[fi * 2 + copy] = 1.0
        seen[fi] = copy + 1
    return vec


def encode_one_hot(value: int, n: int) -> np.ndarray:
    """Return an n-dim one-hot vector with index *value* set to 1."""
    vec = np.zeros(n, dtype=np.float32)
    if 0 <= value < n:
        vec[value] = 1.0
    return vec


def _is_trump_card(card: dict, trump_suit: str, level_rank: str) -> bool:
    """Check whether a card is trump given trump_suit and level_rank strings."""
    rank = card["rank"]
    suit = card["suit"]
    if rank in ("BigJoker", "SmallJoker"):
        return True
    if rank == level_rank:
        return True
    if trump_suit != "NoTrump" and suit == trump_suit:
        return True
    return False


def _is_high_trump(card: dict, trump_suit: str, level_rank: str) -> bool:
    """High trump = jokers + level-rank cards + trump-suit A (if not level rank)."""
    rank = card["rank"]
    suit = card["suit"]
    if rank in ("BigJoker", "SmallJoker"):
        return True
    if rank == level_rank:
        return True
    # Trump-suit Ace counts as high trump when Ace is not the level rank
    if trump_suit != "NoTrump" and suit == trump_suit and rank == "Ace" and level_rank != "Ace":
        return True
    return False


class StateEncoder:
    """Encode a ``state_snapshot`` dict into a 382-dim float32 observation."""

    OBS_DIM = 382

    def encode(self, snapshot: dict, legal_actions: list[dict] | None = None) -> np.ndarray:
        """Return a ``(382,)`` float32 numpy array."""
        parts: list[np.ndarray] = []

        my_hand = snapshot.get("my_hand", [])
        trump_suit = snapshot.get("trump_suit", "NoTrump")
        level_rank = snapshot.get("level_rank", "Two")
        dealer = snapshot.get("dealer", 0)
        my_seat = snapshot.get("my_seat", 0)
        my_role = snapshot.get("my_role", "defender")
        trick_index = snapshot.get("trick_index", 0)
        play_position = snapshot.get("play_position", 0)
        lead_cards = snapshot.get("lead_cards", [])
        winning_cards = snapshot.get("current_winning_cards", [])
        winning_player = snapshot.get("current_winning_player", -1)
        trick_score = snapshot.get("current_trick_score", 0)
        defender_score = snapshot.get("defender_score", 0)
        cards_left = snapshot.get("cards_left_by_player", [25, 25, 25, 25])

        # 1. 自己手牌 CardCount108 (108)
        parts.append(encode_card_count_108(my_hand))

        # 2. 自己角色 OneHot3 (3)
        parts.append(encode_one_hot(_ROLE_INDEX.get(my_role, 2), 3))

        # 3. 自己座位 OneHot4 (4)
        parts.append(encode_one_hot(my_seat, 4))

        # 4. 庄家座位 OneHot4 (4)
        parts.append(encode_one_hot(dealer, 4))

        # 5. 主花色/无主 OneHot5 (5)
        parts.append(encode_one_hot(_TRUMP_SUIT_INDEX.get(trump_suit, 4), 5))

        # 6. 当前级牌 OneHot13 (13)
        parts.append(encode_one_hot(_LEVEL_RANK_INDEX.get(level_rank, 12), 13))

        # 7. 当前第几墩 NormalizedScalar (1)
        parts.append(np.array([trick_index / 25.0], dtype=np.float32))

        # 8. 当前出牌位置 OneHot4 (4)
        parts.append(encode_one_hot(play_position, 4))

        # 9. 当前是否首发 Scalar (1)
        parts.append(np.array([1.0 if play_position == 0 else 0.0], dtype=np.float32))

        # 10. 当前墩首发牌 CardCount108 (108)
        parts.append(encode_card_count_108(lead_cards))

        # 11. 当前领先牌组 CardCount108 (108)
        parts.append(encode_card_count_108(winning_cards))

        # 12. 当前赢家 OneHot5 (5): seats 0-3 + no winner(-1)
        winner_idx = winning_player if 0 <= winning_player <= 3 else 4
        parts.append(encode_one_hot(winner_idx, 5))

        # 13. 队友是否暂时领先 Scalar (1)
        partner_seat = (my_seat + 2) % 4
        partner_winning = 1.0 if winning_player == partner_seat else 0.0
        # Also count as partner winning if I am winning (same team)
        if winning_player == my_seat:
            partner_winning = 1.0
        parts.append(np.array([partner_winning], dtype=np.float32))

        # 14. 当前墩累计分数 NormalizedScalar (1): trick_score / 100
        parts.append(np.array([trick_score / 100.0], dtype=np.float32))

        # 15. 当前闲家得分 NormalizedScalar (1): defender_score / 200
        parts.append(np.array([defender_score / 200.0], dtype=np.float32))

        # 16. 各玩家剩余手牌数 PerPlayerVector (4): /25
        cl = np.array(cards_left[:4], dtype=np.float32) / 25.0
        parts.append(cl)

        # ── Hand structure statistics (items 17-24) ──
        trump_count = 0
        high_trump_count = 0
        joker_count = 0
        level_card_count = 0
        score_card_count = 0
        suit_lengths = [0, 0, 0, 0]  # Spade, Heart, Club, Diamond (non-trump only)

        # Count per-face for pair/tractor detection in trump
        trump_face_counts: dict[int, int] = {}

        for c in my_hand:
            is_trump = _is_trump_card(c, trump_suit, level_rank)
            if is_trump:
                trump_count += 1
                fi = card_face_index(c)
                trump_face_counts[fi] = trump_face_counts.get(fi, 0) + 1
                if _is_high_trump(c, trump_suit, level_rank):
                    high_trump_count += 1
            else:
                s = c.get("suit", "")
                if s in _SUIT_INDEX:
                    suit_lengths[_SUIT_INDEX[s]] += 1

            rank_str = c.get("rank", "")
            if rank_str in ("BigJoker", "SmallJoker"):
                joker_count += 1
            if rank_str == level_rank:
                level_card_count += 1
            if rank_str in _RANK_SCORE:
                score_card_count += 1

        # Trump pairs and tractors
        trump_pair_count = sum(1 for v in trump_face_counts.values() if v >= 2)
        trump_tractor_count = self._count_trump_tractors(
            trump_face_counts, trump_suit, level_rank
        )

        # 17. 主牌数量 NormalizedScalar (1): /25
        parts.append(np.array([trump_count / 25.0], dtype=np.float32))

        # 18. 高主数量 NormalizedScalar (1): /8
        parts.append(np.array([high_trump_count / 8.0], dtype=np.float32))

        # 19. 王数量 Scalar (1)
        parts.append(np.array([float(joker_count)], dtype=np.float32))

        # 20. 级牌数量 Scalar (1)
        parts.append(np.array([float(level_card_count)], dtype=np.float32))

        # 21. 主牌对子数量 Scalar (1)
        parts.append(np.array([float(trump_pair_count)], dtype=np.float32))

        # 22. 主牌拖拉机数量 Scalar (1)
        parts.append(np.array([float(trump_tractor_count)], dtype=np.float32))

        # 23. 各花色长度 PerSuitVector (4)
        parts.append(np.array(suit_lengths, dtype=np.float32))

        # 24. 手牌分牌数量 Scalar (1)
        parts.append(np.array([float(score_card_count)], dtype=np.float32))

        obs = np.concatenate(parts)
        assert obs.shape == (self.OBS_DIM,), (
            f"Observation dim mismatch: expected {self.OBS_DIM}, got {obs.shape[0]}"
        )
        return obs

    # ── private helpers ──

    @staticmethod
    def _count_trump_tractors(
        face_counts: dict[int, int], trump_suit: str, level_rank: str
    ) -> int:
        """Count the number of trump tractors (consecutive pairs) in hand.

        This is a simplified heuristic: we look for runs of >= 2 consecutive
        paired trump faces in the standard trump ordering.
        """
        if len(face_counts) < 2:
            return 0

        # Build the ordered list of trump face indices that have pairs
        paired_faces = sorted(fi for fi, cnt in face_counts.items() if cnt >= 2)
        if len(paired_faces) < 2:
            return 0

        # For simplicity, count runs of consecutive face indices as tractors.
        # This is an approximation — the real tractor logic accounts for
        # level-rank gaps in the trump sequence, but for an observation
        # feature this is sufficient.
        tractor_count = 0
        run_len = 1
        for i in range(1, len(paired_faces)):
            if paired_faces[i] == paired_faces[i - 1] + 1:
                run_len += 1
            else:
                if run_len >= 2:
                    tractor_count += run_len - 1
                run_len = 1
        if run_len >= 2:
            tractor_count += run_len - 1
        return tractor_count
