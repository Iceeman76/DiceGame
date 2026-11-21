using System;
using System.Diagnostics;
using DiceGame;
using DiceGame.Strategies;

namespace Simulator;

public static class PerformanceCommand
{
    public static void Run()
    {
        Measure("Threshold matches", 3000, () => new AdaptiveStrategy());
        Measure("Built-in Expectimax matches", 3000, () => new ExpectimaxStrategy());
        Measure("Evolved Expectimax matches", 100, () => new ParameterizedExpectimaxStrategy(new ExpectimaxParameters(50, 75, 200, 0.5, 0.5, 20)));
        Measure("Risk-only Expectimax matches", 100, () => new ParameterizedExpectimaxStrategy(new ExpectimaxParameters(RiskAdjustment: 50, PassedDicePenalty: 20)));
    }

    private static void Measure(string title, int games, Func<IPlayerStrategy> createStrategy)
    {
        RunMatches(Math.Min(games, 30), createStrategy());
        var timings = new double[3];
        var allocations = new long[3];
        var checksum = 0L;

        for (var repetition = 0; repetition < 3; repetition++)
        {
            var strategy = createStrategy();
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var clock = Stopwatch.StartNew();
            checksum = RunMatches(games, strategy);
            clock.Stop();
            timings[repetition] = clock.Elapsed.TotalMilliseconds;
            allocations[repetition] = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        }

        Array.Sort(timings);
        Array.Sort(allocations);
        Console.WriteLine($"{title}: {games} games, median {timings[1]:F1} ms, {allocations[1] / games:N0} bytes/game, checksum {checksum}");
    }

    private static long RunMatches(int games, IPlayerStrategy candidate)
    {
        var random = new Random(20261121);
        var checksum = 0L;

        for (var index = 0; index < games; index++)
        {
            var players = new[] { new Player("Candidate", candidate), new Player("Adaptive", new AdaptiveStrategy()), new Player("Bold", new BoldStrategy()), new Player("Passive", new PassiveStrategy()) };
            random.Shuffle(players);
            var result = new Game(players, verbose: false, rollDie: () => random.Next(1, 7)).Start();

            foreach (var player in result.Rankings)
            {
                checksum = unchecked(checksum * 31 + player.Score);

                foreach (var character in player.Name)
                {
                    checksum = unchecked(checksum * 31 + character);
                }
            }
        }

        return checksum;
    }
}
