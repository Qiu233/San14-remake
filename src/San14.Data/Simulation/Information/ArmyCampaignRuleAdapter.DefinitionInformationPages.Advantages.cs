using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class DefinitionInformationCurrent
    {
        private readonly Dictionary<int, int?> advantageForceOwners = [];
        private readonly Dictionary<int, int?> advantageProvinces = [];
        private int[] AdvantageCandidates(bool ownOnly)
        {
            // Reuse only the mature actual dispatcher filter, not its old flat display columns.
            var publication = rules.ReadInformationAdvantageList(state, current, playerForceId, ownOnly, "");
            if (publication.UnresolvedOwner is string issue) candidateIssues.Add(issue);
            return publication.Rows.Select(row => row.Id).ToArray();
        }
        private Formats.S14GrowthAdvantageDefinition AdvantageDefinition(int id) => rules._growthQueries.Definitions.Catalog.Advantages[id];
        private string AdvantageLiteral(int id, int offset, int length) => Formats.S14ForceNameDefinitionReader.ReadLiteralUnits(AdvantageDefinition(id).RawBytes.AsSpan(offset, length));
        private PreparedCell AdvantageCell(int id, int column)
        {
            if (column == 1)
            {
                CityDomesticPersonForceName? appearance = null;
                var prepared = MakeCell(() =>
                {
                    if (Need(rules.ReadApplicationInformationRestriction(state), "advantage.application_restriction") ||
                        Need(rules.ReadApplicationTutorialActive(), "advantage.application_tutorial")) return "";
                    int? force = AdvantageQualifiedForce(id);
                    if (force is not int physical) return "";
                    appearance = AdvantageForceName(physical);
                    return appearance?.Text ?? "";
                }, () => AdvantageQualifiedForce(id) is int force ? new ModeZeroNameKey(AdvantageForceSort(force)) : new ModeZeroNameKey("", true));
                return appearance is null ? prepared : prepared with { Display = prepared.Display with { MarkerColour = appearance.MarkerColour } };
            }
            return column switch
            {
                0 => MakeCell(() => AdvantageLiteral(id, 0, 14), () => new ModeZeroNameKey(AdvantageLiteral(id, 14, 44))),
                2 => MakeCell(() => AdvantageLiteral(id, 58, 104)),
                3 => MakeCell(() => AdvantageRequirement(id)),
                _ => throw Missing("advantage.column." + column)
            };
        }
        private int AdvantageAnchor(int table) => Need((current.ReadStaticTableReference(table, 0) ?? throw Missing("advantage.anchor_pointer")).PhysicalSlot, "advantage.anchor_receiver");
        private int? AdvantageProvince(int id)
        {
            if (advantageProvinces.TryGetValue(id, out var retained)) return retained;
            int? result = null;
            for (int slot = 0; slot < 23; slot++)
            {
                int physical = Need((current.ReadStaticTableReference(0xDC80, slot) ?? throw Missing("advantage.province_pointer")).PhysicalSlot, "advantage.province_receiver");
                if (Need(rules._growthQueries.ReadStateAdvantage(state, physical), "advantage.province_effect") == id - AdvantageAnchor(0x85990)) result = physical;
            }
            if (result == AdvantageAnchor(0xDC80)) result = null;
            advantageProvinces[id] = result;
            return result;
        }
        private int? AdvantageQualifiedForce(int id)
        {
            if (advantageForceOwners.TryGetValue(id, out var retained)) return retained;
            if (AdvantageProvince(id) is not int province) { advantageForceOwners[id] = null; return null; }
            var counts = new Dictionary<int, int>();
            foreach (var city in current.ReadRegistryReferences(RegistryKind.City) ?? throw Missing("advantage.city_registry"))
            {
                if (city is not { Kind: RegistryKind.City, PhysicalSlot: int physical }) throw Missing("advantage.city_payload");
                if (Need(rules._growthQueries.ReadCityState(state, physical), "advantage.city_province") != province - AdvantageAnchor(0xDC80)) continue;
                int force = Need(current.ReadBaseForceSlot(new(BaseKind.City, physical)), "advantage.city_force");
                counts[force] = counts.GetValueOrDefault(force) + 1;
            }
            int raw = Need(rules._growthQueries.ReadStateAdvantage(state, province), "advantage.owner_province_effect");
            int requiredId = Need((current.ReadStaticTableReference(0x85990, raw < 18 ? raw : 0) ?? throw Missing("advantage.owner_requirement_pointer")).PhysicalSlot, "advantage.owner_requirement_receiver");
            int required = Need(rules._growthQueries.ReadAdvantageRequirement(state, requiredId), "advantage.required_cities");
            foreach (var force in current.ReadRegistryReferences(RegistryKind.Force) ?? throw Missing("advantage.force_registry"))
            {
                if (force is not { Kind: RegistryKind.Force, PhysicalSlot: int physical }) throw Missing("advantage.force_payload");
                if (counts.GetValueOrDefault(physical) >= required && Need(current.ReadForceNativeValidity(new(physical)), "advantage.force_validity"))
                { advantageForceOwners[id] = physical; return physical; }
            }
            advantageForceOwners[id] = null;
            return null;
        }
        private int AdvantageRuler(int force)
        {
            ushort raw = Need(current.ReadForceRecord(force)?.RawRuler10, "advantage.ruler");
            return Need((raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "advantage.ruler_pointer");
        }
        private string AdvantageForceSort(int force)
        {
            int person = AdvantageRuler(force);
            int identity = Need(current.ReadPersonIdentity(person), "advantage.ruler_identity");
            if (state.Officers.TryGetValue(identity, out var row)) return row.CurrentNameSortKey ?? throw Missing("advantage.force_sort");
            if (person == 0 && rules._scenario.Snapshot.Characters is { Count: > 0 } rows) return rows[0].NameSortKey ?? throw Missing("advantage.force0_sort");
            throw Missing("advantage.force_sort_owner");
        }
        private CityDomesticPersonForceName? AdvantageForceName(int force)
        {
            string body;
            if (force is >= 46 and <= 50) body = rules.UnitDetailBaseName(state, current, BaseKind.City, force + 1);
            else
            {
                int person = AdvantageRuler(force);
                if (!Need(current.ReadPersonNativeValidity(person), "advantage.ruler_validity")) return null;
                int identity = Need(current.ReadPersonIdentity(person), "advantage.ruler_identity");
                body = state.Officers.GetValueOrDefault(identity)?.Name ?? throw Missing("advantage.force_name_owner");
            }
            byte colour = Need(current.ReadForceReportColor(new(force)), "advantage.force_colour");
            return new(Data(1, 0x571) + body, MapSpritePalettes.ForceFill(colour));
        }
        private string AdvantageRequirement(int id)
        {
            //356F90 takes the FIRST native-valid matching State; the owner search uses the LAST.
            for (int slot = 0; slot < 23; slot++)
            {
                int province = Need((current.ReadStaticTableReference(0xDC80, slot) ?? throw Missing("advantage.requirement_province_pointer")).PhysicalSlot, "advantage.requirement_province_receiver");
                if (!Need(current.ReadConditionStateValidity(new(province)), "advantage.requirement_province_validity") ||
                    Need(rules._growthQueries.ReadStateAdvantage(state, province), "advantage.requirement_province_effect") != id - AdvantageAnchor(0x85990)) continue;
                string name = Formats.S14ForceNameDefinitionReader.ReadLiteralUnits(rules._growthQueries.Definitions.Catalog.States[province].RawBytes.AsSpan(24, 6));
                int required = Need(rules._growthQueries.ReadAdvantageRequirement(state, id), "advantage.requirement_count");
                var vm = new NarrationResourceMessages(rules._repository.LoadMessages(rules.Seed.Language, Formats.MessageKind.Game).Messages);
                return vm.ResolveAdvantageInformationRequirement(name, required);
            }
            return "";
        }
    }
}
