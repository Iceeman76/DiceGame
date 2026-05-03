namespace DiceGame;

public sealed class ThresholdStrategy(string name) : IPlayerStrategy
{
    /// <summary>Display name for this strategy.</summary>
    public string Name { get; } = name;

    /// <summary>Base round score threshold at which the player considers stopping.</summary>
    public int StopThreshold { get; init; } = 300;

    /// <summary>Stop threshold used when the player is currently leading. Typically lower to protect the lead.</summary>
    public int StopThresholdWhenLeading { get; init; } = 300;

    /// <summary>Stop threshold used when trailing the leader by at least <see cref="TrailingGapTrigger"/> points. Typically higher to chase the leader.</summary>
    public int StopThresholdWhenTrailing { get; init; } = 300;

    /// <summary>Minimum point gap behind the leader that activates <see cref="StopThresholdWhenTrailing"/>.</summary>
    public int TrailingGapTrigger { get; init; } = int.MaxValue;

    /// <summary>Stop threshold used in the final <see cref="FinalRoundsCount"/> rounds. Overrides all other thresholds.</summary>
    public int StopThresholdFinalRounds { get; init; } = 300;

    /// <summary>Number of rounds at the end of the game where <see cref="StopThresholdFinalRounds"/> applies. Zero disables this.</summary>
    public int FinalRoundsCount { get; init; }

    /// <summary>Maximum number of rolls allowed per turn. The player is forced to stop after this many rolls. Zero means unlimited.</summary>
    public int MaxRollsPerTurn { get; init; }

    /// <summary>Minimum number of dice the player is willing to roll when entering an inherited position. Stops before rolling if hand is smaller. One means always roll.</summary>
    public int MinInheritedHandDiceToRoll { get; init; } = 1;

    /// <summary>Minimum inherited score required to take the passed dice. Use <see cref="int.MaxValue"/> to never take inherited dice.</summary>
    public int TakeInheritanceMinScore { get; init; } = int.MaxValue;

    /// <summary>Minimum number of dice that must remain to roll after taking the inherited dice.</summary>
    public int TakeInheritanceMinHandDice { get; init; } = 1;

    /// <summary>When true, always takes inherited dice while trailing the leader, regardless of <see cref="TakeInheritanceMinScore"/>.</summary>
    public bool TakeInheritanceWhenTrailing { get; init; }

    /// <summary>Returns true if the player should stop before the next roll and pass the dice.</summary>
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

    /// <summary>Returns true if the player should take the dice and score passed by the previous player.</summary>
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
