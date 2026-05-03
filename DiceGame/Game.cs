using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceGame;

public class Game
{
	private readonly List<Player> _players;
	private const int TotalRounds = 10;

	// State passed between consecutive player turns
	private Dice? _passedTable;
	private int _passedScore;

	public Game(int numPlayers)
	{
		_players = Enumerable.Range(1, numPlayers)
			.Select(i => new Player($"Player {i}"))
			.ToList();
	}

	public void Start()
	{
		for (var round = 1; round <= TotalRounds; round++)
		{
			Console.WriteLine($"\n========= ROUND {round} =========");
			foreach (var player in _players)
				PlayTurn(player);
		}

		PrintResults();
	}

	private void PlayTurn(Player player)
	{
		Console.WriteLine($"\n--- {player.Name}'s turn ---");

		var (table, roundScore) = InitializeTurn(player);
		var hand = new Dice(5 - table.Count);

		while (true)
		{
			if (hand.Count == 0)
			{
				Console.WriteLine("All dice kept — rolling fresh set.");
				hand = new Dice(5);
				table = new Dice(0);
			}

			hand.Roll();
			Console.WriteLine($"Rolled: [{string.Join(", ", hand)}]  Table: [{string.Join(", ", table)}]  Score: {roundScore}p");

			if (hand.Score() == 0)
			{
				Console.WriteLine($"{player.Name} busted! Round score lost.");
				return;
			}

			roundScore += KeepScoringDice(hand, table);
			Console.WriteLine($"Kept:   [{string.Join(", ", table)}]  Round score: {roundScore}p");

			if (roundScore >= 250 && ShouldStop())
			{
				player.TotalScore.Add(roundScore);
				_passedTable = table;
				_passedScore = roundScore;
				Console.WriteLine($"{player.Name} stops with {roundScore}p (total: {player.TotalScore.Value}p)");
				return;
			}
		}
	}

	private (Dice table, int score) InitializeTurn(Player player)
	{
		if (_passedTable is null)
			return (new Dice(0), 0);

		var passed = _passedTable;
		var passedScore = _passedScore;
		_passedTable = null;
		_passedScore = 0;

		if (ShouldTakePassedDice())
		{
			Console.WriteLine($"{player.Name} takes passed dice [{string.Join(", ", passed)}] with {passedScore}p");
			return (passed, passedScore);
		}

		Console.WriteLine($"{player.Name} starts fresh (skips {passedScore}p from passed dice)");
		return (new Dice(0), 0);
	}

	// Moves all scoring dice from hand to table and returns the points gained.
	// Ones only score if there are 3+ in the same roll and cannot be saved individually.
	private static int KeepScoringDice(Dice hand, Dice table)
	{
		var score = 0;

		var onesCount = hand.Count(x => x == 1);
		if (onesCount >= 3)
		{
			score += 1000;
			hand.RemoveAll(x => x == 1);
			for (var i = 0; i < onesCount; i++) table.Add(1);
		}

		var twosCount = hand.Count(x => x == 2);
		score += twosCount * 50;
		hand.RemoveAll(x => x == 2);
		for (var i = 0; i < twosCount; i++) table.Add(2);

		var fivesCount = hand.Count(x => x == 5);
		score += fivesCount * 100;
		hand.RemoveAll(x => x == 5);
		for (var i = 0; i < fivesCount; i++) table.Add(5);

		return score;
	}

	private static bool ShouldStop() => Randomizer.Boolean();

	private static bool ShouldTakePassedDice() => Randomizer.Boolean();

	private void PrintResults()
	{
		Console.WriteLine("\n========= GAME OVER =========");
		var ranked = _players.OrderByDescending(p => p.TotalScore.Value).ToList();
		for (var i = 0; i < ranked.Count; i++)
			Console.WriteLine($"{i + 1}. {ranked[i].Name}: {ranked[i].TotalScore.Value}p");
		Console.WriteLine($"\nVinnare: {ranked[0].Name}!");
	}
}
