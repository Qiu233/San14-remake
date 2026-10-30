namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class DefinitionInformationCurrent
    {
        private Formats.S14PolicyCatalog PolicyCatalog() =>
            (rules.informationPolicies ??= rules._repository.LoadPolicies(rules._scenario.FixedDataSource.SourcePath)).Catalog;
        private int[] PolicyCandidates()
        {
            var ids = new List<int>();
            for (int slot = 0; slot < 121; slot++)
            {
                try
                {
                    int id = (current.ReadStaticTableReference(0x78D50, slot) ?? throw Missing("policy.pointer")).PhysicalSlot ?? 0;
                    if (id == 0) continue;
                    var catalog = PolicyCatalog();
                    if ((uint)id >= (uint)catalog.Policies.Count) throw Missing("policy.identity");
                    int raw = catalog.Policies[id].RawEffectSlots[0];
                    int effect = (current.ReadStaticTableReference(0x80880, raw < 101 ? raw : 0) ?? throw Missing("policy.effect_pointer")).PhysicalSlot ?? 0;
                    if (effect == 0) continue;
                    if (!state.Policies.ContainsKey(id)) throw Missing("policy.current_definition");
                    ids.Add(id);
                }
                catch (InvalidDataException error) { candidateIssues.Add(error.Message); }
            }
            return ids.ToArray();
        }
        private PreparedCell PolicyCell(int id, int column)
        {
            var row = state.Policies.GetValueOrDefault(id) ?? throw Missing("policy.current_definition");
            return column switch
            {
                0 => MakeCell(() => row.Name, () => new NameKey(PolicyCatalog().Policies[id].SecondaryText)),
                1 => MakeCell(() => row.Description),
                2 => MakeCell(() => { int value = PolicyDepartment(id); return value < 5 ? Data(0, 0x169 + value) : ""; },
                    () => new NumberKey(PolicyDepartment(id))),
                >= 3 and <= 10 => MakeCell(() => PolicyEffectName(id, column - 3),
                    () => new NumberKey(PolicyEffectSlot(id, column - 3))),
                _ => throw Missing("policy.column." + column)
            };
        }
        private int PolicyDepartment(int id)
        {
            int value = Need(state.Policies.GetValueOrDefault(id)?.DepartmentCode, "policy.department");
            return (uint)value <= byte.MaxValue ? value : throw Missing("policy.department_range");
        }
        private int PolicyEffectSlot(int id, int ordinal)
        {
            var catalog = PolicyCatalog();
            if ((uint)id >= (uint)catalog.Policies.Count) throw Missing("policy.identity");
            var slots = catalog.Policies[id].RawEffectSlots;
            if ((uint)ordinal >= (uint)slots.Count) throw Missing("policy.effect_slot");
            return slots[ordinal];
        }
        private string PolicyEffectName(int id, int ordinal)
        {
            int raw = PolicyEffectSlot(id, ordinal);
            int effect = (current.ReadStaticTableReference(0x80880, raw < 101 ? raw : 0) ?? throw Missing("policy.effect_pointer")).PhysicalSlot ?? 0;
            return effect == 0 ? "" : state.PolicyEffects.GetValueOrDefault(effect)?.Name ?? throw Missing("policy.effect_definition");
        }
    }
}
