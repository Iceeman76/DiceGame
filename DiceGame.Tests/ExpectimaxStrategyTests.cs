using DiceGame.Strategies;
using NUnit.Framework;

namespace DiceGame.Tests;

[TestFixture]
public sealed class ExpectimaxStrategyTests
{
    private readonly ExpectimaxStrategy strategy = new();

    [TestCase(1, true)]
    [TestCase(2, true)]
    [TestCase(5, false)]
    public void StoppingDependsOnTheValueOfTheEntireRemainingTurn(int diceCount, bool shouldStop)
    {
        Assert.That(strategy.ShouldStop(CreateState(300, diceCount)), Is.EqualTo(shouldStop));
    }

    [Test]
    public void InheritanceRequiresAnotherSuccessfulRollEvenWithHotDice()
    {
        var state = CreateState(0, 5);

        Assert.That(strategy.ShouldTakePassedDice(state with { InheritedScore = 250, InheritedHandDiceCount = 1 }), Is.False);
        Assert.That(strategy.ShouldTakePassedDice(state with { InheritedScore = 1000, InheritedHandDiceCount = 1 }), Is.True);
        Assert.That(strategy.ShouldTakePassedDice(state with { InheritedScore = 250, InheritedHandDiceCount = 5 }), Is.True);
    }

    [Test]
    public void SelectionCanLeaveScoringDiceAvailableForMoreValuableFutureRolls()
    {
        var hand = new Dice(0) { 2, 2, 5, 3, 4 };
        var selection = strategy.SelectDiceToKeep(hand, CreateState(0, 5));

        Assert.That(selection.Total, Is.InRange(1, 2));
        Assert.That(selection.Fives, Is.GreaterThan(0));
        Assert.That(hand, Is.EqualTo(new[] { 2, 2, 5, 3, 4 }));
    }

    [Test]
    public void BankingAGuaranteedFinalVictoryOverridesExpectedPoints()
    {
        var state = CreateState(250, 5) with { CurrentRound = 10, OpponentScores = [200], LeaderScore = 200 };

        Assert.That(strategy.ShouldStop(state), Is.True);
    }

    [TestCase(1, true)]
    [TestCase(5, false)]
    public void FinalTurnComparesSharedVictoryWithTheChanceOfWinningOutright(int diceCount, bool shouldStop)
    {
        var state = CreateState(250, diceCount) with { CurrentRound = 10, OpponentScores = [250], LeaderScore = 250 };

        Assert.That(strategy.ShouldStop(state), Is.EqualTo(shouldStop));
    }

    [Test]
    public void FinalTurnSelectionKeepsEnoughPointsToSecureVictory()
    {
        var hand = new Dice(0) { 2, 2, 5, 5, 3 };
        var state = CreateState(0, 5) with { CurrentRound = 10, OpponentScores = [250], LeaderScore = 250 };
        var selection = strategy.SelectDiceToKeep(hand, state);

        Assert.That(selection.Score, Is.EqualTo(300));
    }

    [Test]
    public void FinalTurnAccountsForAllPlayersSharingVictory()
    {
        var state = CreateState(250, 1) with { CurrentRound = 10, PlayerCount = 4, OpponentScores = [250, 250, 250], LeaderScore = 250 };

        Assert.That(strategy.ShouldStop(state), Is.False);
    }

    [Test]
    public void VeryLargeTurnScoresBankWithoutAnArbitraryTableLimit()
    {
        Assert.That(strategy.ShouldStop(CreateState(100_000, 5)), Is.True);
    }

    [Test]
    public void FinalRoundKeepsChasingWhenBankingWouldLose()
    {
        var state = CreateState(300, 1) with { CurrentRound = 10, IsLastPlayerInRound = false, OpponentScores = [1000], LeaderScore = 1000 };

        Assert.That(strategy.ShouldStop(state), Is.False);
    }

    [Test]
    public void BankingRequiresTheMinimumScoreAndASuccessfulRoll()
    {
        Assert.That(strategy.ShouldStop(CreateState(200, 1)), Is.False);
        Assert.That(strategy.ShouldStop(CreateState(1000, 1) with { RollsThisTurn = 0 }), Is.False);
    }

    [Test]
    public void SeededMatchesOutperformAdaptiveAcrossBothSeats()
    {
        var random = new Random(7183);
        var victoryShare = 0d;

        for (var gameIndex = 0; gameIndex < 1000; gameIndex++)
        {
            var players = new[] { new Player(strategy.Name, strategy), new Player("Adaptive", new AdaptiveStrategy()) };

            if (gameIndex % 2 == 1)
            {
                Array.Reverse(players);
            }

            var result = new Game(players, verbose: false, rollDie: () => random.Next(1, 7)).Start();
            var winners = result.Rankings.Where(player => player.Score == result.WinnerScore).ToArray();

            if (winners.Any(player => player.Name == strategy.Name))
            {
                victoryShare += 1d / winners.Length;
            }
        }

        Assert.That(victoryShare, Is.GreaterThan(520));
    }

    private static GameState CreateState(int score, int diceCount) => new()
    {
        RoundScore = score,
        HandDiceCount = diceCount,
        TableDiceCount = 5 - diceCount,
        RollsThisTurn = 1,
        CurrentRound = 1,
        TotalRounds = 10,
        IsLastPlayerInRound = true,
        MyTotalScore = 0,
        PlayerRank = 2,
        PlayerCount = 2,
        OpponentScores = [2000],
        LeaderScore = 2000,
        AverageOpponentScore = 2000
    };
}
