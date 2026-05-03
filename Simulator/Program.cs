using System;
using System.Linq;
using DiceGame;

const int simulations = 10_000;

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

static ThresholdStrategy MakeAdaptive(string name, int minDice) => new(name)
{
    StopThreshold = 350,
    StopThresholdWhenLeading = 250,
    StopThresholdWhenTrailing = 550,
    TrailingGapTrigger = 200,
    StopThresholdFinalRounds = 700,
    FinalRoundsCount = 2,
    TakeInheritanceMinScore = 250,
    TakeInheritanceMinHandDice = 2,
    TakeInheritanceWhenTrailing = true,
    MinInheritedHandDiceToRoll = minDice
};

static void Simulate(string title, int n, IPlayerStrategy[] strats)
{
    var wins = strats.ToDictionary(s => s.Name, _ => 0);
    var totalScores = strats.ToDictionary(s => s.Name, _ => 0L);

    for (var i = 0; i < n; i++)
    {
        var players = strats.Select(s => new Player(s.Name, s)).ToArray();
        var result = new Game(players, verbose: false).Start();
        wins[result.Winner]++;
        foreach (var r in result.Rankings)
            totalScores[r.Name] += r.Score;
    }

    Console.WriteLine($"\n{title} ({n:N0} simuleringar):");
    Console.WriteLine($"{"Strategi",-28} {"Vinster",8}  {"Vinstprocent",12}  {"Snittpoäng",10}");
    Console.WriteLine(new string('-', 65));

    foreach (var s in strats)
    {
        var winPct = (double)wins[s.Name] / n * 100;
        var avg = totalScores[s.Name] / n;
        Console.WriteLine($"{s.Name,-28} {wins[s.Name],8}  {winPct,11:F1}%  {avg,9}p");
    }
}
