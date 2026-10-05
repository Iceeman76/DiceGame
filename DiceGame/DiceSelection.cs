namespace DiceGame;

/// <summary>Specifies how many dice of each scoring face value the player chooses to keep after a roll.</summary>
public sealed record DiceSelection(int Ones, int Twos, int Fives)
{
    public const int TwoScore = 50;
    public const int FiveScore = 100;
    public const int TripleOnesScore = 1000;
    /// <summary>Total number of dice kept. Zero means a voluntary bust.</summary>
    public int Total => Ones + Twos + Fives;
    public int Score => (Ones == 3 ? TripleOnesScore : 0) + Twos * TwoScore + Fives * FiveScore;
}
