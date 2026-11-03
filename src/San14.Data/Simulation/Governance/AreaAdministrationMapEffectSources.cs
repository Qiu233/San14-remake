namespace San14.Data;

/// <summary>One independently resolved City/Gate table receiver. A null native
/// slot is a known null pointer, whose native validity is false, not slot zero.</summary>
public sealed record AreaAdministrationMapEffectBase(int LookupSlot, int? NativeSlot, bool NativeValid);

/// <summary>Only the source facts needed to choose the Domestic effect's
/// existing model view. AreaPhysicalSlot already names the held receiver;
/// City/Gate are null only when the definition did not reach their branch.</summary>
public sealed record AreaAdministrationMapEffectSource(int AreaPhysicalSlot,
    ushort RawRepresentativeHex, int RepresentativeHexId, ushort RawHexObjectLink,
    byte ObjectDefinitionRaw2C, AreaAdministrationMapEffectBase? City,
    AreaAdministrationMapEffectBase? Gate)
{
    public bool UsesBaseView => ObjectDefinitionRaw2C == 1;
}

/// <summary>The original held order captured once for one open draft. The
/// source fields do not change when that draft stages appointments or focus.</summary>
public sealed record AreaAdministrationMapEffectSources(long CapturedRevision,
    IReadOnlyList<AreaAdministrationMapEffectSource> Rows);
