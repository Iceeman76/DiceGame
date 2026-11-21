using System;
using System.Collections.Immutable;
using System.Linq;
using DiceGame;
using DiceGame.Strategies;
using Simulator;

if (args is ["benchmark"])
{
    PerformanceCommand.Run();

    return;
}

if (args.Length > 0)
{
    Environment.ExitCode = TrainingCommand.Run(args);

    return;
}

const int simulations = 25_000;

Simulate("Strategy comparison", simulations, [new PassiveStrategy(), new BoldStrategy(), new AdaptiveStrategy(), new ExpectimaxStrategy()]);

foreach (var opponent in new IPlayerStrategy[] { new PassiveStrategy(), new BoldStrategy(), new AdaptiveStrategy() })
{
    Simulate($"Expectimax vs {opponent.Name}", simulations, [new ExpectimaxStrategy(), opponent]);
}

Simulate("Dice risk comparison", simulations, [MakeAdaptive("Always rolls", 1), MakeAdaptive("Stops with one die", 2), MakeAdaptive("Stops with at most two dice", 3)]);

static AdaptiveStrategy MakeAdaptive(string name, int minimumDice) => new AdaptiveStrategy() with
{
    Name = name,
    StopThresholdByDiceCount = Enumerable.Range(1, minimumDice - 1).ToImmutableDictionary(diceCount => diceCount, diceCount => 250)
};

static void Simulate(string title, int gameCount, IPlayerStrategy[] strategies)
{
    var random = new Random(20261005);
    var wins = strategies.ToDictionary(strategy => strategy.Name, _ => 0d);
    var totalScores = strategies.ToDictionary(strategy => strategy.Name, _ => 0L);
    var maximumScores = strategies.ToDictionary(strategy => strategy.Name, _ => 0);

    for (var gameIndex = 0; gameIndex < gameCount; gameIndex++)
    {
        var players = strategies.Select(strategy => new Player(strategy.Name, strategy)).ToArray();
        random.Shuffle(players);
        var result = new Game(players, verbose: false, rollDie: () => random.Next(1, 7)).Start();
        var winners = result.Rankings.Where(player => player.Score == result.WinnerScore).ToArray();

        foreach (var winner in winners)
        {
            wins[winner.Name] += 1d / winners.Length;
        }

        foreach (var player in result.Rankings)
        {
            totalScores[player.Name] += player.Score;
            maximumScores[player.Name] = Math.Max(maximumScores[player.Name], player.Score);
        }
    }

    Console.WriteLine($"\n{title} ({gameCount:N0} games, seed 20261005):\n");
    Console.WriteLine($"{"Strategy",-28} {"Win share",12} {"Average",12} {"Highest",12}");
    Console.WriteLine(new string('-', 68));

    foreach (var strategy in strategies)
    {
        var winPercentage = wins[strategy.Name] / gameCount * 100;
        var averageScore = (double)totalScores[strategy.Name] / gameCount;
        Console.WriteLine($"{strategy.Name,-28} {winPercentage,11:F2}% {averageScore,12:F1} {maximumScores[strategy.Name],12}");
    }
}
