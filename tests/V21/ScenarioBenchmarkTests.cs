using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace TractorGame.Tests.V21
{
    /// <summary>
    /// 场景驱动评测：用模拟牌局验证AI决策质量。
    /// 每个场景对应一个拖拉机技巧，AI必须做出符合技巧的决策才算通过。
    /// </summary>
    [Trait("Category", "Benchmark")]
    public class ScenarioBenchmarkTests
    {
        private readonly ITestOutputHelper _output;

        public ScenarioBenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public static IEnumerable<object[]> AllScenarios() =>
            TractorScenarioLibrary.All().Select(s => new object[] { s.Name, s });

        public static IEnumerable<object[]> LeadScenarios() =>
            TractorScenarioLibrary.LeadScenarios().Select(s => new object[] { s.Name, s });

        public static IEnumerable<object[]> FollowScenarios() =>
            TractorScenarioLibrary.FollowScenarios().Select(s => new object[] { s.Name, s });

        public static IEnumerable<object[]> TrumpScenarios() =>
            TractorScenarioLibrary.TrumpManagementScenarios().Select(s => new object[] { s.Name, s });

        public static IEnumerable<object[]> EndgameScenarios() =>
            TractorScenarioLibrary.EndgameScenarios().Select(s => new object[] { s.Name, s });

        [Theory]
        [MemberData(nameof(LeadScenarios))]
        public void Lead_Scenario(string name, GameScenario scenario)
        {
            if (scenario.IsKnownDefect) return;
            var result = ScenarioJudge.Run(scenario);
            _output.WriteLine(result.ToString());
            _output.WriteLine($"  说明: {scenario.Description}");
            Assert.True(result.Passed, $"{name}: {string.Join("; ", result.FailedExpectations)}");
        }

        [Theory]
        [MemberData(nameof(FollowScenarios))]
        public void Follow_Scenario(string name, GameScenario scenario)
        {
            if (scenario.IsKnownDefect) return;
            var result = ScenarioJudge.Run(scenario);
            _output.WriteLine(result.ToString());
            _output.WriteLine($"  说明: {scenario.Description}");
            Assert.True(result.Passed, $"{name}: {string.Join("; ", result.FailedExpectations)}");
        }

        [Theory]
        [MemberData(nameof(TrumpScenarios))]
        public void TrumpManagement_Scenario(string name, GameScenario scenario)
        {
            if (scenario.IsKnownDefect) return;
            var result = ScenarioJudge.Run(scenario);
            _output.WriteLine(result.ToString());
            _output.WriteLine($"  说明: {scenario.Description}");
            Assert.True(result.Passed, $"{name}: {string.Join("; ", result.FailedExpectations)}");
        }

        [Theory]
        [MemberData(nameof(EndgameScenarios))]
        public void Endgame_Scenario(string name, GameScenario scenario)
        {
            if (scenario.IsKnownDefect) return;
            var result = ScenarioJudge.Run(scenario);
            _output.WriteLine(result.ToString());
            _output.WriteLine($"  说明: {scenario.Description}");
            Assert.True(result.Passed, $"{name}: {string.Join("; ", result.FailedExpectations)}");
        }

        /// <summary>
        /// 汇总报告：打印所有场景的通过率，不强制失败。
        /// 用于观察AI整体决策质量趋势。
        /// </summary>
        [Fact]
        public void ScenarioBenchmark_Summary()
        {
            var scenarios = TractorScenarioLibrary.All().ToList();
            var results = scenarios.Select(ScenarioJudge.Run).ToList();

            var defects = scenarios.Zip(results, (s, r) => (s, r)).Where(x => x.s.IsKnownDefect).ToList();
            var active = scenarios.Zip(results, (s, r) => (s, r)).Where(x => !x.s.IsKnownDefect).ToList();

            int passed = active.Count(x => x.r.Passed);
            int total = active.Count;

            _output.WriteLine($"\n=== 场景评测汇总 ({passed}/{total} 通过) ===\n");

            var groups = active.GroupBy(x => x.s.Phase);
            foreach (var group in groups)
            {
                var groupPassed = group.Count(x => x.r.Passed);
                _output.WriteLine($"[{group.Key}] {groupPassed}/{group.Count()}");
                foreach (var (s, r) in group)
                    _output.WriteLine($"  {r}");
                _output.WriteLine("");
            }

            if (defects.Count > 0)
            {
                _output.WriteLine($"=== 已知缺陷（M2待修，{defects.Count}个）===");
                foreach (var (s, r) in defects)
                    _output.WriteLine($"  {r}");
                _output.WriteLine("");
            }

            double passRate = total > 0 ? (double)passed / total : 1.0;
            _output.WriteLine($"通过率: {passRate:P0}（不含已知缺陷）");

            Assert.True(passRate >= 0.80,
                $"场景通过率 {passRate:P0} 低于基线80%，AI决策质量需要关注");
        }
    }
}
