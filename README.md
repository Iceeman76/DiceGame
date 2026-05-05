# DiceGame

A dice-rolling risk/reward game engine in C# with pluggable player strategies and a Monte Carlo simulator for evaluating them.

## Game Rules

Players compete over **10 rounds**, rolling up to 5 dice per turn and banking points based on what they keep.

### Scoring

| Dice | Points |
|------|--------|
| Three 1s | 1000 |
| Any 5 | 100 |
| Any 2 | 50 |
| 3, 4, 6 | 0 |

A player must accumulate at least **250 points** in a round before they can bank. If a roll yields no scoring dice, the entire round score is lost (bust).

### Turn Flow

1. Accept or reject the previous player's dice position and partial score
2. Roll the available dice (up to 5)
3. Select which scoring dice to keep
4. Roll again or stop and bank the round score
5. Pass to the next player

## Project Structure

```
DiceGame/          Core library — game engine, scoring, and strategy interfaces
Simulator/         Console app — runs strategy comparison simulations
```

## Building & Running

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build
dotnet run --project Simulator
```

The simulator runs two scenarios with 25,000 games each:

- **Strategy Comparison** — Passive vs. Bold vs. Adaptive
- **Risk Analysis** — How the minimum-dice-to-roll threshold affects win rates

## Strategies

Strategies are configured via `ThresholdStrategy` and constructed through the `PlayerStrategies` factory.

| Strategy | Stop Threshold | Behavior |
|----------|---------------|----------|
| Passive | 250 | Banks early, never takes inherited dice |
| Bold | 600 | Keeps rolling, always takes inherited dice |
| Adaptive | 350 (base) | Adjusts thresholds based on game position |

### Key `ThresholdStrategy` Properties

| Property | Description |
|----------|-------------|
| `StopThreshold` | Base score to stop rolling at |
| `StopThresholdWhenLeading` | Adjusted threshold when in the lead |
| `StopThresholdWhenTrailing` | Higher threshold when trailing (encourages risk) |
| `TrailingGapTrigger` | Point gap that activates trailing behavior |
| `StopThresholdFinalRounds` | Override threshold for the last N rounds |
| `FinalRoundsCount` | How many rounds count as "final" |
| `MaxRollsPerTurn` | Force-stop after this many rolls (0 = unlimited) |
| `TakeInheritanceMinScore` | Minimum inherited score worth accepting |
| `ChaseLeadInFinalRound` | Keep rolling in the last round if you can't catch the leader |

## Architecture

- **Strategy Pattern** — `IPlayerStrategy` decouples decision logic from game mechanics
- **Immutable Records** — `GameState`, `DiceSelection`, and `GameResult` are sealed records
- **State-Based Decisions** — Strategies receive a full `GameState` snapshot including relative standings
- **Thread-Safe RNG** — `Randomizer` wraps `Random` with locking for concurrent simulation runs
