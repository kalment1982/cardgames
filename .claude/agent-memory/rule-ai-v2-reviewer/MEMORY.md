# Rule AI v2.1 Reviewer Memory

## Project Structure
- Solution: `/Users/karmy/Projects/CardGame/tractor/tractor.sln`
- V21 source: `src/Core/AI/V21/` (22 files)
- V21 tests: `tests/V21/` (17 files) + `tests/RuleAIContextBuilderTests.cs`
- Design doc: `doc/规则AI架构设计_v2.1.md`
- Rule validators (truth source): `src/Core/Rules/{PlayValidator,FollowValidator,ThrowValidator,TrickJudge}.cs`
- Card model: `src/Core/Models/Card.cs` — class, IEquatable<Card>, equality by (Suit, Rank)
- Trump logic: `GameConfig.IsTrump()` — Jokers + LevelRank + TrumpSuit cards

## Architecture (v2.1 M1)
- Pipeline: ContextBuilder -> CandidateGenerator -> IntentResolver -> ActionScorer -> DecisionExplainer
- Four phase policies: BidPolicy2, BuryPolicy2, LeadPolicy2, FollowPolicy2
- Integration: AIPlayer wires V21 via RuleAIOptions (UseRuleAIV21 flag + shadow compare)
- All candidates validated through PlayValidator/FollowValidator before scoring

## Key Patterns
- `CardComparer` used for trump-aware ordering throughout
- `RuleAIUtility.BuildSystemGroups()` splits hand into trump + per-suit groups
- `RuleAIUtility.DeduplicateCandidates()` uses sorted (Suit,Rank) string keys
- Scoring: BaseScore (linear weighted features) + ScenarioAdjust + IntentAdjust - RiskPenalty + TieBreakNoise
- Expert baseline weights defined in spec section 12.2.1, implemented in ActionScorer.Weight()

## Known Issues Found (2026-03-15 review)
- See [review-findings.md](review-findings.md) for detailed findings
- CountVoidTargets in ActionScorer doesn't filter by IsTrump, miscounts level-rank cards
- InferenceEngine hardcodes total trump count as 20, incorrect for variable deck sizes
- BidPolicy2.EvaluateSuit creates temporary GameConfig missing AllowBrokenTractor/StrictFollow fields
- LeadPolicy2.ReorderForStructuredLead creates temporary GameConfig instead of using context's config
- BuryCandidateGenerator hardcodes 8-card bottom size
- EndgamePolicy.ResolveScorePressure only considers defender perspective
