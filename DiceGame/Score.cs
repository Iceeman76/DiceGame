namespace DiceGame;

public sealed record Score
{
    public int Value { get; private set; }

    public void Add(int score) => Value += score;
}
