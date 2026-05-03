using System.Collections.Generic;
using System.Linq;

namespace DiceGame;

public record Player
{
	public Score TotalScore { get; } = new();
	//public Score RoundScore { get; } = new();
}