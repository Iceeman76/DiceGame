namespace DiceGame.Strategies;

public sealed record HybridParameters(int BankingMode = 0, int DiceSelectionMode = 0, int InheritanceMode = 0, double MaxBustProbability = 1, double MinimumExpectedNetGain = double.NegativeInfinity);
