namespace San14.Core;

public enum WorldEntityKind
{
    Army, City, Gate, Force, Area, District, MapObject, Officer, Item, Landmark,
    ArmyCautionGroup, ArmyCaution,
    // Unassigned-location lookup entries are not city or gate entities.
    FallbackCity, FallbackGate
}

/// <summary>A domain entity ID. A null ID identifies no entity of this kind;
/// it never identifies a recycled source slot.</summary>
public sealed record WorldEntityReference(WorldEntityKind Kind, int? Id);

public sealed record BaseIndexState(WorldEntityReference Base,
    List<int?>? TerritoryHexIds, List<int?>? OfficerIds);

public sealed record ArmyIndexState(int UnitId, List<int?>?[] TraitRelatedUnitIds);

/// <summary>Ordered membership owned by the world. These indexes are updated
/// at their scheduled operation, rather than rebuilt from current validity.
/// A missing list is unavailable; an empty list is known to be empty.</summary>
public sealed record WorldIndexesState
{
    public Dictionary<WorldEntityKind, List<WorldEntityReference>?> Registries { get; init; } = [];
    public List<WorldEntityReference>? Bases { get; init; }
    public List<BaseIndexState> BaseIndexes { get; init; } = [];
    public Dictionary<int, ArmyIndexState> Armies { get; init; } = [];
    public Dictionary<int, List<int?>?> CautionGroupMembers { get; init; } = [];
    public List<int?>? ReturningPersonnelTaskOrder { get; init; }
    public List<MapObjectReceiverId?>? PendingObjectRetirements { get; init; }
    public List<int?>? PendingUnitIds { get; init; }
    public List<int?>? ChangedPendingUnitIds { get; init; }
    public List<int?>? ChangedWorkingUnitIds { get; init; }
    public bool? ChangedProcessing { get; init; }

    /// <summary>Membership can outlive an active unit. Allocation must keep
    /// these identities vacant until the owning index removes them.</summary>
    public IEnumerable<int> ReferencedUnitIds()
    {
        foreach (var roster in Registries.Values)
            foreach (var entity in roster ?? [])
                if (entity is { Kind: WorldEntityKind.Army, Id: int id }) yield return id;
        foreach (var army in Armies.Values)
        {
            yield return army.UnitId;
            foreach (var related in army.TraitRelatedUnitIds)
                foreach (int? id in related ?? []) if (id is int value) yield return value;
        }
        foreach (var members in CautionGroupMembers.Values)
            foreach (int? id in members ?? []) if (id is int value) yield return value;
        foreach (var list in new[] { PendingUnitIds, ChangedPendingUnitIds, ChangedWorkingUnitIds })
            foreach (int? id in list ?? []) if (id is int value) yield return value;
    }

    public WorldIndexesState Copy() => this with
    {
        Registries = Registries.ToDictionary(row => row.Key,
            row => row.Value is null ? null : new List<WorldEntityReference>(row.Value)),
        Bases = Bases is null ? null : new(Bases),
        BaseIndexes = BaseIndexes.Select(row => row with
        {
            TerritoryHexIds = CopyIds(row.TerritoryHexIds),
            OfficerIds = CopyIds(row.OfficerIds)
        }).ToList(),
        Armies = Armies.ToDictionary(row => row.Key, row => row.Value with
        {
            TraitRelatedUnitIds = row.Value.TraitRelatedUnitIds.Select(CopyIds).ToArray()
        }),
        CautionGroupMembers = CautionGroupMembers.ToDictionary(row => row.Key, row => CopyIds(row.Value)),
        ReturningPersonnelTaskOrder = CopyIds(ReturningPersonnelTaskOrder),
        PendingObjectRetirements = PendingObjectRetirements is null ? null : new(PendingObjectRetirements),
        PendingUnitIds = CopyIds(PendingUnitIds),
        ChangedPendingUnitIds = CopyIds(ChangedPendingUnitIds),
        ChangedWorkingUnitIds = CopyIds(ChangedWorkingUnitIds)
    };

    private static List<int?>? CopyIds(List<int?>? source) => source is null ? null : new(source);
}
