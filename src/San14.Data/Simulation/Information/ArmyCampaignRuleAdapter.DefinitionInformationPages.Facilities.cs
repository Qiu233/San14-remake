namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class DefinitionInformationCurrent
    {
        private Formats.S14ObjectDefinitionCatalog FacilityCatalog() =>
            (rules.informationFacilities ??= rules._repository.LoadObjectDefinitions(rules._scenario.FixedDataSource.SourcePath)).Catalog;
        private int[] FacilityCandidates()
        {
            var ids = new List<int>();
            for (int slot = 0; slot < 31; slot++)
            {
                try
                {
                    var pointer = current.ReadObjectDefinitionReference(slot) ?? throw Missing("facility.pointer");
                    if (Need(current.ReadObjectDefinitionValidity(pointer), "facility.validity") &&
                        Need(current.ReadObjectDefinitionRaw2C(pointer), "facility.category") > 1)
                        ids.Add(Need(pointer.PhysicalSlot, "facility.receiver"));
                }
                catch (InvalidDataException error) { candidateIssues.Add(error.Message); }
            }
            return ids.ToArray();
        }
        private PreparedCell FacilityCell(int id, int column)
        {
            var pointer = new ObjectDefinitionReference(id);
            var row = FacilityCatalog().Definitions[id];
            byte Category() => Need(current.ReadObjectDefinitionRaw2C(pointer), "facility.category");
            FacilityConstructionDefinition Construction() => current.ReadFacilityConstructionDefinition(pointer) ?? throw Missing("facility.construction_fields");
            FacilityEffectDefinition Effects() => current.ReadFacilityEffectDefinition(pointer) ?? throw Missing("facility.effect_fields");
            int Durability() => Need(current.ReadObjectDefinitionBaseDurability(pointer), "facility.durability");
            int Trigger() => Need(Effects().TriggerRange, "facility.trigger_range");
            int Maximum() => Effects().MaximumFor(state, id);
            string Requirement()
            {
                var definition = Construction();
                if (definition.RequiredTrait != 0)
                {
                    var personality = current.ReadPersonalityTableReference(81) ?? throw Missing("facility.required_trait_pointer");
                    string name = Need(current.ReadSkillPersonalityValidity(personality), "facility.required_trait_validity")
                        ? current.ReadPersonalityName(personality) ?? throw Missing("facility.required_trait_name") : "";
                    return FacilityListFormat(Data(1, 0x398), name);
                }
                int raw = definition.RequiredPolicyEffect;
                int? effect = (current.ReadStaticTableReference(0x80880, raw < 101 ? raw : 0) ?? throw Missing("facility.required_policy_pointer")).PhysicalSlot;
                if (effect is not int physical || physical == DefinitionTableAnchor(0x80880)) return Data(1, 0x353);
                string effectName = state.PolicyEffects.GetValueOrDefault(physical)?.Name ?? throw Missing("facility.required_policy_name");
                return FacilityListFormat(Data(1, 0x399), effectName, definition.RequiredPolicyLevel);
            }
            return column switch
            {
                0 => MakeCell(() => row.Name, () => new NameKey(row.NameSortKey ?? throw Missing("facility.name_sort"))),
                1 => MakeCell(() => Category() is >= 1 and <= 4 ? Data(1, 0x40C + Category()) : "", () => new NumberKey(Category())),
                3 => MakeCell(() => Number(Construction().BaseGoldCost), () => new NumberKey(Construction().BaseGoldCost)),
                4 => MakeCell(() => Number(Durability()), () => new NumberKey(Durability())),
                5 => MakeCell(() => Category() is 2 or 3 ? Data(1, 0x353) : Number(Trigger()),
                    () => Category() is 2 or 3 ? new NumberKey(int.MaxValue, true) : new NumberKey(Trigger())),
                6 or 7 => MakeCell(() => Effects().Channel(column - 6).Effect is >= 1 and <= 18
                    ? Data(0, 0x805 + Effects().Channel(column - 6).Effect) : Data(1, 0x353),
                    () => new NumberKey(Effects().Channel(column - 6).Effect)),
                8 => MakeCell(() => id is 9 or 10 or 16 ? Data(1, 0x353) : FacilityListFormat(Data(1, 0x46B), Effects().Minimum, Maximum()),
                    () => id is 9 or 10 or 16 ? new NumberKey(int.MaxValue, true) : new NumberKey(unchecked(Maximum() + Effects().Minimum * 0xBD0F))),
                11 => MakeCell(Requirement),
                _ => throw Missing("facility.column." + column)
            };
        }
    }
}
