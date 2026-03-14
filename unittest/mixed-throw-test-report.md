# 混合甩牌测试报告（扩充版）

- 执行时间：2026-03-14 18:45:23 HKT
- 执行命令：`dotnet test TractorGame.csproj --filter "FullyQualifiedName~MixedThrowApiTests"`
- 结果：`Passed 37, Failed 0, Skipped 0`

## 结果汇总

| 分组 | 用例数 | 通过 | 失败 |
|---|---:|---:|---:|
| ThrowValidator 判定 | 12 | 12 | 0 |
| ThrowValidator 分解/回退 | 9 | 9 | 0 |
| PlayValidator 判定 | 12 | 12 | 0 |
| Game.PlayCardsEx 集成 | 4 | 4 | 0 |
| 合计 | 37 | 37 | 0 |

## 用例与测试方法映射

| 用例ID | 测试方法 | 结果 |
|---|---|---|
| MT-01 | `IsThrowSuccessful_ReturnsFalse_WhenThrowCardsAreNull` | PASS |
| MT-02 | `IsThrowSuccessful_ReturnsFalse_WhenThrowCardsAreEmpty` | PASS |
| MT-03 | `IsThrowSuccessful_ReturnsFalse_WhenThrowCardsAreNotSameSuitOrTrump` | PASS |
| MT-04 | `IsThrowSuccessful_ReturnsTrue_WhenFollowPlaysAreNull` | PASS |
| MT-05 | `AnalyzeThrow_IgnoresNullOrEmptyFollowerEntries` | PASS |
| MT-06 | `AnalyzeThrow_Fails_WhenSingleComponentIsBlocked` | PASS |
| MT-07 | `AnalyzeThrow_Fails_WhenPairComponentIsBlocked` | PASS |
| MT-08 | `AnalyzeThrow_Fails_WhenTractorComponentIsBlocked` | PASS |
| MT-09 | `AnalyzeThrow_UsesFirstBlockingFollowerIndex` | PASS |
| MT-10 | `AnalyzeThrow_Succeeds_WhenFollowersOnlyMatchEqualStrength` | PASS |
| MT-11 | `AnalyzeThrow_Succeeds_WhenFollowerTractorLengthDoesNotMatch` | PASS |
| MT-12 | `IsThrowSuccessful_AllTrumpMixedThrow_Succeeds_WhenNoHigherComponentExists` | PASS |
| MT-13 | `CanBeatThrow_ReturnsFalse_WhenThrowInvalid` | PASS |
| MT-14 | `CanBeatThrow_ReturnsTrue_WhenAnyComponentCanBeBeaten` | PASS |
| MT-15 | `DecomposeThrow_PrioritizesTractorThenPairThenSingle` | PASS |
| MT-16 | `DecomposeThrow_ReturnsEmpty_WhenThrowInvalid` | PASS |
| MT-17 | `GetFallbackPlay_PrefersSingleOverPairAndTractor` | PASS |
| MT-18 | `GetFallbackPlay_ReturnsSmallestPair_WhenNoSingle` | PASS |
| MT-19 | `GetFallbackPlay_ReturnsSmallestTractor_WhenOnlyTractors` | PASS |
| MT-20 | `GetSmallestCard_ReturnsNull_WhenFallbackEmpty` | PASS |
| MT-21 | `GetSmallestCard_ReturnsSmallestCardFromFallback` | PASS |
| MT-22 | `IsValidPlayEx_MixedThrow_Succeeds_WhenNoFollowerCanBeatAnyComponent` | PASS |
| MT-23 | `IsValidPlayEx_MixedThrow_FailsWithThrowNotMax_WhenSingleComponentIsBeaten` | PASS |
| MT-24 | `IsValidPlayEx_MixedThrow_FailsWithThrowNotMax_WhenPairComponentIsBeaten` | PASS |
| MT-25 | `IsValidPlayEx_MixedThrow_FailsWithThrowNotMax_WhenTractorComponentIsBeaten` | PASS |
| MT-26 | `IsValidPlayEx_MixedThrow_Succeeds_WhenOtherHandsNotProvided` | PASS |
| MT-27 | `IsValidPlayEx_Pair_BypassesThrowValidation` | PASS |
| MT-28 | `IsValidPlayEx_Tractor_BypassesThrowValidation` | PASS |
| MT-29 | `IsValidPlayEx_FailsWithPlayPatternInvalid_WhenCardsNotSameSuitOrTrump` | PASS |
| MT-30 | `IsValidPlayEx_FailsWithCardNotInHand` | PASS |
| MT-31 | `IsValidPlayEx_FailsWithPlayPatternInvalid_WhenCardsEmpty` | PASS |
| MT-32 | `ValidatePattern_ReturnsFalse_ForNonPairTwoCards` | PASS |
| MT-33 | `ValidatePattern_ReturnsTrue_ForMixedSameSuitThrowAttempt` | PASS |
| MT-34 | `Game_PlayCardsEx_MixedThrowBlocked_FallsBackToSmallestSingle` | PASS |
| MT-35 | `Game_PlayCardsEx_MixedThrowBlocked_FallsBackToSmallestPair_WhenNoSingle` | PASS |
| MT-36 | `Game_PlayCardsEx_MixedThrowBlocked_FallsBackToSmallestTractor_WhenNoSingleOrPair` | PASS |
| MT-37 | `Game_PlayCardsEx_MixedThrowSuccess_PlaysOriginalCards` | PASS |

## 结论

- 已将混合甩牌相关用例扩充到 37 条，覆盖规则判定、回退策略与集成链路。
- 当前实现下，任一子结构可拦时均正确返回 `THROW_NOT_MAX`；回退策略符合“单牌 > 对子 > 拖拉机”的实现逻辑。
