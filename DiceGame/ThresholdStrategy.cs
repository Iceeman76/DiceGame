namespace DiceGame;

public class ThresholdStrategy : IPlayerStrategy
{
    public string Name { get; }

    /// Stop when round score >= this.
    public int StopThreshold { get; set; } = 300;

    /// When leading, use this lower threshold to protect the lead.
    public int StopThresholdWhenLeading { get; set; } = 300;

    /// When trailing the leader by >= TrailingGapTrigger points, use this higher threshold.
    public int StopThresholdWhenTrailing { get; set; } = 300;
    public int TrailingGapTrigger { get; set; } = int.MaxValue;

    /// In the last FinalRoundsCount rounds, use this threshold (overrides all others).
    public int StopThresholdFinalRounds { get; set; } = 300;
    public int FinalRoundsCount { get; set; } = 0;

    /// Force stop after this many rolls per turn regardless of score (0 = unlimited).
    public int MaxRollsPerTurn { get; set; } = 0;

    /// Take passed dice only if inherited score >= this (int.MaxValue = never).
    public int TakeInheritanceMinScore { get; set; } = int.MaxValue;

    /// Also require at least this many dice remaining to roll after taking.
    public int TakeInheritanceMinHandDice { get; set; } = 1;

    /// Always take passed dice when trailing the leader, regardless of inherited score.
    public bool TakeInheritanceWhenTrailing { get; set; } = false;

    public ThresholdStrategy(string name) => Name = name;

    public bool ShouldStop(GameState state)
    {
        if (MaxRollsPerTurn > 0 && state.RollsThisTurn >= MaxRollsPerTurn)
            return true;

        var threshold = state.IsLeading
            ? StopThresholdWhenLeading
            : state.PointsBehindLeader >= TrailingGapTrigger
                ? StopThresholdWhenTrailing
                : StopThreshold;

        if (FinalRoundsCount > 0 && state.RoundsLeft <= FinalRoundsCount)
            threshold = StopThresholdFinalRounds;

        return state.RoundScore >= threshold;
    }

    public bool ShouldTakePassedDice(GameState state)
    {
        if (state.InheritedHandDiceCount < TakeInheritanceMinHandDice)
            return false;

        if (TakeInheritanceWhenTrailing && !state.IsLeading)
            return true;

        return state.InheritedScore >= TakeInheritanceMinScore;
    }
}
