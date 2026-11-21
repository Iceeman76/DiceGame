namespace DiceGame.Strategies;

public sealed record StrategyParameter(string Key, string Name, string Description, string Value, string Status = "Configured");
