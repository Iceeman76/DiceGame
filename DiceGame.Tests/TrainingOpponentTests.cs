using Bunit;
using DiceGame.Strategies;
using DiceGame.Training;
using DiceGame.Web.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Text.Json;

namespace DiceGame.Tests;

[TestFixture]
public sealed class TrainingOpponentTests
{
    [Test]
    public async Task UnselectedStrategiesNeverParticipateInTrainingOrValidation()
    {
        var options = SmallOptions() with { OpponentNames = ["Expectimax"], IncludeGenerationWinners = false };
        var updates = new List<LiveTrainingProgress>();
        await GeneticOptimizer.TrainAsync(options, reportLive: updates.Add);

        Assert.That(updates.Where(update => update.CurrentEvaluation is not null).All(update => update.Opponents.Select(opponent => opponent.Name).SequenceEqual(new[] { "Expectimax" })), Is.True);
        Assert.That(updates[^1].CompletedGames, Is.EqualTo(updates[^1].TotalGames));
        Assert.That(updates[^1].TotalGames, Is.EqualTo(options.ValidationGames + options.Generations * (options.Population * options.Games + options.ValidationGames)));
    }

    [Test]
    public async Task LoadedOpponentsAreSelfContainedInSavedOptionsAndReplayIdentically()
    {
        var original = SmallOptions() with
        {
            OpponentNames = ["Loaded champion"],
            CustomOpponents = [new StrategyFile("Loaded champion", StrategyGenes.HybridAdaptive)],
            IncludeGenerationWinners = false
        };
        var restored = JsonSerializer.Deserialize<TrainingOptions>(JsonSerializer.Serialize(original))!;
        var updates = new List<LiveTrainingProgress>();
        var first = await GeneticOptimizer.TrainAsync(original, reportLive: updates.Add);
        var second = await GeneticOptimizer.TrainAsync(restored);

        Assert.That(GeneticOptimizer.ResolveOpponents(restored).Single(), Is.TypeOf<HybridStrategy>());
        Assert.That(updates.Where(update => update.CurrentEvaluation is not null).All(update => update.Opponents.Single().Name == "Loaded champion"), Is.True);
        Assert.That(second.Genes.Values, Is.EqualTo(first.Genes.Values));
        Assert.That(second.Validation, Is.EqualTo(first.Validation));
    }

    [Test]
    public async Task GenerationWinnersAreTrainingOnlyAndCannotShadowASelectedOpponent()
    {
        var options = SmallOptions() with { OpponentNames = ["Generation 1"], CustomOpponents = [new StrategyFile("Generation 1", StrategyGenes.Expectimax)], IncludeGenerationWinners = true };
        var updates = new List<LiveTrainingProgress>();
        await GeneticOptimizer.TrainAsync(options, reportLive: updates.Add);
        var secondGeneration = updates.First(update => update.Generation == 2 && update.Phase == "Evaluating candidates" && update.CurrentEvaluation is not null);

        Assert.That(secondGeneration.Opponents.Select(opponent => opponent.Name), Is.EquivalentTo(new[] { "Generation 1", "Generation 1 (2)" }));
        Assert.That(updates.Where(update => update.Phase == "Validating generation winner" && update.CurrentEvaluation is not null).All(update => update.Opponents.Select(opponent => opponent.Name).SequenceEqual(new[] { "Generation 1" })), Is.True);
    }

    [Test]
    public void EmptyOrUnknownOpponentSelectionsFailInsteadOfFallingBackToBuiltIns()
    {
        Assert.Throws<ArgumentException>(() => GeneticOptimizer.ResolveOpponents(new TrainingOptions { OpponentNames = [] }));
        Assert.Throws<InvalidOperationException>(() => GeneticOptimizer.ResolveOpponents(new TrainingOptions { OpponentNames = ["Unknown"] }));
        var legacy = JsonSerializer.Deserialize<TrainingOptions>("{}")!;
        Assert.That(GeneticOptimizer.ResolveOpponents(legacy).Select(opponent => opponent.Name), Is.EqualTo(new[] { "Passive", "Bold", "Adaptive", "Expectimax" }));
    }

    [Test]
    public async Task BrowserRequiresAnOpponentAndSelectionIsIndependentOfTournament()
    {
        await using var context = CreateContext(new StrategyLibrary());
        var component = context.Render<StrategyLab>();
        Assert.That(component.Find("[data-training-opponent='Passive']").HasAttribute("checked"), Is.False);
        Assert.That(component.Find("[data-training-opponent='Bold']").HasAttribute("checked"), Is.False);
        await component.Find("[data-training-opponent='Adaptive']").ChangeAsync(false);
        await component.Find("[data-training-opponent='Expectimax']").ChangeAsync(false);

        Assert.That(component.Find("#train").HasAttribute("disabled"), Is.True);
        Assert.That(component.Find("#simulate").HasAttribute("disabled"), Is.False);
        Assert.That(component.FindAll(".strategy-choices input[checked]").Count, Is.EqualTo(4));
        await component.Find("[data-training-opponent='Expectimax']").ChangeAsync(true);
        Assert.That(component.Find("#train").HasAttribute("disabled"), Is.False);
    }

    [Test]
    public async Task BrowserTrainsAgainstLoadedSelectionsAndShowsTheirLiveStatistics()
    {
        var library = new StrategyLibrary();
        library.Add(new StrategyFile("Loaded challenger", StrategyGenes.Expectimax));
        await using var context = CreateContext(library);
        var component = context.Render<StrategyLab>();
        await component.Find("[data-training-opponent='Adaptive']").ChangeAsync(false);
        await component.Find("[data-training-opponent='Expectimax']").ChangeAsync(false);
        await component.Find("[data-training-opponent='Loaded challenger']").ChangeAsync(true);
        await component.Find("#include-generation-winners").ChangeAsync(false);
        var values = new[] { "2", "2", "3", "3" };

        for (var index = 0; index < values.Length; index++)
        {
            await component.FindAll(".lab-grid > section:first-child input[type=number]")[index].ChangeAsync(values[index]);
        }

        await component.Find("#training-player-mode").ChangeAsync("fixed");
        await component.Find("#training-player-count").ChangeAsync("2");
        await component.Find("#training-seed").ChangeAsync("79");
        await component.Find("#train").ClickAsync(new MouseEventArgs());

        Assert.That(component.FindAll(".comparison-table tbody tr").Count, Is.EqualTo(2));
        Assert.That(component.Find(".comparison-table").TextContent, Does.Contain("Loaded challenger"));
        Assert.That(component.Find(".comparison-table").TextContent, Does.Not.Contain("Passive").And.Not.Contain("Bold"));
        Assert.That(component.Find(".training-opponent-summary").TextContent, Does.Contain("Loaded challenger").And.Contain("disabled"));
        Assert.That(component.Find("progress").GetAttribute("value"), Is.EqualTo(component.Find("progress").GetAttribute("max")));
    }

    private static TrainingOptions SmallOptions() => new() { StrategyFamily = "Hybrid", Population = 2, Generations = 2, Games = 3, ValidationGames = 5, MaximumPlayers = 2, VaryPlayerCount = false, Seed = 73 };

    private static BunitContext CreateContext(StrategyLibrary library)
    {
        var context = new BunitContext();
        context.Services.AddSingleton(library);
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        return context;
    }
}
