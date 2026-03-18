"""
Action mask builder for PPO training.

Constructs a 384-dim binary mask from the legal_actions list returned by
the C# PpoEngineHost.  Each legal action carries a ``slot`` field that
maps directly into the ACTION_DIM=384 action space defined by
``ActionSlotMapper``.

When ``legal_actions`` already contain ``slot``, callers should use those
directly. This module also provides ``compute_slot`` / ``map_all_actions``
as a Python fallback for diagnostics.
"""
import numpy as np

from state_encoder import card_face_index, _RANK_OFFSET

ACTION_DIM = 384

# Slot layout constants (mirrors ActionSlotMapper.cs)
_SINGLE_BASE = 0
_PAIR_BASE = 54
_TRACTOR_BASE = 108
_RESERVED_BASE = 368
_RESERVED_COUNT = 16

_SYSTEM_INDEX = {
    "spade": 0, "heart": 1, "club": 2, "diamond": 3, "trump": 4,
}


def compute_slot(action: dict) -> int:
    """Compute the action slot for a legal action dict.

    Returns the slot index (0..383), or -1 for complex actions
    (throw/mixed) that need reserved-slot assignment.
    """
    pattern = action.get("pattern_type", "")
    cards = action.get("cards", [])

    if pattern == "single" and len(cards) >= 1:
        return _SINGLE_BASE + card_face_index(cards[0])

    if pattern == "pair" and len(cards) >= 1:
        return _PAIR_BASE + card_face_index(cards[0])

    if pattern == "tractor" and len(cards) >= 4:
        rank_names = [c.get("rank", "") for c in cards]
        if "BigJoker" in rank_names or "SmallJoker" in rank_names:
            return -1
        system = action.get("system", "trump")
        sys_idx = _SYSTEM_INDEX.get(system, 4)
        # Find the highest-rank pair representative
        start_rank = _tractor_start_rank_offset(cards)
        pair_count = len(cards) // 2
        length_offset = pair_count - 2
        if length_offset < 0 or length_offset > 3:
            return -1
        return _TRACTOR_BASE + sys_idx * 52 + start_rank * 4 + length_offset

    # throw / mixed -> needs reserved slot
    return -1


def _tractor_start_rank_offset(cards: list[dict]) -> int:
    """Determine the start rank offset (0-12) for a tractor.

    The start is the highest-rank pair among the tractor's cards.
    We group by face index, find pairs, and pick the one with the
    smallest rank offset (= highest rank, since A=0, 2=12).
    """
    from collections import Counter
    face_counts = Counter(card_face_index(c) for c in cards)
    pair_faces = [fi for fi, cnt in face_counts.items() if cnt >= 2]
    if not pair_faces:
        return 0

    # Convert face index to rank offset
    best = 12  # worst case = Two
    for fi in pair_faces:
        if fi <= 1:
            # Joker — map to 0 (A position) per C# spec
            rank_off = 0
        else:
            rank_off = (fi - 2) % 13
        if rank_off < best:
            best = rank_off
    return best


def _complexity_order(action: dict) -> int:
    pt = action.get("pattern_type", "")
    if pt == "mixed":
        return 0
    if pt == "throw":
        return 1
    return 2


def _system_sort_key(action: dict) -> int:
    return _SYSTEM_INDEX.get(action.get("system", ""), 5)


def _card_strength(action: dict) -> int:
    return sum(53 - card_face_index(c) for c in action.get("cards", []))


def map_all_actions(actions: list[dict]) -> list[tuple[int, dict]]:
    """Map all legal actions to slots, replicating ActionSlotMapper.MapAllActions.

    Returns a list of ``(slot, action)`` tuples.
    """
    result: list[tuple[int, dict]] = []
    slot_set: set[int] = set()
    complex_actions: list[dict] = []

    for a in actions:
        slot = compute_slot(a)
        if slot == -1:
            complex_actions.append(a)
            continue
        if slot in slot_set:
            # Conflict — skip duplicate (shouldn't happen with valid engine output)
            continue
        slot_set.add(slot)
        result.append((slot, a))

    if len(complex_actions) > _RESERVED_COUNT:
        keys = ", ".join(a.get("debug_key", "") for a in complex_actions)
        raise RuntimeError(
            f"ACTION_SPACE_OVERFLOW: {len(complex_actions)} complex actions "
            f"exceed reserved slot capacity ({_RESERVED_COUNT}). Keys: [{keys}]"
        )

    # Sort complex actions with stable ordering matching C#
    complex_actions.sort(key=lambda a: (
        -len(a.get("cards", [])),       # 1. total card count desc
        _complexity_order(a),            # 2. pattern complexity
        _system_sort_key(a),             # 3. suit systems before trump
        -_card_strength(a),              # 4. card strength desc
        a.get("debug_key", ""),          # 5. debug_key lexicographic
    ))

    for i, a in enumerate(complex_actions):
        slot = _RESERVED_BASE + i
        slot_set.add(slot)
        result.append((slot, a))

    return result


def build_action_mask(legal_actions: list[dict]) -> np.ndarray:
    """Build a 384-dim 0/1 mask from legal actions.

    If actions already have a ``slot`` field, use it directly.
    Otherwise, compute slots via ``map_all_actions``.
    """
    mask = np.zeros(ACTION_DIM, dtype=np.float32)

    # Check if slot field is present
    if legal_actions and "slot" in legal_actions[0]:
        for a in legal_actions:
            slot = a["slot"]
            if 0 <= slot < ACTION_DIM:
                mask[slot] = 1.0
    else:
        mapped = map_all_actions(legal_actions)
        for slot, _ in mapped:
            if 0 <= slot < ACTION_DIM:
                mask[slot] = 1.0

    return mask
