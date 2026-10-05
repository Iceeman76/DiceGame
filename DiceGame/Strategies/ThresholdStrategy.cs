using System;
using System.Collections.Immutable;
using System.Linq;

namespace DiceGame.Strategies;

public abstract record ThresholdStrategy : IPlayerStrategy
{
    public string Name { get; init; } = "DiceMaster";

    public string Description { get; protected init; } = "";

    public int StopThreshold { get; init; } = 300;
    public int StopThresholdWhenLeading { get; init; } = 300;
    public int StopThresholdWhenTrailing { get; init; } = 300;
    public int TrailingGapTrigger { get; init; } = int.MaxValue;
    public int StopThresholdFinalRounds { get; init; } = 300;
    public ImmutableDictionary<int, int> StopThresholdByDiceCount { get; init; } = ImmutableDictionary<int, int>.Empty;
    public int FinalRoundsCount { get; init; }
    public int MaxRollsPerTurn { get; init; }
    public int MinInheritedHandDiceToRoll { get; init; } = 1;
    public int MaxTwosToKeepPerRoll { get; init; } = int.MaxValue;
    public int MaxFivesToKeepPerRoll { get; init; } = int.MaxValue;
    public bool SaveTripleOnes { get; init; } = true;
    public int TakeInheritanceMinScore { get; init; } = int.MaxValue;
    public int TakeInheritanceMinHandDice { get; init; } = 1;
    public bool TakeInheritanceWhenTrailing { get; init; }
    public bool ChaseLeadInFinalRound { get; init; } = true;
    public bool SecureWinOnFinalTurn { get; init; } = true;
    public double MaxBustProbability { get; init; } = 1;
    public double MinimumExpectedNetGain { get; init; } = double.NegativeInfinity;
    public int TargetLeadMargin { get; init; }
    public int SimulationBudget { get; init; }
    public int MaxDiceToPass { get; init; } = 5;

    public bool ShouldStop(GameState state)
    {
        if (SecureWinOnFinalTurn && state.RoundsLeft == 0 && state.IsLastPlayerInRound && state.ProjectedTotalScore > state.LeaderScore)
        {
            return true;
        }

        if (MaxRollsPerTurn > 0 && state.RollsThisTurn >= MaxRollsPerTurn)
        {
            return true;
        }

        if (ChaseLeadInFinalRound && state.RoundsLeft == 0 && state.ProjectedTotalScore <= state.LeaderScore)
        {
            return false;
        }

        if (ShouldAvoidRoll(state))
        {
            return true;
        }

        if (state.HandDiceCount > MaxDiceToPass && (state.RoundsLeft > 0 || state.IsLastPlayerInRound is false))
        {
            return false;
        }

        if (TargetLeadMargin > 0 && state.ProjectedTotalScore < state.BestOpponentScore + TargetLeadMargin)
        {
            return false;
        }

        if (SimulationBudget > 0)
        {
            return new StrategySimulation(this, state).ShouldBank();
        }

        var threshold = state.IsLeadingAfterBanking
            ? StopThresholdWhenLeading
            : state.PointsBehindLeaderAfterBanking >= TrailingGapTrigger
                ? StopThresholdWhenTrailing
                : StopThreshold;

        if (FinalRoundsCount > 0 && state.RoundsLeft <= FinalRoundsCount)
        {
            threshold = StopThresholdFinalRounds;
        }

        if (StopThresholdByDiceCount.TryGetValue(state.HandDiceCount, out var diceCountThreshold))
        {
            threshold = Math.Min(threshold, diceCountThreshold);
        }

        return state.RoundScore >= threshold;
    }

    public bool ShouldTakePassedDice(GameState state)
    {
        if (state.InheritedHandDiceCount < Math.Max(TakeInheritanceMinHandDice, MinInheritedHandDiceToRoll))
        {
            return false;
        }

        var canTakeInheritance = TakeInheritanceWhenTrailing && state.IsLeading is false || state.InheritedScore >= TakeInheritanceMinScore;

        if (canTakeInheritance is false)
        {
            return false;
        }

        var inheritedState = state with { RoundScore = state.InheritedScore, HandDiceCount = state.InheritedHandDiceCount };

        if (ShouldAvoidRoll(inheritedState))
        {
            return false;
        }

        return SimulationBudget <= 0 || new StrategySimulation(this, state).ShouldTakeInheritedDice();
    }

    public DiceSelection SelectDiceToKeep(Dice hand, GameState state)
    {
        var selection = SelectDiceByLimits(hand, state);

        if (SimulationBudget <= 0 || selection.Total == 0 || SecureWinOnFinalTurn && state.RoundsLeft == 0 && state.IsLastPlayerInRound && state.RoundScore + selection.Score >= InteractiveGame.MinimumBankScore && state.ProjectedTotalScore + selection.Score > state.LeaderScore)
        {
            return selection;
        }

        return new StrategySimulation(this, state).SelectDiceToKeep(hand);
    }

    internal DiceSelection SelectDiceByLimits(Dice hand, GameState state)
    {
        var onesInHand = hand.Count(face => face == 1);
        var availableTwos = hand.Count(face => face == 2);
        var availableFives = hand.Count(face => face == 5);
        var allScoringOnes = onesInHand >= 3 ? 3 : 0;
        var allScoringPoints = hand.Score();

        if (SecureWinOnFinalTurn && state.RoundsLeft == 0 && state.IsLastPlayerInRound && state.RoundScore + allScoringPoints >= InteractiveGame.MinimumBankScore && state.ProjectedTotalScore + allScoringPoints > state.LeaderScore)
        {
            return new DiceSelection(allScoringOnes, availableTwos, availableFives);
        }

        var ones = SaveTripleOnes && onesInHand >= 3 ? 3 : 0;
        var twos = Math.Min(availableTwos, MaxTwosToKeepPerRoll);
        var fives = Math.Min(availableFives, MaxFivesToKeepPerRoll);

        return new DiceSelection(ones, twos, fives);
    }

    private bool ShouldAvoidRoll(GameState state)
    {
        if (MaxBustProbability >= 1 && double.IsNegativeInfinity(MinimumExpectedNetGain))
        {
            return false;
        }

        var forecast = DiceRollStatistics.Forecast(this, state);

        return forecast.BustProbability > MaxBustProbability || forecast.ExpectedNetGain < MinimumExpectedNetGain;
    }
}
