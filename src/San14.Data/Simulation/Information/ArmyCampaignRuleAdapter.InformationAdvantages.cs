using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal InformationList ReadInformationAdvantageList(GameState state, SourceWorldContext current,
        int playerForceId, bool ownOnly, string title)
    {
        var rows = ImmutableArray.CreateBuilder<InformationRow>();
        var issues = new HashSet<string>(StringComparer.Ordinal);
        var columns = new[] { InformationColumnFor("name", title), InformationColumnFor("description"),
            InformationColumnFor("required_cities") };
        try
        {
            if (ownOnly)
            {
                bool restricted = InfoDefinitionKnown(ReadApplicationInformationRestriction(state), "advantage.application_task_scene");
                if (restricted) return Result();
                if (InfoDefinitionKnown(ReadApplicationTutorialActive(), "advantage.application_tutorial")) return Result();
            }
            var catalog = _growthQueries.Definitions.Catalog;
            int advantageAnchor = InfoDefinitionKnown((current.ReadStaticTableReference(0x85990, 0)
                ?? throw InfoDefinitionMissing("advantage.anchor_lookup")).PhysicalSlot, "advantage.anchor_receiver");
            int stateAnchor = ownOnly ? InfoDefinitionKnown((current.ReadStaticTableReference(0xDC80, 0)
                ?? throw InfoDefinitionMissing("advantage.State_anchor_lookup")).PhysicalSlot, "advantage.State_anchor_receiver") : 0;
            int player = ownOnly ? InfoDefinitionKnown((current.ReadForceTableReference(playerForceId)
                ?? throw InfoDefinitionMissing("advantage.player_lookup")).PhysicalSlot, "advantage.player_receiver") : 0;
            IReadOnlyList<RegistryObjectReference?>? cities = null;
            for (int lookup = 0; lookup < 18; lookup++)
            {
                try
                {
                    int? actual = (current.ReadStaticTableReference(0x85990, lookup)
                        ?? throw InfoDefinitionMissing("advantage.lookup")).PhysicalSlot;
                    //233BC0 validity and22DB10 use the actual receiver, not the lookup ordinal.
                    if (actual is null || actual == advantageAnchor) continue;
                    int id = actual.Value;
                    if ((uint)id >= catalog.Advantages.Count) throw InfoDefinitionMissing("advantage.receiver");
                    if (InfoDefinitionKnown(_growthQueries.ReadAdvantageEffect(state, id, 0), "advantage.first_effect") == 0) continue;
                    if (ownOnly)
                    {
                        int? province = null;
                        //The source deliberately keeps the LAST matching State pointer.
                        for (int slot = 0; slot < 23; slot++)
                        {
                            int? physical = (current.ReadStaticTableReference(0xDC80, slot)
                                ?? throw InfoDefinitionMissing("advantage.State_lookup")).PhysicalSlot;
                            if (physical is not int stateId) throw InfoDefinitionMissing("advantage.State_receiver");
                            if (InfoDefinitionKnown(_growthQueries.ReadStateAdvantage(state, stateId), "advantage.State30") == id - advantageAnchor)
                                province = stateId;
                        }
                        if (province is null || province == stateAnchor) continue; //2345D0 actual State validity.
                        byte raw = InfoDefinitionKnown(_growthQueries.ReadStateAdvantage(state, province.Value), "advantage.selected_State30");
                        int requiredId = InfoDefinitionKnown((current.ReadStaticTableReference(0x85990, raw < 18 ? raw : 0)
                            ?? throw InfoDefinitionMissing("advantage.requirement_lookup")).PhysicalSlot, "advantage.requirement_receiver");
                        byte required = InfoDefinitionKnown(_growthQueries.ReadAdvantageRequirement(state, requiredId), "advantage.requirement");
                        cities ??= current.ReadRegistryReferences(RegistryKind.City)
                            ?? throw InfoDefinitionMissing("advantage.registered_Cities");
                        int count = 0; bool reached = false;
                        foreach (var city in cities)
                        {
                            if (city is null || city.Kind != RegistryKind.City || city.PhysicalSlot is not int cityId)
                                throw InfoDefinitionMissing("advantage.City_payload");
                            if (InfoDefinitionKnown(_growthQueries.ReadCityState(state, cityId), "advantage.CityCE") == province.Value - stateAnchor &&
                                InfoDefinitionKnown(current.ReadBaseForceSlot(new(BaseKind.City, cityId)), "advantage.City_Force") == player)
                                count++;
                            //The original tests after each registered City, even a nonmatching one.
                            if (count >= required) { reached = true; break; }
                        }
                        if (!reached) continue;
                    }
                    var definition = catalog.Advantages[id];
                    string name = S14ForceNameDefinitionReader.ReadLiteralUnits(definition.RawBytes.AsSpan(0, 14));
                    string description = S14ForceNameDefinitionReader.ReadLiteralUnits(definition.RawBytes.AsSpan(14, 44));
                    var cells = ImmutableDictionary.CreateBuilder<string, InformationCell>(StringComparer.Ordinal);
                    cells["name"] = new(name, SortText: name); cells["description"] = new(description);
                    byte? requirement = _growthQueries.ReadAdvantageRequirement(state, id);
                    cells["required_cities"] = requirement.HasValue ? new(Number: requirement.Value)
                        : new(UnresolvedOwner: "information.advantage.requirement");
                    rows.Add(new((int)InformationListKind.Advantages, id, name, cells.ToImmutable()));
                }
                catch (InvalidDataException error) { issues.Add(error.Message); }
            }
        }
        catch (InvalidDataException error) { issues.Add(error.Message); }
        return Result();

        InformationList Result() => new(title, columns.ToImmutableArray(), rows.ToImmutable(),
            issues.Count == 0 ? null : string.Join(";", issues));
    }

    private static T InfoDefinitionKnown<T>(T? value, string field) where T : struct =>
        value ?? throw InfoDefinitionMissing(field);
    private static InvalidDataException InfoDefinitionMissing(string field) => new("information." + field);
}
