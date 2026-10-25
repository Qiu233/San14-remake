using San14.Core;

namespace San14.Data;

/// <summary>Fixed CE monthly rule data, extracted offline from the matched data section.
/// Runtime overrides remain owned by the saved simulation; BSS inputs stay unknown.</summary>
internal static class MonthlyCampaignMaintenanceRules
{
    internal static int? ReadCurrent(GameState state, string key)
    {
        int? saved = BaseCaptureResourceInputs.ReadCurrent(state, key);
        if (saved.HasValue) return saved;
        return key switch
        {
            //27B0F0 compares World454 to the fixed CE start-period rule.
            SimulationParameterKeys.ForceRelations.SpecialPersonnelStartPeriod => 3,
            SimulationParameterKeys.MonthlyMaintenance.CrossingRelationBase => -50,
            SimulationParameterKeys.MonthlyMaintenance.CrossingRelationThreshold => 110,
            SimulationParameterKeys.MonthlyMaintenance.CrossingRelationHighBase => 60,
            SimulationParameterKeys.MonthlyMaintenance.CrossingRelationAddend => -50,
            SimulationParameterKeys.MonthlyMaintenance.ThirdForceRelationBase => -5,
            SimulationParameterKeys.MonthlyMaintenance.ThirdForceRelationAddend => -10,
            SimulationParameterKeys.MonthlyMaintenance.DifferentDoctrineLoyaltyLoss => 10,
            SimulationParameterKeys.MonthlyMaintenance.LoyaltyRandomBound => 2,
            SimulationParameterKeys.MonthlyMaintenance.LoyaltyCloseAffinityLoss => 10,
            SimulationParameterKeys.MonthlyMaintenance.LoyaltyMiddleAffinityLoss => 20,
            SimulationParameterKeys.MonthlyMaintenance.LoyaltyOtherAffinityLoss => 30,
            _ => null
        };
    }

    //220880 and226950 explicitly clear Area6B/6C. This fresh constructor
    //fact is never used to fill knowledge absent in an older saved world.
    internal static GameState InstallFresh(GameState state) => state with
    {
        Areas = state.Areas.ToDictionary(row => row.Key,
            row => row.Value with { MonthlyTimers = new(0, 0) })
    };
}
