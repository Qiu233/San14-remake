using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

public sealed record OriginalDeferredLocalAmbientInputs(Vector2 FogDecayRange, Vector2 Decay)
{
    /// <summary>Only after successful exact LocalAmbSpecMaps data construction
    /// with no intervening value setters. These are not current-frame defaults.</summary>
    public static OriginalDeferredLocalAmbientInputs SuccessfulDataDefaults() => new(
        new(BitConverter.UInt32BitsToSingle(0xBA83126F), 1), new(0, 1));
}
public sealed record OriginalDeferredShadowFadeInputs(float Value);

public static class OriginalDeferredLocalAmbientMath
{
    /// <summary>Original TC9B78FA/9B78FF and TC9B7A81/9B7A86. The selected
    /// LocalAmbSpecMaps data prefix, not its module or an unrelated camera.</summary>
    public static OriginalDeferredLocalAmbientInputs ReadData(ReadOnlySpan<byte> dataPrefix)
    {
        if (dataPrefix.Length != 0x50)
            throw new InvalidDataException("Local ambient writer requires the selected data object's explicit 0x50 prefix.");
        var fog = new Vector2(Read(dataPrefix, 0x38), Read(dataPrefix, 0x3C));
        var decay = new Vector2(Read(dataPrefix, 0x48), Read(dataPrefix, 0x4C));
        if (new[] { fog.X, fog.Y, decay.X, decay.Y }.Any(v => !float.IsFinite(v)))
            throw new InvalidDataException("Local ambient writer requires finite consumed data fields.");
        return new(fog, decay);
    }

    /// <summary>Exact node166's TC EAE331..EAE375 shadow-fade named writer.
    /// The two skip gates and named ShadowMask presence must come from the
    /// caller's actual selected owner. A skipped writer preserves prior bytes.</summary>
    public static OriginalDeferredShadowFadeInputs? PrepareShadowFade(bool owner47Bit0,
        bool callerShadowSkipFlag, bool namedShadowMaskPresent, float owner9CC)
    {
        if (owner47Bit0 || callerShadowSkipFlag || !namedShadowMaskPresent) return null;
        if (!float.IsFinite(owner9CC))
            throw new InvalidDataException("Shadow fade writer requires the explicit finite owner+9CC field.");
        return new(owner9CC);
    }
    private static float Read(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(at, 4));
}
