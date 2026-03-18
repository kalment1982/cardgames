"""Generate fixed evaluation seeds for reproducible Phase 1 evaluation."""
import random
import os

NUM_SEEDS = 100
OUTPUT_PATH = os.path.join(os.path.dirname(__file__), "eval_seeds.txt")


def main():
    rng = random.Random(42)
    seeds = [rng.randint(0, 2**31 - 1) for _ in range(NUM_SEEDS)]

    with open(OUTPUT_PATH, "w") as f:
        for s in seeds:
            f.write(f"{s}\n")

    print(f"Wrote {NUM_SEEDS} eval seeds to {OUTPUT_PATH}")


if __name__ == "__main__":
    main()
