using DiceGame.Strategies;
using NUnit.Framework;

namespace DiceGame.Tests;

[TestFixture]
public sealed class InteractiveGameTests
{
    [Test]
    public void AdaptiveBanks300AgainstTwo250PointPlayersBeforeRollingTheLastDie()
    {
        var rolls = new Queue<int>([6, 1, 1, 4, 5, 2, 1, 6, 2, 4, 3, 5, 4, 2, 4, 6, 2, 6, 1, 1, 6, 2, 6, 3, 6, 1]);
        var elsa = new Player("Elsa", new AdaptiveStrategy());
        var human = new Player("Player 1", new PassiveStrategy(), isHuman: true);
        var alma = new Player("Alma", new PassiveStrategy());
        human.TotalScore.Add(250);
        alma.TotalScore.Add(250);
        var match = new InteractiveGame([elsa, human, alma], rolls.Dequeue);
        match.RollOpening();

        while (match.Phase is not MatchPhase.TurnEnded)
        {
            match.PlayComputerStep();
        }

        Assert.That(elsa.TotalScore.Value, Is.EqualTo(300));
        Assert.That(match.Hand.Count, Is.EqualTo(1));
        Assert.That(match.Message, Is.EqualTo("Elsa: banked 300 points."));
        Assert.That(rolls.Peek(), Is.EqualTo(2));
        Assert.That(rolls.Count, Is.EqualTo(13));
    }

    [TestCase(2, 400)]
    [TestCase(5, 450)]
    [TestCase(3, 0)]
    public void Inherited350PointsAndFiveDiceRequireAScoringRoll(int face, int expectedScore)
    {
        var rolls = new Queue<int>([6, 1, 2, 2, 2, 5, 5, face, 3, 3, 3, 3]);
        var match = CreateMatch(rolls);
        match.RollOpening();
        match.Roll();
        match.KeepDice(new DiceSelection(0, 3, 2));
        match.Bank();
        match.NextTurn();
        match.ChooseInheritance(true);
        Assert.That(match.Hand.Count, Is.EqualTo(5));
        Assert.That(match.TurnScore, Is.EqualTo(350));
        Assert.That(match.CanBank, Is.False);
        Assert.Throws<InvalidOperationException>(() => match.Bank());
        match.Roll();
        Assert.That(match.CanBank, Is.False);

        if (face is 2 or 5)
        {
            match.KeepDice(new DiceSelection(0, face == 2 ? 1 : 0, face == 5 ? 1 : 0));
            Assert.That(match.CanBank, Is.True);
            match.Bank();
        }

        Assert.That(match.Phase, Is.EqualTo(MatchPhase.TurnEnded));
        Assert.That(match.TurnScore, Is.EqualTo(expectedScore));
        Assert.That(match.CurrentPlayer.TotalScore.Value, Is.EqualTo(expectedScore));
    }

    [Test]
    public void ComputerMustRollInheritedPointsEvenWhenItsStoppingThresholdIsReached()
    {
        var rolls = new Queue<int>([6, 1, 2, 2, 2, 5, 5, 2, 3, 3, 3, 3]);
        var strategy = new PassiveStrategy() with { TakeInheritanceMinScore = 250 };
        var match = new InteractiveGame([new Player("Player 1", strategy, isHuman: true), new Player("Alma", strategy)], rolls.Dequeue);
        match.RollOpening();
        match.Roll();
        match.KeepDice(new DiceSelection(0, 3, 2));
        match.Bank();
        match.NextTurn();
        match.PlayComputerStep();
        Assert.That(match.ComputerWillRoll, Is.True);
        match.PlayComputerStep();
        Assert.That(match.Phase, Is.EqualTo(MatchPhase.SelectDice));
        match.PlayComputerStep();
        match.PlayComputerStep();

        Assert.That(match.CurrentPlayer.TotalScore.Value, Is.EqualTo(400));
    }

    [Test]
    public void OpeningWinnerIsAnnouncedWithTheRolledFace()
    {
        var match = CreateMatch(new Queue<int>([1, 6]));
        match.RollOpening();

        Assert.That(match.Message, Is.EqualTo("Computer 1 scored a six and starts the game!"));
        Assert.That(match.History[0], Is.EqualTo(match.Message));
    }

    [Test]
    public void PassiveStartsFreshWhenOffered1000PointsAndTwoDice()
    {
        var match = CreateMatch(new Queue<int>([6, 1, 1, 1, 1, 3, 4, 3, 3, 3, 3, 3]));
        match.RollOpening();
        match.Roll();
        match.KeepDice(new DiceSelection(3, 0, 0));
        match.Bank();
        match.NextTurn();
        Assert.That(match.InheritedScore, Is.EqualTo(1000));
        Assert.That(match.InheritedDiceCount, Is.EqualTo(2));
        match.PlayComputerStep();
        Assert.That(match.TurnScore, Is.Zero);
        Assert.That(match.Hand.Count, Is.EqualTo(5));
        match.PlayComputerStep();

        Assert.That(match.Phase, Is.EqualTo(MatchPhase.TurnEnded));
    }

    [Test]
    public void HumanVictoryNamesTheWinnerAndIncludesOneLastRoundAnnouncement()
    {
        var rolls = new Queue<int>([6, 1]);

        for (var round = 0; round < InteractiveGame.TotalRounds; round++)
        {
            foreach (var face in new[] { 1, 1, 1, 3, 4, 2, 3, 3, 3, 3, 3, 3 })
            {
                rolls.Enqueue(face);
            }
        }

        var match = CreateMatch(rolls);
        match.RollOpening();

        for (var round = 0; round < InteractiveGame.TotalRounds; round++)
        {
            if (match.Phase == MatchPhase.ChooseInheritance)
            {
                match.ChooseInheritance(false);
            }

            match.Roll();
            match.KeepDice(new DiceSelection(3, 0, 0));
            match.Roll();
            match.KeepDice(new DiceSelection(0, 1, 0));
            match.Bank();
            match.NextTurn();
            match.PlayComputerStep();
            match.PlayComputerStep();
            match.NextTurn();
        }

        Assert.That(match.Message, Is.EqualTo("Player 1 wins with 10500 points."));
        Assert.That(match.History.Count(entry => entry.StartsWith("Last round!")), Is.EqualTo(1));
        Assert.That(match.History.Count, Is.GreaterThan(60));
        Assert.That(match.History.Last(), Does.Contain("Player 1: 6"));
    }

    [Test]
    public void OpeningRerollsOnlyTiedLeadersUntilOneRemains()
    {
        var rolls = new Queue<int>([2, 6, 6, 4, 4, 2, 5]);
        var match = CreateMatch(rolls, 3);
        match.RollOpening();
        Assert.That(match.Phase, Is.EqualTo(MatchPhase.Opening));
        Assert.That(match.OpeningRolls.Where(roll => roll.IsContender).Select(roll => roll.PlayerName), Is.EqualTo(new[] { "Computer 1", "Computer 2" }));
        match.RollOpening();
        Assert.That(match.OpeningRolls.Select(roll => roll.PlayerName), Is.EqualTo(new[] { "Computer 1", "Computer 2" }));
        Assert.That(match.Phase, Is.EqualTo(MatchPhase.Opening));
        match.RollOpening();
        Assert.That(match.CurrentPlayer.Name, Is.EqualTo("Computer 2"));
        Assert.That(match.Players.Select(player => player.Name), Is.EqualTo(new[] { "Computer 2", "Player 1", "Computer 1" }));
        Assert.That(match.Phase, Is.EqualTo(MatchPhase.ReadyToRoll));
        Assert.That(rolls, Is.Empty);
    }

    [Test]
    public void BankingRequires250PointsAndPassesPositionToNextPlayer()
    {
        var match = CreateMatch(new Queue<int>([6, 1, 5, 5, 2, 3, 4, 2, 3]));
        match.RollOpening();
        match.Roll();
        Assert.Throws<InvalidOperationException>(() => match.Bank());
        match.KeepDice(new DiceSelection(0, 1, 2));
        Assert.That(match.TurnScore, Is.EqualTo(250));
        Assert.That(match.Hand, Is.EqualTo(new[] { 3, 4 }));
        match.Bank();
        Assert.That(match.Players[0].TotalScore.Value, Is.EqualTo(250));
        match.NextTurn();
        Assert.That(match.Phase, Is.EqualTo(MatchPhase.ChooseInheritance));
        match.ChooseInheritance(true);
        Assert.That(match.TurnScore, Is.EqualTo(250));
        Assert.That(match.Table, Is.EqualTo(new[] { 2, 5, 5 }));
        Assert.That(match.Hand.Count, Is.EqualTo(2));
        Assert.That(match.CanBank, Is.False);
        Assert.Throws<InvalidOperationException>(() => match.Bank());
        match.Roll();
        Assert.That(match.CanBank, Is.False);
        match.KeepDice(new DiceSelection(0, 1, 0));
        Assert.That(match.CanBank, Is.True);
    }

    [Test]
    public void LowScoreCannotBeBankedAfterKeepingDice()
    {
        var match = CreateMatch(new Queue<int>([6, 1, 2, 3, 4, 6, 1]));
        match.RollOpening();
        match.Roll();
        match.KeepDice(new DiceSelection(0, 1, 0));
        Assert.That(match.TurnScore, Is.EqualTo(50));
        Assert.That(match.CanBank, Is.False);
        Assert.Throws<InvalidOperationException>(() => match.Bank());
    }

    [Test]
    public void BustLosesInheritedPointsAndDoesNotPassThemOn()
    {
        var match = CreateMatch(new Queue<int>([6, 1, 1, 5, 5, 2, 3, 4, 6, 3]), 3);
        match.RollOpening();
        match.Roll();
        match.KeepDice(new DiceSelection(0, 1, 2));
        match.Bank();
        match.NextTurn();
        match.ChooseInheritance(true);
        match.Roll();
        Assert.That(match.Phase, Is.EqualTo(MatchPhase.TurnEnded));
        Assert.That(match.TurnScore, Is.Zero);
        Assert.That(match.Players[0].TotalScore.Value, Is.EqualTo(250));
        match.NextTurn();
        Assert.That(match.Phase, Is.EqualTo(MatchPhase.ReadyToRoll));
        Assert.That(match.Hand.Count, Is.EqualTo(5));
        Assert.That(match.TurnScore, Is.Zero);
    }

    [Test]
    public void InheritanceCanBeDeclined()
    {
        var match = CreateMatch(new Queue<int>([6, 1, 5, 5, 2, 3, 4]));
        match.RollOpening();
        match.Roll();
        match.KeepDice(new DiceSelection(0, 1, 2));
        match.Bank();
        match.NextTurn();
        match.ChooseInheritance(false);
        Assert.That(match.TurnScore, Is.Zero);
        Assert.That(match.Table, Is.Empty);
        Assert.That(match.Hand.Count, Is.EqualTo(5));
        Assert.That(match.InheritedScore, Is.Zero);
    }

    [Test]
    public void KeepingAllDiceRefreshesTheHandWithoutLosingPoints()
    {
        var match = CreateMatch(new Queue<int>([6, 1, 1, 1, 1, 2, 5]));
        match.RollOpening();
        match.Roll();
        match.KeepDice(new DiceSelection(3, 1, 1));
        Assert.That(match.TurnScore, Is.EqualTo(1150));
        Assert.That(match.Hand.Count, Is.EqualTo(5));
        Assert.That(match.Table, Is.Empty);
        match.Bank();
        match.NextTurn();
        Assert.That(match.InheritedDiceCount, Is.EqualTo(5));
        match.ChooseInheritance(true);
        Assert.That(match.TurnScore, Is.EqualTo(1150));
        Assert.That(match.Hand.Count, Is.EqualTo(5));
    }

    [TestCase(1, 0, 0)]
    [TestCase(2, 0, 0)]
    [TestCase(4, 0, 0)]
    [TestCase(0, -1, 1)]
    [TestCase(0, 2, 0)]
    [TestCase(0, 0, 2)]
    [TestCase(0, 0, 0)]
    public void InvalidSelectionsCannotChangeTheScore(int ones, int twos, int fives)
    {
        var match = CreateMatch(new Queue<int>([6, 1, 1, 1, 1, 2, 5]));
        match.RollOpening();
        match.Roll();
        Assert.Throws<InvalidOperationException>(() => match.KeepDice(new DiceSelection(ones, twos, fives)));
        Assert.That(match.TurnScore, Is.Zero);
        Assert.That(match.Hand.Count, Is.EqualTo(5));
    }

    [Test]
    public void MatchEndsAfterEveryPlayerHasPlayedTenTurnsAndSupportsSharedVictory()
    {
        var openingRolls = new Queue<int>([6, 1]);
        var match = new InteractiveGame([new Player("Player 1", new PassiveStrategy(), isHuman: true), new Player("Computer", new PassiveStrategy())], () => openingRolls.Count > 0 ? openingRolls.Dequeue() : 3);
        match.RollOpening();

        for (var turn = 0; turn < 20; turn++)
        {
            Assert.That(match.CurrentRound, Is.EqualTo(turn / 2 + 1));
            match.Roll();
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.TurnEnded));
            match.NextTurn();
        }

        Assert.That(match.Phase, Is.EqualTo(MatchPhase.Finished));
        Assert.That(match.CurrentRound, Is.EqualTo(10));
        Assert.That(match.Winners.Count, Is.EqualTo(2));
        Assert.Throws<InvalidOperationException>(() => match.Roll());
    }

    [TestCase(2, 1)]
    [TestCase(3, 1)]
    [TestCase(6, 1)]
    [TestCase(3, 2)]
    [TestCase(6, 3)]
    [TestCase(6, 6)]
    public void ExistingStrategiesCompleteFullMatches(int playerCount, int humanPlayerCount)
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var random = new Random(seed);
            IPlayerStrategy[] strategies = [new PassiveStrategy(), new BoldStrategy(), new AdaptiveStrategy()];
            var players = Enumerable.Range(0, playerCount).Select(index => new Player($"Player {index + 1}", strategies[index % strategies.Length], isHuman: index < humanPlayerCount)).ToArray();
            var match = new InteractiveGame(players, () => random.Next(1, 7));
            var steps = 0;
            var turns = new Dictionary<string, int>();

            while (match.Phase is not MatchPhase.Finished && steps++ < 10000)
            {
                if (match.Phase == MatchPhase.Opening)
                {
                    match.RollOpening();
                }
                else if (match.Phase == MatchPhase.TurnEnded)
                {
                    turns[match.CurrentPlayer.Name] = turns.GetValueOrDefault(match.CurrentPlayer.Name) + 1;
                    match.NextTurn();
                }
                else if (match.IsHumanTurn is false)
                {
                    match.PlayComputerStep();
                }
                else
                {
                    switch (match.Phase)
                    {
                        case MatchPhase.ChooseInheritance:
                            match.ChooseInheritance(true);
                            break;
                        case MatchPhase.ReadyToRoll:
                            if (match.CanBank)
                            {
                                match.Bank();
                            }
                            else
                            {
                                match.Roll();
                            }

                            break;
                        case MatchPhase.SelectDice:
                            match.KeepDice(new DiceSelection(match.Hand.Count(value => value == 1) >= 3 ? 3 : 0, match.Hand.Count(value => value == 2), match.Hand.Count(value => value == 5)));
                            break;
                    }
                }
            }

            Assert.That(match.Phase, Is.EqualTo(MatchPhase.Finished), $"Seed {seed}");
            Assert.That(turns.Values, Is.All.EqualTo(10));
            Assert.That(turns.Count, Is.EqualTo(playerCount));
            Assert.That(match.Winners[0].TotalScore.Value, Is.EqualTo(match.Players.Max(player => player.TotalScore.Value)));
        }
    }

    private static InteractiveGame CreateMatch(Queue<int> rolls, int playerCount = 2)
    {
        var players = Enumerable.Range(0, playerCount).Select(index => new Player(index == 0 ? "Player 1" : $"Computer {index}", new PassiveStrategy(), isHuman: index == 0));

        return new InteractiveGame(players, rolls.Dequeue);
    }
}
