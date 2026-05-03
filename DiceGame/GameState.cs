using System;

namespace DiceGame;

public record GameState
{
    // Current turn
    public required int RoundScore { get; init; }
    public required int HandDiceCount { get; init; }
    public required int TableDiceCount { get; init; }
    public required int RollsThisTurn { get; init; }

    // Game progress
    public required int CurrentRound { get; init; }
    public required int TotalRounds { get; init; }
    public int RoundsLeft => TotalRounds - CurrentRound;
    public required bool IsLastPlayerInRound { get; init; }

    // My standing
    public required int MyTotalScore { get; init; }
    public required int PlayerRank { get; init; }
    public required int PlayerCount { get; init; }

    // Competition
    public required int LeaderScore { get; init; }
    public required int AverageOpponentScore { get; init; }

    // Derived
    public int PointsBehindLeader => Math.Max(0, LeaderScore - MyTotalScore);
    public bool IsLeading => PlayerRank == 1;

    // Inheritance (only relevant for ShouldTakePassedDice)
    public int InheritedScore { get; init; }
    public int InheritedHandDiceCount { get; init; }
}
