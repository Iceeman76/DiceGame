using DiceGame.Strategies;
using NUnit.Framework;

namespace DiceGame.Tests;

[TestFixture]
public sealed class GameTests
{
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(6)]
    public void SimulatorAndInteractiveGameProduceTheSameResultsForTheSameRolls(int playerCount)
    {
        for (var seed = 0; seed < 10; seed++)
        {
            var strategies = new PlayerStrategies().Available;
            var simulationPlayers = Enumerable.Range(0, playerCount).Select(index => new Player($"Player {index}", strategies[index % strategies.Count])).ToArray();
            var interactivePlayers = simulationPlayers.Select(player => new Player(player.Name, player.Strategy)).ToArray();
            var simulationRandom = new Random(seed);
            var interactiveRandom = new Random(seed);
            var openingRolls = new Queue<int>(Enumerable.Range(0, playerCount).Select(index => index == 0 ? 6 : 1));
            var result = new Game(simulationPlayers, verbose: false, rollDie: () => simulationRandom.Next(1, 7)).Start();
            var match = new InteractiveGame(interactivePlayers, () => openingRolls.Count > 0 ? openingRolls.Dequeue() : interactiveRandom.Next(1, 7));
            match.RollOpening();

            while (match.Phase is not MatchPhase.Finished)
            {
                match.PlayComputerStep();
            }

            var expectedRankings = match.Players.OrderByDescending(player => player.TotalScore.Value).Select(player => new PlayerResult(player.Name, player.TotalScore.Value));
            Assert.That(result.Rankings, Is.EqualTo(expectedRankings), $"Seed {seed}");
        }
    }

    [Test]
    public void SimulatorCannotBankInherited350PointsWithoutAScoringRoll()
    {
        var rolls = new Queue<int>(Enumerable.Range(0, InteractiveGame.TotalRounds).SelectMany(round => new[] { 2, 2, 2, 5, 5, 3, 3, 3, 3, 3 }));
        var strategy = new PassiveStrategy() with { TakeInheritanceMinScore = 250, StopThreshold = 350, StopThresholdWhenLeading = 350, StopThresholdWhenTrailing = 350 };
        var players = new[] { new Player("Alma", strategy), new Player("Elsa", strategy) };
        var result = new Game(players, verbose: false, rollDie: rolls.Dequeue).Start();

        Assert.That(result.Rankings, Is.EqualTo(new[] { new PlayerResult("Alma", 3500), new PlayerResult("Elsa", 0) }));
        Assert.That(rolls, Is.Empty);
    }
}
