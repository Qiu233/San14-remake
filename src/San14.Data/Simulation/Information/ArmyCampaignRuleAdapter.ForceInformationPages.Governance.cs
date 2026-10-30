using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class ForceInformationCurrent
    {
        private IReadOnlyDictionary<int, DoctrineResearchPanelDefinition>? researchPanels;
        private readonly Dictionary<(int Force, int Group), int> departmentCounts = [];
        private readonly Dictionary<(int Force, int Category), int> learnedPanelCounts = [];

        private PreparedCell GovernanceCell(int forceId, int column) => column switch
        {
            15 => MakeCell(() => Special(forceId) ? Data(1, 0x353) : Doctrine(forceId).Name,
                () => Special(forceId) ? new NameKey("", true) : new NameKey(Doctrine(forceId).Name)),
            16 => MakeCell(() =>
            {
                int person = Person(Need(Current.ReadAssignmentForceAdvisor52(forceId), "Force52_advisor"));
                return Need(Current.ReadPersonNativeValidity(person), "advisor_validity") ? PersonDisplay(person) : "";
            }, () =>
            {
                int raw = Need(Current.ReadAssignmentForceAdvisor52(forceId), "sort_Force52_advisor");
                return raw == 0 ? new NameKey("", true) : new NameKey(PersonDisplay(Person(raw)));
            }),
            29 => MakeCell(() => Valid(forceId) && !Special(forceId) ? Number(DoctrineLevel(forceId)) : "",
                () => new NumberKey(Valid(forceId) && !Special(forceId) ? DoctrineLevel(forceId) : int.MaxValue,
                    !Valid(forceId) || Special(forceId))),
            >= 30 and <= 34 => MakeCell(() => !Valid(forceId) ? "" : Special(forceId) ? Data(1, 0x353)
                    : Number(DepartmentCount(forceId, column - 30)),
                () => Valid(forceId) && !Special(forceId) ? new NumberKey(DepartmentCount(forceId, column - 30))
                    : new NumberKey(int.MaxValue, true)),
            >= 44 and <= 49 => MakeCell(() => DiplomacySuppressed() || Special(forceId) ? Data(1, 0x353)
                    : Number(Valid(forceId) ? LearnedPanelCount(forceId, PanelCategory(column)) : 0),
                () => Special(forceId) ? new NumberKey(int.MaxValue, true)
                    : new NumberKey(Valid(forceId) ? LearnedPanelCount(forceId, PanelCategory(column)) : 0)),
            _ => throw Missing("governance.column." + column)
        };

        private Formats.S14DoctrineDefinition Doctrine(int id)
        {
            int person = Ruler(id);
            int identity = Need(Current.ReadPersonIdentity(person), "doctrine_ruler_identity");
            int raw;
            if (state.Officers.TryGetValue(identity, out var owner))
                raw = Need(OfficerDoctrineRules.ReadDoctrineId(owner.DoctrineId, owner.AbilityState), "ruler14C_doctrine");
            else if (person == 0 && rules._scenario.Snapshot.Characters is { Count: > 0 } rows && rows[0].Slot == 0)
                raw = Need(rows[0].RawDoctrineId, "Person0_doctrine");
            else throw Missing("ruler_doctrine_owner");
            int selected = raw is >= 0 and < 7 ? raw : 0;
            return (rules._scenario.Snapshot.DoctrineParameters ?? throw Missing("Doctrine_catalog")).Doctrines[selected];
        }

        private int DoctrineLevel(int id) => Need(OwnedForce164Inputs.ReadLevel(state,
            Need(Current.ReadForce164(Force(id)), "Force164_experience")), "Force164_level");

        private int DepartmentCount(int id, int group)
        {
            if (departmentCounts.TryGetValue((id, group), out int count)) return count;
            var values = state.NativeArmySimulation?.Parameters ?? throw Missing("department_owner");
            count = 0;
            //22D480 counts each of the seven actual raw member identities in
            //the requested department. Pending and native validity are not gates.
            for (int ordinal = 0; ordinal < 7; ordinal++)
            {
                string key = BattleRemakeCurrentForcePolicies.DepartmentPersonKey(id, group, ordinal);
                if (values.Float32Values.ContainsKey(key) || !values.Int32Values.TryGetValue(key, out int raw) ||
                    raw is < 0 or > ushort.MaxValue) throw Missing("department_member");
                if (raw is >= 1 and <= 6000) count++;
            }
            departmentCounts[(id, group)] = count; return count;
        }

        private static int PanelCategory(int column) => column switch
        { 44 => 1, 45 => 2, 46 => 3, 47 => 4, 48 => 5, 49 => 6, _ => throw Missing("research_category") };

        private int LearnedPanelCount(int id, int category)
        {
            if (learnedPanelCounts.TryGetValue((id, category), out int count)) return count;
            var effects = state.Forces.GetValueOrDefault(id)?.PolicyEffects ?? throw Missing("research_Force_owner");
            researchPanels ??= rules.ReadDoctrineResearchDefinitions(Current);
            count = 0;
            //22C2C0 walks the eighty static pointers. Native slot0 is invalid;
            //root1 and74..79 are excluded. It reads active&&!pending directly.
            for (int raw = 0; raw < 80; raw++)
            {
                var pointer = Current.ReadStaticTableReference(0x85F20, raw) ?? throw Missing("research_panel_pointer");
                int physical = Need(pointer.PhysicalSlot, "research_panel_receiver");
                if (physical == 0 || physical == 1 || physical is >= 74 and <= 79) continue;
                if (!researchPanels.TryGetValue(physical, out var panel)) throw Missing("research_panel_definition");
                if (category != 0 && panel.Category != category) continue;
                if (!Need(effects.ReadActivation(physical), "research_activation")) continue;
                if (!Need(effects.ReadSuppression(physical), "research_pending")) count++;
            }
            learnedPanelCounts[(id, category)] = count; return count;
        }
    }
}
