namespace DiceGame;

public sealed class Player(string name, IPlayerStrategy strategy)
{
    public string Name { get; } = name;
    public IPlayerStrategy Strategy { get; } = strategy;
    public Score TotalScore { get; } = new();
}
