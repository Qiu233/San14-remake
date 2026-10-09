namespace San14.Data;

public enum ArmySavedRegionWriteKind { Reset, Creation, ReachedSnapshot, Retained }

/// <summary>Army word152 is a saved native Hex index. A retained null remains
/// unknown; it is not replaced by the current Hex, a Core region, or Hex0.</summary>
public sealed record ArmySavedRegionWrite(ushort? Word152, bool Written,
    ArmySavedRegionWriteKind Kind);

public sealed record ArmySavedRegionHex(ushort RawWord152, int LookupSlot,
    HexReference Hex);
public sealed record ArmySavedRegionArea(ArmySavedRegionHex SavedHex,
    ushort RawHexAreaWord12, int LookupSlot, AreaReference Area);

/// <summary>Native Army reset/creation and the reached CTurnState step37 or
/// first eligible game-state snapshot. Movement writes word2A independently.
/// This component does not schedule snapshots or derive native positions.</summary>
public static class ArmyCurrentRegionSource
{
    public const int MaximumHexSlot = 48399;
    public const ushort ResetHexSentinel = 48400;
    public const int TurnSequenceStep = 37;

    public static ArmySavedRegionWrite Reset() =>
        new(ResetHexSentinel, true, ArmySavedRegionWriteKind.Reset);

    /// <summary>SC2D3033..2D305C / TC2D31B3..2D31DC. The invalid-object
    /// getter result48400 is clamped to48399 by this creator.</summary>
    public static ushort CreationWord152(int nativeBaseHexGetterResult) =>
        (ushort)Math.Clamp(nativeBaseHexGetterResult, 0, MaximumHexSlot);

    /// <summary>Actual selected Base4E -> Object table -> valid Object10,
    /// followed by the creator's signed clamp. Missing current fields return
    /// null. A supplied null Base would be dereferenced by the native getter.</summary>
    public static ushort? ResolveCreationRegion(BaseReference? selectedBase,
        ICurrentGateMapState current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (selectedBase is null) return null;
        if (!selectedBase.PhysicalSlot.HasValue)
            throw new InvalidDataException("Native Army creation selected a null Base receiver.");
        if (current.ReadBaseObjectLink(selectedBase) is not ushort link) return null;
        ObjectReference? obj = current.ReadObjectTableReference(link <= 3000 ? link : 0);
        if (obj is null) return null;
        //32FDF0(null) returnsfalse before any object virtual predicate.
        if (!obj.PhysicalSlot.HasValue) return CreationWord152(48400);
        if (current.ReadObjectNativeValidity(obj) is not bool valid) return null;
        if (!valid) return CreationWord152(48400);
        return current.ReadObjectHexIndex(obj) is ushort hex ? CreationWord152(hex) : null;
    }

    /// <summary>SC2EC410..2EC43F / TC2EC4F0..2EC51F. Call only at the
    /// reached snapshot boundary. The raw Force getter may return0 or values
    /// above51; only46..51 are excluded. Skipped receivers retain their word.</summary>
    public static ArmySavedRegionWrite? ResolveReachedSnapshot(ushort? previousWord152,
        bool? nativeArmyValid, int? nativeRawForceId, ushort? actualArmyWord2A)
    {
        if (nativeArmyValid is null) return null;
        if (!nativeArmyValid.Value)
            return new(previousWord152, false, ArmySavedRegionWriteKind.Retained);
        if (nativeRawForceId is null) return null;
        if (nativeRawForceId is < 0 or > byte.MaxValue)
            throw new InvalidDataException("Native Army raw Force getter is outside its byte result range.");
        if (nativeRawForceId is >= 46 and <= 51)
            return new(previousWord152, false, ArmySavedRegionWriteKind.Retained);
        if (actualArmyWord2A is not ushort currentHex) return null;
        return new((ushort)Math.Min((int)currentHex, MaximumHexSlot), true,
            ArmySavedRegionWriteKind.ReachedSnapshot);
    }

    /// <summary>SC2A90CC..2A90F3 / TC2A927C..2A92A3. This is the
    /// saved-word lookup used by the common battle factor. Out-of-range words
    /// select table0; pointer identity is supplied by the current native owner.</summary>
    public static ArmySavedRegionHex? ResolveSavedHex(ushort? word152,
        ICurrentGateMapState current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (word152 is not ushort raw) return null;
        int slot = raw <= MaximumHexSlot ? raw : 0;
        HexReference? hex = current.ReadHexTableReference(slot);
        return hex is null ? null : new(raw, slot, hex);
    }

    /// <summary>Reached RegionFactor area lookup, SC2B0A47..2B0A7E.
    /// Caller must first take the native Hex/Force eligibility branches.
    /// It reads current Hex12 rather than any Core area projection.</summary>
    public static ArmySavedRegionArea? ResolveReachedArea(ArmySavedRegionHex savedHex,
        ICurrentGateMapState current)
    {
        ArgumentNullException.ThrowIfNull(savedHex);
        ArgumentNullException.ThrowIfNull(current);
        if (!savedHex.Hex.PhysicalSlot.HasValue)
            throw new InvalidDataException("Native region area lookup requires its reached nonnull Hex branch.");
        if (current.ReadHexAreaSlot(savedHex.Hex) is not ushort raw) return null;
        int slot = raw <= 500 ? raw : 0;
        AreaReference? area = current.ReadAreaTableReference(slot);
        return area is null ? null : new(savedHex, raw, slot, area);
    }
}
