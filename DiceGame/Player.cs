namespace DiceGame;

public class Player
{
    public string Name { get; }
    public IPlayerStrategy Strategy { get; }
    public Score TotalScore { get; } = new();

    public Player(string name, IPlayerStrategy strategy)
    {
        Name = name;
        Strategy = strategy;
    }
}
