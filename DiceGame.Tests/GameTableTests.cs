using Microsoft.Extensions.DependencyInjection;
using DiceGame.Strategies;
using Bunit;
using DiceGame.Web.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace DiceGame.Tests;

[TestFixture]
public sealed class GameTableTests
{
    [TestCase("load")]
    [TestCase("save")]
    [TestCase("enable")]
    public async Task JavaScriptFailureDoesNotPreventStartingTheMatch(string operation)
    {
        await using var context = new BunitContext();
        context.Services.AddSingleton<StrategyLibrary>();
        var audio = context.JSInterop.SetupModule("./js/dice-audio.js");
        audio.Mode = JSRuntimeMode.Loose;
        var preferences = context.JSInterop.SetupModule("./js/player-preferences.js");
        preferences.Mode = JSRuntimeMode.Loose;
        var exception = new Microsoft.JSInterop.JSException("Browser operation failed.");

        if (operation is "load")
        {
            preferences.Setup<PlayerNamePreferences?>("load").SetException(exception);
        }
        else if (operation is "save")
        {
            preferences.SetupVoid("save", _ => true).SetException(exception);
        }
        else
        {
            audio.SetupVoid("enable").SetException(exception);
        }

        var component = context.Render<GameTable>();
        await component.Find(".start-button").ClickAsync(new MouseEventArgs());

        Assert.That(component.FindAll(".opening-table"), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SavedNamesLoadForHumansAndComputersAndRemainAfterReset()
    {
        await using var context = CreateContext(new PlayerNamePreferences { HumanNames = ["Benny", "Emma"], ComputerNames = ["Ada", "Grace"] });
        var component = context.Render<GameTable>();
        await component.Find("#player-count").ChangeAsync("4");
        await component.Find("#human-player-count").ChangeAsync("2");
        Assert.That(component.Find("#human-name-1").GetAttribute("value"), Is.EqualTo("Benny"));
        Assert.That(component.Find("#human-name-2").GetAttribute("value"), Is.EqualTo("Emma"));
        Assert.That(component.Find("#computer-name-Alma").GetAttribute("value"), Is.EqualTo("Ada"));
        await component.Find("#human-name-1").ChangeAsync("Benjamin");
        await component.Find("#computer-name-Alma").ChangeAsync("Alice");
        await component.Find(".start-button").ClickAsync(new MouseEventArgs());
        Assert.That(component.Find(".scoreboard").TextContent, Does.Contain("Benjamin").And.Contain("Emma").And.Contain("Alice").And.Contain("Grace"));
        await component.Find(".history-card > .text-button").ClickAsync(new MouseEventArgs());
        await component.Find(".reset-confirmation .secondary").ClickAsync(new MouseEventArgs());

        Assert.That(component.Find("#human-name-1").GetAttribute("value"), Is.EqualTo("Benjamin"));
        Assert.That(component.Find("#computer-name-Alma").GetAttribute("value"), Is.EqualTo("Alice"));
    }

    [TestCase("")]
    [TestCase("Alma")]
    public async Task EmptyOrDuplicateNamesPreventStartingTheMatch(string name)
    {
        await using var context = CreateContext();
        var component = context.Render<GameTable>();
        await component.Find("#human-name-1").ChangeAsync(name);

        Assert.That(component.Find(".start-button").HasAttribute("disabled"), Is.True);
        Assert.That(component.Find(".name-validation").TextContent, Is.Not.Empty);
    }

    [Test]
    public async Task TurboCompletesAComputerTurnInLessThanTheNormalDelay()
    {
        await using var context = CreateContext();
        var component = context.Render<GameTable>();
        await component.Find("[role='switch']").ClickAsync(new MouseEventArgs());
        Assert.That(component.Find("[role='switch']").GetAttribute("aria-checked"), Is.EqualTo("true"));
        await component.Find(".start-button").ClickAsync(new MouseEventArgs());
        var rolls = new Queue<int>([1, 6]);
        var match = new InteractiveGame([new Player("Player 1", new PassiveStrategy(), isHuman: true), new Player("Alma", new PassiveStrategy())], () => rolls.Count > 0 ? rolls.Dequeue() : 3);
        typeof(GameTable).GetField("match", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(component.Instance, match);
        await component.Find(".game-actions .primary").ClickAsync(new MouseEventArgs());
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await component.Find(".game-actions .primary").ClickAsync(new MouseEventArgs());
        stopwatch.Stop();

        Assert.That(match.IsHumanTurn, Is.True);
        Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));
        Assert.That(component.Find(".active-table h2").TextContent, Is.EqualTo("Player 1"));
    }

    [TestCase(2, 0)]
    [TestCase(6, 0)]
    [TestCase(3, 2)]
    [TestCase(2, 2)]
    [TestCase(6, 6)]
    public async Task HumanPlayerCountUpdatesNamesAndComputerSeats(int playerCount, int humanPlayerCount)
    {
        await using var context = CreateContext();
        var component = context.Render<GameTable>();
        await component.Find("#player-count").ChangeAsync(playerCount.ToString());
        await component.Find("#human-player-count").ChangeAsync(humanPlayerCount.ToString());
        Assert.That(component.FindAll(".score-row").Count, Is.EqualTo(playerCount));
        Assert.That(component.FindAll(".score-avatar.human").Count, Is.EqualTo(humanPlayerCount));
        Assert.That(component.FindAll(".opponent-setting").Count, Is.EqualTo(playerCount - humanPlayerCount));
        Assert.That(component.FindAll(".score-row").Take(humanPlayerCount).Select(row => row.QuerySelector("strong")!.TextContent), Is.EqualTo(Enumerable.Range(1, humanPlayerCount).Select(number => $"Player {number}")));
        await component.Find(".start-button").ClickAsync(new MouseEventArgs());

        Assert.That(component.FindAll(".opening-player").Count, Is.EqualTo(playerCount));
        Assert.That(component.FindAll(".score-avatar.human").Count, Is.EqualTo(humanPlayerCount));
        Assert.That(component.Markup, Does.Not.Contain(">You<"));
    }

    [Test]
    public async Task ReducingTotalPlayersClampsHumanCount()
    {
        await using var context = CreateContext();
        var component = context.Render<GameTable>();
        await component.Find("#player-count").ChangeAsync("6");
        await component.Find("#human-player-count").ChangeAsync("6");
        await component.Find("#player-count").ChangeAsync("2");

        Assert.That(component.FindAll("#human-player-count option").Count, Is.EqualTo(3));
        Assert.That(component.FindAll(".score-row").Count, Is.EqualTo(2));
        Assert.That(component.FindAll(".score-avatar.human").Count, Is.EqualTo(2));
        Assert.That(component.FindAll(".opponent-setting"), Is.Empty);
    }

    [Test]
    public async Task BankingHandsControlToTheSecondHumanAndWaitsForTheirDecision()
    {
        await using var context = CreateContext();
        var component = context.Render<GameTable>();
        await component.Find("#player-count").ChangeAsync("2");
        await component.Find("#human-player-count").ChangeAsync("2");
        await component.Find(".start-button").ClickAsync(new MouseEventArgs());
        var rolls = new Queue<int>([6, 1, 5, 5, 2, 3, 4, 2, 3]);
        var players = new[] { new Player("Player 1", new PassiveStrategy(), isHuman: true), new Player("Player 2", new PassiveStrategy(), isHuman: true) };
        var match = new InteractiveGame(players, rolls.Dequeue);
        match.RollOpening();
        match.Roll();
        match.KeepDice(new DiceSelection(0, 1, 2));
        typeof(GameTable).GetField("match", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(component.Instance, match);
        await component.Find(".icon-button").ClickAsync(new MouseEventArgs());
        await component.Find(".game-actions .secondary").ClickAsync(new MouseEventArgs());
        Assert.That(match.CurrentPlayer.Name, Is.EqualTo("Player 2"));
        Assert.That(match.IsHumanTurn, Is.True);
        Assert.That(component.Find(".active-table h2").TextContent, Is.EqualTo("Player 2"));
        Assert.That(component.Find(".game-actions .primary").TextContent, Is.EqualTo("Take over 250 pts"));
        Assert.Throws<InvalidOperationException>(() => match.PlayComputerStep());
        await Task.Delay(900);
        Assert.That(rolls.Count, Is.EqualTo(2));
        await component.Find(".game-actions .primary").ClickAsync(new MouseEventArgs());
        Assert.That(component.FindAll(".game-actions .secondary"), Is.Empty);
        await component.Find(".game-actions .primary").ClickAsync(new MouseEventArgs());
        await component.Find(".game-actions .primary").ClickAsync(new MouseEventArgs());
        await component.Find(".game-actions .secondary").ClickAsync(new MouseEventArgs());

        Assert.That(match.CurrentPlayer.Name, Is.EqualTo("Player 1"));
        Assert.That(match.CurrentRound, Is.EqualTo(2));
        Assert.That(players[1].TotalScore.Value, Is.EqualTo(300));
        Assert.That(component.Find(".active-table h2").TextContent, Is.EqualTo("Player 1"));
    }

    [TestCase(2, true, 0)]
    [TestCase(3, true, 1)]
    [TestCase(4, true, 1)]
    [TestCase(5, true, 1)]
    [TestCase(3, false, 0)]
    public async Task HumanRollAnnouncesThreeOnesWhenSoundIsEnabled(int onesCount, bool soundEnabled, int expectedAnnouncements)
    {
        await using var context = new BunitContext();
        context.Services.AddSingleton<StrategyLibrary>();
        var audio = context.JSInterop.SetupModule("./js/dice-audio.js");
        audio.Mode = JSRuntimeMode.Loose;
        context.JSInterop.SetupModule("./js/player-preferences.js").Mode = JSRuntimeMode.Loose;
        var component = context.Render<GameTable>();
        await component.Find(".start-button").ClickAsync(new MouseEventArgs());
        var rolls = new Queue<int>(new[] { 6, 1 }.Concat(Enumerable.Repeat(1, onesCount)).Concat(Enumerable.Repeat(3, 5 - onesCount)));
        var match = new InteractiveGame([new Player("Player 1", new PassiveStrategy(), isHuman: true), new Player("Alma", new PassiveStrategy())], rolls.Dequeue);
        match.RollOpening();
        typeof(GameTable).GetField("match", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(component.Instance, match);
        await component.Find(".icon-button").ClickAsync(new MouseEventArgs());

        if (soundEnabled)
        {
            await component.Find(".icon-button").ClickAsync(new MouseEventArgs());
        }

        await component.Find(".game-actions .primary").ClickAsync(new MouseEventArgs());

        Assert.That(audio.Invocations.Count(invocation => invocation.Identifier == "play" && invocation.Arguments[0] is "three-ones"), Is.EqualTo(expectedAnnouncements));
    }

    [Test]
    public async Task ComputerTripleOnesAreAnnouncedOnce()
    {
        await using var context = new BunitContext();
        context.Services.AddSingleton<StrategyLibrary>();
        var audio = context.JSInterop.SetupModule("./js/dice-audio.js");
        audio.Mode = JSRuntimeMode.Loose;
        context.JSInterop.SetupModule("./js/player-preferences.js").Mode = JSRuntimeMode.Loose;
        var component = context.Render<GameTable>();
        await component.Find(".start-button").ClickAsync(new MouseEventArgs());
        var rolls = new Queue<int>([1, 6, 1, 1, 1, 3, 3]);
        var match = new InteractiveGame([new Player("Player 1", new PassiveStrategy(), isHuman: true), new Player("Alma", new PassiveStrategy())], rolls.Dequeue);
        typeof(GameTable).GetField("match", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(component.Instance, match);
        await component.Find(".game-actions .primary").ClickAsync(new MouseEventArgs());
        await component.Find(".game-actions .primary").ClickAsync(new MouseEventArgs());

        Assert.That(audio.Invocations.Count(invocation => invocation.Identifier == "play" && invocation.Arguments[0] is "three-ones"), Is.EqualTo(1));
        Assert.That(match.IsHumanTurn, Is.True);
    }

    [Test]
    public async Task InheritedPointsHaveNoBankButtonUntilScoringDiceAreKept()
    {
        await using var context = CreateContext();
        var component = context.Render<GameTable>();
        await component.Find(".start-button").ClickAsync(new MouseEventArgs());
        var rolls = new Queue<int>([6, 1, 2, 2, 2, 5, 5, 2, 3, 3, 3, 3]);
        var match = new InteractiveGame([new Player("Alma", new PassiveStrategy()), new Player("Player 1", new PassiveStrategy(), isHuman: true)], rolls.Dequeue);
        match.RollOpening();
        match.Roll();
        match.KeepDice(new DiceSelection(0, 3, 2));
        match.Bank();
        match.NextTurn();
        typeof(GameTable).GetField("match", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(component.Instance, match);
        await component.Find(".icon-button").ClickAsync(new MouseEventArgs());
        await component.Find(".game-actions .primary").ClickAsync(new MouseEventArgs());
        Assert.That(match.TurnScore, Is.EqualTo(350));
        Assert.That(component.FindAll(".game-actions .secondary"), Is.Empty);
        await component.Find(".game-actions .primary").ClickAsync(new MouseEventArgs());
        await component.Find(".game-actions .primary").ClickAsync(new MouseEventArgs());

        Assert.That(component.Find(".game-actions .secondary").TextContent, Is.EqualTo("Bank 400 pts"));
    }

    [Test]
    public async Task BankingAdvancesThroughComputerTurnWithoutNextPlayerClick()
    {
        await using var context = CreateContext();
        var component = context.Render<GameTable>();
        await component.Find(".start-button").ClickAsync(new MouseEventArgs());
        var rolls = new Queue<int>([6, 1, 2, 2, 2, 5, 5]);
        var match = new InteractiveGame([new Player("Player 1", new PassiveStrategy(), isHuman: true), new Player("Alma", new PassiveStrategy())], () => rolls.Count > 0 ? rolls.Dequeue() : 3);
        match.RollOpening();
        match.Roll();
        match.KeepDice(new DiceSelection(0, 3, 2));
        typeof(GameTable).GetField("match", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(component.Instance, match);
        await component.Find(".icon-button").ClickAsync(new MouseEventArgs());
        var banking = component.Find(".game-actions .secondary").ClickAsync(new MouseEventArgs());
        component.WaitForAssertion(() =>
        {
            Assert.That(component.Find(".action-panel p").TextContent, Is.EqualTo("Player 1: banked 350 points."));
            Assert.That(component.FindAll(".game-actions button"), Is.Empty);
        });
        await banking;

        Assert.That(match.CurrentRound, Is.EqualTo(2));
        Assert.That(match.IsHumanTurn, Is.True);
        Assert.That(match.Players.Single(player => player.Name == "Player 1").TotalScore.Value, Is.EqualTo(350));
        Assert.That(component.Find(".game-actions .primary").TextContent, Is.EqualTo("Roll dice"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task OpeningWinnerRemainsVisibleUntilStartGameIsClicked(bool humanStarts)
    {
        await using var context = CreateContext();
        var component = context.Render<GameTable>();
        await component.Find("#player-count").ChangeAsync("2");
        await component.Find(".start-button").ClickAsync(new MouseEventArgs());
        var openingRolls = new Queue<int>(humanStarts ? [6, 6, 6, 1] : [6, 6, 1, 6]);
        var match = new InteractiveGame([new Player("Player 1", new PassiveStrategy(), isHuman: true), new Player("Alma", new PassiveStrategy())], () => openingRolls.Count > 0 ? openingRolls.Dequeue() : 3);
        typeof(GameTable).GetField("match", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(component.Instance, match);
        await component.Find(".game-actions .primary").ClickAsync(new MouseEventArgs());
        Assert.That(component.Find(".game-actions .primary").TextContent, Is.EqualTo("Roll tiebreaker"));
        await component.Find(".game-actions .primary").ClickAsync(new MouseEventArgs());
        var winnerName = humanStarts ? "Player 1" : "Alma";
        Assert.That(component.Find(".opening-table h2").TextContent, Is.EqualTo($"{winnerName} scored a six and starts the game!"));
        Assert.That(component.Find(".game-actions .primary").TextContent, Is.EqualTo("Start game"));
        Assert.That(component.Find(".game-actions .primary").HasAttribute("disabled"), Is.False);
        await Task.Delay(900);
        Assert.That(component.Find(".opening-table h2").TextContent, Does.Contain(winnerName));
        Assert.That(match.History.Count, Is.EqualTo(3));
        Assert.That(component.FindAll(".active-table"), Is.Empty);
        await component.Find(".game-actions .primary").ClickAsync(new MouseEventArgs());

        Assert.That(component.FindAll(".opening-table"), Is.Empty);
        Assert.That(component.Find(".active-table h2").TextContent, Is.EqualTo("Player 1"));
        Assert.That(component.Find(".game-actions .primary").TextContent, Is.EqualTo("Roll dice"));
    }

    [Test]
    public async Task StrategyOptionsAndDescriptionsComeFromTheAvailableStrategies()
    {
        await using var context = CreateContext();
        var component = context.Render<GameTable>();
        var strategies = new PlayerStrategies();
        Assert.That(component.FindAll("#strategy-Alma option").Select(option => option.GetAttribute("value")), Is.EqualTo(strategies.Available.Select(strategy => strategy.Name)));

        foreach (var strategy in strategies.Available)
        {
            await component.Find("#strategy-Alma").ChangeAsync(strategy.Name);
            Assert.That(component.Find(".opponent-setting p").TextContent, Is.EqualTo(strategy.Description));
            Assert.That(new ComputerSeat("Alma", strategy.Name).CreateStrategy(new StrategyLibrary()).Name, Is.EqualTo(strategy.Name));
        }
    }

    [Test]
    public async Task OpeningRollButtonIsEnabledWhenTheMatchStarts()
    {
        await using var context = CreateContext();
        var component = context.Render<GameTable>();
        await component.Find(".start-button").ClickAsync(new MouseEventArgs());

        Assert.That(component.Find(".game-actions .primary").HasAttribute("disabled"), Is.False);
    }

    [TestCase(2)]
    [TestCase(3)]
    [TestCase(6)]
    public async Task PlayerCountUpdatesBothSetupAndVisibleScoreboard(int playerCount)
    {
        await using var context = CreateContext();
        var component = context.Render<GameTable>();
        await component.Find("#player-count").ChangeAsync(playerCount.ToString());
        Assert.That(component.FindAll(".score-row").Count, Is.EqualTo(playerCount));
        Assert.That(component.FindAll(".opponent-setting").Count, Is.EqualTo(playerCount - 1));
        await component.Find(".start-button").ClickAsync(new MouseEventArgs());
        Assert.That(component.FindAll(".score-row").Count, Is.EqualTo(playerCount));
        Assert.That(component.Find(".opening-table").TextContent, Does.Contain("Who starts?"));
        Assert.That(component.FindAll(".opening-player").Count, Is.EqualTo(playerCount));
        Assert.That(component.FindAll(".setup-card"), Is.Empty);
    }

    [Test]
    public async Task StrategySelectionIsRetainedWhenMatchStarts()
    {
        await using var context = CreateContext();
        var component = context.Render<GameTable>();
        await component.Find("#strategy-Alma").ChangeAsync("Adaptive");
        Assert.That(component.Find(".opponent-setting").TextContent, Does.Contain("Adjusts risk"));
        await component.Find(".start-button").ClickAsync(new MouseEventArgs());
        var almaRow = component.FindAll(".score-row").Single(row => row.TextContent.Contains("Alma"));
        Assert.That(almaRow.TextContent, Does.Contain("Adaptive"));
    }

    [Test]
    public async Task LeavingMatchRequiresConfirmationAndRestoresSetup()
    {
        await using var context = CreateContext();
        var component = context.Render<GameTable>();
        await component.Find(".start-button").ClickAsync(new MouseEventArgs());
        await component.Find(".history-card > .text-button").ClickAsync(new MouseEventArgs());
        Assert.That(component.Find(".reset-confirmation").TextContent, Does.Contain("End this game"));
        await component.Find(".reset-confirmation .secondary").ClickAsync(new MouseEventArgs());
        Assert.That(component.FindAll(".setup-card").Count, Is.EqualTo(1));
        Assert.That(component.FindAll(".scoreboard").Count, Is.EqualTo(1));
        Assert.That(component.FindAll(".opening-table"), Is.Empty);
    }

    private static BunitContext CreateContext(PlayerNamePreferences? savedNames = null)
    {
        var context = new BunitContext();
        context.Services.AddSingleton<StrategyLibrary>();
        context.JSInterop.SetupModule("./js/dice-audio.js").Mode = JSRuntimeMode.Loose;
        var preferences = context.JSInterop.SetupModule("./js/player-preferences.js");
        preferences.Mode = JSRuntimeMode.Loose;
        preferences.Setup<PlayerNamePreferences?>("load").SetResult(savedNames);

        return context;
    }
}
