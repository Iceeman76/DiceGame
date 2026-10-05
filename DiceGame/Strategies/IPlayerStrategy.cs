namespace DiceGame.Strategies;

public interface IPlayerStrategy
{
    string Name { get; }
    string Description => Name;
    bool ShouldStop(GameState state);
    bool ShouldTakePassedDice(GameState state);
    DiceSelection SelectDiceToKeep(Dice hand, GameState state);
}
