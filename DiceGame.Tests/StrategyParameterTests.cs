using Bunit;
using DiceGame.Strategies;
using DiceGame.Training;
using DiceGame.Web.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;

namespace DiceGame.Tests;

[TestFixture]
public sealed class StrategyParameterTests
{
    [Test]
    public void HybridInspectionExplainsDecisionSourcesAndInactiveThresholds()
    {
        var baseline = StrategyParameterCatalog.Describe(StrategyGenes.Hybrid.CreateStrategy());
        var threshold = StrategyParameterCatalog.Describe(StrategyGenes.HybridAdaptive.CreateStrategy());

        Assert.That(baseline.Single(parameter => parameter.Key == "BankingMode").Value, Is.EqualTo("Expectimax"));
        Assert.That(baseline.Single(parameter => parameter.Key == "Threshold.StopThreshold").Status, Does.Contain("Inactive"));
        Assert.That(threshold.Single(parameter => parameter.Key == "BankingMode").Value, Is.EqualTo("Threshold"));
        Assert.That(threshold.Single(parameter => parameter.Key == "Threshold.StopThreshold").Status, Is.EqualTo("Configured"));
        Assert.That(baseline.Select(parameter => parameter.Key).Distinct().Count(), Is.EqualTo(baseline.Count));
    }

    [Test]
    public async Task HybridIsTheBrowserDefaultAndCanBeSavedLoadedAndInspected()
    {
        await using var context = new BunitContext();
        var library = new StrategyLibrary();
        context.Services.AddSingleton(library);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var component = context.Render<StrategyLab>();
        Assert.That(component.Find("#strategy-name").GetAttribute("value"), Is.EqualTo("Evolved Hybrid"));
        var values = new[] { "2", "1", "2", "3" };

        for (var index = 0; index < values.Length; index++)
        {
            await component.FindAll(".lab-grid > section:first-child input[type=number]")[index].ChangeAsync(values[index]);
        }

        await component.Find("#training-player-mode").ChangeAsync("fixed");
        await component.Find("#training-player-count").ChangeAsync("2");
        await component.Find("#training-seed").ChangeAsync("71");
        await component.Find("#train").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        var saved = library.Saved.Single();
        var imported = library.Add(StrategyFile.FromJson(saved.ToJson(), "Imported"));
        await component.Find("#inspect-strategy").ChangeAsync(imported.Name);

        Assert.That(saved.Genes.Family, Is.EqualTo("Hybrid"));
        Assert.That(saved.Genes.Values.Length, Is.EqualTo(25));
        Assert.That(imported.Genes.Values, Is.EqualTo(saved.Genes.Values));
        Assert.That(library.Get(imported.Name), Is.TypeOf<HybridStrategy>());
        Assert.That(component.Find("[data-parameter=BankingMode] dd p").TextContent, Does.Contain("Expectimax"));
        Assert.That(component.Find("a[download]").GetAttribute("href"), Does.StartWith("data:application/json"));
    }

    [Test]
    public void EveryThresholdConfigurationPropertyHasANameDescriptionAndValue()
    {
        var descriptions = StrategyParameterCatalog.Describe(new AdaptiveStrategy());
        var properties = typeof(ThresholdStrategy).GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(property => property.Name).Where(name => name is not ("Name" or "Description"));

        Assert.That(descriptions.Select(parameter => parameter.Key), Is.EquivalentTo(properties));
        Assert.That(descriptions.All(parameter => string.IsNullOrWhiteSpace(parameter.Name) is false && string.IsNullOrWhiteSpace(parameter.Description) is false && string.IsNullOrWhiteSpace(parameter.Value) is false), Is.True);
        Assert.That(descriptions.Single(parameter => parameter.Key == "StopThreshold").Value, Is.EqualTo("350"));
    }

    [Test]
    public void EveryExpectimaxWeightIsDescribedAndInactiveDependenciesAreIdentified()
    {
        var descriptions = StrategyParameterCatalog.Describe(new ExpectimaxStrategy());
        var properties = typeof(ExpectimaxParameters).GetProperties().Select(property => property.Name).Where(name => name is not "IsBaseline");

        Assert.That(properties.All(name => descriptions.Any(parameter => parameter.Key == name)), Is.True);
        Assert.That(descriptions.Single(parameter => parameter.Key == "ExpectedOpponentTurnPoints").Status, Does.Contain("Inactive"));
        Assert.That(descriptions.Single(parameter => parameter.Key == "LateGameWeight").Status, Does.Contain("Inactive"));
        Assert.That(descriptions.Single(parameter => parameter.Key == "PassedDicePenalty").Value, Is.EqualTo("0"));
    }

    [Test]
    public async Task InspectorShowsEffectiveValuesForLoadedStrategiesFromBothFamilies()
    {
        await using var context = new BunitContext();
        var library = new StrategyLibrary();
        var threshold = library.Add(StrategyFile.FromJson(new StrategyFile("Loaded threshold", StrategyGenes.Adaptive).ToJson(), "Imported"));
        var genes = new StrategyGenes([2, 3, 6, 4, 2, 1]) { Family = "Expectimax" };
        var expectimax = library.Add(StrategyFile.FromJson(new StrategyFile("Loaded Expectimax", genes).ToJson(), "Imported"));
        context.Services.AddSingleton(library);
        var component = context.Render<StrategyInspector>();
        await component.Find("#inspect-strategy").ChangeAsync(threshold.Name);
        Assert.That(component.Find("[data-parameter=StopThreshold] dd strong").TextContent, Is.EqualTo("350"));
        await component.Find("#inspect-strategy").ChangeAsync(expectimax.Name);

        Assert.That(component.Find("[data-parameter=RiskAdjustment] dd strong").TextContent, Is.EqualTo("50"));
        Assert.That(component.Find("[data-parameter=MatchWinWeight] dd strong").TextContent, Is.EqualTo("75"));
        Assert.That(component.Find("[data-parameter=ExpectedOpponentTurnPoints] dd strong").TextContent, Is.EqualTo("300"));
        Assert.That(component.Find("[data-parameter=PassedDicePenalty] dd strong").TextContent, Is.EqualTo("10"));
        Assert.That(component.Find("[data-parameter=RiskAdjustment] dd p").TextContent, Is.Not.Empty);
    }

    [Test]
    public async Task LabCanTrainTheExpectimaxFamilyAndSaveItForInspectionAndPlay()
    {
        await using var context = new BunitContext();
        var library = new StrategyLibrary();
        context.Services.AddSingleton(library);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var component = context.Render<StrategyLab>();
        await component.Find("#training-family").ChangeAsync("Expectimax");
        var values = new[] { "2", "1", "2", "3" };

        for (var index = 0; index < values.Length; index++)
        {
            await component.FindAll(".lab-grid > section:first-child input[type=number]")[index].ChangeAsync(values[index]);
        }

        await component.Find("#training-player-mode").ChangeAsync("fixed");
        await component.Find("#training-player-count").ChangeAsync("2");
        await component.Find("#training-seed").ChangeAsync("47");
        await component.Find("#train").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        var saved = library.Saved.Single();
        await component.Find("#inspect-strategy").ChangeAsync(saved.Name);

        Assert.That(saved.Name, Is.EqualTo("Evolved Expectimax"));
        Assert.That(saved.Genes.Family, Is.EqualTo("Expectimax"));
        Assert.That(library.Get(saved.Name), Is.TypeOf<ParameterizedExpectimaxStrategy>());
        Assert.That(component.FindAll("#strategy-name").Count, Is.EqualTo(1));
        Assert.That(component.Find("[data-parameter=MatchWinWeight]"), Is.Not.Null);
    }
}
