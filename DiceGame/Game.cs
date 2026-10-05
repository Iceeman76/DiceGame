using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceGame;

public sealed class Game
{
    private readonly InteractiveGame match;
    private readonly bool verbose;

    public Game(IEnumerable<Player> players, bool verbose = true, bool randomizeOrder = false, Func<int>? rollDie = null)
    {
        var orderedPlayers = players.ToList();
        this.verbose = verbose;

        if (randomizeOrder)
        {
            Randomizer.Shuffle(orderedPlayers);
        }

        match = new InteractiveGame(orderedPlayers, rollDie);
    }

    public GameResult Start()
    {
        match.StartInSeatingOrder();

        while (match.Phase is not MatchPhase.Finished)
        {
            var previousHistoryCount = match.History.Count;
            match.PlayComputerStep();

            if (verbose)
            {
                foreach (var entry in match.History.Take(match.History.Count - previousHistoryCount).Reverse())
                {
                    Console.WriteLine(entry);
                }
            }
        }

        var rankings = match.Players.OrderByDescending(player => player.TotalScore.Value).Select(player => new PlayerResult(player.Name, player.TotalScore.Value)).ToList();

        if (verbose)
        {
            Console.WriteLine("Game over");

            for (var index = 0; index < rankings.Count; index++)
            {
                Console.WriteLine($"{index + 1}. {rankings[index].Name}: {rankings[index].Score} points");
            }
        }

        return new GameResult(rankings);
    }
}
