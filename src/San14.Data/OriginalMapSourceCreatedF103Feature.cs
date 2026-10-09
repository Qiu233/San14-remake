using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Current accepted View fields and optional execution owner at the
/// node166 producer boundary. Presence and query results are current owned
/// inputs; neither a source Camera declaration nor a feature row supplies them.</summary>
public sealed record OriginalDeferredF103CurrentViewInputs(
    long FrameNumber, string ViewIdentity, string CameraOwnerIdentity,
    int ViewSignedA00, uint ViewFlags19AC, float? ViewA28,
    uint? ViewA3C, ulong? View1BD0CpuVtable,
    bool? CommonViewResource240Present, uint? Resource240Flags0C,
    string SelectedRecordLightOwnerIdentity, string CurrentProducerProvenance);

/// <summary>A native reference represented by its owned producer and exact
/// byte offset. It is not an original-process address or a fabricated pointer.</summary>
public sealed record OriginalDeferredF103OwnedReference(string OwnerIdentity, int Offset);

/// <summary>The complete kind1 M data that survives the source producer.
/// Pointer slots remain typed references, separately from scalar/vector bytes.</summary>
public sealed class OriginalDeferredF103Kind1Scratch
{
    private readonly byte[] counts, ambient, vectors, environment;
    internal OriginalDeferredF103Kind1Scratch(byte[] counts, byte[] ambient, byte[] vectors,
        byte[] environment, OriginalDeferredF103OwnedReference light,
        OriginalDeferredF103OwnedReference? hemi, OriginalDeferredF103OwnedReference view,
        ulong shadowVtable, OriginalDeferredF103OwnedReference? selectedModifier = null)
    {
        this.counts = counts.ToArray(); this.ambient = ambient.ToArray();
        this.vectors = vectors.ToArray(); this.environment = environment.ToArray();
        DirectionalLight10 = light; HemisphereLight28 = hemi; View10AtA0 = view;
        ShadowCpuVtableA8 = shadowVtable;
        SelectedModifier1B0 = selectedModifier ?? new(light.OwnerIdentity, 0x1B0);
    }
    public ReadOnlyMemory<byte> Counts0ToC => counts.ToArray();
    public ReadOnlyMemory<byte> Ambient30 => ambient.ToArray();
    public ReadOnlyMemory<byte> Vectors40To9F => vectors.ToArray();
    public ReadOnlyMemory<byte> EnvironmentRowReferencedAtB0 => environment.ToArray();
    public OriginalDeferredF103OwnedReference EnvironmentReferenceB0 =>
        new(DirectionalLight10.OwnerIdentity + ":owned-copy-of-row80", 0);
    public uint DirectionalCount => Word(counts, 0);
    public uint Kind2Count => Word(counts, 4);
    public uint Kind3Count => Word(counts, 8);
    public uint HemisphereCount => Word(counts, 12);
    public uint Mode50 => Word(vectors, 16);
    public OriginalDeferredF103OwnedReference DirectionalLight10 { get; }
    public OriginalDeferredF103OwnedReference? Kind2Light18 => null;
    public OriginalDeferredF103OwnedReference? Kind3Light20 => null;
    public OriginalDeferredF103OwnedReference? HemisphereLight28 { get; }
    public OriginalDeferredF103OwnedReference View10AtA0 { get; }
    public ulong ShadowCpuVtableA8 { get; }
    public OriginalDeferredF103OwnedReference SelectedModifier1B0 { get; }
    internal byte[] CopyVectors() => vectors.ToArray();
    private static uint Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
}

/// <summary>Owned Camera/Light/View → native M → current48 bytes. The source
/// F103 tail override is included; no selected shader feature row is copied.</summary>
public sealed class OriginalMapSourceCreatedF103FeaturePacket
{
    private readonly byte[] feature, lightData, cameraPrefix, producerTail;
    private readonly OriginalMapCameraConstructionPacket camera;
    private readonly byte[] modifierLightData;
    internal OriginalMapSourceCreatedF103FeaturePacket(OriginalMapCameraConstructionPacket acceptedCamera,
        OriginalMapSourceCreatedLightingPacket lighting, OriginalDeferredF103CurrentViewInputs current,
        OriginalDeferredF103Kind1Scratch scratch, byte[] effectiveLight, byte[] feature,
        byte[] producerTail, bool rebuilt, bool copied, OriginalMapSourceCreatedLightingPacket? innerLighting = null)
    {
        camera = acceptedCamera.CopyAcceptedSnapshot(); cameraPrefix = camera.Prefix.ToArray();
        lightData = effectiveLight.ToArray(); this.feature = feature.ToArray(); this.producerTail = producerTail.ToArray();
        FrameNumber = current.FrameNumber; CurrentView = current; Scratch = scratch; StateRebuilt = rebuilt;
        EffectiveLightDataCopied = copied;
        Binding = lighting.Binding; LightingSourceIdentity = lighting.SourceIdentity;
        EffectiveLightOwnerIdentity = lighting.Binding.LightOwnerIdentity + ":frame=" + FrameNumber;
        LightingProducerProvenance = lighting.SourceProvenance;
        modifierLightData = innerLighting is null || !OriginalDeferredF103Kind1Modifier.HasBuiltinModifier(innerLighting.LocalLightData.Span)
            ? lightData.ToArray() : innerLighting.LocalLightData.ToArray();
        SelectedModifierOwnerIdentity = innerLighting is not null && OriginalDeferredF103Kind1Modifier.HasBuiltinModifier(innerLighting.LocalLightData.Span)
            ? innerLighting.Binding.LightOwnerIdentity + ":frame=" + FrameNumber
            : EffectiveLightOwnerIdentity;
    }
    public long FrameNumber { get; }
    public OriginalDeferredF103CurrentViewInputs CurrentView { get; }
    public OriginalMapLightingBinding Binding { get; }
    public OriginalMapSourceCreatedLightingIdentity LightingSourceIdentity { get; }
    public string LightingProducerProvenance { get; }
    public string EffectiveLightOwnerIdentity { get; }
    public int DirectionalCapacity4 => 4;
    public bool DirectOrdinaryLightData => !EffectiveLightDataCopied;
    public bool EffectiveLightDataCopied { get; }
    public bool NativeArg18 => CurrentView.CommonViewResource240Present == true &&
        (CurrentView.Resource240Flags0C!.Value & 0x800) == 0;
    public bool StateRebuilt { get; }
    public OriginalDeferredF103Kind1Scratch Scratch { get; }
    public OriginalMapCameraConstructionPacket AcceptedCamera => camera.CopyAcceptedSnapshot();
    internal bool MatchesAcceptedCameraPrefix(ReadOnlySpan<byte> currentPrefix)
        => cameraPrefix.AsSpan().SequenceEqual(currentPrefix);
    public ReadOnlyMemory<byte> CurrentFeature48 => feature.ToArray();
    public ReadOnlyMemory<byte> EffectiveLocalLightData => lightData.ToArray();
    /// <summary>The inner record's nonnull H, or the outer record's fallback H.
    /// It remains separate from the transformed outer directional payload.</summary>
    public ReadOnlyMemory<byte> SelectedModifierLightData => modifierLightData.ToArray();
    public string SelectedModifierOwnerIdentity { get; }
    public ReadOnlyMemory<byte> GenericProducerTailBeforeF103Override => producerTail.ToArray();

    public void ValidateFrame(OriginalMapCameraConstructionPacket acceptedCamera,
        OriginalMapSourceCreatedLightingPacket lighting)
    {
        ArgumentNullException.ThrowIfNull(acceptedCamera); ArgumentNullException.ThrowIfNull(lighting);
        if (FrameNumber != acceptedCamera.FrameNumber || FrameNumber != lighting.FrameNumber ||
            Binding != lighting.Binding || LightingSourceIdentity != lighting.SourceIdentity ||
            LightingProducerProvenance != lighting.SourceProvenance ||
            !MatchesAcceptedCameraPrefix(acceptedCamera.Prefix.Span) ||
            camera.Source.Camera.ObjectId != acceptedCamera.Source.Camera.ObjectId ||
            camera.Source.Resource.Source.ResourceUri != acceptedCamera.Source.Resource.Source.ResourceUri ||
            camera.Source.Resource.Source.FileId != acceptedCamera.Source.Resource.Source.FileId ||
            camera.Source.Resource.Source.DecodedSha256 != acceptedCamera.Source.Resource.Source.DecodedSha256)
            throw new InvalidDataException("F103 feature requires its exact accepted Camera, current LightData generation and same owned frame.");
        }
}

/// <summary>SC EAE5D7 source first-kind cache initialization (5), TC D76780
/// kind1 preparation and EADF52's final48-byte feature. Current modifier and
/// execution-owner branches must be resolved before this source path is used.</summary>
public static class OriginalMapSourceCreatedF103Feature
{
    public const int DirectionalCapacity = 4;
    public const int FirstKindCache = 5;

    public static OriginalMapSourceCreatedF103FeaturePacket PrepareFirstKind1(
        OriginalMapCameraConstructionPacket acceptedCamera,
        OriginalMapSourceCreatedLightingPacket lighting,
        OriginalDeferredF103CurrentViewInputs current) => Prepare(acceptedCamera, lighting, current, null);

    /// <summary>Original F103 outer L and independently selected inner H in
    /// one frame/View. The caller's actual loop association supplies both.</summary>
    public static OriginalMapSourceCreatedF103FeaturePacket PrepareFirstKind1WithModifier(
        OriginalMapCameraConstructionPacket acceptedCamera, OriginalMapSourceCreatedLightingPacket outerLighting,
        OriginalMapSourceCreatedLightingPacket innerLighting, OriginalDeferredF103CurrentViewInputs current)
    {
        ArgumentNullException.ThrowIfNull(innerLighting);
        return Prepare(acceptedCamera, outerLighting, current, null, innerLighting);
    }

    /// <summary>Continue the same F103 execution's ordered kind1 records. A new
    /// frame starts with PrepareFirstKind1, as the source initializes kind5 anew.</summary>
    public static OriginalMapSourceCreatedF103FeaturePacket PrepareNextKind1(
        OriginalMapSourceCreatedF103FeaturePacket previous,
        OriginalMapCameraConstructionPacket acceptedCamera,
        OriginalMapSourceCreatedLightingPacket lighting,
        OriginalDeferredF103CurrentViewInputs current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        if (previous.FrameNumber != current.FrameNumber || previous.CurrentView.ViewIdentity != current.ViewIdentity ||
            previous.CurrentView.CameraOwnerIdentity != current.CameraOwnerIdentity)
            throw new InvalidDataException("F103 scratch reuse belongs to the same ordered execution and accepted View/Camera.");
        return Prepare(acceptedCamera, lighting, current, previous.Scratch);
    }

    public static OriginalMapSourceCreatedF103FeaturePacket PrepareNextKind1WithModifier(
        OriginalMapSourceCreatedF103FeaturePacket previous, OriginalMapCameraConstructionPacket acceptedCamera,
        OriginalMapSourceCreatedLightingPacket outerLighting, OriginalMapSourceCreatedLightingPacket innerLighting,
        OriginalDeferredF103CurrentViewInputs current)
    {
        ArgumentNullException.ThrowIfNull(previous); ArgumentNullException.ThrowIfNull(innerLighting);
        ArgumentNullException.ThrowIfNull(current);
        if (previous.FrameNumber != current.FrameNumber || previous.CurrentView.ViewIdentity != current.ViewIdentity ||
            previous.CurrentView.CameraOwnerIdentity != current.CameraOwnerIdentity)
            throw new InvalidDataException("F103 outer/inner scratch continuation belongs to the same ordered View/Camera execution.");
        return Prepare(acceptedCamera, outerLighting, current, previous.Scratch, innerLighting);
    }

    private static OriginalMapSourceCreatedF103FeaturePacket Prepare(
        OriginalMapCameraConstructionPacket acceptedCamera, OriginalMapSourceCreatedLightingPacket lighting,
        OriginalDeferredF103CurrentViewInputs current, OriginalDeferredF103Kind1Scratch? previous,
        OriginalMapSourceCreatedLightingPacket? innerLighting = null)
    {
        ArgumentNullException.ThrowIfNull(acceptedCamera); ArgumentNullException.ThrowIfNull(lighting);
        ArgumentNullException.ThrowIfNull(current);
        ReadOnlyMemory<byte> expectedCameraPrefix=acceptedCamera.Prefix;
        if(lighting.AcceptedInputs.PresentSeasonCamera is{}present)
        {
            var prior=present.AcceptedSeasonCamera.Inputs.MainFrame.Camera;
            // The population owns geometry before the seasonal scalar copy.
            // Only that exact accepted predecessor may cross this boundary;
            // account for its known copies rather than requiring old EV bounds.
            if(prior.FrameNumber==acceptedCamera.FrameNumber&&
                prior.Source.Camera.ObjectId==acceptedCamera.Source.Camera.ObjectId&&
                prior.Source.Resource.Source.ResourceUri==acceptedCamera.Source.Resource.Source.ResourceUri&&
                prior.Source.Resource.Source.DecodedSha256==acceptedCamera.Source.Resource.Source.DecodedSha256&&
                prior.Prefix.Span.SequenceEqual(acceptedCamera.Prefix.Span))
                expectedCameraPrefix=present.AcceptedSeasonCamera.Inputs.BeforeFinalPreparationPrefix;
        }
        if (current.FrameNumber <= 0 || current.FrameNumber != acceptedCamera.FrameNumber ||
            current.FrameNumber != lighting.FrameNumber || current.ViewIdentity != lighting.Binding.ViewIdentity ||
            current.CameraOwnerIdentity != lighting.Binding.CameraOwnerIdentity ||
            string.IsNullOrWhiteSpace(current.ViewIdentity) || string.IsNullOrWhiteSpace(current.CameraOwnerIdentity) ||
            string.IsNullOrWhiteSpace(current.CurrentProducerProvenance) ||
            lighting.PreparedCameraPrefix.Length != OriginalMapCameraConstruction.PrefixByteLength ||
            !lighting.PreparedCameraPrefix.Span[..0x1D0].SequenceEqual(expectedCameraPrefix.Span[..0x1D0]))
            throw new InvalidDataException("F103 kind1 requires complete same-frame owned Camera, lighting and accepted View inputs.");
        if ((current.ViewFlags19AC & 0x9FFFFFFF) == 0)
            throw new InvalidDataException("F103 ordinary accepted View requires the original nonzero19AC mask.");
        if (lighting.LocalLightData.Length != OriginalMainLightConstruction.LightDataBytes ||
            lighting.Binding.BasePath != OriginalMapLightingBasePath.UnmodifiedOrdinary ||
            lighting.Binding.DirectionalShaderCapacity != DirectionalCapacity)
            throw new InvalidDataException("F103 kind1 requires complete source LightData and the original caller capacity4.");
        if (current.CommonViewResource240Present is null ||
            string.IsNullOrWhiteSpace(current.SelectedRecordLightOwnerIdentity) ||
            current.SelectedRecordLightOwnerIdentity != lighting.Binding.LightOwnerIdentity)
            throw new InvalidDataException("F103 requires the current CommonView+240 selection and the same owned selected/current kind1 LightData record.");
        bool arg18 = false;
        if (current.CommonViewResource240Present.Value)
        {
            if (current.Resource240Flags0C is not uint flags)
                throw new InvalidDataException("Present CommonView+240 owner requires its current flags+0C.");
            arg18 = (flags & 0x800) == 0;
        }
        byte[] light = lighting.LocalLightData.ToArray();
        if (innerLighting is not null)
        {
            if (innerLighting.FrameNumber != lighting.FrameNumber ||
                innerLighting.Binding.ViewIdentity != lighting.Binding.ViewIdentity ||
                innerLighting.Binding.CameraOwnerIdentity != lighting.Binding.CameraOwnerIdentity ||
                !innerLighting.PreparedCameraPrefix.Span.SequenceEqual(lighting.PreparedCameraPrefix.Span))
                throw new InvalidDataException("F103 outer and inner records belong to different current View/Camera frames.");
        }
        // EADDED→D59210 selects this record's own H=L+1B0. Different
        // Directional and HDRI classifiers share the source predicate;
        // classification is not an equality test against the HDRI prototype.
        var modifierBytes = innerLighting?.LocalLightData ?? lighting.LocalLightData;
        bool separateModifier = innerLighting is not null && OriginalDeferredF103Kind1Modifier.HasBuiltinModifier(modifierBytes.Span);
        if (!OriginalDeferredF103Kind1Modifier.HasBuiltinModifier(light) && !separateModifier)
            throw new NotSupportedException("F103 null built-in modifier requires its distinct scratch reference producer.");
        var modifier = OriginalDeferredF103Kind1Modifier.ApplyWithModifier(light, modifierBytes.Span, arg18);
        light = modifier.Bytes;

        uint count = 4;
        while (count > 1 && !PositiveRgb(light, checked((int)(count - 1) * 32 + 16))) count--;
        uint hemi = PositiveRgb(light, 0x100) || PositiveRgb(light, 0x110) ? 1u : 0u;
        uint flags0 = 0; uint shadowType = 0, query = 0, localType = 0;
        ulong shadowVtable = 0;
        bool shadow = current.ViewSignedA00 > 0 && (current.ViewFlags19AC & 0x40000000) != 0 && light[0xF1D] != 0;
        if (shadow)
        {
            shadowType = light[0xF1D];
            if (shadowType == 0x6C) { shadowType = 8; flags0 |= 0x4000; }
            if (shadowType is 8 or 9)
            {
                if (current.ViewA28 is not float value || !float.IsFinite(value))
                    throw new InvalidDataException("Active F103 shadow type8/9 requires finite current View+A28.");
                if (value <= 0) shadowType = 1;
            }
            if (current.ViewA3C is not uint viewA3C || current.View1BD0CpuVtable is not ulong vtable)
                throw new InvalidDataException("Active F103 shadow branch requires actual current View+A3C and View1BD0→CPU vtable result.");
            query = ProjectC5Bed0(viewA3C); shadowVtable = vtable; localType = light[0xF1E];
            if ((int)shadowType <= 3) flags0 |= 0x100;
            flags0 |= 0x400;
        }
        bool rebuild = previous is null || previous.ShadowCpuVtableA8 != 0 || previous.Mode50 != 0 ||
            Word(light, 0x160) != 0 || shadow || previous.DirectionalCount != count || previous.HemisphereCount != hemi;
        byte[] vectors = rebuild ? light.AsSpan(0x150, 0x60).ToArray() : previous!.CopyVectors();
        byte[] counts = new byte[16]; Put(counts, 0, count); Put(counts, 12, hemi);
        string lightOwner = lighting.Binding.LightOwnerIdentity + ":frame=" + lighting.FrameNumber;
        var modifierReference = separateModifier ? new OriginalDeferredF103OwnedReference(
            innerLighting!.Binding.LightOwnerIdentity + ":frame=" + lighting.FrameNumber, 0x1B0) : null;
        var scratch = new OriginalDeferredF103Kind1Scratch(counts, light.AsSpan(0xE0, 16).ToArray(), vectors,
            light.AsSpan(0x80, 16).ToArray(), new(lightOwner, 0), hemi != 0 ? new(lightOwner, 0x120) : null,
            rebuild ? new(current.ViewIdentity, 0x10) : previous!.View10AtA0, shadowVtable, modifierReference);
        byte[] feature = new byte[48];
        Put(feature, 0, flags0); counts.CopyTo(feature, 4);
        Put(feature, 20, count == 0 ? 0u : 1u);
        Put(feature, 24, scratch.Mode50); Put(feature, 28, scratch.Mode50 == 7 ? 2u : 1u);
        Put(feature, 44, 3);
        byte[] tail = new byte[16]; Put(tail, 0, shadowType); Put(tail, 4, query); Put(tail, 8, localType); Put(tail, 12, 3);
        return new(acceptedCamera, lighting, current, scratch, light, feature, tail, rebuild, modifier.Copied, innerLighting);
    }
    /// <summary>Exact native C5BED0 bounded jump-table result from current View+A3C.</summary>
    public static uint ProjectC5Bed0(uint currentViewA3C)
    {
        if (currentViewA3C > 18) currentViewA3C = 0;
        ReadOnlySpan<uint> sourceTable = [11, 8, 2, 3, 4, 5, 8, 8, 8, 11, 11, 11];
        return currentViewA3C < 12 ? sourceTable[(int)currentViewA3C] : currentViewA3C;
    }
    private static bool PositiveRgb(byte[] bytes, int offset)
    {
        for (int channel = 0; channel < 3; channel++)
        {
            float value = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(offset + channel * 4, 4));
            if (!float.IsFinite(value)) throw new InvalidDataException("F103 light classification requires finite consumed RGB.");
            if (value > 0) return true;
        }
        return false;
    }
    private static uint Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static void Put(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
}
