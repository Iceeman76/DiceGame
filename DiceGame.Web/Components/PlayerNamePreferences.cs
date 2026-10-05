namespace DiceGame.Web.Components;

public sealed record PlayerNamePreferences
{
    public string[] HumanNames { get; init; } = [];
    public string[] ComputerNames { get; init; } = [];
}
