using System.Collections.Immutable;
using DiceGame.Strategies;
using NUnit.Framework;

namespace DiceGame.Tests;

[TestFixture]
public sealed class ThresholdStrategyTests
{
    [Test]
    public void FinalPlayerKeepsBothTwosToSecureVictoryDespiteTheLimit()
    {
        var strategy = new BoldStrategy() with { MaxTwosToKeepPerRoll = 1 };
        var hand = new Dice(0) { 2, 2, 3, 4, 6 };
        var state = CreateState(200, 5, 1250) with { MyTotalScore = 1000, CurrentRound = 10 };
        var selection = strategy.SelectDiceToKeep(hand, state);

        Assert.That(selection, Is.EqualTo(new DiceSelection(0, 2, 0)));
        Assert.That(strategy.ShouldStop(state with { RoundScore = 300, HandDiceCount = 3 }), Is.True);
    }

    [TestCase(9, true, true, 1250)]
    [TestCase(10, false, true, 1250)]
    [TestCase(10, true, false, 1250)]
    [TestCase(10, true, true, 1300)]
    public void DiceLimitsRemainWhenVictoryCannotBeSecuredOrTheOverrideIsDisabled(int round, bool lastPlayer, bool secureWin, int leaderScore)
    {
        var strategy = new BoldStrategy() with { MaxTwosToKeepPerRoll = 1, SecureWinOnFinalTurn = secureWin };
        var state = CreateState(200, 5, leaderScore) with { MyTotalScore = 1000, CurrentRound = round, IsLastPlayerInRound = lastPlayer };

        Assert.That(strategy.SelectDiceToKeep(new Dice(0) { 2, 2, 3, 4, 6 }, state), Is.EqualTo(new DiceSelection(0, 1, 0)));
    }

    [Test]
    public void SecuringVictoryCanOverrideLimitsForFivesAndTripleOnes()
    {
        var strategy = new AdaptiveStrategy() with { MaxFivesToKeepPerRoll = 1, SaveTripleOnes = false };
        var state = CreateState(0, 5, 1100) with { CurrentRound = 10 };

        Assert.That(strategy.SelectDiceToKeep(new Dice(0) { 1, 1, 1, 5, 5 }, state), Is.EqualTo(new DiceSelection(3, 0, 2)));
    }

    [Test]
    public void VictoryOverrideDoesNotOfferAnIllegalBankBelow250Points()
    {
        var strategy = new BoldStrategy() with { MaxTwosToKeepPerRoll = 1 };
        var state = CreateState(0, 5, 50) with { CurrentRound = 10 };

        Assert.That(strategy.SelectDiceToKeep(new Dice(0) { 2, 2, 3, 4, 6 }, state), Is.EqualTo(new DiceSelection(0, 1, 0)));
    }

    [TestCase(250, 300, true, 0)]
    [TestCase(300, 300, true, 0)]
    [TestCase(600, 400, false, 200)]
    public void ProjectedStandingsIncludeTheCurrentTurn(int leaderScore, int roundScore, bool expectedLeading, int expectedGap)
    {
        var state = CreateState(roundScore, 5, leaderScore);

        Assert.That(state.ProjectedTotalScore, Is.EqualTo(roundScore));
        Assert.That(state.IsLeadingAfterBanking, Is.EqualTo(expectedLeading));
        Assert.That(state.PointsBehindLeaderAfterBanking, Is.EqualTo(expectedGap));
        Assert.That(state.IsLeading, Is.False);
    }

    [Test]
    public void ProjectedTotalIncludesPreviouslyBankedPoints()
    {
        var state = CreateState(300, 5, 600) with { MyTotalScore = 350 };

        Assert.That(state.ProjectedTotalScore, Is.EqualTo(650));
        Assert.That(state.IsLeadingAfterBanking, Is.True);
    }

    [TestCase(250, 300, true)]
    [TestCase(300, 300, true)]
    [TestCase(600, 400, false)]
    [TestCase(600, 401, true)]
    public void AdaptiveUsesTheProjectedLeadAndGap(int leaderScore, int roundScore, bool expectedStop)
    {
        var strategy = new AdaptiveStrategy();

        Assert.That(strategy.ShouldStop(CreateState(roundScore, 4, leaderScore)), Is.EqualTo(expectedStop));
    }

    [TestCase(1, 250, true)]
    [TestCase(2, 250, false)]
    [TestCase(2, 300, true)]
    [TestCase(3, 300, false)]
    [TestCase(4, 450, false)]
    [TestCase(5, 450, false)]
    public void AdaptiveLowersTheThresholdForRiskyHands(int handDiceCount, int roundScore, bool expectedStop)
    {
        var strategy = new AdaptiveStrategy();

        Assert.That(strategy.ShouldStop(CreateState(roundScore, handDiceCount, 1000)), Is.EqualTo(expectedStop));
    }

    [TestCase(1, true)]
    [TestCase(3, true)]
    [TestCase(4, false)]
    [TestCase(5, false)]
    public void AnyThresholdStrategyCanConfigureRiskForAnyDiceCount(int handDiceCount, bool expectedStop)
    {
        var strategy = new BoldStrategy() with { StopThresholdByDiceCount = ImmutableDictionary<int, int>.Empty.Add(1, 250).Add(3, 350) };

        Assert.That(strategy.ShouldStop(CreateState(400, handDiceCount, 1000)), Is.EqualTo(expectedStop));
    }

    [Test]
    public void DiceBasedRiskAdjustmentIsOptional()
    {
        var strategy = new BoldStrategy();

        Assert.That(strategy.StopThresholdByDiceCount, Is.Empty);
        Assert.That(strategy.ShouldStop(CreateState(400, 1, 1000)), Is.False);
    }

    [Test]
    public void RiskCapsCannotRaiseThePositionThreshold()
    {
        var strategy = new PassiveStrategy() with { StopThresholdByDiceCount = ImmutableDictionary<int, int>.Empty.Add(1, 500) };

        Assert.That(strategy.ShouldStop(CreateState(250, 1, 1000)), Is.True);
    }

    [TestCase(8, 5, 300, 250, false)]
    [TestCase(8, 1, 300, 250, true)]
    [TestCase(10, 1, 300, 300, false)]
    [TestCase(10, 2, 600, 1000, false)]
    [TestCase(10, 1, 300, 250, true)]
    public void RiskCapsRespectFinalRoundLeadChasing(int currentRound, int handDiceCount, int roundScore, int leaderScore, bool expectedStop)
    {
        var strategy = new AdaptiveStrategy();
        var state = CreateState(roundScore, handDiceCount, leaderScore) with { CurrentRound = currentRound };

        Assert.That(strategy.ShouldStop(state), Is.EqualTo(expectedStop));
    }

    private static GameState CreateState(int roundScore, int handDiceCount, int leaderScore) => new()
    {
        RoundScore = roundScore,
        HandDiceCount = handDiceCount,
        TableDiceCount = 5 - handDiceCount,
        RollsThisTurn = 1,
        CurrentRound = 1,
        TotalRounds = 10,
        IsLastPlayerInRound = true,
        MyTotalScore = 0,
        PlayerRank = 3,
        PlayerCount = 3,
        LeaderScore = leaderScore,
        AverageOpponentScore = leaderScore,
        InheritedScore = 0,
        InheritedHandDiceCount = 0
    };
}
