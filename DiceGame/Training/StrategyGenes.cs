using System;
using System.Collections.Immutable;
using DiceGame.Strategies;

namespace DiceGame.Training;

public sealed record StrategyGenes(ImmutableArray<int> Values)
{
    private static readonly int[] minimums = [5, 5, 5, 0, 5, 0, 5, 5, 0, 1, 0, 1, 1, 1];
    private static readonly int[] maximums = [30, 30, 40, 30, 40, 4, 30, 30, 40, 5, 1, 5, 5, 5];
    private static readonly int[] expectimaxMinimums = [-4, 0, 0, 0, 0, 0];
    private static readonly int[] expectimaxMaximums = [4, 4, 8, 4, 4, 4];
    private static readonly int[] hybridMinimums = [.. expectimaxMinimums, .. minimums, 0, 0, 0, 0, 0];
    private static readonly int[] hybridMaximums = [.. expectimaxMaximums, .. maximums, 3, 1, 3, 20, 41];

    public string Family { get; init; } = "Threshold";
    private int[] Minimums => Family switch { "Expectimax" => expectimaxMinimums, "Hybrid" => hybridMinimums, _ => minimums };
    private int[] Maximums => Family switch { "Expectimax" => expectimaxMaximums, "Hybrid" => hybridMaximums, _ => maximums };

    public static StrategyGenes Adaptive => new([7, 5, 11, 4, 14, 2, 5, 6, 5, 2, 1, 5, 5, 4]);
    public static StrategyGenes Expectimax => new([0, 0, 4, 0, 0, 0]) { Family = "Expectimax" };
    public static StrategyGenes Hybrid => new([.. Expectimax.Values, .. Adaptive.Values, 0, 0, 0, 20, 0]) { Family = "Hybrid" };
    public static StrategyGenes HybridAdaptive => new([.. Expectimax.Values, .. Adaptive.Values, 1, 1, 1, 20, 0]) { Family = "Hybrid" };
    public static StrategyGenes Baseline(string family) => family switch { "Expectimax" => Expectimax, "Hybrid" => Hybrid, _ => Adaptive };

    public bool IsValid()
    {
        if (Family is not ("Threshold" or "Expectimax" or "Hybrid") || Values.IsDefault || Values.Length.Equals(Minimums.Length) is false)
        {
            return false;
        }

        for (var index = 0; index < Values.Length; index++)
        {
            if (Values[index] < Minimums[index] || Values[index] > Maximums[index])
            {
                return false;
            }
        }

        return true;
    }
    public IPlayerStrategy CreateStrategy(string name = "Evolved")
    {
        if (Family is "Expectimax" or "Hybrid")
        {
            var parameters = new ExpectimaxParameters(Values[0] * 25, Values[1] * 25, Values[2] * 50, Values[3] / 4d, Values[4] / 4d, Values[5] * 10);

            if (Family == "Hybrid")
            {
                var thresholdGenes = new StrategyGenes(Values.Slice(6, 14));
                var threshold = (ThresholdStrategy)thresholdGenes.CreateStrategy();
                var hybrid = new HybridParameters(Values[20], Values[21], Values[22], Values[23] / 20d, Values[24] == 0 ? double.NegativeInfinity : (Values[24] - 21) * 25);

                return new HybridStrategy(parameters, threshold, hybrid, name);
            }

            return new ParameterizedExpectimaxStrategy(parameters, name);
        }

        return new AdaptiveStrategy
        {
            Name = name,
            StopThreshold = Values[0] * 50,
            StopThresholdWhenLeading = Values[1] * 50,
            StopThresholdWhenTrailing = Values[2] * 50,
            TrailingGapTrigger = Values[3] * 50,
            StopThresholdFinalRounds = Values[4] * 50,
            FinalRoundsCount = Values[5],
            StopThresholdByDiceCount = ImmutableDictionary<int, int>.Empty.Add(1, Values[6] * 50).Add(2, Values[7] * 50),
            TakeInheritanceMinScore = Values[8] * 50,
            TakeInheritanceMinHandDice = Values[9],
            TakeInheritanceWhenTrailing = Values[10] == 1,
            MaxTwosToKeepPerRoll = Values[11],
            MaxFivesToKeepPerRoll = Values[12],
            MaxDiceToPass = Values[13]
        };
    }

    public static StrategyGenes CreateRandom(Random random, string family = "Threshold")
    {
        var baseline = Baseline(family);
        var values = ImmutableArray.CreateBuilder<int>(baseline.Minimums.Length);

        for (var index = 0; index < baseline.Minimums.Length; index++)
        {
            values.Add(random.Next(baseline.Minimums[index], baseline.Maximums[index] + 1));
        }

        return new StrategyGenes(values.ToImmutable()) { Family = family };
    }

    public StrategyGenes Breed(StrategyGenes other, Random random, double mutationRate = 0.2)
    {
        var values = Values.ToBuilder();

        for (var index = 0; index < values.Count; index++)
        {
            values[index] = random.Next(2) == 0 ? Values[index] : other.Values[index];

            if (random.NextDouble() < mutationRate)
            {
                values[index] = random.NextDouble() < 0.15
                    ? random.Next(Minimums[index], Maximums[index] + 1)
                    : Math.Clamp(values[index] + random.Next(-3, 4), Minimums[index], Maximums[index]);
            }
        }

        return new StrategyGenes(values.ToImmutable()) { Family = Family };
    }
}

