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
        // Allocate the initial identity before any personnel callback, preserving
        // the established failure order when the current identity range is exhausted.
        int firstUnitId = current.Units.Count == 0 ? 1 : checked(current.Units.Keys.Max() + 1);
        var personnel = PreparedPersonnelStage.ExecuteOwned(periodEntry, current, talent);
        var deployments = PreparedArmyDeploymentStage.ExecuteOwned(periodEntry, personnel.State,
            routes, deploymentCancellation, armyCreationRules, firstUnitId);
        return new(deployments.State, [.. personnel.Reports, .. deployments.Reports]);
    }
}
