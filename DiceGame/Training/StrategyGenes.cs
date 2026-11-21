using System;
using System.Collections.Immutable;
using DiceGame.Strategies;

namespace DiceGame.Training;

public sealed record StrategyGenes(ImmutableArray<int> Values)
{
    private static readonly int[] minimums = [5, 5, 5, 0, 5, 0, 5, 5, 0, 1, 0, 1, 1, 1];
    private static readonly int[] maximums = [30, 30, 40, 30, 40, 4, 30, 30, 40, 5, 1, 5, 5, 5];

    public static StrategyGenes Adaptive => new([7, 5, 11, 4, 14, 2, 5, 6, 5, 2, 1, 5, 5, 4]);

    public bool IsValid()
    {
        if (Values.IsDefault || Values.Length is not 14)
        {
            return false;
        }

        for (var index = 0; index < Values.Length; index++)
        {
            if (Values[index] < minimums[index] || Values[index] > maximums[index])
            {
                return false;
            }
        }

        return true;
    }
    public AdaptiveStrategy CreateStrategy(string name = "Evolved") => new()
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

    public static StrategyGenes CreateRandom(Random random)
    {
        var values = ImmutableArray.CreateBuilder<int>(minimums.Length);

        for (var index = 0; index < minimums.Length; index++)
        {
            values.Add(random.Next(minimums[index], maximums[index] + 1));
        }

        return new StrategyGenes(values.ToImmutable());
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
                    ? random.Next(minimums[index], maximums[index] + 1)
                    : Math.Clamp(values[index] + random.Next(-3, 4), minimums[index], maximums[index]);
            }
        }

        return new StrategyGenes(values.ToImmutable());
    }
}

