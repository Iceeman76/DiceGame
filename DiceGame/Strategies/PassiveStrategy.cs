namespace DiceGame.Strategies;

public sealed record PassiveStrategy : ThresholdStrategy
{
    public PassiveStrategy()
    {
        Name = "Passive";
        Description = "Banks early and always starts fresh instead of taking over passed points and dice.";
        StopThreshold = 250;
        StopThresholdWhenLeading = 250;
        StopThresholdWhenTrailing = 250;
        TakeInheritanceMinScore = int.MaxValue;
    }
}
