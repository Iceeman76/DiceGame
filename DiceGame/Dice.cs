using System.Collections.Generic;
using System.Linq;

namespace DiceGame;

public class Dice : List<int>
{
	public Dice(int numDice)
	{
		AddRange(Enumerable.Repeat(0, numDice));
	}

	public void Roll()
	{
		for (var i = 0; i < Count; i++)
		{
			this[i] = Randomizer.Integer(1, 6);
		}
	}

	public override string ToString() => $"{string.Join(", ", this)} ({Score()}p)";

	public int Score()
	{
		var ones = this.Count(x => x == 1);
		var twos = this.Count(x => x == 2);
		var fives = this.Count(x => x == 5);

		var score = twos * 50 + fives * 100;
		if (ones >= 3) score += 1000;

		return score;
	}
}