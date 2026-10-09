using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Node68's current D76780 result, before the shader feature matcher.
/// The selected kind1 L and its modified copy are distinct from F103's later
/// capacity4 packet. No image or shader submission is proved by this packet.</summary>
public sealed class DeferredEarlyIndirectCurrentFeature
{
    private readonly byte[] feature, effectiveLight;
    internal DeferredEarlyIndirectCurrentFeature(MapSourceCreatedF103CurrentFrame frame,
        MapSourceCreatedLightingPacket light, byte[] feature, byte[] effectiveLight)
        => (Frame, Lighting, this.feature, this.effectiveLight) = (frame, light, feature, effectiveLight);
    public MapSourceCreatedF103CurrentFrame Frame { get; }
    public MapSourceCreatedLightingPacket Lighting { get; }
    public ReadOnlyMemory<byte> CurrentFeature48 => feature;
    public ReadOnlyMemory<byte> EffectiveLightData => effectiveLight;
    public ReadOnlyMemory<byte> SelectedModifierParameter =>
        DeferredF103Kind1Modifier.HasBuiltinModifier(Lighting.LocalLightData.Span)
            ? Lighting.LocalLightData.Slice(0x1B0) : ReadOnlyMemory<byte>.Empty;
    public int NativeDirectionalCapacity => 1;
    public bool NativeArgument18 => true;
    public string SourceEvidence => Frame.ViewFields.SourceProvenance + ";" + Lighting.SourceProvenance +
        ";EBCD70/EBD19B-capacity1/EBD17C-arg18true/D76780-kind1;EBD259-feature40=0/EBD266-feature44=1;no-device-completion";
    public uint[] ReadWords() => Enumerable.Range(0, 12)
        .Select(i => BinaryPrimitives.ReadUInt32LittleEndian(feature.AsSpan(i * 4, 4))).ToArray();
    public void RequireCurrent(MapSourceCreatedF103CurrentFrame actual)
    {
        if (!ReferenceEquals(actual, Frame) || actual.FrameNumber != Lighting.FrameNumber ||
            actual.Lighting.Binding.ViewIdentity != Lighting.Binding.ViewIdentity ||
            actual.Lighting.Binding.CameraOwnerIdentity != Lighting.Binding.CameraOwnerIdentity)
            throw new InvalidDataException("Early68 feature belongs to another current geometry/View/Light owner.");
    }
}

public static class DeferredEarlyIndirectCurrentFeatureSource
{
    /// <summary>The actual current record's independent LightData. The geometry
    /// frame supplies its View shadow inputs; the native caller supplies
    /// capacity1 and arg18true regardless of F103's separate binding capacity.</summary>
    public static DeferredEarlyIndirectCurrentFeature Prepare(
        MapSourceCreatedF103CurrentFrame actualGeometry,
        MapSourceCreatedLightingPacket actualRecordLighting)
    {
        byte[] effective = ReadEffectiveCurrent(actualGeometry, actualRecordLighting);
        return new(actualGeometry, actualRecordLighting, PrepareKind1Words(effective, actualGeometry.View), effective);
    }
    private static byte[] ReadEffectiveCurrent(MapSourceCreatedF103CurrentFrame actualGeometry,
        MapSourceCreatedLightingPacket actualRecordLighting)
    {
        ArgumentNullException.ThrowIfNull(actualGeometry); ArgumentNullException.ThrowIfNull(actualRecordLighting);
        var reference = actualGeometry.Lighting;
        if (actualRecordLighting.FrameNumber != actualGeometry.FrameNumber ||
            actualRecordLighting.Binding.ViewIdentity != reference.Binding.ViewIdentity ||
            actualRecordLighting.Binding.CameraOwnerIdentity != reference.Binding.CameraOwnerIdentity ||
            actualRecordLighting.Binding.BasePath != MapLightingBasePath.UnmodifiedOrdinary ||
            !actualRecordLighting.PreparedCameraPrefix.Span.SequenceEqual(reference.PreparedCameraPrefix.Span))
            throw new InvalidDataException("Early68 record requires this geometry's actual current View/Camera and independent LightData.");
        return DeferredF103Kind1Modifier.Apply(actualRecordLighting.LocalLightData.Span, true).Bytes;
    }

    public static DeferredEarlyIndirectCurrentFeature Prepare(
        MapSourceCreatedF103CurrentFrame actualGeometry,
        MapSourceCreatedLightingPacket actualRecordLighting,
        DeferredEarlyIndirectPatternCameraTail actualCameraTail)
    {
        ArgumentNullException.ThrowIfNull(actualCameraTail); actualCameraTail.RequireCurrent(actualGeometry);
        byte[] effective = ReadEffectiveCurrent(actualGeometry, actualRecordLighting);
        var view = actualGeometry.View with { ViewSignedA00 = actualCameraTail.CameraA00,
            ViewA28 = actualCameraTail.CameraA28, ViewA3C = actualCameraTail.CameraA3C };
        return new(actualGeometry, actualRecordLighting,
            PrepareKind1Words(effective, view), effective);
    }

    // The source always keeps at least one of its supplied capacity1 rows.
    // This helper is internal for exact original-opcode fixtures; public
    // consumers enter through the current owned lighting packet above.
    internal static byte[] PrepareKind1Words(ReadOnlySpan<byte> effective, DeferredF103CurrentViewInputs current)
    {
        if (effective.Length != MainLightConstruction.LightDataBytes)
            throw new InvalidDataException("Early68 kind1 requires complete10C0 LightData.");
        uint hemi = Positive(effective, 0x100) || Positive(effective, 0x110) ? 1u : 0u;
        uint flags = 0, shadow = 0, query = 0;
        if (current.ViewSignedA00 > 0 && (current.ViewFlags19AC & 0x40000000) != 0 && effective[0xF1D] != 0)
        {
            shadow = effective[0xF1D];
            if (shadow == 0x6C) { shadow = 8; flags |= 0x4000; }
            if (shadow is 8 or 9)
            {
                if (current.ViewA28 is not float value || !float.IsFinite(value))
                    throw new InvalidDataException("Early68 active shadow needs actual finite ViewA28.");
                if (value <= 0) shadow = 1;
            }
            if (current.ViewA3C is not uint a3c || current.View1BD0CpuVtable is null)
                throw new InvalidDataException("Early68 active shadow needs actual ViewA3C and wrapper CPU result, including its resolved null.");
            query = MapSourceCreatedF103Feature.ProjectC5Bed0(a3c);
            if ((int)shadow <= 3) flags |= 0x100;
            flags |= 0x400;
        }
        uint mode = BinaryPrimitives.ReadUInt32LittleEndian(effective.Slice(0x160, 4));
        uint[] words = [flags, 1, 0, 0, hemi, 1, mode, mode == 7 ? 2u : 1u, shadow, query, 0, 1];
        byte[] result = new byte[48];
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(i * 4, 4), words[i]);
        return result;
    }
    private static bool Positive(ReadOnlySpan<byte> bytes, int at)
    {
        bool positive = false;
        for (int i = 0; i < 3; i++)
        {
            float value = BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(at + i * 4, 4));
            if (!float.IsFinite(value)) throw new InvalidDataException("Early68 current hemisphere classification is nonfinite.");
            positive |= value > 0;
        }
        return positive;
    }
}
