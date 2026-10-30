using San14.Core;

namespace San14.Data;

public static class FacilityRepairCapacity
{
    /// <summary>Current definition capacity. Map faction and Base affiliation
    /// remain separate; the returned derived value is never saved as a cap.</summary>
    public static int? ReadCapacity(GameState state, SourceWorldContext current,
        ObjectReference obj, OwnedBaseCapacityInputs panels)
    {
        if (current.ReadPhysicalObject(obj.PhysicalSlot ?? -1) is not { } fields) return null;
        var definition = current.ReadObjectDefinitionReference(fields.RawField12 <= 30 ? fields.RawField12 : 0);
        if (definition is null || current.ReadObjectDefinitionValidity(definition) is not bool valid) return null;
        if (!valid) return 0;
        if (current.ReadObjectDefinitionBaseDurability(definition) is not ushort basis ||
            current.ReadObjectForceSlot13(obj) is not byte raw) return null;
        var force = current.ReadForceTableReference(raw <= 51 ? raw : 0);
        return force is null ? null : Capacity(state,current,definition,force,basis,panels);
    }

    /// <summary>The definition/Force form used by ordinary selection columns.
    /// No temporary Object is constructed to obtain a derived definition cap.</summary>
    public static int? ReadCapacity(GameState state, SourceWorldContext current,
        ObjectDefinitionReference definition, ForceReference force, OwnedBaseCapacityInputs panels)
    {
        if(current.ReadObjectDefinitionValidity(definition) is not bool valid)return null;
        if(!valid)return 0;
        if(current.ReadObjectDefinitionBaseDurability(definition) is not ushort basis)return null;
        return Capacity(state,current,definition,force,basis,panels);
    }
    private static int? Capacity(GameState state,SourceWorldContext current,
        ObjectDefinitionReference definition,ForceReference force,ushort basis,OwnedBaseCapacityInputs panels)
    {
        if (current.ReadForceNativeValidity(force) is not bool forceValid) return null;
        if (!forceValid) return basis;
        if (current.ReadObjectDefinitionRaw2C(definition) is not byte category) return null;
        if (category != 2) return basis;
        return force.PhysicalSlot is int id && panels.ReadPanelModifier(state, id, 62) is int modifier
            ? unchecked(basis + modifier) : null;
    }
}
