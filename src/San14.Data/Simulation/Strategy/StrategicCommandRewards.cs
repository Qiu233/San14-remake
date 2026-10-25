using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState ApplyStrategicCommandRewardsCurrent(GameState state, SourceWorldContext current,
        int executorPhysical, int kind, int outcome)
        => ApplyTalentCommonRewardsCurrent(state, current, executorPhysical, kind, outcome);

    //2B9810's actual intelligence-growth and pending-merit rules. Extracted
    //offline from the paired CE_TC data table; saved integer rules override
    //these immutable values. Outcome2 is failure and outcome6 great success.
    private static (int Growth, int Merit) ReadStrategicCommandRewardCurrent(GameState state, int kind, int outcome)
    {
        (int success, int failure, int normalMerit, int greatMerit) = kind switch
        {
            10 => (2500, 500, 140, 180), 11 => (5000, 1000, 150, 300),
            12 => (2500, 500, 160, 210), 13 => (2500, 500, 135, 135),
            14 => (2500, 1000, 90, 135), 20 => (2500, 500, 120, 180),
            24 => (3500, 500, 0, 0), _ => throw StrategyMissing("reward_kind")
        };
        int growth = StrategicParameter(state, $"kind-{kind}." + (outcome == 2 ? "failure-growth" : "success-growth"),
            outcome == 2 ? failure : success);
        int merit = outcome == 2 ? 0 : unchecked(StrategicParameter(state, $"kind-{kind}." +
            (outcome == 6 ? "great-merit" : "success-merit"), outcome == 6 ? greatMerit : normalMerit) * 10);
        return (growth, merit);
    }
}
