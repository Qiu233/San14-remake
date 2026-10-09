namespace San14.Core;

internal readonly record struct PreparedOrderStageResult(GameState State, PeriodReportEntry[] Reports);

/// <summary>Runs the prepared personnel and departure phases of one period.</summary>
internal static class PeriodOrderPreparation
{
    // No copy/validation boundary here: the simulation already owns the period
    // candidate. Creation providers retain their separate detached candidate,
    // and failures are published only by the enclosing successful operation.
    internal static PreparedOrderStageResult ExecuteOwned(GameState periodEntry,
        GameState current, PlayableLandRoutes routes, IPlayableTalentResolution talent,
        IOrderRules deploymentCancellation, IPlayableArmyCreationRules? armyCreationRules)
    {
        current = ArmyIdentityAllocation.Initialize(current);
        // Check the whole departure batch before personnel/source callbacks or debits.
        int departures = periodEntry.PreparedDeployments.Values.Count(plan =>
            !PlayableOfficerAvailability.IsSourceBusy(current, current.Officers[plan.OfficerId]));
        if (departures > 0 && current.ArmyIdentities!.NextId + departures - 1 > int.MaxValue)
            throw new InvalidOperationException("The campaign army identity sequence is exhausted.");
        var personnel = PreparedPersonnelStage.ExecuteOwned(periodEntry, current, talent);
        var deployments = PreparedArmyDeploymentStage.ExecuteOwned(periodEntry, personnel.State,
            routes, deploymentCancellation, armyCreationRules);
        return new(deployments.State, [.. personnel.Reports, .. deployments.Reports]);
    }
}
