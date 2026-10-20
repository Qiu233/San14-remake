using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Original table constraints end here; the simulation uses independent definitions and instances.</summary>
internal sealed record ItemProjection(Dictionary<int, ItemDefinition> Definitions,
    Dictionary<int, ItemState> Items, string[] Issues)
{
    public static ItemProjection Create(ScenarioCityPreview source, uint marker)
    {
        int count = marker switch
        {
            0x33 or 0x5C => 201,
            0x66 => 202,
            _ => throw new InvalidDataException("Unsupported item initialization edition.")
        };
        bool embedded = marker != 0x33;
        S14ItemDefinitionCatalog fixedData = source.ItemDefinitions ??
            throw new InvalidDataException("Scenario initialization requires source item definitions.");
        S14ScenarioItemCatalog scenario = source.ScenarioItems ??
            throw new InvalidDataException("Scenario initialization requires source item state.");
        if (fixedData.FormatMarker != marker || scenario.FormatMarker != marker ||
            fixedData.Definitions?.Count != count || scenario.Items?.Count != count)
            throw new InvalidDataException("Scenario item tables have incompatible editions or counts.");

        var officers = source.Characters.Where(person => person.CharacterId != 0)
            .Select(person => person.CharacterId).ToHashSet();
        var definitions = new Dictionary<int, ItemDefinition>();
        var items = new Dictionary<int, ItemState>();
        // Location and event-driven appearance/transfer transitions are not projected yet.
        var issues = new List<string> { "items.runtime_state" };
        bool unresolvedHolders = false, unresolvedPresence = false;
        for (int slot = 0; slot < count; slot++)
        {
            S14ItemDefinition definition = fixedData.Definitions[slot];
            S14ScenarioItem item = scenario.Items[slot];
            if (definition is null || definition.Slot != slot || definition.Name is null ||
                item is null || item.Slot != slot ||
                (embedded && (item.EmbeddedDefinition is null || item.EmbeddedDefinition.Slot != slot ||
                    item.EmbeddedDefinition.Name is null)) ||
                (!embedded && item.EmbeddedDefinition is not null))
                throw new InvalidDataException($"Scenario item {slot} has an invalid identity or definition.");
            // The original valid-object predicate excludes slot zero. Raw tables retain it.
            if (slot == 0) continue;
            if (embedded) definition = item.EmbeddedDefinition!;
            byte kind = definition.RawKind, value = definition.RawValue;
            definitions.Add(slot, new ItemDefinition(slot, definition.Name,
                new OfficerAbilities(kind == 1 ? value : 0, kind == 2 ? value : 0,
                    kind == 3 ? value : 0, kind == 4 ? value : 0, kind == 5 ? value : 0))
            {
                LoyaltyValue = marker == 0x66 ? S14PersonnelSourceReader.ReadItemLoyaltyValue(definition, marker) : null
            });
            ItemHolder? holder = item.OwnerId == 0 ? new(null) :
                item.OwnerId <= 6000 && officers.Contains(item.OwnerId) ? new(item.OwnerId) : null;
            unresolvedHolders |= holder is null;
            unresolvedPresence |= item.RawStatusCode is null;
            items.Add(slot, new ItemState(slot, slot)
            {
                Holder = holder,
                Lifecycle = new(item.RawStatusCode is byte status ? ItemStatusKnowledge.Exact(status) : null,
                    item.RetainedLocationCode, item.RetainedFlags)
            });
        }
        if (unresolvedHolders) issues.Add("items.unresolved_holders");
        if (unresolvedPresence) issues.Add("items.unresolved_presence");
        return new(definitions, items, issues.ToArray());
    }
}
