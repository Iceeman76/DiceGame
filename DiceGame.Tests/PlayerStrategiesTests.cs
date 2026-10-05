using DiceGame.Strategies;
using NUnit.Framework;

namespace DiceGame.Tests;

[TestFixture]
public sealed class PlayerStrategiesTests
{
    [Test]
    public void CatalogDiscoversConcreteStrategyRecords()
    {
        var strategies = new PlayerStrategies();

        Assert.That(strategies.Available.Select(strategy => strategy.GetType()), Is.EquivalentTo([typeof(PassiveStrategy), typeof(BoldStrategy), typeof(AdaptiveStrategy), typeof(ExpectimaxStrategy)]));
        Assert.That(strategies.Get("Passive"), Is.TypeOf<PassiveStrategy>());
        Assert.That(strategies.Get("Bold"), Is.TypeOf<BoldStrategy>());
        Assert.That(strategies.Get("Adaptive"), Is.TypeOf<AdaptiveStrategy>());
        Assert.That(strategies.Get("Expectimax"), Is.TypeOf<ExpectimaxStrategy>());
    }

    [Test]
    public void NewStrategyIsDiscoveredWithoutChangingTheCatalog()
    {
        var strategies = new PlayerStrategies(typeof(AdditionalStrategy).Assembly);

        Assert.That(strategies.Get("Additional"), Is.TypeOf<AdditionalStrategy>());
        Assert.That(strategies.Get("Passive"), Is.TypeOf<PassiveStrategy>());
        Assert.That(strategies.Available.Count(strategy => strategy is AdditionalStrategy), Is.EqualTo(1));
    }

    [Test]
    public void WithExpressionCustomizesARecordWithoutChangingTheOriginal()
    {
        var strategy = new AdaptiveStrategy();
        var customizedStrategy = strategy with { Name = "Custom", StopThreshold = 500 };

        Assert.That(strategy.Name, Is.EqualTo("Adaptive"));
        Assert.That(strategy.StopThreshold, Is.EqualTo(350));
        Assert.That(customizedStrategy.Name, Is.EqualTo("Custom"));
        Assert.That(customizedStrategy.StopThreshold, Is.EqualTo(500));
    }

    private sealed record AdditionalStrategy : ThresholdStrategy
    {
        public AdditionalStrategy()
        {
            Name = "Additional";
            Description = "Additional strategy discovered from its assembly.";
        }
    }
}
