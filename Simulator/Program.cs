using System;
using System.Linq;
using DiceGame;

const int simulations = 10_000;

var strategies = new IPlayerStrategy[]
{
    PlayerStrategies.Passive(),
    PlayerStrategies.Bold(),
    PlayerStrategies.Adaptive()
};

var wins = strategies.ToDictionary(s => s.Name, _ => 0);
var totalScores = strategies.ToDictionary(s => s.Name, _ => 0L);

for (var i = 0; i < simulations; i++)
{
    var players = strategies.Select(s => new Player(s.Name, s)).ToArray();
    var result = new Game(players, verbose: false).Start();

    wins[result.Winner]++;
    foreach (var r in result.Rankings)
        totalScores[r.Name] += r.Score;
}

Console.WriteLine($"Results after {simulations:N0} simulations:\n");
Console.WriteLine($"{"Strategi",-12} {"Vinster",8}  {"Vinstprocent",12}  {"Snittpoäng",10}");
Console.WriteLine(new string('-', 50));
foreach (var s in strategies)
{
    var winPct = (double)wins[s.Name] / simulations * 100;
    var avg = totalScores[s.Name] / simulations;
    Console.WriteLine($"{s.Name,-12} {wins[s.Name],8}  {winPct,11:F1}%  {avg,9}p");
}
