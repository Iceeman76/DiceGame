using System.Collections.Generic;

namespace DiceGame;

public sealed record PlayerResult(string Name, int Score);

public sealed record GameResult(IReadOnlyList<PlayerResult> Rankings)
{
    public string Winner => Rankings[0].Name;
    public int WinnerScore => Rankings[0].Score;
}
