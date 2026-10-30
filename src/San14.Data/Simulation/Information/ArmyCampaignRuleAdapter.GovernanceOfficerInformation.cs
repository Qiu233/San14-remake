using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The government selectors retain the common Person list reader,
    /// with their own authored groups and the caller's temporary appointments.</summary>
    public OfficerInformationPageSet PrepareGovernanceOfficerInformationPages(GameState state,
        int forceId, IReadOnlyList<int> actualOfficerIds, bool advisor, GovernanceRoster? draft = null,
        bool initialSort = true)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actualOfficerIds);
        var source = new OfficerInformationCurrent(this, state, forceId);
        return source.GovernancePages(actualOfficerIds, advisor, draft, initialSort);
    }

    public int? ReadGovernanceNewAppointmentPending(GameState state)
    {
        var parameters=state.NativeArmySimulation?.Parameters;
        return parameters is not null&&!parameters.Float32Values.ContainsKey(GovernanceRosterParameters.NewAppointmentPending)&&
            parameters.Int32Values.TryGetValue(GovernanceRosterParameters.NewAppointmentPending,out int value)
            ?Math.Clamp(value,0,ushort.MaxValue):null;
    }

    private sealed partial class OfficerInformationCurrent
    {
        private readonly Dictionary<(int Person, int Column), PreparedCell> governancePrepared = [];
        private Lazy<IReadOnlyDictionary<int, int>>? governanceAppointments;

        internal OfficerInformationPageSet GovernancePages(IReadOnlyList<int> ids, bool advisor,
            GovernanceRoster? draft, bool initialSort)
        {
            governanceAppointments = new(() => ReadGovernanceAppointments(draft));
            var columns = new Dictionary<int, OfficerInformationColumn>();
            void Add(int id, int caption, int width, int format, bool ascending = true, bool sortable = true)
                => columns[id] = new(id, caption, Data(1, caption), width, format, ascending, sortable, true);
            Add(0, 0x211, 132, 3);
            Add(10, 0x23a, 76, 5, ascending: false);
            Add(97, 0x44c, 76, 4);
            Add(24, 0x244, 76, 4);
            Add(26, 0x386, 104, 3, ascending: false);
            Add(68, 0x241, 132, 3);
            // This descriptor is initialized before use; its static record is zero.
            Add(69, 0x250, 48, 5);
            Add(70, 0x400, 832, 3, sortable: false);
            Add(77, 0x3d4, 76, 4);
            for (int id = 89; id <= 96; id++) Add(id, 0x48c + id - 89, 188, 3);
            int[][] tails = [[24, 26, 70, 77], [89, 90, 91, 92], [93, 94, 95, 96]];
            int[] common = advisor ? [0, 10, 97, 68, 69] : [0, 68, 69];
            int[] captions = [0x3cd, 0x48c, 0x48d];
            var groups = tails.Select((tail, index) => new OfficerInformationGroup(index, captions[index],
                Data(1, captions[index]), common.Concat(tail).Select(id => columns[id]).ToImmutableArray())).ToImmutableArray();
            var ordered = initialSort ? GovernanceInitialOrder(ids, advisor) : ids;
            return new(Data(1, 0x5d1), groups, ordered,
                (id, column) => GovernanceSelectionCell(id, column).Display with
                    { SortUnresolvedOwner = GovernanceSelectionCell(id, column).SortUnresolvedOwner },
                (id, column) => GovernanceSelectionCell(id, column).SortUnresolvedOwner,
                (left, right, column, ascending) => (GovernanceSelectionCell(left, column).Key ?? throw Missing("governance.sort_key"))
                    .Before(GovernanceSelectionCell(right, column).Key ?? throw Missing("governance.sort_key"), ascending));
        }

        private IReadOnlyList<int> GovernanceInitialOrder(IReadOnlyList<int> ids, bool advisor)
        {
            // These are the caller's initial list predicates. Header sorting
            // continues to use the shared source predicates without this tie.
            var keys = ids.Distinct().ToDictionary(id => id, id =>
            {
                var person = ReadPerson(id);
                return advisor ? (First: InformationAbility(person, 2), Policy: 0, Cost: 0)
                    : (First: MilitaryPolicyLevel(person), Policy: GovernanceRawPolicy(person), Cost: MilitaryPolicyCost(person).Total);
            });
            int[] order = ids.ToArray();
            DeploymentPersonTraitSort.Sort(order, (left, right) =>
            {
                var a = keys[left]; var b = keys[right];
                if (a.First != b.First) return a.First > b.First;
                if (a.Policy != b.Policy) return a.Policy < b.Policy;
                if (a.Cost != b.Cost) return a.Cost < b.Cost;
                return left < right;
            });
            return order;
        }

        private PreparedCell GovernanceSelectionCell(int id, int column)
        {
            if (column is not (68 or 69 or 70 or 77 or >= 89 and <= 96)) return Cell(id, column);
            if (governancePrepared.TryGetValue((id, column), out var saved)) return saved;
            try
            {
                var person = ReadPerson(id);
                saved = column switch
                {
                    68 => MakeCell(() => GovernancePolicy(person)?.Name, () =>
                    {
                        int raw = GovernanceRawPolicy(person);
                        return new NumberKey(raw, raw == 0);
                    }),
                    69 => MakeCell(() => Number(MilitaryPolicyLevel(person)),
                        () => new NumberKey(MilitaryPolicyLevel(person)),
                        () => Cells.CreditBonus(person.Candidate, 3) > 0 ? 0x41 : null),
                    70 => MakeCell(() => GovernancePolicy(person)?.Description),
                    77 => MakeCell(() => GovernanceAppointment(person) is int group
                            ? group == -1 ? Data(1, 0x243) : Data(0, 0x169 + group) : null,
                        () => GovernanceAppointment(person) is int group ? new NumberKey(group) : new NumberKey(0, true)),
                    _ => MakeCell(() => GovernancePolicyEffectName(person, column - 89),
                        () => new NumberKey(GovernancePolicyEffectSlot(person, column - 89)))
                };
            }
            catch (InvalidDataException error)
            { saved = new(new(null, null, error.Message), null, error.Message); }
            return governancePrepared[(id, column)] = saved;
        }

        private int GovernanceRawPolicy(Person person)
        {
            // The saved byte retains the valid zero sentinel; the domain's
            // optional PolicyId deliberately does not distinguish that from an
            // unavailable legacy field.
            return GovernanceByte(BattleRemakeCurrentForcePolicies.PersonFieldKey(person.Id, 0x185));
        }

        private int GovernanceByte(string key)
        {
            var parameters = state.NativeArmySimulation?.Parameters ?? throw Missing("governance.policy_parameters");
            return !parameters.Float32Values.ContainsKey(key) && parameters.Int32Values.TryGetValue(key, out int value) &&
                value is >= 0 and <= byte.MaxValue ? value : throw Missing("governance.policy_byte");
        }

        private int GovernancePolicyPhysical(Person person)
        {
            int raw = GovernanceRawPolicy(person);
            var reference = current.ReadStaticTableReference(0x78d50, raw < 121 ? raw : 0)
                ?? throw Missing("governance.policy_pointer");
            return Need(reference.PhysicalSlot, "governance.policy_receiver");
        }

        private PolicyDefinition? GovernancePolicy(Person person)
        {
            int physical = GovernancePolicyPhysical(person);
            return physical == 0 ? null : state.Policies.GetValueOrDefault(physical)
                ?? throw Missing("governance.policy_definition");
        }

        private int GovernancePolicyEffectSlot(Person person, int ordinal)
        {
            int physical = GovernancePolicyPhysical(person);
            // The invalid policy has a known zero key. Valid policies retain all
            // eight authored slots, including zero and repeated references.
            if (physical == 0) return 0;
            return GovernanceByte(BattleRemakeCurrentForcePolicies.PolicyEffectSlotKey(physical, ordinal));
        }

        private string? GovernancePolicyEffectName(Person person, int ordinal)
        {
            int raw = GovernancePolicyEffectSlot(person, ordinal);
            var reference = current.ReadStaticTableReference(0x80880, raw < 101 ? raw : 0)
                ?? throw Missing("governance.effect_pointer");
            int physical = Need(reference.PhysicalSlot, "governance.effect_receiver");
            if (physical == 0) return null;
            return state.PolicyEffects.GetValueOrDefault(physical)?.Name
                ?? throw Missing("governance.effect_name");
        }

        private int? GovernanceAppointment(Person person)
        {
            var appointments = (governanceAppointments ?? throw Missing("governance.appointments_owner")).Value;
            return appointments.TryGetValue(person.Id, out int group) ? group : null;
        }

        private IReadOnlyDictionary<int, int> ReadGovernanceAppointments(GovernanceRoster? draft)
        {
            var appointments = new Dictionary<int, int>();
            var force = current.ReadForceTableReference(playerForceId) ?? throw Missing("governance.force_pointer");
            if (!Need(current.ReadForceNativeValidity(force), "governance.force_validity")) return appointments;
            if (draft is not null)
            {
                if (draft.Groups is null || draft.Groups.Count != 5 ||
                    !draft.Groups.Select(group => group.Group).Order().SequenceEqual(Enumerable.Range(0, 5)))
                    throw Missing("governance.draft_groups");
                if (draft.AdvisorId is int advisor && advisor is >= 1 and <= 6000) appointments.TryAdd(advisor, -1);
                foreach (var group in draft.Groups.OrderBy(group => group.Group))
                {
                    if (group.OfficerIds is null || group.OfficerIds.Count > 7) throw Missing("governance.draft_members");
                    foreach (int member in group.OfficerIds)
                        if (member is >= 1 and <= 6000) appointments.TryAdd(member, group.Group);
                }
                return appointments;
            }
            var parameters = state.NativeArmySimulation?.Parameters ?? throw Missing("governance.appointments_parameters");
            int Word(string key) => !parameters.Float32Values.ContainsKey(key) &&
                parameters.Int32Values.TryGetValue(key, out int value) && value is >= 0 and <= ushort.MaxValue
                    ? value : throw Missing("governance.appointment_word");
            int currentAdvisor = Word(BattleRemakeCurrentForcePolicies.ForceFieldKey(playerForceId, 0x52));
            if (currentAdvisor is >= 1 and <= 6000) appointments.TryAdd(currentAdvisor, -1);
            for (int group = 0; group < 5; group++)
                for (int ordinal = 0; ordinal < 7; ordinal++)
                {
                    int member = Word(BattleRemakeCurrentForcePolicies.DepartmentPersonKey(playerForceId, group, ordinal));
                    if (member is >= 1 and <= 6000) appointments.TryAdd(member, group);
                }
            return appointments;
        }
    }
}
