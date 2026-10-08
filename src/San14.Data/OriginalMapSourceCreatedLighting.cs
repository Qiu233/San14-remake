using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace San14.Data;

/// <summary>The source declarations and constructed byte generation used by a
/// remake owner. ViewSlot is the original Game setter slot, not an observation
/// of an active original View or publisher.</summary>
public sealed record OriginalMapSourceCreatedLightingIdentity(int ViewSlot, uint ViewParamObjectId,
    string CameraResourceUri, string CameraSha256, uint CameraObjectId,
    string MainLightResourceUri, string MainLightSha256, uint MainLightObjectId,
    uint DeclaredGroupMask, uint DeclaredPrimaryGroupMask, string CreatedLightDataSha256,
    string ConstructedOwnerSha256);

/// <summary>Immutable source-created main-light seed with the exact Camera3/4
/// source selected by Game. Complete constructor-retained bytes are supplied by
/// OriginalMainLightConstruction, so this owner never fills its unwritten holes.</summary>
public sealed class OriginalMapSourceCreatedLightingSource
{
    private readonly byte[] lightData;
    private OriginalMapSourceCreatedLightingSource(LoadedOriginalMainLightBindingCameraSource setter,
        OriginalMainLightConstructionPacket construction)
    {
        LoadedOriginalMainLightBindingSource light = OriginalMainLightBindingSource.ReadGameDefault(construction.Source.Resource);
        if (construction.Bytes.Length != OriginalCameraDirectionalLightData.LightDataBytes ||
            construction.Source.Declaration.ObjectId != light.Declaration.ObjectId ||
            construction.Source.Declaration.ClassId != light.Declaration.ClassId ||
            construction.Source.GroupMask != light.GroupMask || construction.Source.GroupMaskMain != light.GroupMaskMain ||
            string.IsNullOrWhiteSpace(construction.SourceProvenance))
            throw new InvalidDataException("Source-created map lighting requires the complete constructed Game default HDRI and its provenance.");
        lightData = construction.Bytes.ToArray();
        LoadedCameraSkylight camera = setter.Camera.Source;
        Identity = new(setter.ViewSlot, setter.ViewParamObjectId, camera.Resource.Source.ResourceUri,
            camera.Resource.Source.DecodedSha256, camera.Camera.ObjectId, light.Resource.Source.ResourceUri,
            light.Resource.Source.DecodedSha256, light.Declaration.ObjectId, light.GroupMask, light.GroupMaskMain,
            Convert.ToHexString(SHA256.HashData(lightData)),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(construction.SourceProvenance))));
        Controller = setter.Camera.Controller;
        InitialSkylightParameters = camera.Parameters;
        CameraConstructionSource = OriginalMapCameraConstructionSource.ReadPerspective(camera.Resource, camera.Camera.ObjectId);
        ConstructionProvenance = construction.SourceProvenance;
        ResourceBindings = Array.AsReadOnly(construction.ResourceBindings.ToArray());
        AllResourceBindingsReady = construction.AllResourceBindingsReady;
        Construction = construction;
    }

    public OriginalMapSourceCreatedLightingIdentity Identity { get; }
    public OriginalCameraLightingController Controller { get; }
    public OriginalSkylightParameters InitialSkylightParameters { get; }
    public LoadedOriginalMapCameraConstructionSource CameraConstructionSource { get; }
    public string ConstructionProvenance { get; }
    public IReadOnlyList<OriginalMainLightConstructionResourceBinding> ResourceBindings { get; }
    public bool AllResourceBindingsReady { get; }
    public OriginalMainLightConstructionPacket Construction { get; }
    public string SourceProvenance => $"remake-source-created;{ConstructionProvenance};" +
        $"Camera={Identity.CameraResourceUri}#{Identity.CameraObjectId:X8}@{Identity.CameraSha256};" +
        $"ViewSlot={Identity.ViewSlot};ViewParam={Identity.ViewParamObjectId:X8};" +
        $"CreatedLightDataSha256={Identity.CreatedLightDataSha256};ConstructedOwnerSha256={Identity.ConstructedOwnerSha256}";
    public ReadOnlyMemory<byte> CreatedLightData => lightData;

    public static OriginalMapSourceCreatedLightingSource FromGameDefault(OriginalGameRepository repository,
        int viewSlot, OriginalMainLightConstructionPacket constructedLight)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(constructedLight);
        return new(OriginalMainLightBindingSource.LoadSetterCamera(repository, viewSlot), constructedLight);
    }

    /// <summary>Use once when this remake Camera is constructed. Matrix, Eye,
    /// lens and other retained prefix bytes are still explicitly supplied.</summary>
    public byte[] CreateInitialCameraPrefix(ReadOnlySpan<byte> explicitCompletePrefix) =>
        OriginalPreparedCameraLighting.InitializeConstructorLightingFields(explicitCompletePrefix);

    /// <summary>Apply Perspective loader C59762's initial FF transition state
    /// immediately before its delta0 lighting call. The complete current Camera
    /// fields are retained; source optics and optional lens callback resolution
    /// remain with the Camera producer.</summary>
    public byte[] CreatePerspectiveLoaderInitialCameraPrefix(ReadOnlySpan<byte> explicitCurrentPrefix) =>
        OriginalMapCameraConstruction.WritePerspectiveLoaderInitialLightingState(explicitCurrentPrefix);

    /// <summary>Fresh shader-interface Hatching/Ramp null sampling proof from
    /// this same constructed source. The caller explicitly resolves that no
    /// subsequent shader resource assignments have occurred.</summary>
    public IReadOnlyList<OriginalDeferredSourceNull2D> CreateFreshSourceNullImages(OriginalDeferredImageStateScope explicitScope) =>
        OriginalDeferredSourceNull2D.FromFreshConstruction(Construction, explicitScope);

    public OriginalDeferredSourceNullIes CreateFreshSourceNullIes(OriginalDeferredImageStateScope explicitScope) =>
        OriginalDeferredSourceNullIes.FromFreshConstruction(CameraConstructionSource, explicitScope);
}

/// <summary>One remake frame before the auto-clock tail. NextSkylightParameters
/// belongs to the next frame. Current controller overrides, runtime table
/// presence, context flags, ambient selection and Fog ownership are explicit.</summary>
public sealed record OriginalMapSourceCreatedLightingFrameInputs(long FrameNumber, OriginalMapLightingBinding Binding,
    OriginalSkylightParameters LightingSkylightParameters, ReadOnlyMemory<byte> PriorPreparedCameraPrefix,
    uint ContextFlags3E4, float FrameDelta, OriginalMapLightingRuntimeTable RuntimeTable,
    OriginalMapLightingCameraScope CameraScope, float? SelectedBuiltinAmbientCameraRate,
    bool? SelectedSharedFogOwnerEnabled, OriginalDeferredSceneInputs AdditionalSceneInputs,
    OriginalCameraLightingController? CurrentController = null,
    OriginalMapSourceCreatedSeasonLightingFrame? PresentSeasonCamera = null);

public sealed partial class OriginalMapSourceCreatedLightingPacket
{
    private readonly byte[] camera, lightData;
    private readonly byte[] constructedLightData;
    private readonly OriginalMapSourceCreatedLightingFrameInputs acceptedInputs;
    private OriginalMapSourceCreatedLightingPacket(OriginalMapSourceCreatedLightingPacket accepted)
    {
        FrameNumber=accepted.FrameNumber;Binding=accepted.Binding;SourceIdentity=accepted.SourceIdentity;
        SourceProvenance=accepted.SourceProvenance;camera=accepted.camera.ToArray();lightData=accepted.lightData.ToArray();
        constructedLightData=accepted.constructedLightData.ToArray();
        SceneInputs=OriginalMapSourceCreatedLighting.Snapshot(accepted.SceneInputs);Clock=accepted.Clock;
        SelectedController=accepted.SelectedController;LightingSkylightParameters=accepted.LightingSkylightParameters;
        RuntimeTable=accepted.RuntimeTable;ContextFlags3E4=accepted.ContextFlags3E4;
        CameraContextApplied=accepted.CameraContextApplied;
        SourceLightClassId=accepted.SourceLightClassId;
        directionalConstruction=accepted.directionalConstruction;
        directionalProviderFrame=accepted.directionalProviderFrame;
        directionalCameraApplied=accepted.directionalCameraApplied;
        directionalRowsSwapped=accepted.directionalRowsSwapped;
        acceptedInputs=OriginalMapSourceCreatedLighting.SnapshotInputs(accepted.acceptedInputs);
    }
    internal OriginalMapSourceCreatedLightingPacket(OriginalMapSourceCreatedLightingSource source,
        OriginalMapSourceCreatedLightingFrameInputs input, OriginalCameraLightingController controller,
        byte[] camera, OriginalMapSourceCreatedCameraLightDataPacket local, OriginalDeferredSceneInputs scene,
        OriginalSkylightClockResult clock)
    {
        FrameNumber = input.FrameNumber; Binding = input.Binding; SourceIdentity = source.Identity;
        SourceProvenance = source.SourceProvenance + $";View={Binding.ViewIdentity};CameraOwner={Binding.CameraOwnerIdentity};" +
            $"LightOwner={Binding.LightOwnerIdentity};frame={FrameNumber}"+
            (input.PresentSeasonCamera is { } present ? ";"+present.SourceProvenance : "");
        this.camera = camera; lightData = local.Bytes; SceneInputs = scene; Clock = clock;
        constructedLightData = source.CreatedLightData.ToArray();
        SelectedController = controller; LightingSkylightParameters = input.LightingSkylightParameters;
        RuntimeTable = input.RuntimeTable; ContextFlags3E4 = input.ContextFlags3E4;
        CameraContextApplied = local.ContextApplied;
        acceptedInputs=OriginalMapSourceCreatedLighting.SnapshotInputs(input);
    }
    public long FrameNumber { get; }
    public OriginalMapLightingBinding Binding { get; }
    public OriginalMapSourceCreatedLightingIdentity SourceIdentity { get; }
    public string SourceProvenance { get; }
    public ReadOnlyMemory<byte> PreparedCameraPrefix => camera;
    public ReadOnlyMemory<byte> LocalLightData => lightData;
    public OriginalDeferredSceneInputs SceneInputs { get; }
    public OriginalCameraLightingController SelectedController { get; }
    public OriginalSkylightParameters LightingSkylightParameters { get; }
    public OriginalSkylightClockResult Clock { get; }
    public OriginalSkylightParameters NextSkylightParameters => Clock.Parameters;
    public OriginalMapLightingRuntimeTable RuntimeTable { get; }
    public uint ContextFlags3E4 { get; }
    public bool CameraContextApplied { get; }
    public uint SourceLightClassId { get; } = 0x66ECB240;
    public bool CameraLightingApplied => directionalCameraApplied;
    public bool SourceHighBitSwappedRows => directionalRowsSwapped;
    /// <summary>Detached exact preparation input. In particular, an absent
    /// SharedFog owner remains distinct from its disabled retained-byte reader.</summary>
    public OriginalMapSourceCreatedLightingFrameInputs AcceptedInputs =>
        OriginalMapSourceCreatedLighting.SnapshotInputs(acceptedInputs);

    /// <summary>Detach the accepted Camera, local LightData and nested scene
    /// collections before an asynchronous consumer retains this frame.</summary>
    public OriginalMapSourceCreatedLightingPacket CopyAcceptedSnapshot() => new(this);

    /// <summary>The payload owner validates its own constructor and current
    /// Camera writes. Render consumers do not reconstruct a particular light
    /// loader by undoing one byte of the submitted payload.</summary>
    internal void ValidateCurrentSourceFrame()
    {
        if (directionalConstruction is not null)
        { ValidateDirectionalCurrentSourceFrame(); return; }
        if (Convert.ToHexString(SHA256.HashData(constructedLightData)) != SourceIdentity.CreatedLightDataSha256)
            throw new InvalidDataException("Current light belongs to a different constructed source generation.");
        var expected = OriginalMapSourceCreatedCameraLightData.ApplySourceMode2PreparedCamera(
            constructedLightData, camera, ContextFlags3E4);
        if (expected.ContextApplied != CameraContextApplied || !expected.Bytes.AsSpan().SequenceEqual(lightData))
            throw new InvalidDataException("Current LightData differs from its owned constructor and Camera writer.");
    }
}

/// <summary>Source-created Game default HDRI → Camera lighting → clock tail →
/// CBC770 local10C0 → Deferred scene fields. It supplies remake frame coherence
/// without claiming an active original publisher or resolving other modifiers.</summary>
public static partial class OriginalMapSourceCreatedLighting
{
    internal static OriginalMapSourceCreatedLightingFrameInputs SnapshotInputs(
        OriginalMapSourceCreatedLightingFrameInputs input) => input with
    {
        PriorPreparedCameraPrefix = input.PriorPreparedCameraPrefix.ToArray(),
        AdditionalSceneInputs = Snapshot(input.AdditionalSceneInputs)
    };

    public static OriginalMapSourceCreatedLightingPacket Prepare(OriginalMapSourceCreatedLightingSource source,
        OriginalMapSourceCreatedLightingFrameInputs input)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Binding); ArgumentNullException.ThrowIfNull(input.LightingSkylightParameters);
        ArgumentNullException.ThrowIfNull(input.AdditionalSceneInputs);
        OriginalMapLightingBinding binding = input.Binding;
        if (input.FrameNumber < 1 || new[] { binding.ViewIdentity, binding.CameraOwnerIdentity,
            binding.LightOwnerIdentity, binding.DeferredShaderIdentity }.Any(string.IsNullOrWhiteSpace) ||
            binding.DeferredCombination < 0 || binding.DirectionalShaderCapacity is < 1 or > 4)
            throw new InvalidDataException("Source-created map lighting requires explicit remake owners, shader identity/capacity and a positive frame number.");
        if (input.PriorPreparedCameraPrefix.Length != OriginalPreparedCameraLighting.PrefixByteLength)
            throw new InvalidDataException("Source-created map lighting requires the complete retained3C0 Camera prefix.");
        if (input.SelectedBuiltinAmbientCameraRate is float ambientRate && !float.IsFinite(ambientRate))
            throw new InvalidDataException("Source-created built-in ambient requires finite explicit CameraScratch+3CC.");
        if (input.RuntimeTable == OriginalMapLightingRuntimeTable.Present)
        {
            if(input.PresentSeasonCamera is not { } present)
                throw new NotSupportedException("Present lighting requires its successfully loaded owned source-season table and same-frame Main RGB/weather writer.");
            present.ValidateInputs(input);
        }
        else if(input.RuntimeTable!=OriginalMapLightingRuntimeTable.Absent || input.PresentSeasonCamera is not null)
            throw new NotSupportedException("Runtime table state is unresolved or conflicts with its actual owned season producer.");
        if (input.CameraScope != OriginalMapLightingCameraScope.LightingPrefix)
            throw new NotSupportedException("Source-created map lighting produces the3C0 lighting prefix; independent weather outputs require their original producer.");
        if (binding.BasePath != OriginalMapLightingBasePath.UnmodifiedOrdinary)
            throw new NotSupportedException("Additional or external light modifiers require their selected original producer.");
        if (input.AdditionalSceneInputs.Lighting is not null || input.AdditionalSceneInputs.Camera is not null ||
            (input.SelectedBuiltinAmbientCameraRate is not null && input.AdditionalSceneInputs.Ambient is not null) ||
            (input.SelectedSharedFogOwnerEnabled is not null && input.AdditionalSceneInputs.Fog is not null))
            throw new InvalidDataException("Source-created lighting has conflicting scene writer selections.");

        OriginalCameraLightingController controller = input.PresentSeasonCamera?.Controller ?? input.CurrentController ?? source.Controller;
        byte[] camera; OriginalSkylightClockResult clock;
        if(input.PresentSeasonCamera is { } season)
        {camera=season.PreparedCameraPrefix.ToArray();clock=season.Clock;}
        else
        {
            byte[] copied = OriginalPreparedCameraLightingSource.ApplyResolvedInputsToPrefix(controller,
                input.LightingSkylightParameters, input.PriorPreparedCameraPrefix.Span);
            OriginalPreparedCameraLightingPacket prepared = OriginalPreparedCameraLighting.PrepareWithoutTransmittanceTable(
                controller, input.LightingSkylightParameters, copied, input.FrameDelta);
            clock = OriginalSkylightClock.Advance(input.LightingSkylightParameters,
                controller.AutoDateTimeStepType, controller.TimeSpanPerDay, input.FrameDelta);
            camera = OriginalSkylightClock.ApplyToPreparedPrefix(prepared.Prefix.Span, clock);
        }
        OriginalMapSourceCreatedCameraLightDataPacket local = OriginalMapSourceCreatedCameraLightData.ApplySourceMode2PreparedCamera(
            source.CreatedLightData.Span, camera, input.ContextFlags3E4);
        ReadOnlySpan<byte> prefix = local.Bytes.AsSpan(0, 0x1B0);
        OriginalDeferredSceneInputs additional = Snapshot(input.AdditionalSceneInputs);
        OriginalDeferredSceneInputs scene = additional with
        {
            Lighting = OriginalDeferredSceneConstants.ReadPreparedBaseLighting(prefix, Trim(prefix, binding.DirectionalShaderCapacity)),
            Camera = OriginalPreparedCameraSceneSource.ReadCompletePrefix(camera),
            Ambient = input.SelectedBuiltinAmbientCameraRate is float rate
                ? OriginalDeferredSceneConstants.ReadAmbientModifier(local.Bytes.AsSpan(0x1B0, 0x178), rate)
                : additional.Ambient,
            Fog = input.SelectedSharedFogOwnerEnabled is bool fogEnabled
                ? OriginalDeferredSceneConstants.ReadSharedFog(prefix, fogEnabled)
                : additional.Fog
        };
        return new(source, input, controller, camera, local, scene, clock);
    }

    // D77008..D77121 trims trailing rows using RGB only, retaining row0.
    private static int Trim(ReadOnlySpan<byte> bytes, int count)
    {
        while (count > 1)
        {
            int at = (count - 1) * 32 + 16;
            float r = Read(bytes, at), g = Read(bytes, at + 4), b = Read(bytes, at + 8);
            if (r > 0 || g > 0 || b > 0) break;
            count--;
        }
        return count;
    }
    private static float Read(ReadOnlySpan<byte> bytes, int at)
    {
        float value = BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(at, 4));
        if (!float.IsFinite(value)) throw new InvalidDataException("Source-created lighting consumed a nonfinite retained LightData color.");
        return value;
    }
    internal static OriginalDeferredSceneInputs Snapshot(OriginalDeferredSceneInputs input) => input with
    {
        Lighting = input.Lighting is { } lighting
            ? lighting with { Lights = Array.AsReadOnly(lighting.Lights.ToArray()) } : null,
        Fog = input.Fog is { } fog ? fog with { Scattering = Array.AsReadOnly(fog.Scattering.ToArray()) } : null,
        Refraction = input.Refraction is { WorldToTexture: { } rows } refraction
            ? refraction with { WorldToTexture = Array.AsReadOnly(rows.ToArray()) } : input.Refraction,
        HeightFog = input.HeightFog is { } height ? height with { Types = Array.AsReadOnly(height.Types.ToArray()),
            Colors = Array.AsReadOnly(height.Colors.ToArray()), Parameters = Array.AsReadOnly(height.Parameters.ToArray()),
            FluctuationParameters = Array.AsReadOnly(height.FluctuationParameters.ToArray()) } : null,
        RimTable = input.RimTable is { } rim ? rim with { Colors = Array.AsReadOnly(rim.Colors.ToArray()),
            Extents = Array.AsReadOnly(rim.Extents.ToArray()) } : null,
        Ramp = input.Ramp is { } ramp ? ramp with { Parameters = Array.AsReadOnly(ramp.Parameters.ToArray()),
            RimParameters = Array.AsReadOnly(ramp.RimParameters.ToArray()),
            SecondaryRimParameters = Array.AsReadOnly(ramp.SecondaryRimParameters.ToArray()) } : null
    };
}
