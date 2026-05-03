using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceGame;

public sealed class Game
{
    private readonly List<Player> _players;
    private readonly bool _verbose;
    private const int TotalRounds = 10;

    private int _currentRound;
    private int _rollsThisTurn;

    private Dice? _passedTable;
    private int _passedScore;

    public Game(IEnumerable<Player> players, bool verbose = true, bool randomizeOrder = false)
    {
        _players = players.ToList();
        _verbose = verbose;

        if (randomizeOrder)
        {
            Randomizer.Shuffle(_players);
        }
    }

    public GameResult Start()
    {
        for (_currentRound = 1; _currentRound <= TotalRounds; _currentRound++)
        {
            Log($"\n========= ROUND {_currentRound} =========");
            for (var i = 0; i < _players.Count; i++)
            {
                PlayTurn(_players[i], isLastInRound: i == _players.Count - 1);
            }
        }

        var rankings = _players
            .OrderByDescending(p => p.TotalScore.Value)
            .Select(p => new PlayerResult(p.Name, p.TotalScore.Value))
            .ToList();

        if (_verbose)
        {
            Log("\n========= GAME OVER =========");
            for (var i = 0; i < rankings.Count; i++)
            {
                Log($"{i + 1}. {rankings[i].Name}: {rankings[i].Score}p");
            }

            Log($"\nVinnare: {rankings[0].Name}!");
        }

        return new GameResult(rankings);
    }

    private void PlayTurn(Player player, bool isLastInRound)
    {
        Log($"\n--- {player.Name}'s turn ---");
        _rollsThisTurn = 0;

        var (table, roundScore) = InitializeTurn(player, isLastInRound);
        var hand = new Dice(5 - table.Count);

        while (true)
        {
            if (hand.Count == 0)
            {
                Log("All dice kept — rolling fresh set.");
                hand = new Dice(5);
                table = new Dice(0);
            }

            if (roundScore >= 250)
            {
                var state = BuildGameState(player, roundScore, hand, table, isLastInRound);
                if (player.Strategy.ShouldStop(state))
                {
                    player.TotalScore.Add(roundScore);
                    _passedTable = table;
                    _passedScore = roundScore;
                    Log($"{player.Name} stops with {roundScore}p (total: {player.TotalScore.Value}p)");
                    return;
                }
            }

            _rollsThisTurn++;
            hand.Roll();
            Log($"Rolled: [{string.Join(", ", hand)}]  Table: [{string.Join(", ", table)}]  Score: {roundScore}p");

            if (hand.Score() == 0)
            {
                Log($"{player.Name} busted! Round score lost.");
                return;
            }

            var selectionState = BuildGameState(player, roundScore, hand, table, isLastInRound);
            var selection = player.Strategy.SelectDiceToKeep(hand, selectionState);

            if (selection.Total == 0)
            {
                Log($"{player.Name} busted! Round score lost.");
                return;
            }

            roundScore += KeepScoringDice(hand, table, selection);

            Log($"Kept:   [{string.Join(", ", table)}]  Round score: {roundScore}p");
        }
    }

    private (Dice table, int score) InitializeTurn(Player player, bool isLastInRound)
    {
        if (_passedTable is null)
        {
            return (new Dice(0), 0);
        }

        var passed = _passedTable;
        var passedScore = _passedScore;
        _passedTable = null;
        _passedScore = 0;

        var state = BuildGameState(player, 0, new Dice(0), passed, isLastInRound,
            inheritedScore: passedScore, inheritedTableCount: passed.Count);

        if (player.Strategy.ShouldTakePassedDice(state))
        {
            Log($"{player.Name} takes passed dice [{string.Join(", ", passed)}] with {passedScore}p");
            return (passed, passedScore);
        }

        Log($"{player.Name} starts fresh (skips {passedScore}p)");
        return (new Dice(0), 0);
    }

    private GameState BuildGameState(
        Player currentPlayer, int roundScore, Dice hand, Dice table, bool isLastInRound,
        int inheritedScore = 0, int inheritedTableCount = 0)
    {
        var myIdx = _players.IndexOf(currentPlayer);
        var myScore = currentPlayer.TotalScore.Value;
        var allScores = _players.Select(p => p.TotalScore.Value).ToList();
        var leaderScore = allScores.Max();
        var opponentScores = allScores.Where((_, i) => i != myIdx).ToList();

        return new GameState
        {
            RoundScore = roundScore,
            HandDiceCount = hand.Count,
            TableDiceCount = table.Count,
            RollsThisTurn = _rollsThisTurn,
            CurrentRound = _currentRound,
            TotalRounds = TotalRounds,
            IsLastPlayerInRound = isLastInRound,
            MyTotalScore = myScore,
            PlayerRank = allScores.Count(s => s > myScore) + 1,
            PlayerCount = _players.Count,
            LeaderScore = leaderScore,
            AverageOpponentScore = opponentScores.Count > 0 ? (int)opponentScores.Average() : 0,
            InheritedScore = inheritedScore,
            InheritedHandDiceCount = 5 - inheritedTableCount
        };
    }

    private static int KeepScoringDice(Dice hand, Dice table, DiceSelection selection)
    {
        var score = 0;

        if (selection.Ones > 0)
        {
            score += 1000;

            for (var i = 0; i < selection.Ones; i++)
            {
                hand.Remove(1);
                table.Add(1);
            }
        }

        score += selection.Twos * 50;

        for (var i = 0; i < selection.Twos; i++)
        {
            hand.Remove(2);
            table.Add(2);
        }

        score += selection.Fives * 100;

        for (var i = 0; i < selection.Fives; i++)
        {
            hand.Remove(5);
            table.Add(5);
        }

        return score;
    }

    private void Log(string message)
    {
        if (_verbose)
        {
            Console.WriteLine(message);
        }
    }
}
