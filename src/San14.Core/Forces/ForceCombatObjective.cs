namespace San14.Core;

/// <summary>A force's current combat objective. Each nullable value is independently unknown.
/// The target is a location whose current owner is queried when the reward is reached.</summary>
public sealed record ForceCombatObjective(bool? Enabled, int? TargetLocationId, int? Progress)
{
    // Null retains unknown old-save knowledge; zero explicitly has no requester.
    // The byte-format boundary retains values52..255, which resolve to Force0.
    public int? RequestingForceId { get; init; }
    public int? RemainingMonths { get; init; }
    public static ForceCombatObjective Inactive { get; } = new(false, 0, 0)
        { RequestingForceId = 0, RemainingMonths = 0 };
}

public sealed partial record ForceState
{
    public ForceCombatObjective? CombatObjective { get; init; }
}

public sealed record ForceCombatRewardParameters(int TroopLossScale, int DestroyBonus);
public sealed record ForceCombatObjectiveResolution(bool Triggered, bool SelectedForceMatched,
    bool ObjectiveMatched, bool MarkSelectedForceDefeated, int? ProgressAfter, int ProgressDelta);

/// <summary>Current read-only queries. Branches read only the values they need;
/// absent data is distinct from a resolved false or zero.</summary>
public interface IForceCombatObjectiveQueries
{
    bool? ReadAttackerValid();
    bool? ReadSelectedForceTrackingEnabled();
    bool? ReadAttackerTracksSelectedForce();
    int? ReadSelectedForceId();
    bool? ReadObjectiveEnabled();
    int? ReadObjectiveProgress();
    int? ReadObjectiveTargetLocationId();
    int? ResolveObjectiveTargetForceId(int locationId);
    ForceCombatRewardParameters? ReadRewardParameters();
}

public static class ForceCombatObjectiveRules
{
    public const int ProgressLimit = 60000;

    public static ForceCombatObjectiveResolution? Resolve(IForceCombatObjectiveQueries current,
        int targetForceId, int troopLoss, bool destroyed)
    {
        if (current.ReadAttackerValid() is not bool valid) return null;
        if (!valid) return Unmatched();
        if (current.ReadSelectedForceTrackingEnabled() is not bool tracking) return null;
        bool selectedMatched = false;
        if (tracking)
        {
            if (current.ReadAttackerTracksSelectedForce() is not bool eligible) return null;
            if (eligible)
            {
                if (current.ReadSelectedForceId() is not int selected) return null;
                selectedMatched = targetForceId == selected;
            }
        }
        if (current.ReadObjectiveEnabled() is not bool enabled) return null;
        bool objectiveMatched = false;
        int? before = null;
        if (enabled)
        {
            if (current.ReadObjectiveProgress() is not int progress) return null;
            before = progress;
            if (progress < ProgressLimit)
            {
                if (current.ReadObjectiveTargetLocationId() is not int location ||
                    current.ResolveObjectiveTargetForceId(location) is not int force) return null;
                objectiveMatched = targetForceId == force && (troopLoss > 0 || destroyed);
            }
        }
        if (!selectedMatched && !objectiveMatched) return Unmatched();
        bool markDefeated = selectedMatched && destroyed;
        if (!objectiveMatched) return new(true, selectedMatched, false, markDefeated, null, 0);
        if (current.ReadRewardParameters() is not { } parameters) return null;
        int delta = troopLoss > 0 ? unchecked(parameters.TroopLossScale * troopLoss) / 10 : 0;
        if (destroyed) delta = unchecked(delta + parameters.DestroyBonus);
        int after = Math.Clamp(unchecked(before!.Value + delta), 0, ProgressLimit);
        return new(true, selectedMatched, true, markDefeated, after, delta);
    }

    private static ForceCombatObjectiveResolution Unmatched() => new(false, false, false, false, null, 0);
}
