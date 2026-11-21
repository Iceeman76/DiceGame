using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiceGame.Strategies;

namespace DiceGame.Training;

public sealed record TrainingOptions
{
    public int Population { get; init; } = 40;
    public int Generations { get; init; } = 20;
    public int Games { get; init; } = 500;
    public int ValidationGames { get; init; } = 2000;
    public int TestGames { get; init; } = 10000;
    public int Seed { get; init; } = 20261121;
    public double MutationRate { get; init; } = 0.2;
    public int MaximumPlayers { get; init; } = 6;
    public bool VaryPlayerCount { get; init; } = true;
}

public sealed record TrainingProgress(int Generation, StrategyEvaluation Training, StrategyEvaluation Validation);
public sealed record LiveTrainingProgress(string Phase, int Generation, int Candidate, long CompletedGames, long TotalGames, StrategyEvaluation? CurrentEvaluation, double BestValidationWinShare)
{
    public StrategyComparison[] Opponents { get; init; } = [];
    public PlayerCountEvaluation[] ByPlayerCount { get; init; } = [];
}
public sealed record TrainingResult(TrainingOptions Options, StrategyGenes Genes, StrategyEvaluation Validation)
{
    public PlayerCountEvaluation[] ValidationByPlayerCount { get; init; } = [];
}

public static class GeneticOptimizer
{
    public static IPlayerStrategy[] Baselines => [new PassiveStrategy(), new BoldStrategy(), new AdaptiveStrategy(), new ExpectimaxStrategy()];

    public static TrainingResult Train(TrainingOptions options, Action<TrainingProgress>? report = null)
        => TrainAsync(options, report, yieldToBrowser: false).GetAwaiter().GetResult();

    public static async Task<TrainingResult> TrainAsync(TrainingOptions options, Action<TrainingProgress>? report = null, CancellationToken cancellationToken = default, bool yieldToBrowser = true, Action<LiveTrainingProgress>? reportLive = null)
    {
        var random = new Random(options.Seed);
        var population = new List<StrategyGenes> { StrategyGenes.Adaptive };
        var archive = new List<IPlayerStrategy>();
        var bestGenes = StrategyGenes.Adaptive;
        var completedGames = 0L;
        var totalGames = options.ValidationGames + (long)options.Generations * (options.Population * (long)options.Games + options.ValidationGames);
        var bestValidationShare = 0d;
        StrategyComparison[] currentOpponents = [];
        PlayerCountEvaluation[] currentByPlayerCount = [];
        var validationSeed = unchecked(options.Seed ^ 0x39A72D15);
        var bestValidation = await EvaluateAsync(bestGenes, Baselines, options.ValidationGames, validationSeed, "Baseline validation", 0, 0);
        var bestValidationByPlayerCount = currentByPlayerCount;
        bestValidationShare = bestValidation.WinShare;

        while (population.Count < options.Population)
        {
            population.Add(StrategyGenes.CreateRandom(random));
        }

        for (var generation = 1; generation <= options.Generations; generation++)
        {
            var opponents = Baselines.Concat(archive).ToArray();
            var trainingSeed = random.Next();
            var evaluated = new List<(StrategyGenes Genes, StrategyEvaluation Evaluation)>();

            foreach (var genes in population)
            {
                var evaluation = await EvaluateAsync(genes, opponents, options.Games, trainingSeed, "Evaluating candidates", generation, evaluated.Count + 1);
                evaluated.Add((genes, evaluation));
            }

            var ranked = evaluated.OrderByDescending(candidate => candidate.Evaluation.WinShare).ToArray();
            var champion = ranked[0];
            var validation = await EvaluateAsync(champion.Genes, Baselines, options.ValidationGames, validationSeed, "Validating generation winner", generation, options.Population);

            if (validation.WinShare > bestValidation.WinShare)
            {
                bestGenes = champion.Genes;
                bestValidation = validation;
                bestValidationByPlayerCount = currentByPlayerCount;
            }

            bestValidationShare = bestValidation.WinShare;
            reportLive?.Invoke(new LiveTrainingProgress("Generation completed", generation, options.Population, completedGames, totalGames, validation, bestValidationShare) { Opponents = currentOpponents, ByPlayerCount = currentByPlayerCount });
            report?.Invoke(new TrainingProgress(generation, champion.Evaluation, validation));
            archive.Add(champion.Genes.CreateStrategy($"Generation {generation}"));

            if (archive.Count > 8)
            {
                archive.RemoveAt(0);
            }

            var eliteCount = Math.Max(2, options.Population / 5);
            population = ranked.Take(eliteCount).Select(candidate => candidate.Genes).ToList();

            while (population.Count < options.Population)
            {
                var firstParent = ranked[random.Next(eliteCount)].Genes;
                var secondParent = ranked[random.Next(eliteCount)].Genes;
                population.Add(firstParent.Breed(secondParent, random, options.MutationRate));
            }
        }

        return new TrainingResult(options, bestGenes, bestValidation) { ValidationByPlayerCount = bestValidationByPlayerCount };

        async Task<StrategyEvaluation> EvaluateAsync(StrategyGenes genes, IPlayerStrategy[] opponents, int games, int seed, string phase, int generation, int candidate)
        {
            currentOpponents = [];
            currentByPlayerCount = [];
            reportLive?.Invoke(new LiveTrainingProgress(phase, generation, candidate, completedGames, totalGames, null, bestValidationShare));
            var evaluation = await StrategyEvaluator.EvaluateAsync(genes.CreateStrategy(), opponents, games, seed, cancellationToken, yieldToBrowser, reportMatchup: current =>
            {
                currentOpponents = current.Opponents;
                currentByPlayerCount = current.ByPlayerCount;
                reportLive?.Invoke(new LiveTrainingProgress(phase, generation, candidate, completedGames + current.Candidate.Games, totalGames, current.Candidate, bestValidationShare) { Opponents = currentOpponents, ByPlayerCount = currentByPlayerCount });
            }, minimumPlayers: options.VaryPlayerCount ? 2 : options.MaximumPlayers, maximumPlayers: options.MaximumPlayers);
            completedGames += games;

            return evaluation;
        }
    }
}

