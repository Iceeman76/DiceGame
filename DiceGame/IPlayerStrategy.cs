namespace DiceGame;

public interface IPlayerStrategy
{
    string Name { get; }
    bool ShouldStop(GameState state);
    bool ShouldTakePassedDice(GameState state);
    DiceSelection SelectDiceToKeep(Dice hand, GameState state);
}
