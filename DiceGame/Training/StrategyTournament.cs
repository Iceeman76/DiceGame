using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiceGame.Strategies;

namespace DiceGame.Training;

public sealed record TournamentStanding(string Name, double WinShare, double AverageScore)
{
    public int LastSeatGames { get; init; }
    public int LastSeatWins { get; init; }
    public int LastSeatSharedWins { get; init; }
}
public sealed record TournamentProgress(int CompletedGames, int TotalGames, TournamentStanding[] Standings);

public static class StrategyTournament
{
    public static async Task<TournamentStanding[]> RunAsync(IReadOnlyList<IPlayerStrategy> strategies, int games, int seed, Action<int>? report = null, CancellationToken cancellationToken = default, Action<TournamentProgress>? reportLive = null)
    {
        var random = new Random(seed);
        var wins = new double[strategies.Count];
        var scores = new long[strategies.Count];
        var lastSeatGames = new int[strategies.Count];
        var lastSeatWins = new int[strategies.Count];
        var lastSeatSharedWins = new int[strategies.Count];

        for (var gameIndex = 0; gameIndex < games; gameIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var players = strategies.Select((strategy, index) => new Player(index.ToString(), strategy)).ToArray();
            random.Shuffle(players);
            var lastPlayerIndex = int.Parse(players[^1].Name);
            lastSeatGames[lastPlayerIndex]++;
            var result = new Game(players, verbose: false, rollDie: () => random.Next(1, 7)).Start();
            var winnerCount = result.Rankings.Count(player => player.Score == result.WinnerScore);

            foreach (var player in result.Rankings)
            {
                var index = int.Parse(player.Name);
                scores[index] += player.Score;

                if (player.Score == result.WinnerScore)
                {
                    wins[index] += 1d / winnerCount;

                    if (index == lastPlayerIndex)
                    {
                        lastSeatWins[index]++;

                        if (winnerCount > 1)
                        {
                            lastSeatSharedWins[index]++;
                        }
                    }
                }
            }

            if (gameIndex % 10 == 0 || gameIndex == games - 1)
            {
                report?.Invoke(gameIndex + 1);
                reportLive?.Invoke(new TournamentProgress(gameIndex + 1, games, CreateStandings(gameIndex + 1)));
                await Task.Delay(1, cancellationToken);
            }
        }

        return CreateStandings(games);

        TournamentStanding[] CreateStandings(int completedGames) => strategies.Select((strategy, index) => new TournamentStanding(strategy.Name, wins[index] / completedGames, (double)scores[index] / completedGames)
        {
            LastSeatGames = lastSeatGames[index],
            LastSeatWins = lastSeatWins[index],
            LastSeatSharedWins = lastSeatSharedWins[index]
        }).OrderByDescending(standing => standing.WinShare).ToArray();
    }
}
