# DiceGame

A dice-rolling risk/reward game engine in C# with pluggable player strategies and a Monte Carlo simulator for evaluating them.

<img src="docs/images/dice-game.png" alt="Dice Game's blue game table with two human players and computer opponents using Adaptive and Expectimax strategies" width="50%" />

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

After accepting passed points and dice, the player must roll and keep scoring dice at least once before banking, including when all five dice are available. In the web game, banking displays the result briefly and advances to the next player automatically.

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
DiceGame.Web/      Blazor WebAssembly — interactive dice game
DiceGame.Tests/    NUnit — interactive match rules and complete games
```

## Building & Running

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build
dotnet run --project Simulator
```

### Play in the browser

```bash
dotnet run --project DiceGame.Web
```

Open http://localhost:5180. Select 2–6 total players and 0–6 humans. Humans share the same device and are named Player 1, Player 2, and so on. The remaining seats are computers with selectable Passive, Bold, Adaptive, or Expectimax strategies; all-human and all-computer games are supported. The active player's name identifies whose turn it is. The interface is in English, with a dark game table, animated dice, a sticky scoreboard on desktop and mobile, and locally bundled CC0 sound recordings by [Kenney](https://opengameart.org/content/54-casino-sound-effects-cards-dice-chips). Sound starts after interaction and can be muted.

Edit human and computer names in setup. Names are saved locally in the browser and restored between games and sessions. Names must be nonempty and unique among active players. The Turbo switch in the header speeds up computer decisions, rolls, and turn transitions and can be changed during a match.

Before the first turn, every player rolls one die. Only players tied for the highest value reroll until one player wins the opening. The remaining players follow in their original seating order, starting from the opening winner. Match scoring, inheritance, and computer decisions follow the existing simulator; tied final scores share the victory.

The scoreboard stays in playing order with the starter first. Banked-score leaders have gold point totals; tied leaders are all highlighted. Each turn shows the additional points needed to bank an outright lead. During inheritance, green dice are available to roll, dimmed outlines are unavailable, and the passed kept dice appear below. Only opening contenders animate during a tiebreaker. The last-round alert sits inside the table caption without moving the table.

`InteractiveGame` is the shared rules engine for the web game and simulator. It advances one action at a time, allowing the human to select scoring dice, keep rolling, bank, and accept or decline passed positions. `Game` runs this same engine automatically using each player's `IPlayerStrategy`, keeping the simulator's seating order and optional shuffle. Rules and strategy decisions are implemented in one engine.

Rolling at least three ones triggers a short rising two-note “tu du” fanfare when sound is enabled. The fanfare is synthesized locally; the last-round announcement uses the browser's available speech voices.

```bash
dotnet test DiceGame.slnx
dotnet publish DiceGame.Web -c Release
```

The published `DiceGame.Web/bin/Release/net10.0/publish/wwwroot` directory can be served by a static web host. No backend or account is required. Sound attribution is included in `DiceGame.Web/wwwroot/audio/CREDITS.md`.

The simulator runs five scenarios with 25,000 games each, using seed `20261005`, shuffled seating, and fractional win credit for tied winners:

- **Strategy Comparison** — Passive vs. Bold vs. Adaptive vs. Expectimax
- **Head-to-head comparisons** — Expectimax against each of the three existing strategies
- **Risk Analysis** — How the minimum-dice-to-roll threshold affects win rates

## Strategies

### Genetic strategy training

In the browser, choose **Strategy lab** in the game table header. The lab has a genetic training form with generation results and cancellation, plus tournaments between 2–6 selected strategies. Each tournament plays the selected strategies together with shuffled seats and reports fractional win share and average score. Choose the match count and seed to repeat comparisons. Long operations yield regularly so the browser can update the page and handle cancellation. Leaving the lab cancels the current operation.

Evolution's **Table size** setting selects fixed tables of 2–6 players or varying tables from 2 up to a selected maximum. The default remains varying 2–6. Training, baseline validation, and generation-winner validation all use that setting. Varying tables cycle through each size with match counts differing by at most one. The **Results by table size** table updates with the current evaluation and shows candidate win share, average score, match count, and the equal-strength reference of `1 / player count`. The overall fitness remains raw fractional victory share. For fixed training use `--players 5` in the CLI; for varying tables use `--max-players 4` (2–4 players). The console's independent tests and later `evaluate` calls follow the saved settings, and report results by player count. Training artifacts save `MaximumPlayers`, `VaryPlayerCount`, and the selected strategy's validation breakdown; older artifacts retain the varying 2–6 default.

Both run types display live progress, completed and total match counts, elapsed time, throughput, and an approximate remaining duration. Evolution reports baseline validation, the current generation and candidate, candidate win share and average score, and the best validated win share. Its progress includes all training and validation matches. Tournament standings update during the run and are labelled preliminary until all matches finish. Cancelling keeps the partial results visible without saving an unfinished strategy. Blank seed fields generate a random seed at the start, shown below the relevant form for replay.

Evolution also shows the selected opponents' live win shares, average scores, and appearance counts from the current evaluation's actual matches, including selected JSON-loaded strategies. These statistics reset for each candidate and validation phase. Every player using a strategy counts as a separate appearance, so a strategy can have multiple appearances in one match. Shared victories contribute fractional credit. Tournament results additionally report how often each strategy played last, how many of those games it won, and how many of those wins were shared. Last-seat wins include shared wins, counted once per game; the shared-win column identifies that subset. The summary totals show how often the last player won across all simulated games.

Use **Evolution opponents** to choose which strategies candidates train against. This selection is independent of the tournament checkboxes. New browser and CLI runs select Adaptive and Expectimax by default; Passive and Bold can be enabled explicitly. JSON-loaded strategies are also available in the browser. Select at least one opponent; each opponent seat is sampled from that pool, so even one selected strategy can fill a 2–6 player table. Baseline and generation-winner validation use exactly the selected fixed pool. **Also train against previous generation winners** optionally adds the most recent eight winners during training only; it is enabled by default and can be disabled for a strictly fixed pool. The live comparison displays the explicitly selected opponents, and the run summary records whether generation winners were enabled.

CLI examples are `train --opponents Adaptive,Expectimax --generation-winners false` and `train --opponents none --opponent-file champion.json`. Repeat `--opponent-file` to load several strategies. File opponents are added to the selected built-in pool, or used alone with `--opponents none`. Training artifacts embed `OpponentNames`, `CustomOpponents` with full gene snapshots, and `IncludeGenerationWinners`, so later `evaluate` calls use the saved opponent pool without needing the original files. Legacy serialized options without these fields retain the previous four built-in opponents and enabled generation archive.

Load several strategy JSON files using the file picker on either the lab page or game setup. Imported strategies appear in tournament checkboxes and the computer strategy selectors on the game table. A completed training run adds its selected strategy automatically. Download its JSON using **Save JSON**. Loaded and trained strategies stay available while navigating within the app, and must be loaded again after a browser refresh. Duplicate names receive a numeric suffix so built-in and imported strategies remain distinct. Each import accepts up to 20 files of at most 1 MB each.

Browser exports contain `Version`, `Name`, and `Genes`. The file picker also accepts the earlier console training artifacts containing `Training.Genes`. Imported names and gene ranges are checked before the strategy can play. The shared training and file types live in `DiceGame.Training` in the core library; the CLI and browser use the same rules and genetic optimizer.

The simulator can evolve the numeric and discrete settings of an Adaptive-based threshold strategy:

```bash
dotnet run --project Simulator -c Release -- train
```

Defaults are 40 candidates, 20 generations, 500 training games per candidate, 2,000 validation games per generation winner, and 10,000 independent test games per reported strategy. For a shorter experiment:

```bash
dotnet run --project Simulator -c Release -- train --population 12 --generations 5 --games 200 --validation-games 1000 --test-games 2000 --seed 20261121 --output evolved-strategy.json
dotnet run --project Simulator -c Release -- evaluate evolved-strategy.json
```

Training starts with the selected family's baseline strategy and random candidates. Each generation retains the best fifth (at least two when the population permits, while leaving at least one offspring slot), crosses their settings, and attempts to mutate each gene with the selected probability (20% by default). Set **Mutation rate (%)** from 0 to 100 in the lab, or pass `--mutation-rate 0.2` to the CLI using a fraction from 0 to 1. At 0%, crossover still combines parental genes; retained elite candidates are never mutated. Each mutation has a 15% chance of resampling the gene's allowed range; otherwise it adds a random integer from -3 to +3 and clamps to the bounds. A mutation can therefore leave the value unchanged. The CLI saves the mutation rate with the training options; older artifacts default to 20%. The opponent pool is selected separately, with optional previous generation winners during training. Table sizes follow the selected fixed or varying setting, with shuffled seating and sampled opponents. Candidates within a generation use the same schedule and dice seeds; different decisions can still consume different numbers of dice rolls. The fitness is fractional victory share, with no reward for average score.

Generation winners compete with the family's original baseline (Adaptive or Expectimax) on a fixed validation schedule against the selected opponents. The saved candidate has the highest validation share, so training can retain the baseline if no winner improves it. Validation is used for selection and can overfit; the final test uses a separate seed, never used for selection. Evolved, Adaptive, and Expectimax are tested against that same selected pool and configured table schedule. Reported approximate 95% margins describe each strategy's sampled win share, not the uncertainty of the difference between strategies. Small runs do not establish superiority or global optimality.

The console JSON artifact contains the seed, training options, selected genes, validation result, and independent test results. `evaluate` reloads the genes and uses another deterministic test seed; repeating that command repeats the same evaluation. To use either export format in C#, call `StrategyFile.FromJson(json, "Imported").Genes.CreateStrategy()`. `TrainingArtifact` remains in the Simulator project.

The Threshold family's 14 genes, in order, are: normal stopping score, stopping score while leading, stopping score while trailing, trailing gap trigger, final-round stopping score, final-round window, one-die stopping cap, two-dice stopping cap, minimum inherited score, minimum inherited dice, inheritance while trailing (0 or 1), maximum twos kept, maximum fives kept, and maximum dice passed. Score genes use units of 50 points. Stopping scores range from 250 to 1,500 (2,000 for trailing and final rounds); inherited scores range from 0 to 2,000. Dice counts range from 1 to 5. Triple ones and the existing final-turn safeguards remain enabled. Simulation lookahead stays disabled during threshold training.

### Evolving Expectimax

Select **Expectimax search** in the lab's **Strategy family** selector, or use `train --strategy-family expectimax` in the console. New browser and CLI training defaults to the broader Hybrid family; the separate Threshold and Expectimax families remain selectable. Expectimax training starts with genes `[0, 0, 4, 0, 0, 0]`, which reproduce built-in Expectimax decisions exactly. The population can evolve these six integer genes:

| Gene | Range | Effective parameter |
| --- | --- | --- |
| Banked-point risk adjustment | -4 to 4 | Gene × 25 points; multiplied by `score / (score + 500)` |
| Estimated match-win weight | 0 to 4 | Gene × 25 point-equivalent units |
| Expected points for an opponent yet to act | 0 to 8 | Gene × 50 points |
| Late-game match emphasis | 0 to 4 | Gene / 4; scales match weight by `1 + emphasis / (rounds left + 1)` |
| Remaining-player match emphasis | 0 to 4 | Gene / 4; scales match weight by `1 + emphasis × fraction of opponents still to act` |
| Passed-position penalty | 0 to 4 | Gene × 10 points; multiplied by `available dice / 5 × score / (score + 250)` |

The built-in and evolved strategies share the same exact roll probabilities, legal dice selections, hot-dice handling, busts, and dynamic programming search until banking or busting. Evolved banking utility is the banked score plus the bounded risk adjustment and estimated match-win bonus, minus the bounded passed-position penalty. Bust utility uses zero turn points and no passed-position penalty. The finite search boundary is calculated from the maximum utility adjustment and roll/bust probabilities; each strategy retains at most 16 position policies. The last player's final turn always uses the existing exact victory-share policy. Zero risk, match-win, and passed-position weights delegate directly to the original strategy.

Match-win estimates multiply sigmoid comparisons with opponents' projected scores, using uncertainty `200 + 100 × sqrt(rounds left)`. Opponents still to act this round receive the selected expected-points forecast. Equal future-round mean forecasts cancel from score differences. These are heuristics for match context and inheritance, not full simulations of opponents' future choices or calibrated win probabilities. They give evolution additional decisions to explore but do not establish superiority over the built-in strategy. The extra position policies also make this family more expensive per match.

Expectimax exports use file version 2 and `Genes.Family = "Expectimax"`; Threshold exports remain version 1. Files without a family remain Threshold files. Both families and the Hybrid family can be loaded, simulated, inspected, and used in ordinary matches. New console training artifacts record `StrategyFamily`; earlier serialized options without that setting retain the Threshold default.

### Evolving hybrids

**Hybrid: Expectimax + thresholds** is the default for new training in the lab and CLI. It puts both components in one genome so their settings can cross over between parents. The initial population contains a pure-Expectimax candidate, an Adaptive-style candidate, and random hybrids. Validation starts with pure Expectimax and retains it if no generation winner improves the sampled validation share. Select `--strategy-family hybrid` explicitly for scripts, or choose `threshold` or `expectimax` to constrain the search to either earlier family.

Hybrid files use version 3, `Genes.Family = "Hybrid"`, and 25 integer genes:

| Zero-based indexes | Meaning |
| --- | --- |
| 0–5 | The six Expectimax genes described above |
| 6–19 | The fourteen Threshold genes described above |
| 20 | Banking mode: 0 = Expectimax, 1 = Threshold, 2 = either component may bank, 3 = both must agree to bank |
| 21 | Dice selection: 0 = Expectimax, 1 = Threshold keeping rules |
| 22 | Inheritance mode: 0 = Expectimax, 1 = Threshold, 2 = both must accept, 3 = either may accept |
| 23 | Maximum next-roll bust probability: gene / 20, from 0% to 100%; 100% disables the limit |
| 24 | Minimum expected net point gain: 0 disables it; genes 1–41 map to `(gene - 21) × 25`, from -500 to +500 |

Banking and inheritance risk limits use exact next-roll outcomes with the hybrid's selected dice-keeping rule. Banking still requires at least 250 points and a successful scoring roll, and final-round lead chasing retains priority. The last player's final turn always uses the exact Expectimax victory-share decisions for banking, keeping dice, and accepting inheritance, bypassing hybrid overrides and risk gates. Neutral Expectimax weights with final genes `[0, 0, 0, 20, 0]` reproduce built-in Expectimax; changing them to `[1, 1, 1, 20, 0]` selects Adaptive-style behavior with the shared final-turn safeguards.

The Expectimax component evaluates its own continuation policy exactly. Threshold overrides can subsequently choose different actions, so the component's calculated values are guidance rather than exact values of the entire hybrid continuation. Actual training and validation play complete matches using the hybrid's real decisions. Winning match share remains the fitness objective, and an improvement is not guaranteed. The parameter inspector explains each decision source, all effective values, and threshold settings that are currently inactive.

### Inspecting strategy parameters

Use **Inspect strategy parameters** in the lab's **Your strategies** card to select any built-in or loaded strategy and expand its parameters. Game setup also has **Strategy parameters** below each computer's selected strategy. Both views show effective values rather than raw gene indexes, meaningful names, descriptions, and inactive/conditional settings. Threshold strategies expose all 21 behavior settings; Expectimax exposes its six weights and fixed search/final-turn behavior. A zero match-win weight makes opponent forecasts and both match-emphasis weights inactive, so those dependencies are identified rather than removed.

The parameter audit removed the redundant `MinInheritedHandDiceToRoll` property: it duplicated `TakeInheritanceMinHandDice` in the same inheritance check and was never configured anywhere in the repository. Use `TakeInheritanceMinHandDice` for that rule. The other threshold settings have decision paths and remain available, including opt-in risk and simulation settings. Two-candidate populations now retain one elite and generate one offspring so mutation is still effective.

Strategies live in `DiceGame/Strategies` under the `DiceGame.Strategies` namespace and are instantiated as sealed records: `new PassiveStrategy()`, `new BoldStrategy()`, and `new AdaptiveStrategy()`. Their common threshold behavior lives in the abstract `ThresholdStrategy` record, which implements `IPlayerStrategy`. Records can be customized with `with` expressions.

To add a strategy to the web interface, add a public sealed record implementing `IPlayerStrategy` or deriving from `ThresholdStrategy` in the `DiceGame` project. Provide a public parameterless constructor, a unique `Name`, and a `Description`. The instance-based `PlayerStrategies` catalog discovers concrete strategies automatically; no central list or UI change is needed. The web application preserves the `DiceGame` assembly during trimming so newly discovered strategies remain available in Release builds. Additional assemblies can be supplied to the catalog and must also be preserved when trimming. Passive always declines passed points and dice, regardless of their value.

The last-round voice and sound cue are reduced to 60% of their previous volume: their individual volume settings changed from 0.6 to 0.36. Other sound levels are unchanged.

| Strategy | Stop Threshold | Behavior |
|----------|---------------|----------|
| Passive | 250 | Banks early, never takes inherited dice |
| Bold | 600 | Keeps rolling, always takes inherited dice |
| Adaptive | 350 (base) | Uses standings after banking; stops at 250 with one die or 300 with two dice |
| Expectimax | Calculated per position | Maximizes expected turn points using all future rolls and scoring subsets; maximizes victory share on the match's final turn |

### Expectimax

`new ExpectimaxStrategy()` implements `IPlayerStrategy` directly and is automatically available in the web strategy selector. Its precomputed dynamic programming policy enumerates exact roll probabilities, every legal scoring subset, the 250-point banking minimum, busts, and hot dice. Inheritance is compared with starting fresh and includes the mandatory roll before banking. It can leave scoring dice unkept when the extra dice improve expected future points.

The score policy has a calculated banking boundary: above the largest expected roll score divided by the smallest bust probability, even an optimal continuation cannot justify risking the accumulated points. This gives a finite table without an arbitrary score cutoff. On the last player's final turn, a separate cached policy optimizes victory share, including ties and retaining an existing lead after a bust. Earlier players in the final round continue chasing if banking would still leave them behind.

In the seeded 25,000-game comparisons, Expectimax earns 55.47% of victory share against Adaptive, 68.12% against Passive, and 74.94% against Bold. In the four-strategy comparison it earns 40.02%, ahead of Adaptive at 31.50%, Passive at 14.72%, and Bold at 13.76%. These results support it as the strongest default among the current strategies, but do not prove optimal match play: outside the final turn, it optimizes its own turn score rather than modeling opponents' future turns or the value of the position it passes.

### Key `ThresholdStrategy` Properties

| Property | Description |
|----------|-------------|
| `StopThreshold` | Base score to stop rolling at |
| `StopThresholdWhenLeading` | Adjusted threshold when the projected total after banking leads or ties the leader |
| `StopThresholdWhenTrailing` | Higher threshold when the projected total after banking is still behind |
| `StopThresholdByDiceCount` | Optional immutable map from remaining dice count to a lower stopping threshold; empty disables dice-based risk adjustments |
| `TrailingGapTrigger` | Point gap that activates trailing behavior |
| `StopThresholdFinalRounds` | Override threshold for the last N rounds |
| `FinalRoundsCount` | How many rounds count as "final" |
| `MaxRollsPerTurn` | Force-stop after this many rolls (0 = unlimited) |
| `TakeInheritanceMinScore` | Minimum inherited score worth accepting |
| `ChaseLeadInFinalRound` | Keep rolling in the last round if you can't catch the leader |
| `SecureWinOnFinalTurn` | Enabled by default; the last player in the final round may exceed dice-keeping limits and bank immediately to secure an outright victory |
| `MaxTwosToKeepPerRoll` | Maximum twos to keep from a roll unless securing a final-turn victory |
| `MaxFivesToKeepPerRoll` | Maximum fives to keep from a roll unless securing a final-turn victory |
| `MaxBustProbability` | Maximum acceptable next-roll bust probability, from 0 to 1; default 1 leaves this limit disabled |
| `MinimumExpectedNetGain` | Minimum expected point gain from the next roll, accounting for lost turn points on a bust; negative infinity disables this check |
| `TargetLeadMargin` | Desired point margin over the best opponent after banking; default 0 disables this target |
| `SimulationBudget` | Number of complete simulated continuations per candidate action; default 0 disables lookahead |
| `MaxDiceToPass` | Preferred maximum dice to leave for the next player, normally 1–5; default 5 disables this preference, Adaptive uses 4 |

Stopping decisions use `GameState.ProjectedTotalScore`, `IsLeadingAfterBanking`, and `PointsBehindLeaderAfterBanking`. Dice-based thresholds cap the usual position or final-round threshold. Adaptive uses a cap of 250 points with one remaining die and 300 with two; an all-kept hand resets to five dice. In the last round, chasing an outright lead still takes priority over these caps.

With `SecureWinOnFinalTurn`, a guaranteed final-turn win takes priority over normal stopping thresholds and limits for twos, fives, and triple ones. A tie does not trigger this exception. The engine still requires a scoring roll and at least 250 turn points before banking. Set the property to `false` to retain the ordinary limits.

`MaxDiceToPass` encourages another roll when banking would leave too many dice, including the fresh set of five after keeping all dice. Explicit bust-risk, expected-gain, and maximum-roll limits can still require banking. A guaranteed final-turn victory and a completed game do not sacrifice points merely to avoid passing dice.

Next-roll probabilities and expected gains enumerate every possible outcome, including triple ones and the strategy's dice-keeping limits. Risk and expected-gain limits also apply when considering inherited dice. These limits take priority over building a larger lead; final-round lead chasing and securing a guaranteed victory retain priority.

With a positive `SimulationBudget`, the strategy compares banking versus rolling, eligible inheritance versus starting fresh, and valid subsets of scoring dice. Simulations use the same `InteractiveGame` engine, opponent scores, actual computer strategies, and remaining turn order. Human opponents use their attached baseline strategy during simulations. Lookahead is disabled inside simulated continuations, so simulations cannot recursively simulate further games. Candidates use reproducible random samples, maximize estimated victory share, and break ties using expected final points; shared victories contribute a fraction of a win.

The additional settings are opt-in and can be combined on any threshold strategy:

```csharp
var optimizedStrategy = new AdaptiveStrategy() with
{
    MaxBustProbability = 0.45,
    MinimumExpectedNetGain = 0,
    TargetLeadMargin = 100,
    SimulationBudget = 64
};
```

`GameState.OpponentScores` is an immutable snapshot in seating order excluding the current player. `PlayersRemainingInRound` identifies how many opponents still have a turn this round. `BestOpponentScore` supports lead targets without comparing the player against their own banked score. Larger simulation budgets increase computation per decision and should be benchmarked against other strategies.

## Simulation performance

Silent automated matches use the same `InteractiveGame` rules with presentation disabled, avoiding history storage and dynamic turn messages. Opponent strategy snapshots are built once per seating order, while fresh score snapshots are assembled in one pass for each decision. Interactive matches keep their normal messages and history.

Expectimax precomputes score transitions and bounded banking values, and stores its policy tables in flat arrays. Evolved policy caches use relative opponent margins and effective match weights; when match-win weight is zero, the same risk/passed-position policy can be reused independently of the standings. These optimizations preserve the decisions and random-number sequence.

Run the reproducible benchmark with:

```bash
dotnet run --project Simulator -c Release -- benchmark
```

It uses four-player tables, shuffled seating, seed `20261121`, a warmup, and the median of three measured runs. Each workload prints a checksum over rankings and scores plus allocated bytes per game. On the development machine, comparing the preceding revision with the optimized engine gave:

| Candidate workload | Matches | Before | After | Speedup |
| --- | ---: | ---: | ---: | ---: |
| Adaptive threshold | 3,000 | 362.7 ms | 142.5 ms | 2.5× |
| Built-in Expectimax | 3,000 | 291.4 ms | 119.3 ms | 2.4× |
| Evolved Expectimax with match estimates | 100 | 273.0 ms | 145.2 ms | 1.9× |
| Evolved Expectimax with risk/passed-position weights only | 100 | 99.4 ms | 7.2 ms | 13.8× |

All four result checksums were unchanged. Threshold allocations fell from 140,928 to 33,816 bytes per game. These are native desktop Release measurements, not browser timings; results depend on strategy weights, hardware, and workload. Regression tests additionally compare complete automated and interactive matches with presentation enabled and disabled.

## Architecture

- **Strategy Pattern** — `IPlayerStrategy` decouples decision logic from game mechanics
- **Immutable Records** — `GameState`, `DiceSelection`, and `GameResult` are sealed records
- **State-Based Decisions** — Strategies receive a full `GameState` snapshot including relative standings
- **Thread-Safe RNG** — `Randomizer` wraps `Random` with locking for concurrent simulation runs
