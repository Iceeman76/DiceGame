namespace DiceGame;

/// <summary>Specifies how many dice of each scoring face value the player chooses to keep after a roll.</summary>
public sealed record DiceSelection(int Twos, int Fives);
