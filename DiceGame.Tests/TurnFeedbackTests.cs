using DiceGame.Strategies;
using NUnit.Framework;

namespace DiceGame.Tests;

[TestFixture]
public sealed class TurnFeedbackTests
{
    [Test]
    public void AdaptiveAvoidsPassingFiveDiceButThePreferenceCanBeDisabled()
    {
        var strategy = new AdaptiveStrategy();
        var state = CreateState(5);

        Assert.That(strategy.MaxDiceToPass, Is.EqualTo(4));
        Assert.That(strategy.ShouldStop(state), Is.False);
        Assert.That((strategy with { MaxDiceToPass = 5 }).ShouldStop(state), Is.True);
        Assert.That(strategy.ShouldStop(state with { HandDiceCount = 4 }), Is.True);
    }

    [Test]
    public void AnyStrategyCanLimitTheNumberOfPassedDice()
    {
        var strategy = new PassiveStrategy() with { MaxDiceToPass = 3 };

        Assert.That(strategy.ShouldStop(CreateState(4)), Is.False);
        Assert.That(strategy.ShouldStop(CreateState(3)), Is.True);
    }

    [Test]
    public void PassingPreferenceDoesNotPreventAGuaranteedVictory()
    {
        var strategy = new AdaptiveStrategy();

        Assert.That(strategy.ShouldStop(CreateState(5) with { CurrentRound = 10 }), Is.True);
    }

    [Test]
    public void PassingPreferenceDoesNotApplyWhenThereIsNoNextPlayer()
    {
        var strategy = new AdaptiveStrategy() with { SecureWinOnFinalTurn = false };
        var state = CreateState(5) with { CurrentRound = 10, RoundScore = 750 };

        Assert.That(strategy.ShouldStop(state), Is.True);
        Assert.That(strategy.ShouldStop(state with { IsLastPlayerInRound = false }), Is.False);
    }

    [Test]
    public void ExplicitRiskLimitsTakePriorityOverPassingPreferences()
    {
        var strategy = new AdaptiveStrategy() with { MaxBustProbability = 0.01 };

        Assert.That(strategy.ShouldStop(CreateState(5)), Is.True);
    }

    [Test]
    public void LeadTargetUsesReachableScoresAndUpdatesWithKeptPoints()
    {
        var rolls = new Queue<int>([6, 1, 5, 2, 3, 4, 6, 5, 2, 3]);
        var human = new Player("Player 1", new PassiveStrategy(), isHuman: true);
        var opponent = new Player("Alma", new PassiveStrategy());
        opponent.TotalScore.Add(250);
        var match = new InteractiveGame([human, opponent], rolls.Dequeue);
        match.RollOpening();
        Assert.That(match.TurnPointsToTakeLead, Is.EqualTo(300));
        Assert.That(match.AdditionalPointsToTakeLead, Is.EqualTo(300));
        match.Roll();
        match.KeepDice(new DiceSelection(0, 1, 1));
        Assert.That(match.AdditionalPointsToTakeLead, Is.EqualTo(150));
        match.Roll();
        match.KeepDice(new DiceSelection(0, 1, 1));
        Assert.That(match.AdditionalPointsToTakeLead, Is.Zero);
        Assert.That(match.IsCurrentPlayerLeading, Is.False);
        match.Bank();

        Assert.That(match.IsCurrentPlayerLeading, Is.True);
        Assert.That(match.TurnPointsToTakeLead, Is.Zero);
    }

    private static GameState CreateState(int diceCount) => new()
    {
        RoundScore = 300,
        HandDiceCount = diceCount,
        TableDiceCount = 5 - diceCount,
        RollsThisTurn = 1,
        CurrentRound = 1,
        TotalRounds = 10,
        IsLastPlayerInRound = true,
        MyTotalScore = 0,
        PlayerRank = 2,
        PlayerCount = 2,
        LeaderScore = 250,
        AverageOpponentScore = 250,
        InheritedScore = 0,
        InheritedHandDiceCount = 0
    };
}
