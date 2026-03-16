#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 2 ]]; then
  echo "Usage: $0 <source_bundle_json> <fixture_name>" >&2
  exit 1
fi

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOURCE_PATH="$1"
FIXTURE_NAME="$2"
FIXTURE_DIR="$ROOT_DIR/tests/V21/Fixtures/decision_bundles"
FIXTURE_PATH="$FIXTURE_DIR/${FIXTURE_NAME}.json"

mkdir -p "$FIXTURE_DIR"
cp "$SOURCE_PATH" "$FIXTURE_PATH"

cat <<EOF
Copied fixture:
  $FIXTURE_PATH

Suggested next step:
  1. Add a regression test with:
     DecisionBundleScenarioFactory.FromBundleFile(
       "tests/V21/Fixtures/decision_bundles/${FIXTURE_NAME}.json",
       expectations: new IScenarioExpectation[] { ... });
  2. If the issue is "候选缺失", add a generator-layer test in tests/V21/FollowCandidateGeneratorTests.cs
  3. Run:
     dotnet test TractorGame.csproj --filter "FullyQualifiedName~${FIXTURE_NAME}"
EOF
