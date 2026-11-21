using Bunit;
using DiceGame.Strategies;
using DiceGame.Training;
using DiceGame.Web.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Text.Json;

namespace DiceGame.Tests;

[TestFixture]
public sealed class StrategyLabTests
{
    [TestCase("Threshold")]
    [TestCase("Expectimax")]
    [TestCase("Hybrid")]
    public void DuplicateMaximumLengthNamesCanStillBeExportedAndLoaded(string family)
    {
        var library = new StrategyLibrary();

        for (var index = 0; index < 12; index++)
        {
            var name = new string(index % 2 == 0 ? 'A' : 'a', 60);
            var saved = library.Add(new StrategyFile(name, StrategyGenes.Baseline(family)));
            var restored = StrategyFile.FromJson(saved.ToJson(), "Imported");

            Assert.That(saved.Name.Length, Is.LessThanOrEqualTo(60));
            Assert.That(restored.Name, Is.EqualTo(saved.Name));
            Assert.That(restored.Genes.Family, Is.EqualTo(family));
            Assert.That(library.Get(saved.Name).Name, Is.EqualTo(saved.Name));
        }

        Assert.That(library.Saved.Select(strategy => strategy.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(12));
    }

    [TestCase(-1, true)]
    [TestCase(0, false)]
    [TestCase(100, false)]
    [TestCase(101, true)]
    public async Task MutationRateIsEditableAndMustBeAValidPercentage(int percentage, bool disabled)
    {
        await using var context = new BunitContext();
        context.Services.AddSingleton<StrategyLibrary>();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var component = context.Render<StrategyLab>();
        Assert.That(component.Find("#mutation-rate").GetAttribute("value"), Is.EqualTo("20"));
        await component.Find("#mutation-rate").ChangeAsync(percentage.ToString());

        Assert.That(component.Find("#train").HasAttribute("disabled"), Is.EqualTo(disabled));
    }

    [Test]
    public async Task LiveOpponentStatisticsAccountForEveryAppearanceAndWin()
    {
        var updates = new List<EvaluationProgress>();
        var result = await StrategyEvaluator.EvaluateAsync(new AdaptiveStrategy(), GeneticOptimizer.Baselines, 35, 17, reportMatchup: updates.Add);
        var latest = updates[^1];
        var opponentAppearances = Enumerable.Range(0, 35).Sum(index => 1 + index % 5);
        var totalVictoryCredit = latest.Candidate.WinShare * 35 + latest.Opponents.Sum(opponent => opponent.WinShare * opponent.Games);

        Assert.That(latest.Candidate, Is.EqualTo(result));
        Assert.That(latest.Opponents.Sum(opponent => opponent.Games), Is.EqualTo(opponentAppearances));
        Assert.That(totalVictoryCredit, Is.EqualTo(35).Within(0.000001));
        Assert.That(latest.Opponents.Select(opponent => opponent.Name), Is.EquivalentTo(GeneticOptimizer.Baselines.Select(strategy => strategy.Name)));
        Assert.That(latest.Opponents.All(opponent => opponent.AverageScore >= 0), Is.True);
    }

    [Test]
    public async Task LastSeatStatisticsUseTheActualShuffledPlayingOrder()
    {
        var strategies = GeneticOptimizer.Baselines;
        var random = new Random(19);
        var expectedGames = strategies.ToDictionary(strategy => strategy.Name, _ => 0);
        var expectedWins = strategies.ToDictionary(strategy => strategy.Name, _ => 0);
        var expectedSharedWins = strategies.ToDictionary(strategy => strategy.Name, _ => 0);

        for (var gameIndex = 0; gameIndex < 100; gameIndex++)
        {
            var players = strategies.Select(strategy => new Player(strategy.Name, strategy)).ToArray();
            random.Shuffle(players);
            var lastPlayer = players[^1];
            var result = new Game(players, verbose: false, rollDie: () => random.Next(1, 7)).Start();
            expectedGames[lastPlayer.Name]++;

            if (result.Rankings.Single(player => player.Name == lastPlayer.Name).Score == result.WinnerScore)
            {
                expectedWins[lastPlayer.Name]++;

                if (result.Rankings.Count(player => player.Score == result.WinnerScore) > 1)
                {
                    expectedSharedWins[lastPlayer.Name]++;
                }
            }
        }

        var standings = await StrategyTournament.RunAsync(strategies, 100, 19);

        foreach (var standing in standings)
        {
            Assert.That(standing.LastSeatGames, Is.EqualTo(expectedGames[standing.Name]));
            Assert.That(standing.LastSeatWins, Is.EqualTo(expectedWins[standing.Name]));
            Assert.That(standing.LastSeatSharedWins, Is.EqualTo(expectedSharedWins[standing.Name]));
        }

        Assert.That(standings.Sum(standing => standing.LastSeatGames), Is.EqualTo(100));
    }

    [Test]
    public async Task TrainingReportsEveryPhaseAndCountsAllMatches()
    {
        var options = new TrainingOptions { Population = 2, Generations = 2, Games = 12, ValidationGames = 11, Seed = 12 };
        var updates = new List<LiveTrainingProgress>();
        var result = await GeneticOptimizer.TrainAsync(options, reportLive: updates.Add);
        var expectedMatches = options.ValidationGames + options.Generations * (options.Population * options.Games + options.ValidationGames);

        Assert.That(updates.Select(update => update.Phase), Does.Contain("Baseline validation").And.Contain("Evaluating candidates").And.Contain("Validating generation winner"));
        Assert.That(updates.Select(update => update.CompletedGames), Is.Ordered);
        Assert.That(updates[^1].CompletedGames, Is.EqualTo(expectedMatches));
        Assert.That(updates[^1].TotalGames, Is.EqualTo(expectedMatches));
        Assert.That(updates[^1].BestValidationWinShare, Is.EqualTo(result.Validation.WinShare));
        Assert.That(updates.Any(update => update.CurrentEvaluation is { Games: 10 }), Is.True);
        Assert.That(updates[^1].Opponents.Sum(opponent => opponent.Games), Is.GreaterThan(0));
    }

    [Test]
    public async Task TournamentSnapshotsContainLiveResultsAndMatchTheFinalResults()
    {
        var updates = new List<TournamentProgress>();
        var result = await StrategyTournament.RunAsync(GeneticOptimizer.Baselines, 25, 13, reportLive: updates.Add);

        Assert.That(updates.Count, Is.GreaterThan(1));
        Assert.That(updates[0].CompletedGames, Is.LessThan(25));
        Assert.That(updates[0].Standings.Sum(standing => standing.WinShare), Is.EqualTo(1).Within(0.000001));
        Assert.That(updates[^1].CompletedGames, Is.EqualTo(25));
        Assert.That(updates[^1].Standings, Is.EqualTo(result));
    }

    [Test]
    public async Task RunningSimulationShowsProgressAndLiveStandingsBeforeCancellation()
    {
        await using var context = new BunitContext();
        context.Services.AddSingleton<StrategyLibrary>();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var component = context.Render<StrategyLab>();
        await component.Find("#simulation-games").ChangeAsync("10000");
        var running = component.Find("#simulate").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        component.WaitForAssertion(() =>
        {
            Assert.That(component.Find("progress").GetAttribute("max"), Is.EqualTo("10000"));
            Assert.That(long.Parse(component.Find("progress").GetAttribute("value")!), Is.InRange(1, 9999));
            Assert.That(component.FindAll("tbody tr").Count, Is.EqualTo(4));
            Assert.That(component.FindAll("fieldset[disabled]").Count, Is.EqualTo(2));
        });
        await component.Find(".lab-status button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await running;

        Assert.That(component.FindAll("fieldset[disabled]"), Is.Empty);
        Assert.That(component.Find(".lab-status").TextContent, Does.Contain("Cancelled"));
        Assert.That(component.FindAll("tbody tr").Count, Is.EqualTo(4));
        Assert.That(component.Find(".lab-table + p").TextContent, Does.Contain("Preliminary results"));
    }

    [Test]
    public void IdleLabHasEditableFormsAndEmptySeeds()
    {
        using var context = new BunitContext();
        context.Services.AddSingleton<StrategyLibrary>();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var component = context.Render<StrategyLab>();

        Assert.That(component.FindAll("fieldset[disabled]"), Is.Empty);
        Assert.That(component.Find("#train").HasAttribute("disabled"), Is.False);
        Assert.That(component.Find("#simulate").HasAttribute("disabled"), Is.False);
        Assert.That(component.Find("#training-seed").GetAttribute("value"), Is.Null.Or.Empty);
        Assert.That(component.Find("#simulation-seed").GetAttribute("value"), Is.Null.Or.Empty);
    }

    [TestCase("")]
    [TestCase("0")]
    [TestCase("42")]
    public async Task SimulationDisplaysTheActualSeedAndCanBeReplayed(string seed)
    {
        await using var context = new BunitContext();
        context.Services.AddSingleton<StrategyLibrary>();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var component = context.Render<StrategyLab>();
        await component.Find("#simulation-games").ChangeAsync("10");
        await component.Find("#simulation-seed").ChangeAsync(seed);
        await component.Find("#simulate").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        var displayedSeed = int.Parse(component.Find(".lab-seed").TextContent.Split(':')[1].Trim());
        var firstResults = component.Find("tbody").TextContent;

        if (seed.Length > 0)
        {
            Assert.That(displayedSeed, Is.EqualTo(int.Parse(seed)));
        }

        Assert.That(component.FindAll("fieldset[disabled]"), Is.Empty);
        await component.Find("#simulation-seed").ChangeAsync(displayedSeed.ToString());
        await component.Find("#simulate").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(component.Find("tbody").TextContent, Is.EqualTo(firstResults));
    }

    [Test]
    public void StrategyFilesSupportNewAndConsoleFormats()
    {
        var original = new StrategyFile("Custom", StrategyGenes.Adaptive);
        var restored = StrategyFile.FromJson(original.ToJson(), "Unused");
        var legacy = JsonSerializer.Serialize(new { Training = new { Genes = StrategyGenes.Adaptive } });
        var consoleStrategy = StrategyFile.FromJson(legacy, "Console winner");

        Assert.That(restored.Name, Is.EqualTo("Custom"));
        Assert.That(restored.Genes.Values, Is.EqualTo(original.Genes.Values));
        Assert.That(consoleStrategy.Name, Is.EqualTo("Console winner"));
        Assert.That(consoleStrategy.Genes.Values, Is.EqualTo(original.Genes.Values));
    }

    [TestCase("{\"Name\":\"Broken\",\"Genes\":{\"Values\":[1]}}")]
    [TestCase("{\"Name\":\"Broken\",\"Genes\":null}")]
    [TestCase("null")]
    public void InvalidFilesAreRejected(string json)
    {
        Assert.That(() => StrategyFile.FromJson(json, "Imported"), Throws.TypeOf<JsonException>());
    }

    [Test]
    public async Task TournamentIsReproducibleAndDistributesAllWinCredit()
    {
        IPlayerStrategy[] strategies = [new AdaptiveStrategy(), new BoldStrategy(), new PassiveStrategy()];
        var first = await StrategyTournament.RunAsync(strategies, 30, 89);
        var second = await StrategyTournament.RunAsync(strategies, 30, 89);

        Assert.That(second, Is.EqualTo(first));
        Assert.That(first.Sum(standing => standing.WinShare), Is.EqualTo(1).Within(0.000001));
    }

    [Test]
    public void CancelledTrainingAndTournamentsStopWithoutResults()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () => await GeneticOptimizer.TrainAsync(new TrainingOptions(), cancellationToken: cancellation.Token));
        Assert.ThrowsAsync<OperationCanceledException>(async () => await StrategyTournament.RunAsync(GeneticOptimizer.Baselines, 10, 1, cancellationToken: cancellation.Token));
    }

    [Test]
    public async Task LabRunsSimulationsIncludingSeveralLoadedStrategies()
    {
        await using var context = new BunitContext();
        var library = new StrategyLibrary();
        library.Add(new StrategyFile("Custom A", StrategyGenes.Adaptive));
        library.Add(new StrategyFile("Custom B", StrategyGenes.CreateRandom(new Random(19))));
        context.Services.AddSingleton(library);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var component = context.Render<StrategyLab>();
        await component.FindAll(".strategy-choices input")[^2].ChangeAsync(true);
        await component.FindAll(".strategy-choices input")[^1].ChangeAsync(true);
        await component.Find("#simulation-games").ChangeAsync("5");
        await component.Find("#simulate").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(component.FindAll("tbody tr").Count, Is.EqualTo(6));
        Assert.That(component.Find("table").TextContent, Does.Contain("Custom A").And.Contain("Custom B"));
        Assert.That(component.Find(".lab-status").TextContent, Does.Contain("completed"));
        Assert.That(component.Find(".tournament-table thead").TextContent, Does.Contain("Played last").And.Contain("Wins playing last"));
        Assert.That(component.Find(".last-seat-summary").TextContent, Does.Contain("5 matches"));
    }

    [Test]
    public async Task BrowserTrainingAddsTheWinnerAndOffersJsonDownload()
    {
        await using var context = new BunitContext();
        var library = new StrategyLibrary();
        context.Services.AddSingleton(library);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var component = context.Render<StrategyLab>();
        var values = new[] { "2", "1", "2", "3" };

        for (var index = 0; index < values.Length; index++)
        {
            await component.FindAll(".lab-grid > section:first-child input[type=number]")[index].ChangeAsync(values[index]);
        }

        await component.Find("#train").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(library.Saved.Count, Is.EqualTo(1));
        Assert.That(library.Saved[0].Genes.IsValid(), Is.True);
        Assert.That(component.Find("a[download]").GetAttribute("href"), Does.StartWith("data:application/json"));
        Assert.That(component.Find(".lab-status").TextContent, Does.Contain("Saved Evolved"));
        Assert.That(int.TryParse(component.Find(".lab-seed").TextContent.Split(':')[1].Trim(), out _), Is.True);
        Assert.That(component.FindAll("fieldset[disabled]"), Is.Empty);
        Assert.That(component.FindAll(".comparison-table tbody tr").Count, Is.EqualTo(3));
        Assert.That(component.Find(".comparison-table").TextContent, Does.Contain("Adaptive").And.Contain("Expectimax"));
        Assert.That(component.Find(".comparison-table").TextContent, Does.Not.Contain("Passive").And.Not.Contain("Bold"));
    }

    [Test]
    public void FilePickerLoadsSeveralStrategiesAndReportsMalformedFiles()
    {
        using var context = new BunitContext();
        var library = new StrategyLibrary();
        context.Services.AddSingleton(library);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var component = context.Render<StrategyFiles>();
        var picker = component.FindComponent<Microsoft.AspNetCore.Components.Forms.InputFile>();
        picker.UploadFiles(InputFileContent.CreateFromText(new StrategyFile("First", StrategyGenes.Adaptive).ToJson(), "first.json"), InputFileContent.CreateFromText(new StrategyFile("Second", StrategyGenes.Adaptive).ToJson(), "second.json"), InputFileContent.CreateFromText("broken", "broken.json"));
        component.WaitForAssertion(() => Assert.That(library.Saved.Count, Is.EqualTo(2)));

        Assert.That(component.Find("[role=status]").TextContent, Does.Contain("Could not load broken.json"));
    }

    [Test]
    public async Task ImportedStrategyCanPlayAnOrdinaryMatch()
    {
        await using var context = new BunitContext();
        var library = new StrategyLibrary();
        var saved = library.Add(StrategyFile.FromJson(new StrategyFile("Adaptive", StrategyGenes.Adaptive).ToJson(), "Imported"));
        context.Services.AddSingleton(library);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.JSInterop.SetupModule("./js/dice-audio.js").Mode = JSRuntimeMode.Loose;
        context.JSInterop.SetupModule("./js/player-preferences.js").Mode = JSRuntimeMode.Loose;
        var component = context.Render<GameTable>();
        var picker = component.FindComponent<Microsoft.AspNetCore.Components.Forms.InputFile>();
        picker.UploadFiles(InputFileContent.CreateFromText(new StrategyFile("Fresh import", StrategyGenes.Adaptive).ToJson(), "fresh.json"));
        component.WaitForAssertion(() => Assert.That(component.FindAll("#strategy-Alma option").Select(option => option.TextContent), Does.Contain("Fresh import")));
        await component.Find("#strategy-Alma").ChangeAsync(saved.Name);
        await component.Find(".start-button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(saved.Name, Is.EqualTo("Adaptive (2)"));
        Assert.That(component.Find(".scoreboard").TextContent, Does.Contain(saved.Name));
        Assert.That(component.Find("a[href='strategies']").TextContent, Is.EqualTo("Strategy lab"));
    }
}
