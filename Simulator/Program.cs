using System;
using System.Linq;
using DiceGame;

const int simulations = 25_000;

Simulate("Strategi-jämförelse", simulations,
[
    PlayerStrategies.Passive(),
    PlayerStrategies.Bold(),
    PlayerStrategies.Adaptive()
]);

Simulate("Chansa? (annars identisk strategi)", simulations,
[
    MakeAdaptive("Slår alltid", minDice: 1),
    MakeAdaptive("Stannar vid 1 tärning", minDice: 2),
    MakeAdaptive("Stannar vid ≤2 tärningar", minDice: 3)
]);

return;

static ThresholdStrategy MakeAdaptive(string name, int minDice) => PlayerStrategies.Adaptive() with
{
    Name = name,
    MinInheritedHandDiceToRoll = minDice
};

static void Simulate(string title, int n, IPlayerStrategy[] strategies)
{
    var wins = strategies.ToDictionary(s => s.Name, _ => 0);
    var totalScores = strategies.ToDictionary(s => s.Name, _ => 0L);
    var maxScores = strategies.ToDictionary(s => s.Name, _ => 0);

    for (var i = 0; i < n; i++)
    {
        var players = strategies.Select(s => new Player(s.Name, s)).ToArray();
        var result = new Game(players, verbose: false, randomizeOrder: true).Start();

        wins[result.Winner]++;

        foreach (var rank in result.Rankings)
        {
            totalScores[rank.Name] += rank.Score;

            if (rank.Score > maxScores[rank.Name])
            {
                maxScores[rank.Name] = rank.Score;
            }
        }
    }

    Console.WriteLine($"\n{title} ({n:N0} simuleringar):\n");
    Console.WriteLine($"{"Strategi",-28} {"Vinster",8}  {"Vinstprocent",12}  {"Snitt",10}  {"Högsta",12}");
    Console.WriteLine(new string('-', 80));

    foreach (var strategy in strategies)
    {
        var winPct = (double)wins[strategy.Name] / n * 100;
        var avg = totalScores[strategy.Name] / n;

        Console.WriteLine($"{strategy.Name,-28} {wins[strategy.Name],8}  {winPct,11:F1}%  {avg,9}p  {maxScores[strategy.Name],10}p");
    }
}
