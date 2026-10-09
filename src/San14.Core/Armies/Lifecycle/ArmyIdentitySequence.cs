using System.Text.Json.Serialization;

namespace San14.Core;

/// <summary>Campaign-wide logical army identities. Retiring an army never rewinds this sequence.</summary>
public sealed record ArmyIdentitySequence(long NextId)
{
    public const long Exhausted = (long)int.MaxValue + 1;
    [JsonIgnore]
    public int Candidate => NextId is >= 1 and <= int.MaxValue
        ? (int)NextId : throw new InvalidOperationException("The campaign army identity sequence is exhausted.");
    public ArmyIdentitySequence Advance() => new((long)Candidate + 1);
}

/// <summary>Logical identities retained by an imported compatibility owner.
/// This boundary exposes identities only, never source encodings or receiver slots.</summary>
public interface IArmyIdentityReferences
{
    IEnumerable<int> ReferencedArmyIds { get; }
}

public sealed partial record GameState
{
    // Null is an uninitialized imported/directly constructed world. Normal scenario
    // import and save loading initialize it; a direct caller bootstraps on publication.
    public ArmyIdentitySequence? ArmyIdentities { get; init; }
}

public static class ArmyIdentityAllocation
{
    public static ArmyIdentitySequence ForWorld(GameState state) => state.ArmyIdentities ?? Import(state);

    public static GameState Initialize(GameState state) => state.ArmyIdentities is not null
        ? state : state with { ArmyIdentities = Import(state) };

    /// <summary>Publish only with the successful creation. Candidate/rehearsal owners remain private.</summary>
    public static GameState Commit(GameState state, int allocatedId)
    {
        ArmyIdentitySequence sequence = ForWorld(state);
        if (sequence.Candidate != allocatedId)
            throw new InvalidOperationException("Army creation does not own the next campaign identity.");
        return state with { ArmyIdentities = sequence.Advance() };
    }

    // Used once at import/legacy loading, and at the save boundary to reject an
    // inconsistent persisted cursor. Ordinary allocation reads one scalar.
    public static ArmyIdentitySequence Import(GameState state)
    {
        long next = 1;
        foreach (int id in ReferencedIds(state)) next = Math.Max(next, (long)id + 1);
        return new(next);
    }

    internal static void ValidateSaved(GameState state)
    {
        if (state.ArmyIdentities is not { NextId: >= 1 and <= ArmyIdentitySequence.Exhausted } sequence ||
            sequence.NextId < Import(state).NextId)
            throw new InvalidDataException("Saved army identity sequence precedes an existing campaign identity.");
    }

    private static IEnumerable<int> ReferencedIds(GameState state)
    {
        foreach (int id in state.Units.Keys) yield return id;
        IEnumerable<UnitState> images = state.Units.Values.Concat(
            state.ArmyLifecycle?.Receivers.Entries.Where(slot => slot.PendingImage is not null)
                .Select(slot => slot.PendingImage!) ?? []).Concat(
            state.ForceDefeats.Values.SelectMany(record => record.WithdrawnUnits));
        foreach (UnitState unit in images)
        {
            yield return unit.Id;
            if (unit.TacticRuntime?.Pending is { } tactic) yield return tactic.TargetUnitId;
            foreach (var status in unit.TacticRuntime?.Statuses ?? []) yield return status.SourceUnitId;
            if (unit.NativeTacticRuntime?.ProvocationOwner is { Kind: DeploymentTargetKind.Unit } owner)
                yield return owner.Id;
            if (unit.MilitaryPlan is { } plan)
            {
                if (plan.Action.Target.Kind == DeploymentTargetKind.Unit) yield return plan.Action.Target.Id;
                if (plan.IncidentalEnemyId is int incidental) yield return incidental;
            }
        }
        foreach (var plan in state.PreparedDeployments.Values)
            foreach (var action in plan.Actions)
                if (action.Target.Kind == DeploymentTargetKind.Unit) yield return action.Target.Id;
        foreach (int id in state.PendingCombatSettlements?.ReferencedUnitIds() ?? []) yield return id;
        foreach (int id in state.ArmyLifecycle?.Indexes.ReferencedUnitIds() ?? []) yield return id;
        foreach (int id in state.ArmyLifecycle?.ArmyCautions?.ReferencedUnitIds() ?? []) yield return id;
        if (state.ArmyLifecycle?.ArmyGroups is { } groups)
            foreach (int id in groups.UnitAssignments.Keys) yield return id;
        if (state.ArmyLifecycle?.Indexes is { } indexes)
            foreach (var entity in (indexes.Bases ?? []).Concat(indexes.BaseIndexes.Select(row => row.Base)))
                if (entity is { Kind: WorldEntityKind.Army, Id: int id }) yield return id;
        foreach (var slot in state.ArmyLifecycle?.Receivers.Entries ?? [])
            if (slot.ActiveUnitId is int id) yield return id;
        foreach (int id in state.ArmyMapOccupancy?.ReferencedUnitIds() ?? []) yield return id;
        foreach (int id in state.NativeArmySimulation?.World.PendingArmyIds ?? []) yield return id;
        foreach (var pair in state.NativeArmySimulation?.World.ObjectPairs ?? [])
        {
            if (pair.FirstArmyId is int first) yield return first;
            if (pair.SecondArmyId is int second) yield return second;
        }
        foreach (var report in state.TacticReports ?? [])
        {
            yield return report.UnitId;
            if (report.TargetUnitId is int target) yield return target;
        }
        foreach (var report in state.NativeSubdayReports ?? []) yield return report.UnitId;
        if (state.ResourceState is IArmyIdentityReferences imported)
            foreach (int id in imported.ReferencedArmyIds) yield return id;
    }
}
