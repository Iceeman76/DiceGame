namespace DiceGame;

/// <summary>Specifies how many dice of each scoring face value the player chooses to keep after a roll.</summary>
public sealed record DiceSelection(int Ones, int Twos, int Fives)
{
    /// <summary>Total number of dice kept. Zero means a voluntary bust.</summary>
    public int Total => Ones + Twos + Fives;
}
