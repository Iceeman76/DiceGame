using Microsoft.Extensions.DependencyInjection;
using Bunit;
using DiceGame.Strategies;
using DiceGame.Web.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;

namespace DiceGame.Tests;

[TestFixture]
public sealed class MatchPresentationTests
{
    [Test]
    public async Task LeadHintUpdatesAsPointsAreKeptAndBankingBecomesEnough()
    {
        await using var context = CreateContext();
        var component = context.Render<GameTable>();
        await component.Find(".start-button").ClickAsync(new MouseEventArgs());
        var rolls = new Queue<int>([6, 1, 5, 2, 3, 4, 6, 5, 2, 3]);
        var human = new Player("Player 1", new PassiveStrategy(), isHuman: true);
        var opponent = new Player("Alma", new PassiveStrategy());
        opponent.TotalScore.Add(250);
        var match = new InteractiveGame([human, opponent], rolls.Dequeue);
        match.RollOpening();
        await ShowMatchAsync(component, match);
        Assert.That(component.Find(".lead-target").TextContent, Is.EqualTo("300 more turn points to take the lead"));
        await component.Find(".game-actions .primary").ClickAsync(new MouseEventArgs());
        await component.Find(".game-actions .primary").ClickAsync(new MouseEventArgs());
        Assert.That(component.Find(".lead-target").TextContent, Is.EqualTo("150 more turn points to take the lead"));
        await component.Find(".game-actions .primary").ClickAsync(new MouseEventArgs());
        await component.Find(".game-actions .primary").ClickAsync(new MouseEventArgs());

        Assert.That(component.Find(".lead-target").TextContent, Is.EqualTo("Bank to take the lead"));
    }

    [Test]
    public async Task ScoreboardKeepsPlayingOrderAndFramesEveryTiedLeader()
    {
        await using var context = CreateContext();
        var component = context.Render<GameTable>();
        await component.Find(".start-button").ClickAsync(new MouseEventArgs());
        var players = new[] { new Player("Player 1", new PassiveStrategy(), isHuman: true), new Player("Alma", new PassiveStrategy()), new Player("Hugo", new BoldStrategy()) };
        players[0].TotalScore.Add(250);
        players[1].TotalScore.Add(600);
        players[2].TotalScore.Add(600);
        var rolls = new Queue<int>([1, 2, 6]);
        var match = new InteractiveGame(players, rolls.Dequeue);
        match.RollOpening();
        await ShowMatchAsync(component, match);

        Assert.That(component.FindAll(".score-row > div > strong").Select(player => player.TextContent), Is.EqualTo(new[] { "Hugo", "Player 1", "Alma" }));
        Assert.That(component.FindAll(".score-row.leader > div > strong").Select(player => player.TextContent), Is.EqualTo(new[] { "Hugo", "Alma" }));
    }

    [Test]
    public async Task InheritancePreviewShowsOneAvailableDieAndFourKeptDice()
    {
        await using var context = CreateContext();
        var component = context.Render<GameTable>();
        await component.Find(".start-button").ClickAsync(new MouseEventArgs());
        var rolls = new Queue<int>([6, 1, 5, 5, 2, 2, 3]);
        var match = new InteractiveGame([new Player("Alma", new PassiveStrategy()), new Player("Player 1", new PassiveStrategy(), isHuman: true)], rolls.Dequeue);
        match.RollOpening();
        match.Roll();
        match.KeepDice(new DiceSelection(0, 2, 2));
        match.Bank();
        match.NextTurn();
        await ShowMatchAsync(component, match);

        Assert.That(component.FindAll(".inheritance-preview .available").Count, Is.EqualTo(1));
        Assert.That(component.FindAll(".inheritance-preview .muted").Count, Is.EqualTo(4));
        Assert.That(component.FindAll(".inheritance-preview [aria-label='Die unavailable']").Count, Is.EqualTo(4));
        Assert.That(component.FindAll(".kept-dice .die").Count, Is.EqualTo(4));
        Assert.That(component.Find(".inheritance-hint").TextContent, Does.Contain("1 die available to roll"));
    }

    [Test]
    public async Task OnlyOpeningContendersAnimateAndEliminatedRollsStayVisible()
    {
        await using var context = CreateContext();
        var component = context.Render<GameTable>();
        await component.Find(".start-button").ClickAsync(new MouseEventArgs());
        var rolls = new Queue<int>([2, 6, 6, 4, 4, 2, 5]);
        var match = new InteractiveGame([new Player("Player 1", new PassiveStrategy(), isHuman: true), new Player("Alma", new PassiveStrategy()), new Player("Hugo", new BoldStrategy())], rolls.Dequeue);
        match.RollOpening();
        await ShowMatchAsync(component, match);
        var reroll = component.Find(".game-actions .primary").ClickAsync(new MouseEventArgs());
        component.WaitForAssertion(() =>
        {
            Assert.That(component.FindAll(".opening-player .rolling").Count, Is.EqualTo(2));
            Assert.That(component.FindAll(".opening-player.eliminated .rolling"), Is.Empty);
        });
        await reroll;
        Assert.That(component.Find(".opening-player.eliminated .die").GetAttribute("aria-label"), Is.EqualTo("Die 2"));
        await component.Find(".game-actions .primary").ClickAsync(new MouseEventArgs());

        Assert.That(component.FindAll(".opening-player.eliminated").Count, Is.EqualTo(2));
        Assert.That(component.Find(".opening-table h2").TextContent, Does.Contain("Hugo scored a five"));
    }

    [Test]
    public async Task LastRoundLabelLivesInsideTheExistingTableCaption()
    {
        await using var context = CreateContext();
        var component = context.Render<GameTable>();
        await component.Find(".start-button").ClickAsync(new MouseEventArgs());
        var opening = new Queue<int>([6, 1]);
        var match = new InteractiveGame([new Player("Player 1", new PassiveStrategy(), isHuman: true), new Player("Alma", new PassiveStrategy())], () => opening.Count > 0 ? opening.Dequeue() : 3);
        match.RollOpening();

        for (var turn = 0; turn < 18; turn++)
        {
            match.Roll();
            match.NextTurn();
        }

        await ShowMatchAsync(component, match);

        Assert.That(component.Find(".table-caption .last-round-label").TextContent, Is.EqualTo("Last round!"));
        Assert.That(component.FindAll(".play-area > [role='alert']"), Is.Empty);
    }

    [Test]
    public async Task AnAllComputerGameRunsToCompletionWithoutHumanTurnButtons()
    {
        await using var context = CreateContext();
        var component = context.Render<GameTable>();
        await component.Find("#human-player-count").ChangeAsync("0");
        await component.Find("[role='switch']").ClickAsync(new MouseEventArgs());
        await component.Find(".start-button").ClickAsync(new MouseEventArgs());
        var opening = new Queue<int>([6, 1]);
        var match = new InteractiveGame([new Player("Alma", new PassiveStrategy()), new Player("Hugo", new BoldStrategy())], () => opening.Count > 0 ? opening.Dequeue() : 3);
        await ShowMatchAsync(component, match);
        await component.Find(".game-actions .primary").ClickAsync(new MouseEventArgs());
        await component.Find(".game-actions .primary").ClickAsync(new MouseEventArgs());

        Assert.That(match.Phase, Is.EqualTo(MatchPhase.Finished));
        Assert.That(match.CurrentRound, Is.EqualTo(10));
        Assert.That(component.Find(".welcome-table").TextContent, Does.Contain("GAME OVER"));
        Assert.That(component.FindAll(".score-avatar.human"), Is.Empty);
        Assert.That(component.FindAll(".game-actions button"), Is.Empty);
    }

    private static async Task ShowMatchAsync(IRenderedComponent<GameTable> component, InteractiveGame match)
    {
        typeof(GameTable).GetField("match", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(component.Instance, match);
        await component.Find(".icon-button").ClickAsync(new MouseEventArgs());
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddSingleton<StrategyLibrary>();
        context.JSInterop.SetupModule("./js/dice-audio.js").Mode = JSRuntimeMode.Loose;
        context.JSInterop.SetupModule("./js/player-preferences.js").Mode = JSRuntimeMode.Loose;

        return context;
    }
}
