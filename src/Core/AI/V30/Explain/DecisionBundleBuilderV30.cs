using System;
using System.IO;
using System.Text.Json;

namespace TractorGame.Core.AI.V30.Explain
{
    /// <summary>
    /// V30 decision bundle builder and serializer.
    /// </summary>
    public sealed class DecisionBundleBuilderV30
    {
        private readonly DecisionExplainerV30 _explainer = new DecisionExplainerV30();

        private static readonly JsonSerializerOptions CompactJsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false
        };

        private static readonly JsonSerializerOptions IndentedJsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public DecisionBundleV30 Build(
            DecisionExplainInputV30 input,
            AIDecisionLogContextV30? logContext = null)
        {
            var bundle = _explainer.Build(input);
            bundle.LogContext = logContext;
            return bundle;
        }

        public string Serialize(DecisionBundleV30 bundle, bool indented = false)
        {
            if (bundle == null)
                throw new ArgumentNullException(nameof(bundle));

            return JsonSerializer.Serialize(bundle, indented ? IndentedJsonOptions : CompactJsonOptions);
        }

        public DecisionBundleV30 Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("json is empty", nameof(json));

            var bundle = JsonSerializer.Deserialize<DecisionBundleV30>(json);
            if (bundle == null)
                throw new InvalidOperationException("deserialize decision bundle failed");

            return bundle;
        }

        public string WriteFixture(
            string fixtureDirectory,
            string fixtureName,
            DecisionBundleV30 bundle,
            bool overwrite = true)
        {
            if (string.IsNullOrWhiteSpace(fixtureDirectory))
                throw new ArgumentException("fixture directory is empty", nameof(fixtureDirectory));

            if (string.IsNullOrWhiteSpace(fixtureName))
                throw new ArgumentException("fixture name is empty", nameof(fixtureName));

            Directory.CreateDirectory(fixtureDirectory);

            var safeName = fixtureName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? fixtureName
                : fixtureName + ".json";
            var path = Path.Combine(fixtureDirectory, safeName);

            if (File.Exists(path) && !overwrite)
                throw new IOException($"fixture already exists: {path}");

            File.WriteAllText(path, Serialize(bundle, indented: true));
            return path;
        }
    }
}
