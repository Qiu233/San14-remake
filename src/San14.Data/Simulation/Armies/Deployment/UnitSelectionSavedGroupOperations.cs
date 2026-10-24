namespace San14.Data;

/// <summary>7D79F0's page-local sort then adjacent merge. Call once on all
/// initial row seeds; later append a retained row seed to the existing merged
/// groups and call again. Never rebuild this provenance from selected rows.</summary>
public static class UnitSelectionSavedGroupOperations
{
    public static UnitSelectionSavedGroups SortAndMerge(IReadOnlyList<UnitSelectionSavedGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        var sorted = groups.ToArray();
        try
        {
            UnitSelectionGroupSourceSort.Sort(sorted, Before);
            var result = new List<UnitSelectionSavedGroup>();
            foreach (var group in sorted)
            {
                var previous = result.LastOrDefault();
                if (previous is not null && previous.PhysicalPoints.SequenceEqual(group.PhysicalPoints) &&
                    previous.Key == group.Key)
                    result[^1] = previous with
                    {
                        OrderedUnitIds = Array.AsReadOnly(previous.OrderedUnitIds.Concat(group.OrderedUnitIds).ToArray())
                    };
                else result.Add(group);
            }
            return new(result.AsReadOnly(), null);
        }
        catch (InvalidDataException error)
        {
            //The input provenance is retained intact when native ordering
            //depends on an unavailable pointer identity. No partial sort leaks.
            return new(Array.AsReadOnly(groups.ToArray()), error.Message);
        }
    }

    private static bool Before(UnitSelectionSavedGroup left, UnitSelectionSavedGroup right)
    {
        if (left.PhysicalPoints.Count != right.PhysicalPoints.Count)
            return left.PhysicalPoints.Count < right.PhysicalPoints.Count;
        for (int i = 0; i < left.PhysicalPoints.Count; i++)
        {
            int? first = left.PhysicalPoints[i], second = right.PhysicalPoints[i];
            if (first == second) continue;
            //22DEF0(null) depends on the original allocation base. It cannot
            //be replaced by zero or an arbitrary native address in our game.
            if (!first.HasValue || !second.HasValue)
                throw new InvalidDataException("unit.selection_saved_groups.null-pointer-sort-identity");
            return first.Value < second.Value;
        }
        return false;
    }
}
