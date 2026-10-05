using System.Collections.Generic;
using System.Linq;

namespace DiceGame;

public sealed class Dice : List<int>
{
    public Dice(int numDice)
    {
        AddRange(Enumerable.Repeat(0, numDice));
    }

    public void Roll()
    {
        for (var i = 0; i < Count; i++)
        {
            this[i] = Randomizer.Integer(1, 6);
        }
    }

    public override string ToString() => $"{string.Join(", ", this)} ({Score()}p)";

    public int Score()
    {
        var ones = this.Count(face => face == 1);
        var twos = this.Count(face => face == 2);
        var fives = this.Count(face => face == 5);

        return new DiceSelection(ones >= 3 ? 3 : 0, twos, fives).Score;
    }
}
