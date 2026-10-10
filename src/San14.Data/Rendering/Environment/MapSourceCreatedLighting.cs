using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace San14.Data;

/// <summary>The source declarations and constructed byte generation used by a
/// remake owner. ViewSlot is the original Game setter slot, not an observation
/// of an active original View or publisher.</summary>
public sealed record MapSourceCreatedLightingIdentity(int ViewSlot, uint ViewParamObjectId,
    string CameraResourceUri, string CameraSha256, uint CameraObjectId,
    string MainLightResourceUri, string MainLightSha256, uint MainLightObjectId,
    uint DeclaredGroupMask, uint DeclaredPrimaryGroupMask, string CreatedLightDataSha256,
    string ConstructedOwnerSha256);

/// <summary>Immutable source-created main-light seed with the exact Camera3/4
/// source selected by Game. Complete constructor-retained bytes are supplied by
/// OriginalMainLightConstruction, so this owner never fills its unwritten holes.</summary>
public sealed class MapSourceCreatedLightingSource
{
    private readonly byte[] lightData;
    private MapSourceCreatedLightingSource(LoadedMainLightBindingCameraSource setter,
        MainLightConstructionPacket construction)
    {
        LoadedMainLightBindingSource light = MainLightBindingSource.ReadGameDefault(construction.Source.Resource);
        if (construction.Bytes.Length != CameraDirectionalLightData.LightDataBytes ||
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
        CameraConstructionSource = MapCameraConstructionSource.ReadPerspective(camera.Resource, camera.Camera.ObjectId);
        ConstructionProvenance = construction.SourceProvenance;
        ResourceBindings = Array.AsReadOnly(construction.ResourceBindings.ToArray());
        AllResourceBindingsReady = construction.AllResourceBindingsReady;
        Construction = construction;
    }

    public MapSourceCreatedLightingIdentity Identity { get; }
    public CameraLightingController Controller { get; }
    public SkylightParameters InitialSkylightParameters { get; }
    public LoadedMapCameraConstructionSource CameraConstructionSource { get; }
    public string ConstructionProvenance { get; }
    public IReadOnlyList<MainLightConstructionResourceBinding> ResourceBindings { get; }
    public bool AllResourceBindingsReady { get; }
    public MainLightConstructionPacket Construction { get; }
    public string SourceProvenance => $"remake-source-created;{ConstructionProvenance};" +
        $"Camera={Identity.CameraResourceUri}#{Identity.CameraObjectId:X8}@{Identity.CameraSha256};" +
        $"ViewSlot={Identity.ViewSlot};ViewParam={Identity.ViewParamObjectId:X8};" +
        $"CreatedLightDataSha256={Identity.CreatedLightDataSha256};ConstructedOwnerSha256={Identity.ConstructedOwnerSha256}";
    public ReadOnlyMemory<byte> CreatedLightData => lightData;

    public static MapSourceCreatedLightingSource FromGameDefault(GameResourceRepository repository,
        int viewSlot, MainLightConstructionPacket constructedLight)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(constructedLight);
        return new(MainLightBindingSource.LoadSetterCamera(repository, viewSlot), constructedLight);
    }

    /// <summary>Use once when this remake Camera is constructed. Matrix, Eye,
    /// lens and other retained prefix bytes are still explicitly supplied.</summary>
    public byte[] CreateInitialCameraPrefix(ReadOnlySpan<byte> explicitCompletePrefix) =>
        PreparedCameraLighting.InitializeConstructorLightingFields(explicitCompletePrefix);

    /// <summary>Apply Perspective loader C59762's initial FF transition state
    /// immediately before its delta0 lighting call. The complete current Camera
    /// fields are retained; source optics and optional lens callback resolution
    /// remain with the Camera producer.</summary>
    public byte[] CreatePerspectiveLoaderInitialCameraPrefix(ReadOnlySpan<byte> explicitCurrentPrefix) =>
        MapCameraConstruction.WritePerspectiveLoaderInitialLightingState(explicitCurrentPrefix);

    /// <summary>Fresh shader-interface Hatching/Ramp null sampling proof from
    /// this same constructed source. The caller explicitly resolves that no
    /// subsequent shader resource assignments have occurred.</summary>
    public IReadOnlyList<DeferredSourceNull2D> CreateFreshSourceNullImages(DeferredImageStateScope explicitScope) =>
        DeferredSourceNull2D.FromFreshConstruction(Construction, explicitScope);

    public DeferredSourceNullIes CreateFreshSourceNullIes(DeferredImageStateScope explicitScope) =>
        DeferredSourceNullIes.FromFreshConstruction(CameraConstructionSource, explicitScope);
}

/// <summary>One remake frame before the auto-clock tail. NextSkylightParameters
/// belongs to the next frame. Current controller overrides, runtime table
/// presence, context flags, ambient selection and Fog ownership are explicit.</summary>
public sealed record MapSourceCreatedLightingFrameInputs(long FrameNumber, MapLightingBinding Binding,
    SkylightParameters LightingSkylightParameters, ReadOnlyMemory<byte> PriorPreparedCameraPrefix,
    uint ContextFlags3E4, float FrameDelta, MapLightingRuntimeTable RuntimeTable,
    MapLightingCameraScope CameraScope, float? SelectedBuiltinAmbientCameraRate,
    bool? SelectedSharedFogOwnerEnabled, DeferredSceneInputs AdditionalSceneInputs,
    CameraLightingController? CurrentController = null,
    MapSourceCreatedSeasonLightingFrame? PresentSeasonCamera = null);

public sealed partial class MapSourceCreatedLightingPacket
{
    private readonly byte[] camera, lightData;
    private readonly MapSourceCreatedLightingFrameInputs acceptedInputs;
    private MapSourceCreatedLightingPacket(MapSourceCreatedLightingPacket accepted)
    {
        FrameNumber=accepted.FrameNumber;Binding=accepted.Binding;SourceIdentity=accepted.SourceIdentity;
        SourceProvenance=accepted.SourceProvenance;camera=accepted.camera.ToArray();lightData=accepted.lightData.ToArray();
        SceneInputs=MapSourceCreatedLighting.Snapshot(accepted.SceneInputs);Clock=accepted.Clock;
        SelectedController=accepted.SelectedController;LightingSkylightParameters=accepted.LightingSkylightParameters;
        RuntimeTable=accepted.RuntimeTable;ContextFlags3E4=accepted.ContextFlags3E4;
        CameraContextApplied=accepted.CameraContextApplied;
        SourceLightClassId=accepted.SourceLightClassId;
        directionalConstruction=accepted.directionalConstruction;
        directionalProviderFrame=accepted.directionalProviderFrame;
        Optics=accepted.Optics;
        directionalCameraApplied=accepted.directionalCameraApplied;
        directionalRowsSwapped=accepted.directionalRowsSwapped;
        acceptedInputs=MapSourceCreatedLighting.SnapshotInputs(accepted.acceptedInputs);
    }
    internal MapSourceCreatedLightingPacket(MapSourceCreatedLightingSource source,
        MapSourceCreatedLightingFrameInputs input, CameraLightingController controller,
        byte[] camera, MapSourceCreatedCameraLightDataPacket local, DeferredSceneInputs scene,
        SkylightClockResult clock)
    {
        FrameNumber = input.FrameNumber; Binding = input.Binding; SourceIdentity = source.Identity;
        SourceProvenance = source.SourceProvenance + $";View={Binding.ViewIdentity};CameraOwner={Binding.CameraOwnerIdentity};" +
            $"LightOwner={Binding.LightOwnerIdentity};frame={FrameNumber}"+
            (input.PresentSeasonCamera is { } present ? ";"+present.SourceProvenance : "");
        this.camera = camera; lightData = local.Bytes; SceneInputs = scene; Clock = clock;
        Optics=CameraOpticsEncoding.DecodePrefix(source.CameraConstructionSource,camera);
        SelectedController = controller; LightingSkylightParameters = input.LightingSkylightParameters;
        RuntimeTable = input.RuntimeTable; ContextFlags3E4 = input.ContextFlags3E4;
        CameraContextApplied = local.ContextApplied;
        acceptedInputs=MapSourceCreatedLighting.SnapshotInputs(input);
    }
    public long FrameNumber { get; }
    public MapLightingBinding Binding { get; }
    public MapSourceCreatedLightingIdentity SourceIdentity { get; }
    public string SourceProvenance { get; }
    public ReadOnlyMemory<byte> PreparedCameraPrefix => camera;
    public CameraOpticsSettings Optics { get; }
    public ReadOnlyMemory<byte> LocalLightData => lightData;
    public DeferredSceneInputs SceneInputs { get; }
    public CameraLightingController SelectedController { get; }
    public SkylightParameters LightingSkylightParameters { get; }
    public SkylightClockResult Clock { get; }
    public SkylightParameters NextSkylightParameters => Clock.Parameters;
    public MapLightingRuntimeTable RuntimeTable { get; }
    public uint ContextFlags3E4 { get; }
    public bool CameraContextApplied { get; }
    public uint SourceLightClassId { get; } = 0x66ECB240;
    public bool CameraLightingApplied => directionalCameraApplied;
    public bool SourceHighBitSwappedRows => directionalRowsSwapped;
    /// <summary>Detached exact preparation input. In particular, an absent
    /// SharedFog owner remains distinct from its disabled retained-byte reader.</summary>
    public MapSourceCreatedLightingFrameInputs AcceptedInputs =>
        MapSourceCreatedLighting.SnapshotInputs(acceptedInputs);

    /// <summary>Detach the accepted Camera, local LightData and nested scene
    /// collections before an asynchronous consumer retains this frame.</summary>
    public MapSourceCreatedLightingPacket CopyAcceptedSnapshot() => new(this);

}

/// <summary>Source-created Game default HDRI → Camera lighting → clock tail →
/// CBC770 local10C0 → Deferred scene fields. It supplies remake frame coherence
/// without claiming an active original publisher or resolving other modifiers.</summary>
public static partial class MapSourceCreatedLighting
{
    internal static MapSourceCreatedLightingFrameInputs SnapshotInputs(
        MapSourceCreatedLightingFrameInputs input) => input with
    {
        PriorPreparedCameraPrefix = input.PriorPreparedCameraPrefix.ToArray(),
        AdditionalSceneInputs = Snapshot(input.AdditionalSceneInputs)
    };

    public static MapSourceCreatedLightingPacket Prepare(MapSourceCreatedLightingSource source,
        MapSourceCreatedLightingFrameInputs input)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Binding); ArgumentNullException.ThrowIfNull(input.LightingSkylightParameters);
        ArgumentNullException.ThrowIfNull(input.AdditionalSceneInputs);
        MapLightingBinding binding = input.Binding;
        if (input.FrameNumber < 1 || new[] { binding.ViewIdentity, binding.CameraOwnerIdentity,
            binding.LightOwnerIdentity, binding.DeferredShaderIdentity }.Any(string.IsNullOrWhiteSpace) ||
            binding.DeferredCombination < 0 || binding.DirectionalShaderCapacity is < 1 or > 4)
            throw new InvalidDataException("Source-created map lighting requires explicit remake owners, shader identity/capacity and a positive frame number.");
        if (input.PriorPreparedCameraPrefix.Length != PreparedCameraLighting.PrefixByteLength)
            throw new InvalidDataException("Source-created map lighting requires the complete retained3C0 Camera prefix.");
        if (input.SelectedBuiltinAmbientCameraRate is float ambientRate && !float.IsFinite(ambientRate))
            throw new InvalidDataException("Source-created built-in ambient requires finite explicit CameraScratch+3CC.");
        if (input.RuntimeTable == MapLightingRuntimeTable.Present)
        {
            if(input.PresentSeasonCamera is not { } present)
                throw new NotSupportedException("Present lighting requires its successfully loaded owned source-season table and same-frame Main RGB/weather writer.");
            present.ValidateInputs(input);
        }
        else if(input.RuntimeTable!=MapLightingRuntimeTable.Absent || input.PresentSeasonCamera is not null)
            throw new NotSupportedException("Runtime table state is unresolved or conflicts with its actual owned season producer.");
        if (input.CameraScope != MapLightingCameraScope.LightingPrefix)
            throw new NotSupportedException("Source-created map lighting produces the3C0 lighting prefix; independent weather outputs require their original producer.");
        if (binding.BasePath != MapLightingBasePath.UnmodifiedOrdinary)
            throw new NotSupportedException("Additional or external light modifiers require their selected original producer.");
        if (input.AdditionalSceneInputs.Lighting is not null || input.AdditionalSceneInputs.Camera is not null ||
            (input.SelectedBuiltinAmbientCameraRate is not null && input.AdditionalSceneInputs.Ambient is not null) ||
            (input.SelectedSharedFogOwnerEnabled is not null && input.AdditionalSceneInputs.Fog is not null))
            throw new InvalidDataException("Source-created lighting has conflicting scene writer selections.");

        CameraLightingController controller = input.PresentSeasonCamera?.Controller ?? input.CurrentController ?? source.Controller;
        byte[] camera; SkylightClockResult clock;
        if(input.PresentSeasonCamera is { } season)
        {camera=season.PreparedCameraPrefix.ToArray();clock=season.Clock;}
        else
        {
            byte[] copied = PreparedCameraLightingSource.ApplyResolvedInputsToPrefix(controller,
                input.LightingSkylightParameters, input.PriorPreparedCameraPrefix.Span);
            PreparedCameraLightingPacket prepared = PreparedCameraLighting.PrepareWithoutTransmittanceTable(
                controller, input.LightingSkylightParameters, copied, input.FrameDelta);
            clock = SkylightClock.Advance(input.LightingSkylightParameters,
                controller.AutoDateTimeStepType, controller.TimeSpanPerDay, input.FrameDelta);
            camera = SkylightClock.ApplyToPreparedPrefix(prepared.Prefix.Span, clock);
        }
        MapSourceCreatedCameraLightDataPacket local = MapSourceCreatedCameraLightData.ApplySourceMode2PreparedCamera(
            source.CreatedLightData.Span, camera, input.ContextFlags3E4);
        ReadOnlySpan<byte> prefix = local.Bytes.AsSpan(0, 0x1B0);
        DeferredSceneInputs additional = Snapshot(input.AdditionalSceneInputs);
        DeferredSceneInputs scene = additional with
        {
            Lighting = DeferredSceneConstants.ReadPreparedBaseLighting(prefix, Trim(prefix, binding.DirectionalShaderCapacity)),
            Camera = PreparedCameraSceneSource.ReadCompletePrefix(camera),
            Ambient = input.SelectedBuiltinAmbientCameraRate is float rate
                ? DeferredSceneConstants.ReadAmbientModifier(local.Bytes.AsSpan(0x1B0, 0x178), rate)
                : additional.Ambient,
            Fog = input.SelectedSharedFogOwnerEnabled is bool fogEnabled
                ? DeferredSceneConstants.ReadSharedFog(prefix, fogEnabled)
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
    internal static DeferredSceneInputs Snapshot(DeferredSceneInputs input) => input with
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
