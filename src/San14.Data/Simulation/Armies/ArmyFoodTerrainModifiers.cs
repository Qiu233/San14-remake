using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

/// <summary>Operation-local result of the facility food-reduction writer.
/// The source resets Hex19 then takes the capped maximum of enabled effect18
/// channels. Build once for the whole expense batch, not once per Army.</summary>
internal static class ArmyFoodTerrainModifiers
{
    //Hex19 is latched by normal Turn38. Expense queries consume the saved
    //field; they must not run the facility writer ahead of that scheduled cut.
    internal static ImmutableArray<byte> ReadScheduled(GameState state)
    {
        var field = state.FacilityFoodReduction ?? throw Missing("scheduled_field");
        if (field.PercentByHex.IsDefault || field.PercentByHex.Length != 48400) throw Missing("scheduled_field_length");
        return field.PercentByHex;
    }

    internal static byte[] Capture(GameState state, SourceWorldContext current)
    {
        var values = new byte[48400];
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Object) ?? throw Missing("object_registry"))
        {
            if (entry is not { Kind: RegistryKind.Object, PhysicalSlot: int physical }) throw Missing("object_pointer");
            var obj = new ObjectReference(physical);
            var data = current.ReadPhysicalObject(physical) ?? throw Missing("object_fields");
            var definitionRef = current.ReadObjectDefinitionReference(data.RawField12 <= 30 ? data.RawField12 : 0)
                ?? throw Missing("definition_pointer");
            ushort fields = current.ReadMapObjectFields16(obj) ?? throw Missing("object_activation");
            if ((fields & 1) == 0) continue;
            var definition = current.ReadFacilityEffectDefinition(definitionRef) ?? throw Missing("definition_effects");
            if (definition.First.Effect != 18 && definition.Second.Effect != 18) continue;
            var channels = new[] { current.CreateNearbyTemporary(), current.CreateNearbyTemporary() };
            try
            {
                bool valid = current.ReadObjectNativeValidity(obj) ?? throw Missing("object_validity");
                bool definitionValid = current.ReadObjectDefinitionValidity(definitionRef) ?? throw Missing("definition_validity");
                if (!valid || !definitionValid) continue;
                byte category = current.ReadObjectDefinitionRaw2C(definitionRef) ?? throw Missing("definition_category");
                FacilityEffectTargets.Fill(state, current, obj, definition, data.RawField12, channels, category);
                for (int index = 0; index < 2; index++)
                {
                    var channel = definition.Channel(index);
                    if (channel.Effect != 18) continue;
                    foreach (var hex in FacilityEffectTargets.ReadHexes(current, channels[index]))
                    {
                        int at = hex.PhysicalSlot ?? throw Missing("effect_hex");
                        values[at] = (byte)Math.Min(100, Math.Max((int)values[at], channel.Amount));
                    }
                }
            }
            finally { foreach (var channel in channels) current.ClearNearbyTemporary(channel, true); }
        }
        return values;
    }

    private static InvalidDataException Missing(string field) => new("food_upkeep.facility_reduction." + field);
}
