using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiceGame.Strategies;

namespace DiceGame.Training;

public sealed record StrategyEvaluation(int Games, double WinShare, double StandardError, double AverageScore);
public sealed record StrategyComparison(string Name, int Games, double WinShare, double AverageScore);
public sealed record PlayerCountEvaluation(int PlayerCount, int Games, double WinShare, double AverageScore);
public sealed record EvaluationProgress(StrategyEvaluation Candidate, StrategyComparison[] Opponents)
{
    public PlayerCountEvaluation[] ByPlayerCount { get; init; } = [];
}

public static class StrategyEvaluator
{
    public static StrategyEvaluation Evaluate(IPlayerStrategy candidate, IReadOnlyList<IPlayerStrategy> opponents, int games, int seed, int minimumPlayers = 2, int maximumPlayers = 6)
        => EvaluateAsync(candidate, opponents, games, seed, yieldToBrowser: false, minimumPlayers: minimumPlayers, maximumPlayers: maximumPlayers).GetAwaiter().GetResult();

    public static async Task<StrategyEvaluation> EvaluateAsync(IPlayerStrategy candidate, IReadOnlyList<IPlayerStrategy> opponents, int games, int seed, CancellationToken cancellationToken = default, bool yieldToBrowser = true, Action<StrategyEvaluation>? report = null, Action<EvaluationProgress>? reportMatchup = null, int minimumPlayers = 2, int maximumPlayers = 6)
    {
        var scheduleRandom = new Random(seed);
        var wins = 0d;
        var squaredWins = 0d;
        var totalScore = 0L;
        var tableTotals = Enumerable.Range(minimumPlayers, maximumPlayers - minimumPlayers + 1).ToDictionary(count => count, _ => (Games: 0, Wins: 0d, Score: 0L));
        var opponentTotals = opponents.Select(strategy => strategy.Name).Distinct().ToDictionary(name => name, _ => (Games: 0, Wins: 0d, Score: 0L));

        for (var gameIndex = 0; gameIndex < games; gameIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (yieldToBrowser && gameIndex % 10 == 0)
            {
                await Task.Delay(1, cancellationToken);
            }

            var diceSeed = scheduleRandom.Next();
            var playerCount = minimumPlayers + gameIndex % (maximumPlayers - minimumPlayers + 1);
            var players = new Player[playerCount];
            players[0] = new Player("Candidate", candidate);

            for (var seat = 1; seat < playerCount; seat++)
            {
                players[seat] = new Player($"Opponent {seat}", opponents[scheduleRandom.Next(opponents.Count)]);
            }

            scheduleRandom.Shuffle(players);
            var diceRandom = new Random(diceSeed);
            var result = new Game(players, verbose: false, rollDie: () => diceRandom.Next(1, 7)).Start();
            var score = result.Rankings.Single(player => player.Name == "Candidate").Score;
            var victoryShare = score == result.WinnerScore ? 1d / result.Rankings.Count(player => player.Score == score) : 0;
            wins += victoryShare;
            squaredWins += victoryShare * victoryShare;
            totalScore += score;
            var tableTotal = tableTotals[playerCount];
            tableTotals[playerCount] = (tableTotal.Games + 1, tableTotal.Wins + victoryShare, tableTotal.Score + score);

            if (reportMatchup is not null)
            {
                var winnerCount = result.Rankings.Count(player => player.Score == result.WinnerScore);

                foreach (var opponent in players.Where(player => player.Name is not "Candidate"))
                {
                    var opponentScore = result.Rankings.Single(player => player.Name == opponent.Name).Score;
                    var totals = opponentTotals[opponent.Strategy.Name];
                    opponentTotals[opponent.Strategy.Name] = (totals.Games + 1, totals.Wins + (opponentScore == result.WinnerScore ? 1d / winnerCount : 0), totals.Score + opponentScore);
                }
            }

            if ((gameIndex + 1) % 10 == 0 || gameIndex == games - 1)
            {
                report?.Invoke(CreateEvaluation(gameIndex + 1, wins, squaredWins, totalScore));
                reportMatchup?.Invoke(new EvaluationProgress(CreateEvaluation(gameIndex + 1, wins, squaredWins, totalScore), opponentTotals.Select(entry => new StrategyComparison(entry.Key, entry.Value.Games, entry.Value.Games > 0 ? entry.Value.Wins / entry.Value.Games : 0, entry.Value.Games > 0 ? (double)entry.Value.Score / entry.Value.Games : 0)).ToArray())
                {
                    ByPlayerCount = tableTotals.Select(entry => new PlayerCountEvaluation(entry.Key, entry.Value.Games, entry.Value.Games > 0 ? entry.Value.Wins / entry.Value.Games : 0, entry.Value.Games > 0 ? (double)entry.Value.Score / entry.Value.Games : 0)).ToArray()
                });
            }
        }

        return CreateEvaluation(games, wins, squaredWins, totalScore);
    }

    private static StrategyEvaluation CreateEvaluation(int games, double wins, double squaredWins, long totalScore)
    {
        var mean = wins / games;
        var standardError = games > 1 ? Math.Sqrt(Math.Max(0, squaredWins - games * mean * mean) / (games - 1) / games) : 0;

        return new StrategyEvaluation(games, mean, standardError, (double)totalScore / games);
    }
}

