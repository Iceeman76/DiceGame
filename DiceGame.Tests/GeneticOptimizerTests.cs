using System.Text.Json;
using DiceGame.Strategies;
using NUnit.Framework;
using DiceGame.Training;

namespace DiceGame.Tests;

[TestFixture]
public sealed class GeneticOptimizerTests
{
    [Test]
    public void ZeroMutationRateOnlyCombinesParentGenes()
    {
        var random = new Random(81);
        var firstParent = StrategyGenes.Adaptive;
        var secondParent = StrategyGenes.CreateRandom(random);

        for (var offspringIndex = 0; offspringIndex < 100; offspringIndex++)
        {
            var offspring = firstParent.Breed(secondParent, random, 0);

            for (var geneIndex = 0; geneIndex < offspring.Values.Length; geneIndex++)
            {
                Assert.That(offspring.Values[geneIndex], Is.EqualTo(firstParent.Values[geneIndex]).Or.EqualTo(secondParent.Values[geneIndex]));
            }
        }
    }

    [Test]
    public void FullMutationRateCanIntroduceValuesOutsideTheParentsWhileRespectingBounds()
    {
        var random = new Random(83);
        var parent = StrategyGenes.Adaptive;
        var offspring = Enumerable.Range(0, 100).Select(_ => parent.Breed(parent, random, 1)).ToArray();

        Assert.That(offspring.All(genes => genes.IsValid()), Is.True);
        Assert.That(offspring.Any(genes => genes.Values.SequenceEqual(parent.Values) is false), Is.True);
    }

    [Test]
    public void DefaultMutationRatePreservesTheOriginalBehavior()
    {
        var firstRandom = new Random(85);
        var secondRandom = new Random(85);
        var parent = StrategyGenes.Adaptive;
        var implicitRate = parent.Breed(parent, firstRandom);
        var explicitRate = parent.Breed(parent, secondRandom, new TrainingOptions().MutationRate);

        Assert.That(explicitRate.Values, Is.EqualTo(implicitRate.Values));
        Assert.That(new TrainingOptions().MutationRate, Is.EqualTo(0.2));
    }

    [Test]
    public void AdaptiveGenesReproduceTheExistingBaseline()
    {
        var random = new Random(72);
        var opponents = GeneticOptimizer.Baselines;
        var expected = StrategyEvaluator.Evaluate(new AdaptiveStrategy(), opponents, 30, random.Next());
        random = new Random(72);
        var actual = StrategyEvaluator.Evaluate(StrategyGenes.Adaptive.CreateStrategy(), opponents, 30, random.Next());

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void TrainingIsReproducibleAndRetainsAtLeastTheBaselineValidationScore()
    {
        var options = new TrainingOptions { Population = 4, Generations = 2, Games = 10, ValidationGames = 15, TestGames = 10, Seed = 51 };
        var progress = new List<TrainingProgress>();
        var first = GeneticOptimizer.Train(options, progress.Add);
        var second = GeneticOptimizer.Train(options);
        var baseline = StrategyEvaluator.Evaluate(new AdaptiveStrategy(), GeneticOptimizer.Baselines, options.ValidationGames, options.Seed ^ 0x39A72D15);

        Assert.That(second.Genes.Values, Is.EqualTo(first.Genes.Values));
        Assert.That(second.Validation, Is.EqualTo(first.Validation));
        Assert.That(first.Validation.WinShare, Is.GreaterThanOrEqualTo(baseline.WinShare));
        Assert.That(progress.Select(item => item.Generation), Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public void SavedGenesPreserveDecisionsAfterJsonRoundTrip()
    {
        var genes = StrategyGenes.CreateRandom(new Random(31));
        var restored = JsonSerializer.Deserialize<StrategyGenes>(JsonSerializer.Serialize(genes))!;
        var opponents = GeneticOptimizer.Baselines;
        var expected = StrategyEvaluator.Evaluate(genes.CreateStrategy(), opponents, 25, 91);
        var actual = StrategyEvaluator.Evaluate(restored.CreateStrategy(), opponents, 25, 91);

        Assert.That(restored.Values, Is.EqualTo(genes.Values));
        Assert.That(actual, Is.EqualTo(expected));
        Assert.That(actual.WinShare, Is.InRange(0, 1));
        Assert.That(actual.StandardError, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void BreedingProducesStrategiesThatCompleteGames()
    {
        var random = new Random(17);
        var genes = StrategyGenes.Adaptive;

        for (var generation = 0; generation < 100; generation++)
        {
            genes = genes.Breed(StrategyGenes.CreateRandom(random), random);
            var strategy = (ThresholdStrategy)genes.CreateStrategy();
            var evaluation = StrategyEvaluator.Evaluate(strategy, GeneticOptimizer.Baselines, 1, random.Next());

            Assert.That(strategy.MaxTwosToKeepPerRoll, Is.InRange(1, 5));
            Assert.That(strategy.MaxFivesToKeepPerRoll, Is.InRange(1, 5));
            Assert.That(evaluation.AverageScore, Is.GreaterThanOrEqualTo(0));
        }
    }
}
