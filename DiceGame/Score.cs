namespace DiceGame;

public record Score
{
	private int _score;
	
	public void Clear()
	{
		_score = 0;
	}

	public void Add(int score)
	{
		_score += score;
	}
}