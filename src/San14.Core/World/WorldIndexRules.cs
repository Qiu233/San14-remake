namespace San14.Core;

public static class WorldIndexRules
{
    /// <summary>Call after applying the entity's registration eligibility.
    /// Registration of other kinds deliberately retains repeated entries.</summary>
    public static bool Register(List<WorldEntityReference> registry, WorldEntityReference entity)
    {
        if ((entity.Kind is WorldEntityKind.Army or WorldEntityKind.Officer or
            WorldEntityKind.ArmyCautionGroup or WorldEntityKind.ArmyCaution) && registry.Contains(entity))
            return false;
        registry.Add(entity);
        return true;
    }

    public static bool AddPendingUnit(List<int?> pending, int? unitId)
    {
        if (pending.Contains(unitId)) return false;
        pending.Add(unitId);
        return true;
    }

    /// <summary>Read entries in order, including entries appended by a
    /// visitor. Consumers that remove entries use RemoveAll separately.</summary>
    public static void VisitLive<T>(IReadOnlyList<T> entries, Action<T> visitor)
    {
        for (int index = 0; index < entries.Count; index++) visitor(entries[index]);
    }

    public static int RemoveAll<T>(List<T> entries, T value)
        => entries.RemoveAll(item => EqualityComparer<T>.Default.Equals(item, value));

    /// <summary>Changed orders are a separate pending batch. Repeated and
    /// empty references survive the handoff; later appends form the next batch.</summary>
    public static WorldIndexesState BeginChangedWork(WorldIndexesState indexes) => indexes with
    {
        ChangedWorkingUnitIds = indexes.ChangedPendingUnitIds is { } pending ? new(pending) :
            throw new InvalidOperationException("Pending changed-unit orders are unavailable."),
        ChangedPendingUnitIds = [],
        ChangedProcessing = true
    };

    public static WorldIndexesState CompleteChangedWork(WorldIndexesState indexes) => indexes with
    {
        ChangedWorkingUnitIds = [],
        ChangedProcessing = false
    };
}
