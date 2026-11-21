namespace DiceGame.Strategies;

public sealed record HybridStrategy(ExpectimaxParameters ExpectimaxParameters, ThresholdStrategy ThresholdParameters, HybridParameters HybridParameters, string Name = "Evolved Hybrid") : IPlayerStrategy
{
    private ParameterizedExpectimaxStrategy expectimax = new(ExpectimaxParameters);
    private ParameterizedExpectimaxStrategy Expectimax
    {
        get
        {
            if (expectimax.Parameters.Equals(ExpectimaxParameters) is false)
            {
                expectimax = new ParameterizedExpectimaxStrategy(ExpectimaxParameters);
            }

            return expectimax;
        }
    }

    public string Description => "Combines exact Expectimax search with evolved threshold decisions, dice-keeping rules, inheritance gates, and roll-risk limits.";

    public bool ShouldStop(GameState state)
    {
        if (IsFinalTurn(state))
        {
            return Expectimax.ShouldStop(state);
        }

        if (state.RoundScore < InteractiveGame.MinimumBankScore || state.RollsThisTurn == 0)
        {
            return false;
        }

        if (state.RoundsLeft == 0 && state.ProjectedTotalScore < state.BestOpponentScore)
        {
            return false;
        }

        var shouldStop = HybridParameters.BankingMode switch
        {
            1 => ThresholdParameters.ShouldStop(state),
            2 => ThresholdParameters.ShouldStop(state) || Expectimax.ShouldStop(state),
            3 => ThresholdParameters.ShouldStop(state) && Expectimax.ShouldStop(state),
            _ => Expectimax.ShouldStop(state)
        };

        return shouldStop || ShouldAvoidRoll(state);
    }

    public bool ShouldTakePassedDice(GameState state)
    {
        if (IsFinalTurn(state))
        {
            return Expectimax.ShouldTakePassedDice(state);
        }

        var accept = HybridParameters.InheritanceMode switch
        {
            1 => ThresholdParameters.ShouldTakePassedDice(state),
            2 => ThresholdParameters.ShouldTakePassedDice(state) && Expectimax.ShouldTakePassedDice(state),
            3 => ThresholdParameters.ShouldTakePassedDice(state) || Expectimax.ShouldTakePassedDice(state),
            _ => Expectimax.ShouldTakePassedDice(state)
        };
        var inheritedState = state with { RoundScore = state.InheritedScore, HandDiceCount = state.InheritedHandDiceCount, RollsThisTurn = 0, TableDiceCount = 5 - state.InheritedHandDiceCount };

        return accept && ShouldAvoidRoll(inheritedState) is false;
    }

    public DiceSelection SelectDiceToKeep(Dice hand, GameState state) => IsFinalTurn(state) || HybridParameters.DiceSelectionMode == 0 ? Expectimax.SelectDiceToKeep(hand, state) : ThresholdParameters.SelectDiceToKeep(hand, state);

    private bool ShouldAvoidRoll(GameState state)
    {
        if (HybridParameters.MaxBustProbability >= 1 && double.IsNegativeInfinity(HybridParameters.MinimumExpectedNetGain))
        {
            return false;
        }

        var forecast = DiceRollStatistics.Forecast(this, state);

        return forecast.BustProbability > HybridParameters.MaxBustProbability || forecast.ExpectedNetGain < HybridParameters.MinimumExpectedNetGain;
    }

    private static bool IsFinalTurn(GameState state) => state is { RoundsLeft: 0, IsLastPlayerInRound: true };
}
