using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DiceGame.Strategies;

namespace DiceGame;

public sealed class InteractiveGame
{
    private readonly Func<int> rollDie;
    private readonly List<Player> players;
    private readonly List<int> openingContenders;
    private readonly List<string> history = [];
    private readonly bool enablePresentation;
    private ImmutableArray<IPlayerStrategy>[] opponentStrategies;
    private Dice passedTable = new(0);
    private int passedScore;
    private int rollsThisTurn;
    private int currentPlayerIndex;

    public InteractiveGame(IEnumerable<Player> players, Func<int>? rollDie = null, bool enablePresentation = true)
    {
        this.players = players.ToList();
        this.rollDie = rollDie ?? (() => Randomizer.Integer(1, 6));
        this.enablePresentation = enablePresentation;
        opponentStrategies = BuildOpponentStrategies();
        openingContenders = Enumerable.Range(0, this.players.Count).ToList();
    }

    public const int TotalRounds = 10;
    public const int MinimumBankScore = 250;
    public IReadOnlyList<Player> Players => players;
    public IReadOnlyList<string> History => history;
    public IReadOnlyList<OpeningRoll> OpeningRolls { get; private set; } = [];
    public MatchPhase Phase { get; private set; } = MatchPhase.Opening;
    public Player CurrentPlayer => players[currentPlayerIndex];
    public bool IsHumanTurn => CurrentPlayer.IsHuman;
    public int CurrentRound { get; private set; } = 1;
    public int OpeningRound { get; private set; }
    public int TurnScore { get; private set; }
    public int InheritedScore => passedScore;
    public IReadOnlyList<int> InheritedTable => passedTable;
    public int InheritedDiceCount => passedTable.Count == 5 ? 5 : 5 - passedTable.Count;
    public Dice Hand { get; private set; } = new(5);
    public Dice Table { get; private set; } = new(0);
    public string Message { get; private set; } = "Roll one die each. Highest roll starts.";
    public bool CanBank => Phase == MatchPhase.ReadyToRoll && rollsThisTurn > 0 && TurnScore >= MinimumBankScore;
    public bool ComputerWillRoll => Phase == MatchPhase.ReadyToRoll && (CanBank is false || CurrentPlayer.Strategy.ShouldStop(BuildState()) is false);
    public IReadOnlyList<Player> Winners => players.Where(player => player.TotalScore.Value == players.Max(candidate => candidate.TotalScore.Value)).ToList();
    public int LeaderScore => players.Max(player => player.TotalScore.Value);
    public bool IsCurrentPlayerLeading => CurrentPlayer.TotalScore.Value == LeaderScore;
    public int TurnPointsToTakeLead => IsCurrentPlayerLeading ? 0 : Math.Max(MinimumBankScore, (LeaderScore - CurrentPlayer.TotalScore.Value) / DiceSelection.TwoScore * DiceSelection.TwoScore + DiceSelection.TwoScore);
    public int AdditionalPointsToTakeLead => Math.Max(0, TurnPointsToTakeLead - TurnScore);

    public bool IsOpeningContender(Player player) => Phase == MatchPhase.Opening && openingContenders.Contains(players.IndexOf(player));

    internal void StartInSeatingOrder()
    {
        RequirePhase(MatchPhase.Opening);
        BeginTurn();
    }

    internal static InteractiveGame CreateSimulation(GameState state, ThresholdStrategy strategy, Func<int> rollDie)
    {
        var playerIndex = state.PlayerCount - state.PlayersRemainingInRound - 1;
        var simulatedPlayers = new List<Player>();
        var opponentIndex = 0;

        for (var index = 0; index < state.PlayerCount; index++)
        {
            var simulatedStrategy = index == playerIndex ? strategy : opponentIndex < state.OpponentStrategies.Length ? state.OpponentStrategies[opponentIndex] : new PassiveStrategy();

            if (simulatedStrategy is ThresholdStrategy thresholdStrategy)
            {
                simulatedStrategy = thresholdStrategy with { SimulationBudget = 0 };
            }

            var player = new Player($"Player {index + 1}", simulatedStrategy);

            if (index == playerIndex)
            {
                player.TotalScore.Add(state.MyTotalScore);
            }
            else
            {
                var opponentScore = opponentIndex < state.OpponentScores.Length ? state.OpponentScores[opponentIndex] : state.LeaderScore;
                player.TotalScore.Add(opponentScore);
                opponentIndex++;
            }

            simulatedPlayers.Add(player);
        }

        var table = new Dice(0);
        table.AddRange(Enumerable.Repeat(2, state.TableDiceCount));

        return new InteractiveGame(simulatedPlayers, rollDie, enablePresentation: false)
        {
            currentPlayerIndex = playerIndex,
            CurrentRound = state.CurrentRound,
            TurnScore = state.RoundScore,
            rollsThisTurn = state.RollsThisTurn,
            Hand = new Dice(state.HandDiceCount),
            Table = table,
            Phase = MatchPhase.ReadyToRoll
        };
    }

    public void RollOpening()
    {
        RequirePhase(MatchPhase.Opening);
        OpeningRound++;
        var rolls = openingContenders.Select(index => (Index: index, Value: rollDie())).ToList();
        var highest = rolls.Max(roll => roll.Value);
        openingContenders.Clear();
        openingContenders.AddRange(rolls.Where(roll => roll.Value == highest).Select(roll => roll.Index));
        OpeningRolls = rolls.Select(roll => new OpeningRoll(players[roll.Index].Name, roll.Value, roll.Value == highest)).ToList();
        Log(string.Join(" · ", OpeningRolls.Select(roll => $"{roll.PlayerName}: {roll.Value}")));

        if (openingContenders.Count > 1)
        {
            Message = $"Tie at {highest}. {string.Join(", ", openingContenders.Select(index => players[index].Name))} roll again.";

            return;
        }

        var starterIndex = openingContenders[0];
        var orderedPlayers = players.Skip(starterIndex).Concat(players.Take(starterIndex)).ToList();
        players.Clear();
        players.AddRange(orderedPlayers);
        opponentStrategies = BuildOpponentStrategies();
        BeginTurn();
        var faceName = new[] { "", "one", "two", "three", "four", "five", "six" }[highest];
        Message = $"{CurrentPlayer.Name} scored a {faceName} and starts the game!";
        Log(Message);
    }

    public void ChooseInheritance(bool accept)
    {
        RequirePhase(MatchPhase.ChooseInheritance);

        if (accept)
        {
            Table = new Dice(0);
            Table.AddRange(passedTable);
            TurnScore = passedScore;
            Hand = new Dice(5 - Table.Count);
            RefreshHotDice();
            if (enablePresentation)
            {
                Log($"{CurrentPlayer.Name}: took over {TurnScore} points.");
            }
        }
        else
        {
            if (enablePresentation)
            {
                Log($"{CurrentPlayer.Name}: starts fresh with five dice.");
            }
        }

        passedTable = new Dice(0);
        passedScore = 0;
        Phase = MatchPhase.ReadyToRoll;
        Message = accept ? "Roll and keep scoring dice before banking the inherited points." : "Roll the dice.";
    }

    public void Roll()
    {
        RequirePhase(MatchPhase.ReadyToRoll);
        rollsThisTurn++;

        for (var index = 0; index < Hand.Count; index++)
        {
            Hand[index] = rollDie();
        }

        if (enablePresentation)
        {
            Log($"{CurrentPlayer.Name}: rolled {string.Join(", ", Hand)}.");
        }

        if (Hand.Score() == 0)
        {
            Bust();

            return;
        }

        Phase = MatchPhase.SelectDice;
        Message = "Select scoring dice to keep.";
    }

    public bool IsValidSelection(DiceSelection selection) => selection.Total > 0 && (selection.Ones == 0 || selection.Ones == 3) && selection.Ones <= Hand.Count(value => value == 1) && selection.Twos >= 0 && selection.Twos <= Hand.Count(value => value == 2) && selection.Fives >= 0 && selection.Fives <= Hand.Count(value => value == 5);

    public void KeepDice(DiceSelection selection)
    {
        RequirePhase(MatchPhase.SelectDice);

        if (IsValidSelection(selection) is false)
        {
            throw new InvalidOperationException("Select available scoring dice; ones must be kept as a triple.");
        }

        KeepFace(1, selection.Ones);
        KeepFace(2, selection.Twos);
        KeepFace(5, selection.Fives);
        var earnedScore = selection.Score;
        TurnScore += earnedScore;
        Phase = MatchPhase.ReadyToRoll;
        if (enablePresentation)
        {
            Message = $"{earnedScore} points added. {(TurnScore >= 250 ? "Bank or roll again." : "Reach 250 points to bank.")}";
        }
        RefreshHotDice();
    }

    public void Bank()
    {
        if (CanBank is false)
        {
            throw new InvalidOperationException("Roll and keep scoring dice, with at least 250 points in total, before banking.");
        }

        CurrentPlayer.TotalScore.Add(TurnScore);
        passedTable = new Dice(0);
        passedTable.AddRange(Table);
        passedScore = TurnScore;
        if (enablePresentation)
        {
            Message = $"{CurrentPlayer.Name}: banked {TurnScore} points.";
            Log(Message);
        }
        Phase = MatchPhase.TurnEnded;
    }

    public void NextTurn()
    {
        RequirePhase(MatchPhase.TurnEnded);
        currentPlayerIndex++;

        if (currentPlayerIndex == players.Count)
        {
            currentPlayerIndex = 0;
            CurrentRound++;
        }

        if (CurrentRound > TotalRounds)
        {
            CurrentRound = TotalRounds;
            Phase = MatchPhase.Finished;
            if (enablePresentation)
            {
                Message = Winners.Count == 1 ? $"{Winners[0].Name} wins with {Winners[0].TotalScore.Value} points." : $"Shared victory: {string.Join(", ", Winners.Select(player => player.Name))}.";
                Log(Message);
            }

            return;
        }

        BeginTurn();

        if (enablePresentation && CurrentRound == TotalRounds && currentPlayerIndex == 0)
        {
            Log("Last round! Every player has one final turn.");
        }
    }

    public void PlayComputerStep()
    {
        if (IsHumanTurn || Phase is MatchPhase.Opening or MatchPhase.Finished)
        {
            throw new InvalidOperationException("A computer turn must be active.");
        }

        switch (Phase)
        {
            case MatchPhase.ChooseInheritance:
                ChooseInheritance(CurrentPlayer.Strategy.ShouldTakePassedDice(BuildState()));
                break;
            case MatchPhase.ReadyToRoll:
                if (CanBank && CurrentPlayer.Strategy.ShouldStop(BuildState()))
                {
                    Bank();
                }
                else
                {
                    Roll();
                }

                break;
            case MatchPhase.SelectDice:
                var selection = CurrentPlayer.Strategy.SelectDiceToKeep(Hand, BuildState());

                if (selection.Total == 0)
                {
                    Bust();
                }
                else
                {
                    KeepDice(selection);
                }

                break;
            case MatchPhase.TurnEnded:
                NextTurn();
                break;
        }
    }

    private void BeginTurn()
    {
        TurnScore = 0;
        rollsThisTurn = 0;
        Hand = new Dice(5);
        Table = new Dice(0);
        Phase = passedScore > 0 ? MatchPhase.ChooseInheritance : MatchPhase.ReadyToRoll;
        if (enablePresentation)
        {
            var turnMessage = $"{CurrentPlayer.Name}'s turn.{(IsHumanTurn ? " Roll the dice." : "")}";
            Message = Phase == MatchPhase.ChooseInheritance ? $"Take over {passedScore} points or start fresh." : turnMessage;
        }
    }

    private void RefreshHotDice()
    {
        if (Hand.Count == 0)
        {
            Hand = new Dice(5);
            Table = new Dice(0);
            Message = "All five dice kept. Roll a fresh set or bank your points.";
        }
    }

    private void KeepFace(int face, int count)
    {
        for (var index = 0; index < count; index++)
        {
            Hand.Remove(face);
            Table.Add(face);
        }
    }

    private void Bust()
    {
        if (enablePresentation)
        {
            Message = TurnScore == 0 ? $"{CurrentPlayer.Name}: no scoring dice. Turn over." : $"{CurrentPlayer.Name}: bust. {TurnScore} points lost.";
        }
        TurnScore = 0;
        passedScore = 0;
        passedTable = new Dice(0);
        Phase = MatchPhase.TurnEnded;
        if (enablePresentation)
        {
            Log(Message);
        }
    }

    private GameState BuildState()
    {
        var myScore = CurrentPlayer.TotalScore.Value;
        var opponentScores = ImmutableArray.CreateBuilder<int>(players.Count - 1);
        var opponentTotal = 0L;
        var leaderScore = myScore;
        var playerRank = 1;

        for (var index = 0; index < players.Count; index++)
        {
            var score = players[index].TotalScore.Value;
            leaderScore = Math.Max(leaderScore, score);

            if (score > myScore)
            {
                playerRank++;
            }

            if (index.Equals(currentPlayerIndex) is false)
            {
                opponentScores.Add(score);
                opponentTotal += score;
            }
        }

        var averageOpponentScore = opponentScores.Count > 0 ? (int)(opponentTotal / opponentScores.Count) : 0;

        return new GameState
        {
            RoundScore = TurnScore,
            HandDiceCount = Hand.Count,
            TableDiceCount = Table.Count,
            RollsThisTurn = rollsThisTurn,
            CurrentRound = CurrentRound,
            TotalRounds = TotalRounds,
            IsLastPlayerInRound = currentPlayerIndex == players.Count - 1,
            MyTotalScore = myScore,
            PlayerRank = playerRank,
            PlayerCount = players.Count,
            PlayersRemainingInRound = players.Count - currentPlayerIndex - 1,
            OpponentScores = opponentScores.MoveToImmutable(),
            OpponentStrategies = opponentStrategies[currentPlayerIndex],
            LeaderScore = leaderScore,
            AverageOpponentScore = averageOpponentScore,
            InheritedScore = passedScore,
            InheritedHandDiceCount = InheritedDiceCount
        };
    }

    private ImmutableArray<IPlayerStrategy>[] BuildOpponentStrategies() => players.Select(current => players.Where(player => ReferenceEquals(player, current) is false).Select(player => player.Strategy).ToImmutableArray()).ToArray();

    private void RequirePhase(MatchPhase expected)
    {
        if (Phase is not MatchPhase.Finished && Phase == expected)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {expected}, but the match is in {Phase}.");
    }

    private void Log(string message)
    {
        if (enablePresentation)
        {
            history.Insert(0, message);
        }
    }
}
