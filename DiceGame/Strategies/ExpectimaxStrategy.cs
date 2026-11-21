using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace DiceGame.Strategies;

public sealed record ExpectimaxStrategy : IPlayerStrategy
{
    private static readonly Outcome[][] outcomes = [.. Enumerable.Range(0, 6).Select(BuildOutcomes)];
    private static readonly TurnPolicy scoringPolicy = new(FindBankingBoundary(), score => score);
    private static readonly ConcurrentDictionary<(int Gap, int TiedOpponents), TurnPolicy> finalPolicies = new();

    public string Name => "Expectimax";
    public string Description => "Evaluates every roll and scoring selection to maximize expected turn points, then victory share on the final turn.";

    public bool ShouldStop(GameState state)
    {
        var policy = GetPolicy(state);

        if (state.RoundScore < InteractiveGame.MinimumBankScore || state.RollsThisTurn == 0)
        {
            return false;
        }

        if (state is { RoundsLeft: 0, IsLastPlayerInRound: true } && state.ProjectedTotalScore > state.BestOpponentScore)
        {
            return true;
        }

        if (state.RoundsLeft == 0 && state.ProjectedTotalScore < state.BestOpponentScore)
        {
            return false;
        }

        return policy.BankValue(state.RoundScore) >= policy.RollValue(state.RoundScore, state.HandDiceCount);
    }

    public bool ShouldTakePassedDice(GameState state)
    {
        var policy = GetPolicy(state);

        return policy.RollValue(state.InheritedScore, state.InheritedHandDiceCount) > policy.RollValue(0, 5);
    }

    public DiceSelection SelectDiceToKeep(Dice hand, GameState state)
    {
        var policy = GetPolicy(state);
        var bestSelection = new DiceSelection(0, 0, 0);
        var bestValue = double.NegativeInfinity;

        foreach (var selection in GetSelections(hand.Count(face => face == 1), hand.Count(face => face == 2), hand.Count(face => face == 5)))
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

    private static TurnPolicy GetPolicy(GameState state)
    {
        if (state.RoundsLeft > 0 || state.IsLastPlayerInRound is false)
        {
            return scoringPolicy;
        }

        var gap = state.BestOpponentScore - state.MyTotalScore;
        var tiedOpponents = state.OpponentScores.IsEmpty ? state.PlayerCount - 1 : state.OpponentScores.Count(score => score == state.BestOpponentScore);

        return finalPolicies.GetOrAdd((gap, tiedOpponents), key => new TurnPolicy(Math.Max(InteractiveGame.MinimumBankScore, key.Gap + DiceSelection.TwoScore), score => score > key.Gap ? 1 : score == key.Gap ? 1d / (key.TiedOpponents + 1) : 0));
    }

    internal static TurnPolicy CreatePolicy(double maximumAdjustment, Func<int, int, double> utility, double bustValue) => new(FindBankingBoundary(maximumAdjustment), score => score, utility, bustValue);

    private static int FindBankingBoundary(double maximumAdjustment = 0)
    {
        var maximumExpectedScore = outcomes.Skip(1).Max(rolls => rolls.Sum(outcome => outcome.Probability * outcome.Selections.Select(selection => selection.Score).DefaultIfEmpty().Max()));
        var minimumBustProbability = outcomes.Skip(1).Min(rolls => rolls.Where(outcome => outcome.Selections.Length == 0).Sum(outcome => outcome.Probability));

        return (int)Math.Ceiling((maximumExpectedScore + 2 * maximumAdjustment) / minimumBustProbability / DiceSelection.TwoScore) * DiceSelection.TwoScore;
    }

    private static Outcome[] BuildOutcomes(int diceCount)
    {
        var combinations = (int)Math.Pow(6, diceCount);
        var frequencies = new Dictionary<(int Ones, int Twos, int Fives), int>();

        for (var combination = 0; combination < combinations; combination++)
        {
            var remaining = combination;
            var ones = 0;
            var twos = 0;
            var fives = 0;

            for (var dieIndex = 0; dieIndex < diceCount; dieIndex++)
            {
                var face = remaining % 6 + 1;
                remaining /= 6;
                ones += face == 1 ? 1 : 0;
                twos += face == 2 ? 1 : 0;
                fives += face == 5 ? 1 : 0;
            }

            var faces = (ones, twos, fives);
            frequencies[faces] = frequencies.GetValueOrDefault(faces) + 1;
        }

        return frequencies.Select(frequency => new Outcome(GetSelections(frequency.Key.Ones, frequency.Key.Twos, frequency.Key.Fives).ToArray(), (double)frequency.Value / combinations)).ToArray();
    }

    internal static IEnumerable<DiceSelection> GetSelections(int availableOnes, int availableTwos, int availableFives)
    {
        for (var ones = 0; ones <= (availableOnes >= 3 ? 3 : 0); ones += 3)
        {
            for (var twos = 0; twos <= availableTwos; twos++)
            {
                for (var fives = 0; fives <= availableFives; fives++)
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

    private sealed record Outcome(DiceSelection[] Selections, double Probability);

    internal sealed class TurnPolicy
    {
        private readonly Func<int, double> utility;
        private readonly double[,] values;
        private readonly double[,] rollValues;
        private readonly Func<int, int, double>? positionUtility;
        private readonly double bustValue;

        public TurnPolicy(int maximumScore, Func<int, double> utility, Func<int, int, double>? positionUtility = null, double? bustValue = null)
        {
            this.utility = utility;
            this.positionUtility = positionUtility;
            this.bustValue = bustValue ?? utility(0);
            var scoreSteps = (maximumScore + DiceSelection.TwoScore - 1) / DiceSelection.TwoScore;
            values = new double[scoreSteps + 1, 6];
            rollValues = new double[scoreSteps + 1, 6];

            for (var scoreIndex = scoreSteps; scoreIndex >= 0; scoreIndex--)
            {
                var score = scoreIndex * DiceSelection.TwoScore;

                for (var diceCount = 1; diceCount <= 5; diceCount++)
                {
                    var rollValue = EvaluateRoll(score, diceCount);
                    rollValues[scoreIndex, diceCount] = rollValue;
                    values[scoreIndex, diceCount] = score >= InteractiveGame.MinimumBankScore ? Math.Max(BankValue(score, diceCount), rollValue) : rollValue;
                }
            }
        }

        public double BankValue(int score) => utility(score);
        public double BankValue(int score, int diceCount) => positionUtility?.Invoke(score, diceCount) ?? utility(score);
        public double Value(int score, int diceCount) => score / DiceSelection.TwoScore < values.GetLength(0) ? values[score / DiceSelection.TwoScore, diceCount] : BankValue(score, diceCount);
        public double RollValue(int score, int diceCount) => score / DiceSelection.TwoScore < rollValues.GetLength(0) ? rollValues[score / DiceSelection.TwoScore, diceCount] : EvaluateRoll(score, diceCount);

        private double EvaluateRoll(int score, int diceCount)
        {
            var expectedValue = 0d;

            foreach (var outcome in outcomes[diceCount])
            {
                var bestValue = outcome.Selections.Length == 0 ? bustValue : double.NegativeInfinity;

                foreach (var selection in outcome.Selections)
                {
                    var remainingDice = diceCount - selection.Total;
                    bestValue = Math.Max(bestValue, Value(score + selection.Score, remainingDice == 0 ? 5 : remainingDice));
                }

                expectedValue += outcome.Probability * bestValue;
            }

            return expectedValue;
        }
    }
}
