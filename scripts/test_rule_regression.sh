#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_PATH="$ROOT_DIR/TractorGame.csproj"
FILTER="FullyQualifiedName~LeadPatternMatrixTests|FullyQualifiedName~FollowValidatorRegressionTests|FullyQualifiedName~TrickJudgeRegressionTests|FullyQualifiedName~ThrowRulesTests|FullyQualifiedName~MixedThrowApiTests"

dotnet test "$PROJECT_PATH" \
  --filter "$FILTER" \
  --logger "console;verbosity=minimal" \
  "$@"
