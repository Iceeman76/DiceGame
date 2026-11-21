using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceGame.Strategies;

public sealed record ParameterizedExpectimaxStrategy(ExpectimaxParameters Parameters, string Name = "Evolved Expectimax") : IPlayerStrategy
{
    private static readonly ExpectimaxStrategy baseline = new();
    private readonly Dictionary<PolicyKey, ExpectimaxStrategy.TurnPolicy> policies = [];
    private readonly Queue<PolicyKey> policyOrder = [];

    public string Description => "Exact Expectimax roll search with evolved risk, match-outcome estimates, playing order, and passed-dice values.";

    public bool ShouldStop(GameState state)
    {
        if (UseBaseline(state))
        {
            return baseline.ShouldStop(state);
        }

        if (state.RoundScore < InteractiveGame.MinimumBankScore || state.RollsThisTurn == 0)
        {
            return false;
        }

        if (state.RoundsLeft == 0 && state.ProjectedTotalScore < state.BestOpponentScore)
        {
            return false;
        }

        var policy = GetPolicy(state);

        return policy.BankValue(state.RoundScore, state.HandDiceCount) >= policy.RollValue(state.RoundScore, state.HandDiceCount);
    }

    public bool ShouldTakePassedDice(GameState state)
    {
        if (UseBaseline(state))
        {
            return baseline.ShouldTakePassedDice(state);
        }

        var policy = GetPolicy(state);

        return policy.RollValue(state.InheritedScore, state.InheritedHandDiceCount) > policy.RollValue(0, 5);
    }

    public DiceSelection SelectDiceToKeep(Dice hand, GameState state)
    {
        if (UseBaseline(state))
        {
            return baseline.SelectDiceToKeep(hand, state);
        }

        var policy = GetPolicy(state);
        var bestSelection = new DiceSelection(0, 0, 0);
        var bestValue = double.NegativeInfinity;

        foreach (var selection in ExpectimaxStrategy.GetSelections(hand.Count(face => face == 1), hand.Count(face => face == 2), hand.Count(face => face == 5)))
        {
            var remainingDice = hand.Count - selection.Total;
            var value = policy.Value(state.RoundScore + selection.Score, remainingDice == 0 ? 5 : remainingDice);

            if (value > bestValue || value == bestValue && selection.Score > bestSelection.Score)
            {
                bestSelection = selection;
                bestValue = value;
            }
        }

        return bestSelection;
    }

    private bool UseBaseline(GameState state) => Parameters.IsBaseline || state is { RoundsLeft: 0, IsLastPlayerInRound: true };

    private ExpectimaxStrategy.TurnPolicy GetPolicy(GameState state)
    {
        var key = new PolicyKey(Parameters, state.RoundsLeft, state.PlayerCount, state.PlayersRemainingInRound, state.MyTotalScore, state.BestOpponentScore, string.Join(',', state.OpponentScores));

        if (policies.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var matchWeight = Parameters.MatchWeight(state);
        var maximumAdjustment = Math.Abs(Parameters.RiskAdjustment) + matchWeight + Parameters.PassedDicePenalty;
        var opponents = state.OpponentScores.IsEmpty ? Enumerable.Repeat(state.BestOpponentScore, state.PlayerCount - 1).ToArray() : state.OpponentScores.ToArray();
        var uncertainty = 200 + 100 * Math.Sqrt(state.RoundsLeft);
        var myScore = state.MyTotalScore;
        var playersRemaining = state.PlayersRemainingInRound;
        var expectedOpponentPoints = Parameters.ExpectedOpponentTurnPoints;
        var riskAdjustment = Parameters.RiskAdjustment;
        var passedDicePenalty = Parameters.PassedDicePenalty;
        var matchValues = new Dictionary<int, double>();
        var utility = (int score, int diceCount) =>
        {
            if (matchValues.TryGetValue(score, out var matchValue) is false)
            {
                var winChance = 1d;

                for (var index = 0; index < opponents.Length; index++)
                {
                    var opponentFuture = opponents[index] + (index >= opponents.Length - playersRemaining ? expectedOpponentPoints : 0);
                    var margin = myScore + score - opponentFuture;
                    winChance *= 1 / (1 + Math.Exp(Math.Clamp(-margin / uncertainty, -40, 40)));
                }

                matchValue = matchWeight * winChance;
                matchValues.Add(score, matchValue);
            }

            var riskValue = riskAdjustment * score / (score + 500d);
            var passedValue = passedDicePenalty * diceCount / 5 * score / (score + 250d);

            return score + riskValue + matchValue - passedValue;
        };
        var policy = ExpectimaxStrategy.CreatePolicy(maximumAdjustment, utility, utility(0, 5));

        if (policies.Count >= 16)
        {
            policies.Remove(policyOrder.Dequeue());
        }

        policies.Add(key, policy);
        policyOrder.Enqueue(key);

        return policy;
    }

    private sealed record PolicyKey(ExpectimaxParameters Parameters, int RoundsLeft, int PlayerCount, int PlayersRemaining, int MyScore, int BestOpponentScore, string OpponentScores);
}
