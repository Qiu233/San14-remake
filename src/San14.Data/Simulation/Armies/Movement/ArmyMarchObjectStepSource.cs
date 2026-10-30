namespace San14.Data;

internal enum ArmyMarchObjectStepKind { Empty, Invalid, City, Gate, GenericPassable, GenericBlocked }

//3014C0 and2D99C0 classify the same current Object, but consume an invalid
//receiver at different times. Keep the classification separate from writes.
internal static class ArmyMarchObjectStepSource
{
    internal static ArmyMarchObjectStepKind? Read(SourceWorldContext current, ushort next)
    {
        var hex = current.ReadHexTableReference(next < 48400 ? next : 0);
        if (hex?.PhysicalSlot is null || current.ReadHexObjectLink(hex) is not ushort link) return null;
        if (link == 0) return ArmyMarchObjectStepKind.Empty;
        var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0);
        if (obj is null || current.ReadObjectNativeValidity(obj) is not bool valid) return null;
        if (!valid) return ArmyMarchObjectStepKind.Invalid;
        if (obj.PhysicalSlot is >= 1 and <= 51) return ArmyMarchObjectStepKind.City;
        if (obj.PhysicalSlot is >= 52 and <= 61) return ArmyMarchObjectStepKind.Gate;
        if (obj.PhysicalSlot is not int physical || current.ReadPhysicalObject(physical) is not { } fields)
            return null;
        var definition = current.ReadObjectDefinitionReference(fields.RawField12 <= 30 ? fields.RawField12 : 0);
        if (definition is null || current.ReadObjectDefinitionRaw2C(definition) is not byte category) return null;
        //Neither reached source classifier inserts a definition-validity test.
        return category == 4 ? ArmyMarchObjectStepKind.GenericPassable : ArmyMarchObjectStepKind.GenericBlocked;
    }
}
