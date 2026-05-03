using System.Collections.Generic;

namespace DiceGame;

public record PlayerResult(string Name, int Score);

public record GameResult(IReadOnlyList<PlayerResult> Rankings)
{
    public string Winner => Rankings[0].Name;
    public int WinnerScore => Rankings[0].Score;
}
