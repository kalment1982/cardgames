namespace TractorGame.Core.AI.Evolution.PolicyFactory
{
    public enum MonotonicConstraint
    {
        None,
        AscByDifficulty,
        DescByDifficulty
    }

    public sealed class GeneSpec
    {
        public string Name { get; init; } = string.Empty;
        public double Min { get; init; } = 0.0;
        public double Max { get; init; } = 1.0;
        public MonotonicConstraint Constraint { get; init; } = MonotonicConstraint.None;
        public int FloatPrecision { get; init; } = 6;
    }
}
