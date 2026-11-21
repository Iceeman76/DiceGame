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
public sealed class PlayerCountTrainingTests
{
    [TestCase(2)]
    [TestCase(5)]
    [TestCase(6)]
    public async Task FixedTablesUseExactlyTheSelectedNumberOfPlayers(int playerCount)
    {
        var updates = new List<EvaluationProgress>();
        var result = await StrategyEvaluator.EvaluateAsync(new AdaptiveStrategy(), GeneticOptimizer.Baselines, 23, 21, reportMatchup: updates.Add, minimumPlayers: playerCount, maximumPlayers: playerCount);
        var latest = updates[^1];

        Assert.That(latest.ByPlayerCount.Select(table => table.PlayerCount), Is.EqualTo(new[] { playerCount }));
        Assert.That(latest.ByPlayerCount[0].Games, Is.EqualTo(23));
        Assert.That(latest.ByPlayerCount[0].WinShare, Is.EqualTo(result.WinShare));
        Assert.That(latest.Opponents.Sum(opponent => opponent.Games), Is.EqualTo(23 * (playerCount - 1)));
    }

    [Test]
    public async Task VaryingTablesCycleEvenlyAndTheirWeightedResultsMatchTheTotal()
    {
        var updates = new List<EvaluationProgress>();
        var result = await StrategyEvaluator.EvaluateAsync(new AdaptiveStrategy(), GeneticOptimizer.Baselines, 11, 23, reportMatchup: updates.Add, maximumPlayers: 4);
        var tables = updates[^1].ByPlayerCount;

        Assert.That(tables.Select(table => table.PlayerCount), Is.EqualTo(new[] { 2, 3, 4 }));
        Assert.That(tables.Select(table => table.Games), Is.EqualTo(new[] { 4, 4, 3 }));
        Assert.That(tables.Sum(table => table.WinShare * table.Games) / 11, Is.EqualTo(result.WinShare).Within(0.000001));
        Assert.That(tables.Sum(table => table.AverageScore * table.Games) / 11, Is.EqualTo(result.AverageScore).Within(0.000001));
    }

    [TestCase(false, 5)]
    [TestCase(true, 3)]
    public async Task TrainingAndWinnerValidationUseTheSameConfiguredTables(bool varyPlayerCount, int maximumPlayers)
    {
        var options = new TrainingOptions { Population = 2, Generations = 1, Games = 5, ValidationGames = 7, Seed = 25, MaximumPlayers = maximumPlayers, VaryPlayerCount = varyPlayerCount };
        var updates = new List<LiveTrainingProgress>();
        var result = await GeneticOptimizer.TrainAsync(options, reportLive: updates.Add);
        var minimumPlayers = varyPlayerCount ? 2 : maximumPlayers;
        var expectedCounts = Enumerable.Range(minimumPlayers, maximumPlayers - minimumPlayers + 1).ToArray();
        var validation = StrategyEvaluator.Evaluate(result.Genes.CreateStrategy(), GeneticOptimizer.Baselines, options.ValidationGames, options.Seed ^ 0x39A72D15, minimumPlayers, maximumPlayers);

        Assert.That(updates.Where(update => update.ByPlayerCount.Length > 0).All(update => update.ByPlayerCount.Select(table => table.PlayerCount).SequenceEqual(expectedCounts)), Is.True);
        Assert.That(result.ValidationByPlayerCount.Select(table => table.PlayerCount), Is.EqualTo(expectedCounts));
        Assert.That(result.ValidationByPlayerCount.Sum(table => table.Games), Is.EqualTo(options.ValidationGames));
        Assert.That(result.Validation, Is.EqualTo(validation));
    }

    [TestCase(false, 5, 1)]
    [TestCase(true, 4, 3)]
    public async Task BrowserUsesSelectedTablesAndShowsResultsByPlayerCount(bool varyPlayerCount, int playerCount, int rows)
    {
        await using var context = new BunitContext();
        context.Services.AddSingleton<StrategyLibrary>();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var component = context.Render<StrategyLab>();
        await component.Find("#training-player-mode").ChangeAsync(varyPlayerCount ? "varying" : "fixed");
        await component.Find("#training-player-count").ChangeAsync(playerCount.ToString());
        var values = new[] { "2", "1", "3", "3" };

        for (var index = 0; index < values.Length; index++)
        {
            await component.FindAll(".lab-grid > section:first-child input[type=number]")[index].ChangeAsync(values[index]);
        }

        await component.Find("#train").ClickAsync(new MouseEventArgs());

        Assert.That(component.FindAll(".player-count-table tbody tr").Count, Is.EqualTo(rows));
        Assert.That(component.FindAll(".player-count-table tbody tr")[^1].Children[0].TextContent, Is.EqualTo(playerCount.ToString()));

        if (varyPlayerCount is false)
        {
            Assert.That(component.Find(".player-count-table").TextContent, Does.Contain((1d / playerCount).ToString("P2")));
        }
    }

    [Test]
    public void ExistingOptionsDefaultToVaryingTablesAndNewSettingsRoundTrip()
    {
        var legacy = JsonSerializer.Deserialize<TrainingOptions>("{}")!;
        var original = new TrainingOptions { VaryPlayerCount = false, MaximumPlayers = 5 };
        var restored = JsonSerializer.Deserialize<TrainingOptions>(JsonSerializer.Serialize(original));

        Assert.That(legacy.VaryPlayerCount, Is.True);
        Assert.That(legacy.MaximumPlayers, Is.EqualTo(6));
        Assert.That(restored, Is.EqualTo(original));
    }
}
