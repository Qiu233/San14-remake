using San14.Core;

namespace San14.Data;

/// <summary>The ordered configuration writers operate inside the command's
/// private transaction. The existing saved policy bank remains authoritative.</summary>
internal sealed class GovernanceRosterApplication(GameState state, SourceWorldContext current,
    WorldIndexContext pool, int forceId)
{
    private GameState working = state;

    internal GameState Apply(GovernanceRoster roster)
    {
        BindPolicyReader();
        SetAdvisor(roster.AdvisorId ?? 0);
        foreach (var group in roster.Groups.OrderBy(group => group.Group))
            SetDepartment(group.Group, group.OfficerIds);
        return working.WithArmyLifecycle((working.ReadArmyLifecycle() ?? throw Missing("saved_pool")) with
            { Indexes = pool.ProjectOwnedRoles() });
    }

    private void SetAdvisor(int id)
    {
        int previous = Word(BattleRemakeCurrentForcePolicies.ForceFieldKey(forceId, 0x52));
        int previousIdentity = Valid(previous) ? Identity(previous) : 0;
        if (id == 0 ? previous == 0 : previousIdentity == id) return;
        if (id != 0)
        {
            //2EF690 clears the FIRST matching ID in the new advisor's policy
            //group. Its companion word is not cleared by that ID-only setter.
            var person = working.Officers.GetValueOrDefault(id) ?? throw Missing("advisor_person");
            int? group = person.PolicyId is int policy && working.Policies.TryGetValue(policy, out var definition)
                ? definition.DepartmentCode : null;
            if (group is >= 0 and < 5)
                for (int ordinal = 0; ordinal < 7; ordinal++)
                {
                    string key = BattleRemakeCurrentForcePolicies.DepartmentPersonKey(forceId, group.Value, ordinal);
                    if (Word(key) != id) continue;
                    Bank()[key] = 0;
                    break;
                }
        }
        Bank()[BattleRemakeCurrentForcePolicies.ForceFieldKey(forceId, 0x52)] = id;
        Bank()[BattleRemakeCurrentForcePolicies.ForceFieldKey(forceId, 0x16C)] = id == 0 ? 0 : NewPending();
        current.SetAssignmentForceAdvisor52(forceId, checked((ushort)id));
        Refresh(morale: true, policyCache: true, cities: true);
    }

    private void SetDepartment(int group, IReadOnlyList<int> ids)
    {
        if ((uint)group >= 5 || ids.Count > 7) throw Missing("department_shape");
        var pending = new int[ids.Count];
        //2ED980 constructs these values BEFORE validating or writing targets.
        //A duplicate old ID is scanned to its last matching valid slot.
        for (int at = 0; at < ids.Count; at++)
        {
            int? retained = null;
            for (int ordinal = 0; ordinal < 7; ordinal++)
            {
                int old = Word(BattleRemakeCurrentForcePolicies.DepartmentPersonKey(forceId, group, ordinal));
                if (Valid(old) && Identity(old) == ids[at])
                    retained = Word(BattleRemakeCurrentForcePolicies.DepartmentFilterKey(forceId, group, ordinal));
            }
            pending[at] = retained ?? NewPending();
        }
        int advisor = Word(BattleRemakeCurrentForcePolicies.ForceFieldKey(forceId, 0x52));
        if (ids.Contains(advisor) && advisor != 0) SetAdvisor(0);
        for (int ordinal = 0; ordinal < 7; ordinal++)
        {
            bool occupied = ordinal < ids.Count;
            Bank()[BattleRemakeCurrentForcePolicies.DepartmentPersonKey(forceId, group, ordinal)] = occupied ? ids[ordinal] : 0;
            Bank()[BattleRemakeCurrentForcePolicies.DepartmentFilterKey(forceId, group, ordinal)] = occupied ? pending[ordinal] : 0;
        }
        //The expansion byte belongs to capacity and is deliberately untouched.
        if (group == 1) Refresh(morale: true, policyCache: false, cities: false);
        else if (group == 3) Refresh(morale: false, policyCache: false, cities: true);
        else if (group == 4) Refresh(morale: false, policyCache: true, cities: false);
    }

    private void Refresh(bool morale, bool policyCache, bool cities)
    {
        BindPolicyReader();
        var result = PersonAdvisorResetSource.RefreshGovernanceCurrent(working, current, pool,
            new(forceId), morale, policyCache, cities);
        working = result.State ?? throw Missing(result.UnresolvedOwner ?? "derived_refresh");
        BindPolicyReader();
    }

    private void BindPolicyReader() => current.BaseForcePolicyEffectReader = (force, effect) =>
        force.PhysicalSlot is int physical && working.Forces.TryGetValue(physical, out var owner)
            ? BattleRemakeCurrentForcePolicies.ReadForceEffect(working, owner, effect, current) : null;

    private Dictionary<string, int> Bank() => working.NativeArmySimulation?.Parameters.Int32Values
        ?? throw Missing("parameter_owner");
    private int Word(string key) => Bank().TryGetValue(key, out int value) && value is >= 0 and <= ushort.MaxValue
        ? value : throw Missing("word:" + key);
    private int NewPending()
    {
        var parameters = working.NativeArmySimulation?.Parameters ?? throw Missing("parameter_owner");
        if (parameters.Float32Values.ContainsKey(GovernanceRosterParameters.NewAppointmentPending) ||
            !parameters.Int32Values.TryGetValue(GovernanceRosterParameters.NewAppointmentPending, out int value))
            throw Missing("new_appointment_pending");
        return Math.Clamp(value, 0, ushort.MaxValue);
    }
    private int Physical(int id) => (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))
        ?.PhysicalSlot ?? throw Missing("person_pointer");
    private bool Valid(int id) => current.ReadPersonNativeValidity(Physical(id)) ?? throw Missing("person_validity");
    private int Identity(int id) => current.ReadPersonIdentity(Physical(id)) ?? throw Missing("person_identity");
    private static InvalidDataException Missing(string name) => new("governance.application." + name);
}
