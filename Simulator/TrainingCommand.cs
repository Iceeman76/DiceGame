using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using DiceGame.Training;

namespace Simulator;

public static class TrainingCommand
{
    private static readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

    public static int Run(string[] args)
    {
        if (args is ["evaluate", var artifactPath])
        {
            var json = File.ReadAllText(artifactPath);
            var strategy = StrategyFile.FromJson(json, Path.GetFileNameWithoutExtension(artifactPath));
            using var document = JsonDocument.Parse(json);
            var evaluationOptions = document.RootElement.TryGetProperty("Training", out var training) ? training.GetProperty("Options").Deserialize<TrainingOptions>()! : new TrainingOptions();
            var evaluationSeed = unchecked(evaluationOptions.Seed ^ 0x1563AC29);
            EvaluateStrategy(strategy.Name, strategy.Genes.CreateStrategy(), evaluationOptions, evaluationSeed);

            return 0;
        }

        if (args[0] is not "train" || args.Length % 2 == 0)
        {
            PrintUsage();

            return 1;
        }

        var options = new TrainingOptions { StrategyFamily = "Hybrid", OpponentNames = ["Adaptive", "Expectimax"] };
        var output = "evolved-strategy.json";

        for (var index = 1; index < args.Length; index += 2)
        {
            var option = args[index];
            var value = args[index + 1];

            if (option == "--output")
            {
                output = value;
                continue;
            }

            if (option == "--opponents")
            {
                options = options with { OpponentNames = value == "none" ? [] : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) };
                continue;
            }

            if (option == "--opponent-file")
            {
                try
                {
                    var opponent = LoadOpponent(value, options);
                    options = options with { CustomOpponents = [.. options.CustomOpponents, opponent] };
                }
                catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or KeyNotFoundException)
                {
                    Console.WriteLine($"Could not load opponent: {exception.Message}");

                    return 1;
                }

                continue;
            }

            if (option == "--generation-winners")
            {
                if (bool.TryParse(value, out var includeWinners) is false)
                {
                    PrintUsage();

                    return 1;
                }

                options = options with { IncludeGenerationWinners = includeWinners };
                continue;
            }

            if (option == "--strategy-family")
            {
                if (value is not ("threshold" or "expectimax" or "hybrid"))
                {
                    PrintUsage();

                    return 1;
                }

                options = options with { StrategyFamily = value switch { "expectimax" => "Expectimax", "hybrid" => "Hybrid", _ => "Threshold" } };
                continue;
            }

            if (option == "--mutation-rate")
            {
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mutationRate) is false || mutationRate is not (>= 0 and <= 1))
                {
                    PrintUsage();

                    return 1;
                }

                options = options with { MutationRate = mutationRate };
                continue;
            }

            if (int.TryParse(value, out var number) is false || number < 1)
            {
                PrintUsage();

                return 1;
            }

            options = option switch
            {
                "--population" => options with { Population = number },
                "--generations" => options with { Generations = number },
                "--games" => options with { Games = number },
                "--validation-games" => options with { ValidationGames = number },
                "--test-games" => options with { TestGames = number },
                "--seed" => options with { Seed = number },
                "--players" => options with { MaximumPlayers = number, VaryPlayerCount = false },
                "--max-players" => options with { MaximumPlayers = number, VaryPlayerCount = true },
                _ => options
            };

            if (new[] { "--population", "--generations", "--games", "--validation-games", "--test-games", "--seed", "--players", "--max-players" }.Contains(option) is false)
            {
                PrintUsage();

                return 1;
            }
        }

        if (options.Population < 2 || options.MaximumPlayers is not (>= 2 and <= 6))
        {
            PrintUsage();

            return 1;
        }

        options = options with { OpponentNames = [.. options.OpponentNames, .. options.CustomOpponents.Select(opponent => opponent.Name)] };
        DiceGame.Strategies.IPlayerStrategy[] selectedOpponents;

        try
        {
            selectedOpponents = GeneticOptimizer.ResolveOpponents(options);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            Console.WriteLine("Select at least one known opponent: Passive, Bold, Adaptive, Expectimax, or a loaded opponent file.");

            return 1;
        }

        Console.WriteLine($"Training {options.Population} candidates for {options.Generations} generations, seed {options.Seed}, mutation rate {options.MutationRate:P0} per gene.");
        Console.WriteLine(options.VaryPlayerCount ? $"Tables vary from 2 to {options.MaximumPlayers} players." : $"Tables have {options.MaximumPlayers} players.");
        Console.WriteLine($"Strategy family: {options.StrategyFamily}");
        Console.WriteLine($"Opponents: {string.Join(", ", selectedOpponents.Select(opponent => opponent.Name))}; generation winners: {options.IncludeGenerationWinners}");
        var result = GeneticOptimizer.Train(options, progress => Console.WriteLine($"Generation {progress.Generation,3}: training {progress.Training.WinShare:P2}, validation {progress.Validation.WinShare:P2}"));
        var testSeed = unchecked(options.Seed ^ 0x617B2E43);
        Console.WriteLine("\nIndependent test using the same table-size settings. Win share ± approximate 95% margin.");
        var evolved = EvaluateStrategy("Evolved", result.Genes.CreateStrategy(), options, testSeed);
        var adaptive = EvaluateStrategy("Adaptive", StrategyGenes.Adaptive.CreateStrategy(), options, testSeed);
        var expectimax = EvaluateStrategy("Expectimax", new DiceGame.Strategies.ExpectimaxStrategy(), options, testSeed);
        var artifact = new TrainingArtifact(result, evolved, adaptive, expectimax);
        File.WriteAllText(output, JsonSerializer.Serialize(artifact, jsonOptions));
        Console.WriteLine($"Saved {Path.GetFullPath(output)}");

        return 0;
    }

    private static void PrintEvaluation(string name, StrategyEvaluation evaluation) => Console.WriteLine($"{name,-12} {evaluation.WinShare:P2} ± {1.96 * evaluation.StandardError:P2}, average score {evaluation.AverageScore:F1} ({evaluation.Games} games)");

    private static StrategyEvaluation EvaluateStrategy(string name, DiceGame.Strategies.IPlayerStrategy strategy, TrainingOptions options, int seed)
    {
        PlayerCountEvaluation[] byPlayerCount = [];
        var evaluation = StrategyEvaluator.EvaluateAsync(strategy, GeneticOptimizer.ResolveOpponents(options), options.TestGames, seed, yieldToBrowser: false, reportMatchup: current => byPlayerCount = current.ByPlayerCount, minimumPlayers: options.VaryPlayerCount ? 2 : options.MaximumPlayers, maximumPlayers: options.MaximumPlayers).GetAwaiter().GetResult();
        PrintEvaluation(name, evaluation);

        foreach (var table in byPlayerCount)
        {
            Console.WriteLine($"  {table.PlayerCount} players: {table.WinShare:P2}, average {table.AverageScore:F1}, {table.Games} games");
        }

        return evaluation;
    }

    private static StrategyFile LoadOpponent(string path, TrainingOptions options)
    {
        var opponent = StrategyFile.FromJson(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
        var names = GeneticOptimizer.Baselines.Select(strategy => strategy.Name).Concat(options.CustomOpponents.Select(strategy => strategy.Name)).ToArray();
        var uniqueName = opponent.Name;
        var suffix = 2;

        while (names.Contains(uniqueName, StringComparer.OrdinalIgnoreCase))
        {
            var ending = $" ({suffix++})";
            uniqueName = opponent.Name[..Math.Min(opponent.Name.Length, 60 - ending.Length)] + ending;
        }

        return opponent with { Name = uniqueName };
    }

    private static void PrintUsage() => Console.WriteLine("Usage: dotnet run --project Simulator -c Release -- train [--strategy-family hybrid|threshold|expectimax] [--opponents Adaptive,Expectimax] [--opponent-file strategy.json] [--generation-winners true|false] [--population 40] [--generations 20] [--games 500] [--validation-games 2000] [--test-games 10000] [--seed 20261121] [--mutation-rate 0.2] [--players 5 | --max-players 6] [--output evolved-strategy.json]\n       dotnet run --project Simulator -c Release -- evaluate evolved-strategy.json\nNew training defaults to Hybrid against Adaptive and Expectimax. Opponent files may be repeated; use --opponents none for file-only opponents. Earlier generation winners are training-only and enabled by default. Player counts must be 2–6. Mutation rate must be between 0 and 1. Population must be at least 2; other numbers must be positive.");
}

public sealed record TrainingArtifact(TrainingResult Training, StrategyEvaluation EvolvedTest, StrategyEvaluation AdaptiveTest, StrategyEvaluation ExpectimaxTest);
