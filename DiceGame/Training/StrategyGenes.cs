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

    public string Family { get; init; } = "Threshold";
    private int[] Minimums => Family == "Expectimax" ? expectimaxMinimums : minimums;
    private int[] Maximums => Family == "Expectimax" ? expectimaxMaximums : maximums;

    public static StrategyGenes Adaptive => new([7, 5, 11, 4, 14, 2, 5, 6, 5, 2, 1, 5, 5, 4]);
    public static StrategyGenes Expectimax => new([0, 0, 4, 0, 0, 0]) { Family = "Expectimax" };
    public static StrategyGenes Baseline(string family) => family == "Expectimax" ? Expectimax : Adaptive;

    public bool IsValid()
    {
        if (Family is not ("Threshold" or "Expectimax") || Values.IsDefault || Values.Length.Equals(Minimums.Length) is false)
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
        if (Family == "Expectimax")
        {
            var parameters = new ExpectimaxParameters(Values[0] * 25, Values[1] * 25, Values[2] * 50, Values[3] / 4d, Values[4] / 4d, Values[5] * 10);

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

