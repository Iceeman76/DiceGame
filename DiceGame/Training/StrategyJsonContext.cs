using System.Text.Json.Serialization;

namespace DiceGame.Training;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(StrategyFile))]
[JsonSerializable(typeof(StrategyGenes))]
internal sealed partial class StrategyJsonContext : JsonSerializerContext;
