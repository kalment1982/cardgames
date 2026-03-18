#!/usr/bin/env bash
set -euo pipefail

ROOT="${1:-/Users/karmy/Projects/CardGame/tractor/logs/decision}"

if ! command -v jq >/dev/null 2>&1; then
  echo "jq is required" >&2
  exit 1
fi

find "$ROOT" -name '*.json' -print0 | while IFS= read -r -d '' file; do
  jq -r '
    .payload.bundle as $bundle
    | select($bundle.intent_snapshot.primary_intent == "PassToMate")
    | $bundle.candidate_details as $candidates
    | select(($candidates | length) > 1)
    | $bundle.selected_action as $selected
    | select(($selected.features.TrickWinValue // 0) == 0)
    | ($candidates
        | map(select(
            (.features.TrickWinValue // 0) == 0 and
            (.features.MateSyncValue // 0) == ($selected.features.MateSyncValue // 0) and
            (.features.DiscardPointCost // 0) == ($selected.features.DiscardPointCost // 0)
          ))) as $peers
    | select(($peers | length) > 1)
    | ($peers
        | min_by([
            (.features.TrumpConsumptionCost // 0),
            (.features.DiscardRankCost // 0),
            (.features.HighControlLossCost // 0),
            (.features.InfoLeakCost // 0),
            (.features.StructureBreakCost // 0),
            -(.score // 0)
          ])) as $best
    | select(($best.cards | tojson) != ($selected.cards | tojson))
    | [
        input_filename,
        ($bundle.meta.player_index // -1),
        ($bundle.context_snapshot.trick_index // -1),
        ($selected.cards | map(.text) | join(" ")),
        ($best.cards | map(.text) | join(" ")),
        ($selected.features.DiscardRankCost // 0),
        ($best.features.DiscardRankCost // 0),
        ($selected.features.HighControlLossCost // 0),
        ($best.features.HighControlLossCost // 0)
      ]
    | @tsv
  ' "$file"
done
