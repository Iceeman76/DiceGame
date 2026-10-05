using DiceGame.Strategies;
namespace DiceGame;

public sealed class Player(string name, IPlayerStrategy strategy, bool isHuman = false)
{
    public string Name { get; } = name;
    public bool IsHuman { get; } = isHuman;
    public IPlayerStrategy Strategy { get; } = strategy;
    public Score TotalScore { get; } = new();
}
