namespace DiceGame;

public static class PlayerStrategies
{
    public static ThresholdStrategy Passive() => new("Passiv")
    {
        StopThreshold = 250,
        StopThresholdWhenLeading = 250,
        StopThresholdWhenTrailing = 250,
        TakeInheritanceMinScore = int.MaxValue
    };

    public static ThresholdStrategy Bold() => new("Bold")
    {
        StopThreshold = 600,
        StopThresholdWhenLeading = 600,
        StopThresholdWhenTrailing = 600,
        TakeInheritanceMinScore = 0,
        TakeInheritanceMinHandDice = 1
    };

    public static ThresholdStrategy Adaptive() => new("Adaptiv")
    {
        StopThreshold = 350,
        StopThresholdWhenLeading = 250,
        StopThresholdWhenTrailing = 550,
        TrailingGapTrigger = 200,
        StopThresholdFinalRounds = 700,
        FinalRoundsCount = 2,
        TakeInheritanceMinScore = 250,
        TakeInheritanceMinHandDice = 2,
        TakeInheritanceWhenTrailing = true
    };
}
