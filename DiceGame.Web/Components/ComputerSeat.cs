using DiceGame.Strategies;
namespace DiceGame.Web.Components;

public sealed class ComputerSeat(string name, string strategy)
{
    public string Id { get; } = name;
    public string Name { get; set; } = name;
    public string Strategy { get; set; } = strategy;

    public IPlayerStrategy CreateStrategy(PlayerStrategies strategies) => strategies.Get(Strategy);
}
