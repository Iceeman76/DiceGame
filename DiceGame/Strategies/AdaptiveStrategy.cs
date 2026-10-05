using System.Collections.Immutable;

namespace DiceGame.Strategies;

public sealed record AdaptiveStrategy : ThresholdStrategy
{
    public AdaptiveStrategy()
    {
        Name = "Adaptive";
        Description = "Adjusts risk to the standings and remaining dice, and avoids passing all five dice.";
        StopThreshold = 350;
        StopThresholdWhenLeading = 250;
        StopThresholdWhenTrailing = 550;
        TrailingGapTrigger = 200;
        StopThresholdFinalRounds = 700;
        StopThresholdByDiceCount = ImmutableDictionary<int, int>.Empty.Add(1, 250).Add(2, 300);
        FinalRoundsCount = 2;
        TakeInheritanceMinScore = 250;
        TakeInheritanceMinHandDice = 2;
        TakeInheritanceWhenTrailing = true;
        MaxDiceToPass = 4;
    }
}
