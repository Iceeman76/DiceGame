using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceGame;

public class Game
{
	private Dice _hand = new(5);
	private Dice _table = new(0);
	private List<Player> _players;

	public Game(int numPlayers)
	{
		_players = Enumerable.Repeat(new Player(), numPlayers).ToList();
	}

	public void Start()
	{
		var roll = 0;
		var player = _players.First();
		var score = 0;

		while (true)
		{
			// Roll dice
			roll++;
			_hand.Roll();
			var handScore = _hand.Score();
			Console.WriteLine("-------------------------------------------------");
			Console.WriteLine($"Roll {roll}");
			Console.WriteLine($"Hand: {_hand}");

			if (handScore == 0)
			{
				Console.WriteLine("No score");
				score = 0;
				break;
			}

			// determine what dice to keep
			score += KeepDice();

			if (score >= 250 && ShouldPass())
			{
				player.TotalScore.Add(score);
				Console.WriteLine($"Passes and keep {score} score");
				break;
			}

			if (roll == 3 && score < 250)
			{
				Console.WriteLine($"Score {score} is not enough after three rolls");
				score = 0;
				break;
			}
		}
	}

	private int KeepDice( )
	{
		var h = _hand.FindIndex(x => x == 2);
		if (h < 0)
		{
			Console.WriteLine("Keep none!");
			return 0;
		}
		Console.WriteLine($"Keep die at index {h}");
		_table.Add(_hand[h]);
		_hand.RemoveAt(h);
		Console.WriteLine($"Table: {_table}");
		Console.WriteLine($"Hand: {_hand}");
		return 50;
	}
	
	private bool ShouldPass()
	{
		return Randomizer.Boolean();
	}
}