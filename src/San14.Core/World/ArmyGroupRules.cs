namespace San14.Core;

public static class ArmyGroupRules
{
    /// <summary>Null means an assignment needed for this traversal is unknown.
    /// Pending or otherwise excluded units are skipped before assignment reads.</summary>
    public static List<int>? GetMembers(ArmyGroupsState groups, int groupId,
        IEnumerable<int> roster, IReadOnlySet<int> excludedUnitIds)
    {
        var members = new List<int>();
        foreach (int unitId in roster)
        {
            if (excludedUnitIds.Contains(unitId)) continue;
            if (!groups.UnitAssignments.TryGetValue(unitId, out var assignment) || !assignment.IsKnown)
                return null;
            if (assignment.GroupId == groupId) members.Add(unitId);
        }
        return members;
    }

    /// <summary>The first eligible member determines the district. An invalid
    /// first commander does not allow a later member to replace it.</summary>
    public static ArmyGroupDistrict? GetDistrict(ArmyGroupsState groups, int groupId,
        IEnumerable<int> roster, IReadOnlySet<int> excludedUnitIds,
        Func<int, ArmyGroupCommander?> commander)
    {
        foreach (int unitId in roster)
        {
            if (excludedUnitIds.Contains(unitId)) continue;
            if (!groups.UnitAssignments.TryGetValue(unitId, out var assignment) || !assignment.IsKnown)
                return null;
            if (assignment.GroupId != groupId) continue;
            var first = commander(unitId);
            if (first is null) return null;
            return first.IsValid
                ? new(first.DistrictId, first.HasRecognizedDistrict)
                : new(null, false);
        }
        return new(null, false);
    }

    /// <summary>The caller supplies candidate group order and the roster with
    /// the newly registered unit already included. Null rejects the operation
    /// without changing the supplied state.</summary>
    public static ArmyGroupsState? AdmitCreation(ArmyGroupsState groups, int unitId,
        IReadOnlyList<int> roster, IReadOnlySet<int> excludedUnitIds,
        Func<int, ArmyGroupCommander?> commander, int? playerPrimaryDistrictId,
        IEnumerable<int> candidateGroupIds,
        ArmyGroupLeaderPolicy leaderPolicy = ArmyGroupLeaderPolicy.PreserveForPlayerDistrict)
    {
        if (groups.UnitAssignments.TryGetValue(unitId, out var previous) &&
            (!previous.IsKnown || previous.GroupId is not null)) return null;
        var next = groups.Copy();
        next.UnitAssignments[unitId] = new(true, null);
        int? selectedGroup = null;
        foreach (int groupId in candidateGroupIds)
        {
            var district = GetDistrict(next, groupId, roster, excludedUnitIds, commander);
            if (district is null) return null;
            if (district.IsRecognized) continue;
            selectedGroup = groupId;
            break;
        }
        if (selectedGroup is not int selected) return null;
        next.UnitAssignments[unitId] = new(true, selected);
        var groupDistrict = GetDistrict(next, selected, roster, excludedUnitIds, commander);
        var created = commander(unitId);
        if (groupDistrict is null || created is null) return null;
        if (created.CanLeadGroup && (leaderPolicy == ArmyGroupLeaderPolicy.AssignCommander ||
            groupDistrict.DistrictId != playerPrimaryDistrictId))
            next.Leaders[selected] = new(true, created.Officer);
        return next;
    }

    /// <summary>Before removing a retiring member, replace its matching leader
    /// with the first different commander identity. An ineligible candidate
    /// ends the search without changing the leader.</summary>
    public static ArmyGroupsState? UpdateLeaderBeforeRetirement(ArmyGroupsState groups,
        int retiringUnitId, IReadOnlyList<int> roster, IReadOnlySet<int> excludedUnitIds,
        Func<int, ArmyGroupCommander?> commander)
    {
        if (!groups.UnitAssignments.TryGetValue(retiringUnitId, out var assignment) || !assignment.IsKnown)
            return null;
        if (assignment.GroupId is not int groupId) return groups;
        var district = GetDistrict(groups, groupId, roster, excludedUnitIds, commander);
        if (district is null) return null;
        if (!district.IsRecognized) return groups;
        var leader = groups.GetLeader(groupId);
        var retiring = commander(retiringUnitId);
        if (!leader.IsKnown || retiring is null) return null;
        if (leader.Officer != retiring.Officer) return groups;
        foreach (int unitId in roster)
        {
            if (excludedUnitIds.Contains(unitId)) continue;
            if (!groups.UnitAssignments.TryGetValue(unitId, out var candidateAssignment) || !candidateAssignment.IsKnown)
                return null;
            if (candidateAssignment.GroupId != groupId) continue;
            var candidate = commander(unitId);
            if (candidate is null) return null;
            if (candidate.Officer == leader.Officer) continue;
            if (!candidate.CanLeadGroup) return groups;
            var next = groups.Copy();
            next.Leaders[groupId] = new(true, candidate.Officer);
            return next;
        }
        return groups;
    }

    /// <summary>Called after roster removal. The data boundary supplies the
    /// reset leader identity so a default lookup officer is not lost.</summary>
    public static ArmyGroupsState? RemoveUnitAndResetEmptyGroup(ArmyGroupsState groups,
        int unitId, IReadOnlyList<int> rosterAfterRemoval, IReadOnlySet<int> excludedUnitIds,
        ArmyGroupOfficerReference? emptyGroupLeader = null)
    {
        if (!groups.UnitAssignments.TryGetValue(unitId, out var assignment) || !assignment.IsKnown)
            return null;
        var next = groups.Copy();
        next.UnitAssignments.Remove(unitId);
        if (assignment.GroupId is not int groupId) return next;
        var members = GetMembers(next, groupId, rosterAfterRemoval, excludedUnitIds);
        if (members is null) return null;
        if (members.Count == 0)
        {
            next.Leaders[groupId] = new(true, emptyGroupLeader ?? next.ResetLeader);
            next.CautionTargets?.Remove(groupId);
        }
        return next;
    }
}
