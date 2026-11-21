using DiceGame.Strategies;
using DiceGame.Training;

namespace DiceGame.Web.Components;

public sealed class StrategyLibrary
{
    private readonly PlayerStrategies builtIns = new();
    private readonly List<StrategyFile> saved = [];

    public IReadOnlyList<StrategyFile> Saved => saved;
    public IEnumerable<IPlayerStrategy> Available => builtIns.Available.Concat(saved.Select(strategy => strategy.Genes.CreateStrategy(strategy.Name)));

    public IPlayerStrategy Get(string name) => Available.Single(strategy => strategy.Name == name);

    public StrategyFile Add(StrategyFile strategy)
    {
        var name = strategy.Name.Trim();
        var uniqueName = name;
        var suffix = 2;

        while (Available.Any(existing => existing.Name.Equals(uniqueName, StringComparison.OrdinalIgnoreCase)))
        {
            var ending = $" ({suffix++})";
            uniqueName = name[..Math.Min(name.Length, 60 - ending.Length)] + ending;
        }

        var savedStrategy = strategy with { Name = uniqueName };
        saved.Add(savedStrategy);

        return savedStrategy;
    }
}
