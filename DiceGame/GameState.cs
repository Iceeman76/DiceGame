using System;

namespace DiceGame;

public sealed record GameState
{
    public required int RoundScore { get; init; }
    public required int HandDiceCount { get; init; }
    public required int TableDiceCount { get; init; }
    public required int RollsThisTurn { get; init; }

    public required int CurrentRound { get; init; }
    public required int TotalRounds { get; init; }
    public int RoundsLeft => TotalRounds - CurrentRound;
    public required bool IsLastPlayerInRound { get; init; }

    public required int MyTotalScore { get; init; }
    public required int PlayerRank { get; init; }
    public required int PlayerCount { get; init; }

    public required int LeaderScore { get; init; }
    public required int AverageOpponentScore { get; init; }

    public int PointsBehindLeader => Math.Max(0, LeaderScore - MyTotalScore);
    public bool IsLeading => PlayerRank == 1;

    public int InheritedScore { get; init; }
    public int InheritedHandDiceCount { get; init; }
}
