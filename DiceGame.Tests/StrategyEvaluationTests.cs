using System.Collections.Immutable;
using DiceGame.Strategies;
using NUnit.Framework;

namespace DiceGame.Tests;

[TestFixture]
public sealed class StrategyEvaluationTests
{
    [Test]
    public void TheEngineSuppliesOpponentScoresAndRemainingPlayersInTurnOrder()
    {
        var recordingStrategy = new RecordingStrategy(new AdaptiveStrategy());
        var firstOpponent = new Player("Bold", new BoldStrategy());
        var secondOpponent = new Player("Passive", new PassiveStrategy());
        firstOpponent.TotalScore.Add(100);
        secondOpponent.TotalScore.Add(300);
        var rolls = new Queue<int>([6, 1, 1, 5, 5, 2, 3, 4]);
        var match = new InteractiveGame([new Player("Recorder", recordingStrategy), firstOpponent, secondOpponent], rolls.Dequeue);
        match.RollOpening();
        match.PlayComputerStep();
        match.PlayComputerStep();
        var state = recordingStrategy.States.Single();
        firstOpponent.TotalScore.Add(500);

        Assert.That(state.PlayersRemainingInRound, Is.EqualTo(2));
        Assert.That(state.OpponentScores, Is.EqualTo(new[] { 100, 300 }));
        Assert.That(state.BestOpponentScore, Is.EqualTo(300));
    }

    [TestCase(1, 2d / 3)]
    [TestCase(2, 4d / 9)]
    [TestCase(3, 63d / 216)]
    [TestCase(4, 243d / 1296)]
    [TestCase(5, 918d / 7776)]
    public void BustLimitUsesTheExactProbabilityIncludingTripleOnes(int diceCount, double probability)
    {
        var state = CreateState(250, diceCount, 2000);
        var belowLimit = new BoldStrategy() with { MaxBustProbability = probability - 0.000001 };
        var aboveLimit = new BoldStrategy() with { MaxBustProbability = probability + 0.000001 };

        Assert.That(belowLimit.ShouldStop(state), Is.True);
        Assert.That(aboveLimit.ShouldStop(state), Is.False);
    }

    [Test]
    public void ExpectedNetGainAccountsForThePointsLostOnABust()
    {
        var state = CreateState(250, 3, 2000);
        var lowerRequirement = new BoldStrategy() with { MinimumExpectedNetGain = 6 };
        var higherRequirement = new BoldStrategy() with { MinimumExpectedNetGain = 7 };

        Assert.That(lowerRequirement.ShouldStop(state), Is.False);
        Assert.That(higherRequirement.ShouldStop(state), Is.True);
    }

    [Test]
    public void ExpectedNetGainAccountsForDiceKeepingLimits()
    {
        var state = CreateState(250, 3, 2000);
        var keepAll = new BoldStrategy() with { MinimumExpectedNetGain = 3 };
        var keepOneTwo = keepAll with { MaxTwosToKeepPerRoll = 1 };

        Assert.That(keepAll.ShouldStop(state), Is.False);
        Assert.That(keepOneTwo.ShouldStop(state), Is.True);
    }

    [Test]
    public void LeadMarginRequiresTheProjectedTotalToReachTheTarget()
    {
        var strategy = new PassiveStrategy() with { TargetLeadMargin = 100 };

        Assert.That(strategy.ShouldStop(CreateState(300, 5, 250)), Is.False);
        Assert.That(strategy.ShouldStop(CreateState(350, 5, 250)), Is.True);
    }

    [Test]
    public void LeadMarginUsesTheBestOpponentRatherThanThePlayersOwnScore()
    {
        var strategy = new PassiveStrategy() with { TargetLeadMargin = 600 };
        var state = CreateState(250, 5, 500) with { MyTotalScore = 500, PlayerRank = 1, OpponentScores = [100] };

        Assert.That(strategy.ShouldStop(state), Is.True);
    }

    [Test]
    public void RiskLimitsTakePriorityOverBuildingALargerLead()
    {
        var strategy = new BoldStrategy() with { TargetLeadMargin = 1000, MaxBustProbability = 0.5 };

        Assert.That(strategy.ShouldStop(CreateState(250, 1, 2000)), Is.True);
    }

    [Test]
    public void FinalRoundLeadChasingTakesPriorityOverRiskLimits()
    {
        var strategy = new BoldStrategy() with { MaxBustProbability = 0.01, MinimumExpectedNetGain = 1000 };

        Assert.That(strategy.ShouldStop(CreateState(250, 1, 2000) with { CurrentRound = 10 }), Is.False);
    }

    [Test]
    public void InheritanceCanBeRejectedBecauseOfRiskOrExpectedLoss()
    {
        var state = CreateState(0, 5, 2000) with { InheritedScore = 400, InheritedHandDiceCount = 2 };
        var riskLimited = new BoldStrategy() with { MaxBustProbability = 0.4 };
        var gainLimited = new BoldStrategy() with { MinimumExpectedNetGain = 0 };

        Assert.That(new BoldStrategy().ShouldTakePassedDice(state), Is.True);
        Assert.That(riskLimited.ShouldTakePassedDice(state), Is.False);
        Assert.That(gainLimited.ShouldTakePassedDice(state), Is.False);
    }

    [Test]
    public void SimulationsPreferBankingAWinnerOverRollingBelowTheUsualThreshold()
    {
        var state = CreateState(250, 1, 200) with { CurrentRound = 10 };
        var baseline = new BoldStrategy() with { SecureWinOnFinalTurn = false };
        var simulated = baseline with { SimulationBudget = 16 };

        Assert.That(baseline.ShouldStop(state), Is.False);
        Assert.That(simulated.ShouldStop(state), Is.True);
    }

    [Test]
    public void SimulationsCompareInheritedHotDiceAgainstStartingFresh()
    {
        var strategy = new BoldStrategy() with { SimulationBudget = 16 };
        var state = CreateState(0, 5, 300) with { CurrentRound = 10, InheritedScore = 350, InheritedHandDiceCount = 5 };

        Assert.That(strategy.ShouldTakePassedDice(state), Is.True);
    }

    [Test]
    public void SimulatedDiceSelectionsAreReproducibleAndRespectTheLimits()
    {
        var strategy = new BoldStrategy() with { SimulationBudget = 16, MaxTwosToKeepPerRoll = 1, MaxFivesToKeepPerRoll = 1 };
        var hand = new Dice(0) { 2, 2, 5, 5, 3 };
        var state = CreateState(0, 5, 2000) with { CurrentRound = 10 };
        var selection = strategy.SelectDiceToKeep(hand, state);

        Assert.That(selection.Total, Is.GreaterThan(0));
        Assert.That(selection.Twos, Is.LessThanOrEqualTo(1));
        Assert.That(selection.Fives, Is.LessThanOrEqualTo(1));
        Assert.That(strategy.SelectDiceToKeep(hand, state), Is.EqualTo(selection));
        Assert.That(hand, Is.EqualTo(new[] { 2, 2, 5, 5, 3 }));
    }

    [Test]
    public void SimulationEnabledStrategiesCompleteMatchesThroughTheSharedEngine()
    {
        var random = new Random(42);
        var strategy = new AdaptiveStrategy() with { SimulationBudget = 4, MaxBustProbability = 0.5, MinimumExpectedNetGain = 0 };
        var players = new[] { new Player("Adaptive", strategy), new Player("Bold", new BoldStrategy()), new Player("Passive", new PassiveStrategy()) };
        var result = new Game(players, verbose: false, rollDie: () => random.Next(1, 7)).Start();

        Assert.That(result.Rankings.Count, Is.EqualTo(3));
        Assert.That(result.Rankings.Select(player => player.Name), Is.EquivalentTo(players.Select(player => player.Name)));
        Assert.That(result.Rankings.Select(player => player.Score), Is.All.GreaterThanOrEqualTo(0));
    }

    private static GameState CreateState(int roundScore, int diceCount, int leaderScore) => new()
    {
        RoundScore = roundScore,
        HandDiceCount = diceCount,
        TableDiceCount = 5 - diceCount,
        RollsThisTurn = 1,
        CurrentRound = 1,
        TotalRounds = 10,
        IsLastPlayerInRound = true,
        MyTotalScore = 0,
        PlayerRank = 2,
        PlayerCount = 2,
        PlayersRemainingInRound = 0,
        OpponentScores = ImmutableArray.Create(leaderScore),
        LeaderScore = leaderScore,
        AverageOpponentScore = leaderScore,
        InheritedScore = 0,
        InheritedHandDiceCount = 0
    };

    private sealed class RecordingStrategy(IPlayerStrategy strategy) : IPlayerStrategy
    {
        public string Name => "Recorder";
        public List<GameState> States { get; } = [];
        public bool ShouldStop(GameState state) => strategy.ShouldStop(state);
        public bool ShouldTakePassedDice(GameState state) => strategy.ShouldTakePassedDice(state);

        public DiceSelection SelectDiceToKeep(Dice hand, GameState state)
        {
            States.Add(state);

            return strategy.SelectDiceToKeep(hand, state);
        }
    }
}
