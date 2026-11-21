using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceGame.Strategies;

public static class StrategyParameterCatalog
{
    public static IReadOnlyList<StrategyParameter> Describe(IPlayerStrategy strategy) => strategy switch
    {
        ThresholdStrategy threshold => DescribeThreshold(threshold),
        ParameterizedExpectimaxStrategy expectimax => DescribeExpectimax(expectimax.Parameters),
        HybridStrategy hybrid => DescribeHybrid(hybrid),
        ExpectimaxStrategy => DescribeExpectimax(new ExpectimaxParameters()),
        _ => []
    };

    private static StrategyParameter[] DescribeHybrid(HybridStrategy strategy)
    {
        var modes = strategy.HybridParameters;
        var controls = new StrategyParameter[]
        {
            new(nameof(modes.BankingMode), "Banking decision source", "Expectimax, threshold rules, either component may bank early, or both must agree before banking. Optional risk limits can also bank; final-turn safeguards take priority.", modes.BankingMode switch { 1 => "Threshold", 2 => "Either may bank", 3 => "Both must agree", _ => "Expectimax" }),
            new(nameof(modes.DiceSelectionMode), "Dice-selection source", "Use Expectimax's scoring subset or the threshold component's keeping limits. The last player's final turn always uses the exact victory-share policy.", modes.DiceSelectionMode == 0 ? "Expectimax" : "Threshold keeping limits"),
            new(nameof(modes.InheritanceMode), "Inheritance decision source", "Choose the Expectimax decision, threshold gates, require both to accept, or accept if either agrees. Optional risk limits may reject inheritance.", modes.InheritanceMode switch { 1 => "Threshold", 2 => "Both must accept", 3 => "Either may accept", _ => "Expectimax" }),
            new(nameof(modes.MaxBustProbability), "Hybrid maximum bust probability", "Bank or reject inheritance if the next-roll bust chance exceeds this value. Uses the hybrid's actual dice-selection rule. 100% disables the limit.", modes.MaxBustProbability.ToString("P0"), modes.MaxBustProbability >= 1 ? "Disabled" : "Configured"),
            new(nameof(modes.MinimumExpectedNetGain), "Hybrid minimum expected net gain", "Bank or reject inheritance when next-roll expected point gain, including lost points on bust, falls below this value. Uses the selected dice rule.", double.IsNegativeInfinity(modes.MinimumExpectedNetGain) ? "Disabled" : modes.MinimumExpectedNetGain.ToString())
        };
        var expectimax = DescribeExpectimax(strategy.ExpectimaxParameters).Select(parameter => parameter.Key == "RollSearch" ? parameter with { Description = "Exact roll search for the Expectimax component. Hybrid overrides can change later decisions, so this is not an exact value calculation for the entire hybrid policy. Complete match simulations evaluate the actual hybrid.", Value = "Expectimax component" } : parameter);
        var threshold = DescribeThreshold(strategy.ThresholdParameters)
            .Where(parameter => parameter.Key is not ("SecureWinOnFinalTurn" or "SimulationBudget" or "MaxBustProbability" or "MinimumExpectedNetGain"))
            .Select(parameter =>
            {
                var active = parameter.Key switch
                {
                    "MaxTwosToKeepPerRoll" or "MaxFivesToKeepPerRoll" or "SaveTripleOnes" => modes.DiceSelectionMode == 1,
                    "TakeInheritanceMinScore" or "TakeInheritanceMinHandDice" or "TakeInheritanceWhenTrailing" => modes.InheritanceMode is not 0,
                    _ => modes.BankingMode is not 0
                };

                return parameter with { Key = "Threshold." + parameter.Key, Name = "Threshold: " + parameter.Name, Status = active ? parameter.Status : "Inactive: component not selected" };
            });

        return [.. controls, .. expectimax, .. threshold];
    }

    private static StrategyParameter[] DescribeThreshold(ThresholdStrategy strategy) =>
    [
        new(nameof(strategy.StopThreshold), "Normal banking score", "Bank at this turn score when neither the leading nor trailing rule applies.", strategy.StopThreshold.ToString()),
        new(nameof(strategy.StopThresholdWhenLeading), "Banking score while leading", "Banking threshold when banking would tie or beat the leader.", strategy.StopThresholdWhenLeading.ToString()),
        new(nameof(strategy.StopThresholdWhenTrailing), "Banking score while trailing", "Threshold while still behind by at least the trailing gap after banking.", strategy.StopThresholdWhenTrailing.ToString(), strategy.TrailingGapTrigger == int.MaxValue ? "Inactive: trailing adjustment disabled" : "Conditional"),
        new(nameof(strategy.TrailingGapTrigger), "Trailing gap trigger", "Points behind after banking needed to activate the trailing banking threshold.", Limit(strategy.TrailingGapTrigger, "Disabled")),
        new(nameof(strategy.StopThresholdFinalRounds), "Late-game banking score", "Overrides the usual score threshold inside the late-game window.", strategy.StopThresholdFinalRounds.ToString(), strategy.FinalRoundsCount == 0 ? "Inactive: late-game window disabled" : "Conditional"),
        new(nameof(strategy.FinalRoundsCount), "Late-game window", "Applies when rounds left after the current round are at most this value. Zero disables the override.", strategy.FinalRoundsCount.ToString()),
        new(nameof(strategy.StopThresholdByDiceCount), "Banking caps by available dice", "Caps the banking threshold for the listed dice counts. Other counts have no cap.", strategy.StopThresholdByDiceCount.IsEmpty ? "Disabled" : string.Join("; ", strategy.StopThresholdByDiceCount.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key} dice: {entry.Value} points"))),
        new(nameof(strategy.MaxRollsPerTurn), "Maximum rolls per turn", "Forces banking once this roll count is reached and banking is legal. Zero means unlimited.", strategy.MaxRollsPerTurn == 0 ? "Unlimited" : strategy.MaxRollsPerTurn.ToString()),
        new(nameof(strategy.MaxTwosToKeepPerRoll), "Maximum twos kept", "Maximum scoring twos to keep from each roll, except when securing a final-turn victory.", Limit(strategy.MaxTwosToKeepPerRoll, "All")),
        new(nameof(strategy.MaxFivesToKeepPerRoll), "Maximum fives kept", "Maximum scoring fives to keep from each roll, except when securing a final-turn victory.", Limit(strategy.MaxFivesToKeepPerRoll, "All")),
        new(nameof(strategy.SaveTripleOnes), "Keep triple ones", "Allows keeping three ones together for 1,000 points.", YesNo(strategy.SaveTripleOnes)),
        new(nameof(strategy.TakeInheritanceMinScore), "Minimum inherited points", "Accept inherited positions meeting this score. The trailing override can accept lower scores.", Limit(strategy.TakeInheritanceMinScore, "Never by score")),
        new(nameof(strategy.TakeInheritanceMinHandDice), "Minimum inherited dice", "Reject inherited positions with fewer available dice, including when trailing.", strategy.TakeInheritanceMinHandDice.ToString()),
        new(nameof(strategy.TakeInheritanceWhenTrailing), "Accept inheritance while trailing", "Accept inherited positions while behind, regardless of inherited points; dice and risk limits still apply.", YesNo(strategy.TakeInheritanceWhenTrailing)),
        new(nameof(strategy.ChaseLeadInFinalRound), "Chase the lead in the final round", "Continue rolling when banking cannot take an outright lead, overriding usual risk limits.", YesNo(strategy.ChaseLeadInFinalRound)),
        new(nameof(strategy.SecureWinOnFinalTurn), "Secure a final-turn win", "Bank a guaranteed outright win and allow all scoring dice needed to secure it.", YesNo(strategy.SecureWinOnFinalTurn)),
        new(nameof(strategy.MaxBustProbability), "Maximum bust probability", "Bank or reject inheritance if next-roll bust probability exceeds this limit. 100% disables the limit.", strategy.MaxBustProbability.ToString("P0"), strategy.MaxBustProbability >= 1 ? "Disabled" : "Configured"),
        new(nameof(strategy.MinimumExpectedNetGain), "Minimum expected net gain", "Bank or reject inheritance when the expected next-roll point gain, including bust losses, is below this value.", double.IsNegativeInfinity(strategy.MinimumExpectedNetGain) ? "Disabled" : strategy.MinimumExpectedNetGain.ToString()),
        new(nameof(strategy.TargetLeadMargin), "Target lead margin", "Continue towards this point margin over the best opponent, subject to higher-priority rules. Zero disables the target.", strategy.TargetLeadMargin.ToString(), strategy.TargetLeadMargin == 0 ? "Disabled" : "Configured"),
        new(nameof(strategy.SimulationBudget), "Match simulations per action", "Complete simulated continuations per candidate decision. Zero disables lookahead; earlier safeguards still apply.", strategy.SimulationBudget.ToString(), strategy.SimulationBudget == 0 ? "Disabled" : "Configured"),
        new(nameof(strategy.MaxDiceToPass), "Maximum dice preferred to pass", "Prefer another roll if banking would give the next player more available dice. Five disables this preference.", strategy.MaxDiceToPass.ToString(), strategy.MaxDiceToPass >= 5 ? "Disabled" : "Configured")
    ];

    private static StrategyParameter[] DescribeExpectimax(ExpectimaxParameters parameters) =>
    [
        new(nameof(parameters.RiskAdjustment), "Banked-point risk adjustment", "Adds weight × score / (score + 500) to banked value. Positive values favor securing points; negative values favor larger gains.", parameters.RiskAdjustment.ToString(), parameters.RiskAdjustment == 0 ? "Disabled" : "Configured"),
        new(nameof(parameters.MatchWinWeight), "Estimated match-win weight", "Weight in point-equivalent units for an estimated probability of beating all opponents. Uses forecasts rather than simulating their future turns.", parameters.MatchWinWeight.ToString(), parameters.MatchWinWeight == 0 ? "Disabled" : "Configured"),
        new(nameof(parameters.ExpectedOpponentTurnPoints), "Expected points from an opponent yet to act", "Expected additional points for opponents still to play this round. Equal future-round point forecasts cancel from score differences.", parameters.ExpectedOpponentTurnPoints.ToString(), parameters.MatchWinWeight == 0 ? "Inactive: match-win weight is zero" : "Conditional"),
        new(nameof(parameters.LateGameWeight), "Late-game match emphasis", "Multiplies match-win weight by 1 + this value / (rounds left + 1).", parameters.LateGameWeight.ToString("F2"), parameters.MatchWinWeight == 0 ? "Inactive: match-win weight is zero" : "Conditional"),
        new(nameof(parameters.RemainingPlayersWeight), "Remaining-player match emphasis", "Increases match-win emphasis according to the fraction of opponents still to act this round.", parameters.RemainingPlayersWeight.ToString("F2"), parameters.MatchWinWeight == 0 ? "Inactive: match-win weight is zero" : "Conditional"),
        new(nameof(parameters.PassedDicePenalty), "Passed-position penalty", "Subtracts weight × available dice / 5 × score / (score + 250) from banking value. Approximates the position passed to the next player; a bust passes no banked value.", parameters.PassedDicePenalty.ToString(), parameters.PassedDicePenalty == 0 ? "Disabled" : "Configured"),
        new("RollSearch", "Roll and dice-selection search", "Enumerates exact roll probabilities and every legal scoring subset until banking or busting. The banking boundary is calculated from the maximum bounded utility adjustment.", "Exact Expectimax", "Fixed"),
        new("FinalTurn", "Last player's final turn", "Uses the built-in exact victory-share policy, including shared victories and guaranteed wins. Learned weights are bypassed.", "Exact victory share", "Fixed"),
        new("FinalRoundChase", "Final-round lead chasing", "Continue when banking would leave the player behind. Uses the same safeguard as built-in Expectimax.", "Enabled", "Fixed")
    ];

    private static string Limit(int value, string unlimited) => value == int.MaxValue ? unlimited : value.ToString();
    private static string YesNo(bool value) => value ? "Yes" : "No";
}
