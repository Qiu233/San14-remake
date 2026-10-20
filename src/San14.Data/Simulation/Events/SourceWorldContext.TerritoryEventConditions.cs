using San14.Data.Formats;

namespace San14.Data;

public sealed partial class SourceWorldContext
{
    public float? ReadConditionForceTerritoryPercent(S14EventConditionForceReference force)
    {
        if (ReadForcePhysicalSlot(new(force.PhysicalSlot)) is not int owner) return null;
        int total = 0, owned = 0;
        for (int index = 0; index < 48400; index++)
        {
            var hex = ReadHexTableReference(index);
            if (hex is null || ReadHexSurfaceCode(hex) is not byte code) return null;
            var surface = ReadSurfaceTypeTableReference(code <= 30 ? code : 0);
            if (surface is null || ReadSurfaceTypeRawField2A(surface) is not ushort value) return null;
            if (value == 0) continue;
            total++;
            if (ReadHexForceSlot(hex) is not byte hexOwner) return null;
            if (hexOwner == owner) owned++;
        }
        return total == 0 ? 0f : MathF.Round(((float)owned * 100f / total) * 10f, MidpointRounding.AwayFromZero) / 10f;
    }
}
