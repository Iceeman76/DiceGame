namespace DiceGame;

public sealed class ThresholdStrategy(string name) : IPlayerStrategy
{
    public string Name { get; } = name;

    public int StopThreshold { get; init; } = 300;
    public int StopThresholdWhenLeading { get; init; } = 300;
    public int StopThresholdWhenTrailing { get; init; } = 300;
    public int TrailingGapTrigger { get; init; } = int.MaxValue;
    public int StopThresholdFinalRounds { get; init; } = 300;
    public int FinalRoundsCount { get; init; } = 0;
    public int MaxRollsPerTurn { get; init; } = 0;
    public int MinInheritedHandDiceToRoll { get; init; } = 1;
    public int TakeInheritanceMinScore { get; init; } = int.MaxValue;
    public int TakeInheritanceMinHandDice { get; init; } = 1;
    public bool TakeInheritanceWhenTrailing { get; init; } = false;

    public bool ShouldStop(GameState state)
    {
        if (MaxRollsPerTurn > 0 && state.RollsThisTurn >= MaxRollsPerTurn)
        {
            return true;
        }

        if (state.RollsThisTurn == 0 && state.HandDiceCount < MinInheritedHandDiceToRoll)
        {
            return true;
        }

        var threshold = state.IsLeading
            ? StopThresholdWhenLeading
            : state.PointsBehindLeader >= TrailingGapTrigger
                ? StopThresholdWhenTrailing
                : StopThreshold;

        if (FinalRoundsCount > 0 && state.RoundsLeft <= FinalRoundsCount)
        {
            threshold = StopThresholdFinalRounds;
        }

        return state.RoundScore >= threshold;
    }

    public bool ShouldTakePassedDice(GameState state)
    {
        if (state.InheritedHandDiceCount < TakeInheritanceMinHandDice)
        {
            return false;
        }

        if (TakeInheritanceWhenTrailing && !state.IsLeading)
        {
            return true;
        }

        return state.InheritedScore >= TakeInheritanceMinScore;
    }
}
