#!/usr/bin/env python3
import argparse
import json
import os
import sys
from types import SimpleNamespace

import numpy as np
import torch

sys.path.insert(0, os.path.dirname(__file__))

from enhanced_state_encoder import EnhancedStateEncoder
from game_engine import Rank, Suit
from ppo_agent import PPOAgent


SUIT_MAP = {
    "Spade": Suit.SPADE,
    "Heart": Suit.HEART,
    "Club": Suit.CLUB,
    "Diamond": Suit.DIAMOND,
    "Joker": Suit.JOKER,
}

RANK_MAP = {
    "Two": Rank.TWO,
    "Three": Rank.THREE,
    "Four": Rank.FOUR,
    "Five": Rank.FIVE,
    "Six": Rank.SIX,
    "Seven": Rank.SEVEN,
    "Eight": Rank.EIGHT,
    "Nine": Rank.NINE,
    "Ten": Rank.TEN,
    "Jack": Rank.JACK,
    "Queen": Rank.QUEEN,
    "King": Rank.KING,
    "Ace": Rank.ACE,
    "SmallJoker": Rank.SMALL_JOKER,
    "BigJoker": Rank.BIG_JOKER,
}


def parse_card(payload):
    return SimpleNamespace(
        suit=SUIT_MAP[payload["suit"]],
        rank=RANK_MAP[payload["rank"]],
    )


def parse_play(play_payload):
    return (
        int(play_payload["playerIndex"]),
        [parse_card(card) for card in play_payload["cards"]],
    )


def build_state(payload):
    hands = []
    for hand_payload in payload["hands"]:
        hands.append([parse_card(card) for card in hand_payload])

    played_tricks = []
    for trick_payload in payload["playedTricks"]:
        played_tricks.append([parse_play(play) for play in trick_payload])

    current_trick = [parse_play(play) for play in payload["currentTrick"]]

    trump_name = payload.get("trumpSuit")
    trump_suit = SUIT_MAP[trump_name] if trump_name else None

    return SimpleNamespace(
        dealer=int(payload["dealer"]),
        current_player=int(payload["currentPlayer"]),
        trump_suit=trump_suit,
        level_rank=RANK_MAP[payload["levelRank"]],
        hands=hands,
        played_tricks=played_tricks,
        current_trick=current_trick,
        dealer_score=int(payload["dealerScore"]),
        defender_score=int(payload["defenderScore"]),
        tricks_remaining=int(payload["tricksRemaining"]),
    )


def main():
    parser = argparse.ArgumentParser(description="PPO bridge server")
    parser.add_argument("--checkpoint", required=True, help="Path to PPO checkpoint")
    parser.add_argument("--device", default="cpu", help="Inference device")
    args = parser.parse_args()

    checkpoint = torch.load(args.checkpoint, map_location=args.device)
    agent = PPOAgent(state_dim=412, action_dim=1000, device=args.device)
    agent.network.load_state_dict(checkpoint["model_state_dict"])
    encoder = EnhancedStateEncoder()

    ready = {
        "ok": True,
        "type": "ready",
        "iteration": int(checkpoint.get("iteration", 0)),
        "completed_iterations": int(checkpoint.get("completed_iterations", 0)),
        "total_games": int(checkpoint.get("total_games", 0)),
    }
    print(json.dumps(ready, ensure_ascii=False), flush=True)

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue

        try:
            request = json.loads(line)
            request_type = request.get("type", "select")

            if request_type == "close":
                print(json.dumps({"ok": True, "type": "closed"}, ensure_ascii=False), flush=True)
                break

            if request_type != "select":
                print(json.dumps({"ok": False, "error": f"unsupported request type: {request_type}"}, ensure_ascii=False), flush=True)
                continue

            legal_action_count = int(request["legalActionCount"])
            if legal_action_count <= 0:
                print(json.dumps({"ok": False, "error": "legal_action_count must be positive"}, ensure_ascii=False), flush=True)
                continue

            state = build_state(request["state"])
            player_index = int(request["playerIndex"])
            state_vector = encoder.encode_state(state, player_index)

            action_mask = np.zeros(1000, dtype=np.float32)
            action_mask[: min(legal_action_count, 1000)] = 1.0
            action_index, log_prob = agent.select_action(
                state_vector,
                action_mask,
                deterministic=bool(request.get("deterministic", True)),
            )

            response = {
                "ok": True,
                "action_index": int(action_index),
                "log_prob": float(log_prob),
            }
            print(json.dumps(response, ensure_ascii=False), flush=True)
        except Exception as ex:
            print(json.dumps({"ok": False, "error": str(ex)}, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
