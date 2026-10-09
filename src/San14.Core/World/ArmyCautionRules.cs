namespace San14.Core;

public sealed record ArmyCautionRetirement(ArmyCautionsState Cautions,
    Dictionary<int, List<int?>?> GroupMembers, int[] RemovedRelationIds, int[] ChangedGroupIds);

public static class ArmyCautionRules
{
    /// <summary>Target selection is checked before the base field. An expired
    /// imported army selector remains assigned, although it has no reusable ID.</summary>
    public static bool? IsRelationRegisteredEligibility(ArmyCautionRelation relation) => relation.Unit.Kind switch
    {
        ArmyCautionUnitKind.Unknown => null,
        ArmyCautionUnitKind.None => false,
        ArmyCautionUnitKind.Unit or ArmyCautionUnitKind.Expired => relation.Base.IsKnown ? relation.Base.MapObjectId is not null : null,
        _ => throw new ArgumentOutOfRangeException(nameof(relation))
    };

    public static bool? IsGroupRegisteredEligibility(ArmyCautionGroupLabel label) =>
        label.IsKnown ? label.Label is > 0 : null;

    /// <summary>Reset matching relations before the caller removes their
    /// registry occurrences. All groups participate, even inactive groups.
    /// Labels are retained when the final member retires. Null means a required
    /// relation target or member list is unavailable; inputs remain unchanged.</summary>
    public static ArmyCautionRetirement? RetireArmy(ArmyCautionsState cautions,
        IReadOnlyDictionary<int, List<int?>?> groupMembers, int unitId)
    {
        if (cautions.Relations.Values.Any(relation => relation.Unit.Kind == ArmyCautionUnitKind.Unknown) ||
            groupMembers.Values.Any(members => members is null)) return null;

        var next = cautions.Copy();
        var removedRelations = new List<int>();
        foreach (var relation in cautions.Relations)
        {
            if (relation.Value.Unit is not { Kind: ArmyCautionUnitKind.Unit } target || target.UnitId != unitId) continue;
            next.Relations.Remove(relation.Key); // The omitted row is the reset relation.
            removedRelations.Add(relation.Key);
        }

        var nextMembers = new Dictionary<int, List<int?>?>();
        var changedGroups = new List<int>();
        foreach (var group in groupMembers)
        {
            var members = new List<int?>(group.Value!);
            if (members.RemoveAll(member => member == unitId) != 0) changedGroups.Add(group.Key);
            nextMembers.Add(group.Key, members);
        }
        return new(next, nextMembers, removedRelations.ToArray(), changedGroups.ToArray());
    }

    /// <summary>Allocate the lowest positive unused label within the supplied
    /// group selection. Labels outside that selection do not reserve a value.</summary>
    public static int? AllocateLowestGroupLabel(ArmyCautionsState cautions, IEnumerable<int> selectedGroupIds)
    {
        var occupied = new HashSet<int>();
        foreach (int groupId in selectedGroupIds)
        {
            var label = cautions.GetGroupLabel(groupId);
            if (!label.IsKnown) return null;
            if (label.Label is > 0) occupied.Add(label.Label.Value);
        }
        int candidate = 1;
        while (occupied.Contains(candidate)) candidate = checked(candidate + 1);
        return candidate;
    }
}
