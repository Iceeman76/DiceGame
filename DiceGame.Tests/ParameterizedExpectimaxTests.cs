using DiceGame.Strategies;
using DiceGame.Training;
using NUnit.Framework;

namespace DiceGame.Tests;

[TestFixture]
public sealed class ParameterizedExpectimaxTests
{
    [Test]
    public void DefaultGenesReproduceBuiltInDecisionsAndSeededMatches()
    {
        var baseline = new ExpectimaxStrategy();
        var evolved = StrategyGenes.Expectimax.CreateStrategy();
        var hand = new Dice(0) { 2, 2, 5, 5, 3 };

        foreach (var round in new[] { 1, 9, 10 })
        {
            for (var diceCount = 1; diceCount <= 5; diceCount++)
            {
                for (var score = 0; score <= 2000; score += 50)
                {
                    var state = CreateState(score, diceCount) with { CurrentRound = round };
                    Assert.That(evolved.ShouldStop(state), Is.EqualTo(baseline.ShouldStop(state)));
                    Assert.That(evolved.ShouldTakePassedDice(state), Is.EqualTo(baseline.ShouldTakePassedDice(state)));
                    Assert.That(evolved.SelectDiceToKeep(hand, state), Is.EqualTo(baseline.SelectDiceToKeep(hand, state)));
                }
            }
        }

        var expected = StrategyEvaluator.Evaluate(baseline, GeneticOptimizer.Baselines, 30, 31);
        var actual = StrategyEvaluator.Evaluate(evolved, GeneticOptimizer.Baselines, 30, 31);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void NonDefaultWeightsChangeDecisionsAndKeepExactFinalTurnBehavior()
    {
        var baseline = new ExpectimaxStrategy();
        var evolved = new ParameterizedExpectimaxStrategy(new ExpectimaxParameters(RiskAdjustment: 100, MatchWinWeight: 100, PassedDicePenalty: 40));
        var differences = 0;

        for (var diceCount = 1; diceCount <= 5; diceCount++)
        {
            for (var score = 250; score <= 1500; score += 50)
            {
                var state = CreateState(score, diceCount);

                if (evolved.ShouldStop(state).Equals(baseline.ShouldStop(state)) is false)
                {
                    differences++;
                }

                var finalState = state with { CurrentRound = 10 };
                Assert.That(evolved.ShouldStop(finalState), Is.EqualTo(baseline.ShouldStop(finalState)));
            }
        }

        Assert.That(differences, Is.GreaterThan(0));
    }

    [Test]
    public void BothGeneFamiliesRoundTripAndProducePlayableStrategies()
    {
        var random = new Random(37);

        foreach (var family in new[] { "Threshold", "Expectimax" })
        {
            var genes = StrategyGenes.CreateRandom(random, family);
            var restored = StrategyFile.FromJson(new StrategyFile("Custom", genes).ToJson(), "Imported");

            Assert.That(restored.Genes.Family, Is.EqualTo(family));
            Assert.That(restored.Genes.Values, Is.EqualTo(genes.Values));
            Assert.That(restored.Genes.IsValid(), Is.True);
            Assert.That(restored.Version, Is.EqualTo(family == "Expectimax" ? 2 : 1));
            Assert.That(StrategyEvaluator.Evaluate(restored.Genes.CreateStrategy(), GeneticOptimizer.Baselines, 1, 41).AverageScore, Is.GreaterThanOrEqualTo(0));
        }
    }

    [Test]
    public async Task TrainingRetainsTheExpectimaxBaselineAndReportsProgress()
    {
        var options = new TrainingOptions { StrategyFamily = "Expectimax", Population = 3, Generations = 2, Games = 2, ValidationGames = 3, MaximumPlayers = 2, VaryPlayerCount = false, Seed = 43 };
        var progress = new List<LiveTrainingProgress>();
        var result = await GeneticOptimizer.TrainAsync(options, reportLive: progress.Add);
        var baseline = StrategyEvaluator.Evaluate(new ExpectimaxStrategy(), GeneticOptimizer.Baselines, 3, options.Seed ^ 0x39A72D15, 2, 2);

        Assert.That(result.Genes.Family, Is.EqualTo("Expectimax"));
        Assert.That(result.Genes.IsValid(), Is.True);
        Assert.That(result.Validation.WinShare, Is.GreaterThanOrEqualTo(baseline.WinShare));
        Assert.That(progress[^1].CompletedGames, Is.EqualTo(progress[^1].TotalGames));
    }

    private static GameState CreateState(int score, int diceCount) => new()
    {
        RoundScore = score,
        HandDiceCount = diceCount,
        TableDiceCount = 5 - diceCount,
        RollsThisTurn = 1,
        CurrentRound = 1,
        TotalRounds = 10,
        IsLastPlayerInRound = true,
        MyTotalScore = 0,
        PlayerRank = 2,
        PlayerCount = 2,
        OpponentScores = [1000],
        LeaderScore = 1000,
        AverageOpponentScore = 1000,
        InheritedScore = 500,
        InheritedHandDiceCount = 3
    };
}
