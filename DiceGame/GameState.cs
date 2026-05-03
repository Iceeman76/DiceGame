using System;

namespace DiceGame;

public sealed record GameState
{
    /// <summary>Points accumulated in the current turn so far.</summary>
    public required int RoundScore { get; init; }

    /// <summary>Number of dice currently in hand, about to be rolled.</summary>
    public required int HandDiceCount { get; init; }

    /// <summary>Number of dice saved on the table this turn.</summary>
    public required int TableDiceCount { get; init; }

    /// <summary>Number of rolls completed this turn. Zero means no roll has been made yet, e.g. an inherited position.</summary>
    public required int RollsThisTurn { get; init; }

    /// <summary>The round currently being played (1-based).</summary>
    public required int CurrentRound { get; init; }

    /// <summary>Total number of rounds in the game.</summary>
    public required int TotalRounds { get; init; }

    /// <summary>Rounds remaining after the current one.</summary>
    public int RoundsLeft => TotalRounds - CurrentRound;

    /// <summary>True if this player is the last to act in the current round.</summary>
    public required bool IsLastPlayerInRound { get; init; }

    /// <summary>The player's accumulated total score before this turn.</summary>
    public required int MyTotalScore { get; init; }

    /// <summary>Rank among all players (1 = leading). Players with equal scores share the same rank.</summary>
    public required int PlayerRank { get; init; }

    /// <summary>Total number of players in the game.</summary>
    public required int PlayerCount { get; init; }

    /// <summary>Highest total score among all players.</summary>
    public required int LeaderScore { get; init; }

    /// <summary>Average total score of all opponents.</summary>
    public required int AverageOpponentScore { get; init; }

    /// <summary>Points behind the leader. Zero if this player is leading.</summary>
    public int PointsBehindLeader => Math.Max(0, LeaderScore - MyTotalScore);

    /// <summary>True if this player is currently leading.</summary>
    public bool IsLeading => PlayerRank == 1;

    /// <summary>Score inherited from the previous player. Zero if this is not an inherited position.</summary>
    public int InheritedScore { get; init; }

    /// <summary>Number of dice remaining to roll if the player takes the inherited dice.</summary>
    public int InheritedHandDiceCount { get; init; }
}
