namespace DiceGame;

public record Player(string Name)
{
	public Score TotalScore { get; } = new();
}
