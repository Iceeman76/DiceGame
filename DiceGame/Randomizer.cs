using System;

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
}
