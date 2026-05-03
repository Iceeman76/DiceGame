using System;
using System.Collections.Generic;

namespace DiceGame;

public static class Randomizer
{
    private static readonly Random Rnd = new();

    public static int Integer(int min, int max)
    {
        lock (Rnd)
        {
            return Rnd.Next(min, max + 1);
        }
    }

    public static void Shuffle<T>(IList<T> list)
    {
        lock (Rnd)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = Rnd.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
