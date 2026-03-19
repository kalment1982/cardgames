using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TractorGame.Tests.V30.Specs
{
    public class V30ModuleTestMatrixTests
    {
        [Fact]
        public void Matrix_AllModulesHavePositiveNegativeBoundaryCases()
        {
            foreach (var module in V30TestMatrixCatalog.Modules)
            {
                Assert.True(
                    V30TestMatrixCatalog.CasesByModule.ContainsKey(module),
                    $"Module `{module}` is missing from matrix.");

                var caseTypes = V30TestMatrixCatalog.CasesByModule[module]
                    .Select(c => c.CaseType)
                    .Distinct()
                    .ToHashSet();

                Assert.Contains(V30CaseType.Positive, caseTypes);
                Assert.Contains(V30CaseType.Negative, caseTypes);
                Assert.Contains(V30CaseType.Boundary, caseTypes);
            }
        }

        [Fact]
        public void Matrix_EveryFrozenEntryHasAtLeastOneAcceptanceCase()
        {
            var byEntry = V30TestMatrixCatalog.Cases
                .Where(c => !string.IsNullOrWhiteSpace(c.FrozenEntryId))
                .GroupBy(c => c.FrozenEntryId!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            var missing = V30TestMatrixCatalog.FrozenEntryIds
                .Where(entry => !byEntry.ContainsKey(entry))
                .ToList();

            Assert.True(
                missing.Count == 0,
                "Frozen entries missing acceptance coverage: " + string.Join(", ", missing));
        }

        [Fact]
        public void Matrix_ExplainContainsRequiredFieldCompletenessCase()
        {
            var explainCase = V30TestMatrixCatalog.Cases.SingleOrDefault(c =>
                c.Module == "Explain" &&
                c.CaseId == "Explain_Positive_RequiredFieldsPresent");

            Assert.NotNull(explainCase);
            Assert.Equal(V30CaseType.Positive, explainCase!.CaseType);
            Assert.Contains("generated_at_utc", V30TestMatrixCatalog.RequiredExplainFields);
            Assert.Contains("log_context", V30TestMatrixCatalog.RequiredExplainFields);
            Assert.Equal(15, V30TestMatrixCatalog.RequiredExplainFields.Count);
        }

        [Fact]
        public void Matrix_SmokeRegressionCasesCoverCoreModules()
        {
            var smokeCases = V30TestMatrixCatalog.Cases
                .Where(c => c.IsSmoke)
                .ToList();

            Assert.True(smokeCases.Count >= 5, "Smoke coverage should have at least 5 cases.");

            var smokeModules = smokeCases.Select(c => c.Module).Distinct().ToHashSet();
            Assert.Contains("Lead", smokeModules);
            Assert.Contains("Bottom", smokeModules);
            Assert.Contains("Memory", smokeModules);
            Assert.Contains("Explain", smokeModules);
        }

        [Fact]
        public void Matrix_CaseIdsAreUnique()
        {
            var duplicates = V30TestMatrixCatalog.Cases
                .GroupBy(c => c.CaseId, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.True(
                duplicates.Count == 0,
                "Duplicate case ids: " + string.Join(", ", duplicates));
        }
    }
}
