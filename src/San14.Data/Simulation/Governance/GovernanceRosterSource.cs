using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>A view of the saved policy owner or an explicitly supplied normal
/// resource owner before startup. This never imports the
/// legacy six-slot draft into the native ordered departments.</summary>
internal static class GovernanceRosterSource
{
    // 30BDF0(group): Data section0, 0x169 + group. The separate Data1
    // phrases used by appointment narration are not department headings.
    private static readonly int[] GroupNames = [361, 362, 363, 364, 365];

    internal static GovernanceRosterView Read(GameState state, int forceId,
        S14DoctrineCatalog? doctrines, IReadOnlyList<GovernanceCandidate> candidates,
        ForcePolicyPersonnelFields? normalSource = null)
    {
        var groups = new List<GovernanceDepartmentView>(5);
        GovernanceRosterMember? advisor = null;
        string? unresolved = null;
        if (!state.Forces.TryGetValue(forceId, out var force) || forceId is < 1 or > 51)
            return new(forceId, null, [], [], null, null, "governance.force");
        var values = state.NativeArmySimulation?.Parameters.Int32Values;
        int? advisorId = normalSource is not null ? normalSource.Advisor
            : Word(BattleRemakeCurrentForcePolicies.ForceFieldKey(forceId, 0x52));
        int? advisorPending = normalSource is not null ? normalSource.AdvisorPending
            : Word(BattleRemakeCurrentForcePolicies.ForceFieldKey(forceId, 0x16C));
        if (advisorId is null || advisorPending is null) unresolved = "governance.advisor_owner";
        else if (advisorId.Value != 0) advisor = new(advisorId.Value, advisorPending.Value);

        var doctrine = ReadDoctrine(state, force, doctrines);
        for (int group = 0; group < 5; group++)
        {
            string? missing = null;
            int? expansion = normalSource is not null ? normalSource.Departments[group].Expansion
                : Byte(BattleRemakeCurrentForcePolicies.DepartmentTrailerKey(forceId, group));
            int? capacity = doctrine is not null && expansion is int extra
                ? doctrine.RawBytes[91 + group] + extra : null;
            if (capacity is null) missing = "governance.department_capacity";
            var members = new List<GovernanceRosterMember>(7);
            for (int ordinal = 0; ordinal < 7; ordinal++)
            {
                int? person = normalSource is not null ? normalSource.Departments[group].Members[ordinal]
                    : Word(BattleRemakeCurrentForcePolicies.DepartmentPersonKey(forceId, group, ordinal));
                int? pending = normalSource is not null ? normalSource.Departments[group].PendingValues[ordinal]
                    : Word(BattleRemakeCurrentForcePolicies.DepartmentFilterKey(forceId, group, ordinal));
                if (person is null || pending is null) { missing ??= "governance.department_owner"; continue; }
                if (person.Value != 0) members.Add(new(person.Value, pending.Value));
            }
            groups.Add(new(group, GroupNames[group], capacity, expansion, members.AsReadOnly(), missing));
            unresolved ??= missing;
        }
        int? totalCost = force.RulerId is int currentRuler && PolicyCosts.TryCalculate(state, currentRuler, out var rulerCost)
            ? rulerCost.Total : null;
        foreach (var member in groups.SelectMany(group => group.Members))
        {
            if (totalCost is null || !PolicyCosts.TryCalculate(state, member.OfficerId, out var cost)) { totalCost = null; break; }
            totalCost = checked(totalCost!.Value + cost.Total);
        }
        // CostLimit from legacy Governance is provisional. It is deliberately
        // not presented as the source capacity/experience cost-limit consumer.
        return new(forceId, advisor, groups.AsReadOnly(), candidates, totalCost, null, unresolved);

        int? Word(string key) => values is not null && values.TryGetValue(key, out int value) &&
            value is >= 0 and <= ushort.MaxValue ? value : null;
        int? Byte(string key) => values is not null && values.TryGetValue(key, out int value) &&
            value is >= 0 and <= byte.MaxValue ? value : null;
    }
    internal static S14DoctrineDefinition? ReadDoctrine(GameState state, ForceState force, S14DoctrineCatalog? doctrines)
    {
        if (force.RulerId is int rulerId && state.Officers.TryGetValue(rulerId, out var ruler) &&
            OfficerDoctrineRules.ReadDoctrineId(ruler.DoctrineId, ruler.AbilityState) is int doctrineId &&
            doctrineId is >= 1 and <= 6 && state.Doctrines.ContainsKey(doctrineId))
            return doctrines?.Doctrines.FirstOrDefault(row => row.Slot == doctrineId &&
                !string.IsNullOrEmpty(row.Name) && row.RawBytes.Length == 102);
        return null;
    }

}
