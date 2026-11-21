using DiceGame.Strategies;
using DiceGame.Training;
using NUnit.Framework;

namespace DiceGame.Tests;

[TestFixture]
public sealed class HybridStrategyTests
{
    [Test]
    public void InvalidHybridModesAreRejectedWhenLoadingJson()
    {
        var genes = StrategyGenes.Hybrid with { Values = StrategyGenes.Hybrid.Values.SetItem(20, 4) };
        var json = new StrategyFile("Invalid hybrid", genes).ToJson();

        Assert.That(() => StrategyFile.FromJson(json, "Imported"), Throws.TypeOf<System.Text.Json.JsonException>());
    }

    [Test]
    public void NeutralHybridReproducesExpectimaxDecisionsAndMatches()
    {
        var hybrid = StrategyGenes.Hybrid.CreateStrategy();
        var baseline = new ExpectimaxStrategy();
        var hand = new Dice(0) { 2, 2, 5, 5, 3 };

        foreach (var round in new[] { 1, 9, 10 })
        {
            for (var diceCount = 1; diceCount <= 5; diceCount++)
            {
                for (var score = 0; score <= 1500; score += 50)
                {
                    var state = CreateState(score, diceCount) with { CurrentRound = round };
                    Assert.That(hybrid.ShouldStop(state), Is.EqualTo(baseline.ShouldStop(state)));
                    Assert.That(hybrid.ShouldTakePassedDice(state), Is.EqualTo(baseline.ShouldTakePassedDice(state)));
                    Assert.That(hybrid.SelectDiceToKeep(hand, state), Is.EqualTo(baseline.SelectDiceToKeep(hand, state)));
                }
            }
        }

        Assert.That(StrategyEvaluator.Evaluate(hybrid, GeneticOptimizer.Baselines, 20, 61), Is.EqualTo(StrategyEvaluator.Evaluate(baseline, GeneticOptimizer.Baselines, 20, 61)));
    }

    [Test]
    public void ThresholdsCanBankEarlierOrContinueAfterExpectimaxWouldBank()
    {
        var early = new HybridStrategy(new ExpectimaxParameters(), new PassiveStrategy(), new HybridParameters(BankingMode: 2));
        var late = new HybridStrategy(new ExpectimaxParameters(), new BoldStrategy(), new HybridParameters(BankingMode: 3));
        var baseline = new ExpectimaxStrategy();

        Assert.That(baseline.ShouldStop(CreateState(300, 5)), Is.False);
        Assert.That(early.ShouldStop(CreateState(300, 5)), Is.True);
        Assert.That(baseline.ShouldStop(CreateState(300, 1)), Is.True);
        Assert.That(late.ShouldStop(CreateState(300, 1)), Is.False);
    }

    [Test]
    public void DiceSelectionAndInheritanceSourcesCanBeMixedIndependently()
    {
        var hybrid = new HybridStrategy(new ExpectimaxParameters(), new BoldStrategy(), new HybridParameters(DiceSelectionMode: 1, InheritanceMode: 3));
        var hand = new Dice(0) { 2, 2, 5, 3, 4 };
        var state = CreateState(0, 5) with { InheritedScore = 250, InheritedHandDiceCount = 1 };
        var baseline = new ExpectimaxStrategy();

        Assert.That(hybrid.SelectDiceToKeep(hand, state).Total, Is.EqualTo(3));
        Assert.That(baseline.SelectDiceToKeep(hand, state).Total, Is.LessThan(3));
        Assert.That(baseline.ShouldTakePassedDice(state), Is.False);
        Assert.That(hybrid.ShouldTakePassedDice(state), Is.True);
        var bothRequired = hybrid with { HybridParameters = new HybridParameters(InheritanceMode: 2) };
        Assert.That(bothRequired.ShouldTakePassedDice(state), Is.False);
    }

    [Test]
    public void RiskLimitsApplyToRollingAndInheritanceButRespectBankingLegality()
    {
        var hybrid = new HybridStrategy(new ExpectimaxParameters(), new AdaptiveStrategy(), new HybridParameters(MaxBustProbability: 0));
        var gainLimited = hybrid with { HybridParameters = new HybridParameters(MinimumExpectedNetGain: 500) };
        var state = CreateState(300, 5);

        Assert.That(hybrid.ShouldStop(state), Is.True);
        Assert.That(gainLimited.ShouldStop(state), Is.True);
        Assert.That(hybrid.ShouldTakePassedDice(state), Is.False);
        Assert.That(hybrid.ShouldStop(state with { RoundScore = 200 }), Is.False);
        Assert.That(hybrid.ShouldStop(state with { RollsThisTurn = 0 }), Is.False);
    }

    [Test]
    public void ExactFinalTurnPolicyOverridesAllHybridRules()
    {
        var hybrid = new HybridStrategy(new ExpectimaxParameters(100, 100, 400, 1, 1, 40), new PassiveStrategy() with { MaxTwosToKeepPerRoll = 1, MaxFivesToKeepPerRoll = 1 }, new HybridParameters(1, 1, 1, 0, 500));
        var baseline = new ExpectimaxStrategy();
        var state = CreateState(250, 5) with { CurrentRound = 10, OpponentScores = [250], LeaderScore = 250 };
        var hand = new Dice(0) { 2, 2, 5, 5, 3 };

        Assert.That(hybrid.ShouldStop(state), Is.EqualTo(baseline.ShouldStop(state)));
        Assert.That(hybrid.SelectDiceToKeep(hand, state with { RoundScore = 0 }).Score, Is.EqualTo(300));
        Assert.That(hybrid.ShouldTakePassedDice(state), Is.EqualTo(baseline.ShouldTakePassedDice(state)));
    }

    [Test]
    public void HybridParentsCanProduceMixedChildrenWithoutMutation()
    {
        var random = new Random(63);
        var children = Enumerable.Range(0, 100).Select(_ => StrategyGenes.Hybrid.Breed(StrategyGenes.HybridAdaptive, random, 0)).ToArray();

        Assert.That(children.All(child => child.Family == "Hybrid" && child.IsValid()), Is.True);
        Assert.That(children.Any(child => child.Values.Skip(20).Take(3).Distinct().Count() > 1), Is.True);
        Assert.That(children.All(child => child.Values.Length == 25), Is.True);
    }

    [Test]
    public void HybridJsonRoundTripsAllGenesAndIdenticalGameDecisions()
    {
        var genes = StrategyGenes.CreateRandom(new Random(65), "Hybrid");
        var original = new StrategyFile("Mixed", genes);
        var restored = StrategyFile.FromJson(original.ToJson(), "Imported");

        Assert.That(restored.Version, Is.EqualTo(3));
        Assert.That(restored.Genes.Family, Is.EqualTo("Hybrid"));
        Assert.That(restored.Genes.Values, Is.EqualTo(genes.Values));
        Assert.That(StrategyEvaluator.Evaluate(restored.Genes.CreateStrategy(), GeneticOptimizer.Baselines, 3, 67), Is.EqualTo(StrategyEvaluator.Evaluate(genes.CreateStrategy(), GeneticOptimizer.Baselines, 3, 67)));
    }

    [Test]
    public async Task HybridTrainingKeepsTheBaselineAvailableAndIsReproducible()
    {
        var options = new TrainingOptions { StrategyFamily = "Hybrid", Population = 4, Generations = 2, Games = 3, ValidationGames = 5, MaximumPlayers = 2, VaryPlayerCount = false, Seed = 69 };
        var first = await GeneticOptimizer.TrainAsync(options);
        var second = await GeneticOptimizer.TrainAsync(options);
        var baseline = StrategyEvaluator.Evaluate(new ExpectimaxStrategy(), GeneticOptimizer.Baselines, 5, options.Seed ^ 0x39A72D15, 2, 2);

        Assert.That(first.Genes.Family, Is.EqualTo("Hybrid"));
        Assert.That(first.Genes.Values.Length, Is.EqualTo(25));
        Assert.That(first.Validation.WinShare, Is.GreaterThanOrEqualTo(baseline.WinShare));
        Assert.That(second.Genes.Values, Is.EqualTo(first.Genes.Values));
        Assert.That(second.Validation, Is.EqualTo(first.Validation));
    }

    private static GameState CreateState(int score, int diceCount) => new()
    {
        RoundScore = score,
        HandDiceCount = diceCount,
        TableDiceCount = 5 - diceCount,
        RollsThisTurn = 1,
        CurrentRound = 5,
        TotalRounds = 10,
        IsLastPlayerInRound = true,
        MyTotalScore = 0,
        PlayerRank = 2,
        PlayerCount = 2,
        OpponentScores = [1000],
        LeaderScore = 1000,
        AverageOpponentScore = 1000,
        InheritedScore = 1000,
        InheritedHandDiceCount = 5
    };
}
