using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceGame.Strategies;

internal static class DiceRollStatistics
{
    private static readonly IReadOnlyList<Outcome>[] outcomes = Enumerable.Range(0, 6).Select(BuildOutcomes).ToArray();

    internal static RollForecast Forecast(ThresholdStrategy strategy, GameState state)
    {
        var bustProbability = 0d;
        var expectedNetGain = 0d;

        foreach (var outcome in outcomes[state.HandDiceCount])
        {
            var selection = strategy.SelectDiceByLimits(outcome.Hand, state);

            if (selection.Score == 0)
            {
                bustProbability += outcome.Probability;
                expectedNetGain -= outcome.Probability * state.RoundScore;
            }
            else
            {
                expectedNetGain += outcome.Probability * selection.Score;
            }
        }

        return new RollForecast(bustProbability, expectedNetGain);
    }

    private static IReadOnlyList<Outcome> BuildOutcomes(int diceCount)
    {
        var combinations = (int)Math.Pow(6, diceCount);
        var frequencies = new Dictionary<(int Ones, int Twos, int Fives), int>();

        for (var combination = 0; combination < combinations; combination++)
        {
            var remaining = combination;
            var ones = 0;
            var twos = 0;
            var fives = 0;

            for (var dieIndex = 0; dieIndex < diceCount; dieIndex++)
            {
                var face = remaining % 6 + 1;
                remaining /= 6;
                ones += face == 1 ? 1 : 0;
                twos += face == 2 ? 1 : 0;
                fives += face == 5 ? 1 : 0;
            }

            var faces = (ones, twos, fives);
            frequencies[faces] = frequencies.GetValueOrDefault(faces) + 1;
        }

        return frequencies.Select(frequency =>
        {
            var hand = new Dice(0);
            hand.AddRange(Enumerable.Repeat(1, frequency.Key.Ones));
            hand.AddRange(Enumerable.Repeat(2, frequency.Key.Twos));
            hand.AddRange(Enumerable.Repeat(5, frequency.Key.Fives));
            hand.AddRange(Enumerable.Repeat(3, diceCount - hand.Count));

            return new Outcome(hand, (double)frequency.Value / combinations);
        }).ToArray();
    }

    private sealed record Outcome(Dice Hand, double Probability);
}

internal sealed record RollForecast(double BustProbability, double ExpectedNetGain);
