namespace DiceGame.Strategies;

public sealed record BoldStrategy : ThresholdStrategy
{
    public BoldStrategy()
    {
        Name = "Bold";
        Description = "Takes risks and aims for larger scores.";
        StopThreshold = 600;
        StopThresholdWhenLeading = 600;
        StopThresholdWhenTrailing = 600;
        TakeInheritanceMinScore = 0;
        TakeInheritanceMinHandDice = 1;
    }
}
