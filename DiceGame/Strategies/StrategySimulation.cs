using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceGame.Strategies;

internal sealed class StrategySimulation(ThresholdStrategy strategy, GameState state)
{
    internal bool ShouldBank() => IsBetter(Evaluate(state, game => game.Bank()), Evaluate(state, game => game.Roll()));

    internal bool ShouldTakeInheritedDice()
    {
        var inheritedState = state with { RoundScore = state.InheritedScore, HandDiceCount = state.InheritedHandDiceCount, TableDiceCount = 5 - state.InheritedHandDiceCount, RollsThisTurn = 0 };
        var freshState = state with { RoundScore = 0, HandDiceCount = 5, TableDiceCount = 0, RollsThisTurn = 0 };

        return IsBetter(Evaluate(inheritedState, game => game.Roll()), Evaluate(freshState, game => game.Roll()));
    }

    internal DiceSelection SelectDiceToKeep(Dice hand)
    {
        var bestSelection = strategy.SelectDiceByLimits(hand, state);
        var bestEvaluation = EvaluateSelection(bestSelection);

        foreach (var selection in GetSelections(hand))
        {
            var evaluation = EvaluateSelection(selection);

            if (IsBetter(evaluation, bestEvaluation))
            {
                bestSelection = selection;
                bestEvaluation = evaluation;
            }
        }

        return bestSelection;
    }

    private Evaluation EvaluateSelection(DiceSelection selection)
    {
        var remainingDice = state.HandDiceCount - selection.Total;
        var selectedState = state with { RoundScore = state.RoundScore + selection.Score, HandDiceCount = remainingDice == 0 ? 5 : remainingDice, TableDiceCount = remainingDice == 0 ? 0 : state.TableDiceCount + selection.Total };

        return Evaluate(selectedState, game => game.PlayComputerStep());
    }

    private Evaluation Evaluate(GameState initialState, Action<InteractiveGame> firstAction)
    {
        var winProbability = 0d;
        var totalScore = 0d;

        for (var simulationIndex = 0; simulationIndex < strategy.SimulationBudget; simulationIndex++)
        {
            var random = new Random(7919 * simulationIndex + state.CurrentRound * 1009 + state.PlayerCount * 53);
            var game = InteractiveGame.CreateSimulation(initialState, strategy, () => random.Next(1, 7));
            var simulatedPlayer = game.CurrentPlayer;
            firstAction(game);

            while (game.Phase is not MatchPhase.Finished)
            {
                game.PlayComputerStep();
            }

            if (game.Winners.Contains(simulatedPlayer))
            {
                winProbability += 1d / game.Winners.Count;
            }

            totalScore += simulatedPlayer.TotalScore.Value;
        }

        return new Evaluation(winProbability / strategy.SimulationBudget, totalScore / strategy.SimulationBudget);
    }

    private IEnumerable<DiceSelection> GetSelections(Dice hand)
    {
        var availableOnes = strategy.SaveTripleOnes && hand.Count(face => face == 1) >= 3 ? new[] { 0, 3 } : [0];
        var maximumTwos = Math.Min(strategy.MaxTwosToKeepPerRoll, hand.Count(face => face == 2));
        var maximumFives = Math.Min(strategy.MaxFivesToKeepPerRoll, hand.Count(face => face == 5));

        foreach (var ones in availableOnes)
        {
            for (var twos = 0; twos <= maximumTwos; twos++)
            {
                for (var fives = 0; fives <= maximumFives; fives++)
                {
                    var selection = new DiceSelection(ones, twos, fives);

                    if (selection.Total > 0)
                    {
                        yield return selection;
                    }
                }
            }
        }
    }

    private static bool IsBetter(Evaluation candidate, Evaluation baseline) => candidate.WinProbability > baseline.WinProbability || candidate.WinProbability == baseline.WinProbability && candidate.ExpectedTotalScore >= baseline.ExpectedTotalScore;

    private sealed record Evaluation(double WinProbability, double ExpectedTotalScore);
}
