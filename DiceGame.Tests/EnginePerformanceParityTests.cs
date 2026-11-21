using DiceGame.Strategies;
using NUnit.Framework;

namespace DiceGame.Tests;

[TestFixture]
public sealed class EnginePerformanceParityTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void AutomatedAndInteractiveGamesProduceIdenticalResultsWithOrWithoutPresentation(bool enablePresentation)
    {
        for (var gameIndex = 0; gameIndex < 20; gameIndex++)
        {
            var seed = 123 + gameIndex;
            var automatedRandom = new Random(seed);
            var interactiveRandom = new Random(seed);
            var automated = new Game(CreatePlayers(gameIndex), verbose: false, rollDie: () => automatedRandom.Next(1, 7)).Start();
            var opening = new Queue<int>([6, 1, 1, 1]);
            var interactive = new InteractiveGame(CreatePlayers(gameIndex), () => opening.Count > 0 ? opening.Dequeue() : interactiveRandom.Next(1, 7), enablePresentation);
            interactive.RollOpening();

            while (interactive.Phase is not MatchPhase.Finished)
            {
                interactive.PlayComputerStep();
            }

            var rankings = interactive.Players.OrderByDescending(player => player.TotalScore.Value).Select(player => new PlayerResult(player.Name, player.TotalScore.Value));

            Assert.That(rankings, Is.EqualTo(automated.Rankings));
            Assert.That(interactive.History.Count > 0, Is.EqualTo(enablePresentation));
        }
    }

    [Test]
    public void MatchUtilitiesDependOnRelativeStandingsRatherThanAbsoluteScores()
    {
        var strategy = new ParameterizedExpectimaxStrategy(new ExpectimaxParameters(50, 100, 200, 0.5, 0.5, 20));
        var hand = new Dice(0) { 2, 2, 5, 5, 3 };

        for (var diceCount = 1; diceCount <= 5; diceCount++)
        {
            for (var score = 250; score <= 1000; score += 50)
            {
                var original = CreateState(score, diceCount);
                var shifted = original with { MyTotalScore = 2000, OpponentScores = [2200, 1900], LeaderScore = 2200, AverageOpponentScore = 2050 };

                Assert.That(strategy.ShouldStop(shifted), Is.EqualTo(strategy.ShouldStop(original)));
                Assert.That(strategy.ShouldTakePassedDice(shifted), Is.EqualTo(strategy.ShouldTakePassedDice(original)));
                Assert.That(strategy.SelectDiceToKeep(hand, shifted), Is.EqualTo(strategy.SelectDiceToKeep(hand, original)));
            }
        }
    }

    private static Player[] CreatePlayers(int rotation)
    {
        IPlayerStrategy[] strategies = [new AdaptiveStrategy(), new BoldStrategy(), new ExpectimaxStrategy(), new ParameterizedExpectimaxStrategy(new ExpectimaxParameters(50, 75, 200, 0.5, 0.5, 20))];

        return Enumerable.Range(0, strategies.Length).Select(index => new Player($"Player {index}", strategies[(index + rotation) % strategies.Length])).ToArray();
    }

    private static GameState CreateState(int score, int diceCount) => new()
    {
        RoundScore = score,
        HandDiceCount = diceCount,
        TableDiceCount = 5 - diceCount,
        RollsThisTurn = 1,
        CurrentRound = 5,
        TotalRounds = 10,
        IsLastPlayerInRound = false,
        MyTotalScore = 1000,
        PlayerRank = 2,
        PlayerCount = 3,
        PlayersRemainingInRound = 1,
        OpponentScores = [1200, 900],
        LeaderScore = 1200,
        AverageOpponentScore = 1050,
        InheritedScore = 500,
        InheritedHandDiceCount = 3
    };
}
