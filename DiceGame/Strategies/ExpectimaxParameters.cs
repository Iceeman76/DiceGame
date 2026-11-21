using System;

namespace DiceGame.Strategies;

public sealed record ExpectimaxParameters(double RiskAdjustment = 0, double MatchWinWeight = 0, int ExpectedOpponentTurnPoints = 200, double LateGameWeight = 0, double RemainingPlayersWeight = 0, double PassedDicePenalty = 0)
{
    public bool IsBaseline => RiskAdjustment == 0 && MatchWinWeight == 0 && PassedDicePenalty == 0;

    internal double MatchWeight(GameState state) => MatchWinWeight * (1 + LateGameWeight / (state.RoundsLeft + 1)) * (1 + RemainingPlayersWeight * state.PlayersRemainingInRound / Math.Max(1d, state.PlayerCount - 1));
}
