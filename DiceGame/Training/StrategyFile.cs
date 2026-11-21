using System;
using System.Text.Json;

namespace DiceGame.Training;

public sealed record StrategyFile(string Name, StrategyGenes Genes)
{
    public int Version { get; init; } = Genes?.Family switch { "Hybrid" => 3, "Expectimax" => 2, _ => 1 };

    public string ToJson() => JsonSerializer.Serialize(this, StrategyJsonContext.Default.StrategyFile);

    public static StrategyFile FromJson(string json, string fallbackName)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind is not JsonValueKind.Object)
        {
            throw new JsonException("The file must contain a strategy object.");
        }

        var strategy = root.TryGetProperty("Training", out var training)
            ? new StrategyFile(fallbackName, training.GetProperty("Genes").Deserialize(StrategyJsonContext.Default.StrategyGenes)!)
            : root.Deserialize(StrategyJsonContext.Default.StrategyFile)!;

        if (strategy is null || strategy.Version is not (1 or 2 or 3) || string.IsNullOrWhiteSpace(strategy.Name) || strategy.Name.Length > 60 || strategy.Genes is null || strategy.Genes.IsValid() is false)
        {
            throw new JsonException("The file must contain a name and valid Threshold, Expectimax, or Hybrid genes (version 1, 2, or 3).");
        }

        return strategy;
    }
}
