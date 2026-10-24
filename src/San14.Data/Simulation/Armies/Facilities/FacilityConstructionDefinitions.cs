using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record FacilityConstructionDefinition(int Physical, byte Category, ushort BaseGoldCost,
    ushort RequiredTrait, byte RequiredPolicyEffect, byte RequiredPolicyLevel, byte ExclusionRadius, uint TerrainMask);

internal static class FacilityConstructionDefinitions
{
    // Explicit consumers: arrow tower, military music stand, fire trap and pit.
    // Other catalog entries keep their existing unresolved consumers.
    internal static bool IsSupported(int? physical) => physical is 5 or 7 or 11 or 14;

    internal static FacilityConstructionDefinition Read(SourceWorldContext current, ObjectDefinitionReference reference) =>
        current.ReadFacilityConstructionDefinition(reference) ?? throw Missing("definition_fields");
    internal static ObjectDefinitionReference Selected(GameState state, SourceWorldContext current, UnitState image)
    {
        var owner = state.ReadArmyLifecycle()?.Receivers.FindUnit(image.Id)?.FacilityConstruction
            ?? throw Missing("current_selection");
        int slot = owner.DefinitionKey is null ? 0 : MapObjectSourceDirectory.DefinitionSlot(owner.DefinitionKey);
        return current.ReadObjectDefinitionReference(slot) ?? throw Missing("selected_definition_pointer");
    }
    internal static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    internal static InvalidDataException Missing(string field) => new("army.facility-construction." + field);
}

public sealed partial class SourceWorldContext
{
    private readonly Dictionary<int, FacilityConstructionDefinition> _facilityConstructionDefinitions = [];
    internal FacilityConstructionDefinition? ReadFacilityConstructionDefinition(ObjectDefinitionReference reference) =>
        reference.PhysicalSlot is int physical ? _facilityConstructionDefinitions.GetValueOrDefault(physical) : null;
    private void SetFacilityConstructionDefinition(int physical, S14ObjectDefinition row) =>
        _facilityConstructionDefinitions[physical] = new(physical, row.RawField2C, row.BaseGoldCost,
            row.RequiredTrait, row.RequiredPolicyEffect, row.RequiredPolicyLevel, row.ExclusionRadius, row.ConstructionTerrainMask);
}
