using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public enum MapLightingRuntimeTable { Unresolved, Absent, Present }
public enum MapLightingBasePath { UnmodifiedOrdinary, AdditionalModifiers }
public enum MapLightingCameraScope { LightingPrefix, CompleteCameraWeatherOutputs }

/// <summary>Caller-selected active associations. None of these identities is
/// inferred from a group mask, Camera order bit, cached pointer or source name.
/// DeferredShaderIdentity is the selected container's ResourceUri@DecodedSha256.</summary>
public sealed record MapLightingBinding(string ViewIdentity, string CameraOwnerIdentity,
    string LightOwnerIdentity, string DeferredShaderIdentity, int DeferredCombination,
    int DirectionalShaderCapacity, MapLightingBasePath BasePath);

public sealed record MapLightingSourceIdentity(string CameraResourceUri, string CameraSha256,
    uint CameraObjectId, string DirectionalResourceUri, string DirectionalSha256,
    uint DirectionalObjectId, uint DeclaredGroupMask);

/// <summary>Selected original declarations; selecting them does not select an
/// active publisher. Source-initialized complete LightData remains an explicit
/// per-frame input, including its quaternion, Hemi, occurrence and ambient words.</summary>
public sealed record MapLightingSource(LoadedPreparedCameraLightingSource Camera,
    LoadedDirectionalLightDeclaration Directional, DeferredAmbientSourceParameters Ambient)
{
    public MapLightingSourceIdentity Identity => new(Camera.Source.Resource.Source.ResourceUri,
        Camera.Source.Resource.Source.DecodedSha256, Camera.Source.Camera.ObjectId,
        Directional.Source.ResourceUri, Directional.Source.DecodedSha256,
        Directional.Declaration.ObjectId, Directional.Declaration.SourceGroupMask);

    public static MapLightingSource Load(GameResourceRepository repository,
        string cameraArchive, uint cameraFileId, uint cameraObjectId,
        string directionalArchive, uint directionalFileId, uint directionalObjectId)
    {
        ArgumentNullException.ThrowIfNull(repository);
        LoadedObjects camera = repository.LoadObjects(cameraArchive, cameraFileId);
        LoadedObjects light = repository.LoadObjects(directionalArchive, directionalFileId);
        DokObject[] matches = light.Document.Objects.Where(o => o.ObjectId == directionalObjectId).ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException("Map lighting requires one explicitly selected Directional source object.");
        return new(PreparedCameraLightingSource.ReadPerspective(camera, cameraObjectId),
            new(DirectionalLightSource.Read(matches[0]), light.Source),
            DeferredAmbientSource.Read(matches[0]));
    }
}

/// <summary>Source state used before the original auto-clock tail. The next
/// frame receives NextSkylightParameters and PreparedCameraPrefix explicitly.
/// Null ambient/Fog selections retain the corresponding additional inputs and
/// ultimately the caller's complete CB0 template.</summary>
public sealed record MapLightingFrameInputs(long FrameNumber, MapLightingBinding Binding,
    CameraLightingController SelectedController, SkylightParameters ResolvedSkylightParameters,
    ReadOnlyMemory<byte> PriorPreparedCameraPrefix, ReadOnlyMemory<byte> SourceInitializedLightData,
    uint ContextFlags3E4, float FrameDelta, MapLightingRuntimeTable RuntimeTable,
    MapLightingCameraScope CameraScope, float? SelectedBuiltinAmbientCameraRate,
    bool? SelectedSharedFogOwnerEnabled, DeferredSceneInputs AdditionalSceneInputs);

public sealed class MapLightingPacket
{
    private readonly byte[] camera, lightData;
    internal MapLightingPacket(MapLightingFrameInputs input, MapLightingSourceIdentity source,
        byte[] camera, byte[] lightData, DeferredSceneInputs sceneInputs,
        SkylightClockResult clock, bool cameraApplied, bool sourceHighBitSwappedRows)
    {
        FrameNumber = input.FrameNumber; Binding = input.Binding; SourceIdentity = source;
        this.camera = camera; this.lightData = lightData; SceneInputs = sceneInputs; Clock = clock;
        LightingSkylightParameters = input.ResolvedSkylightParameters;
        SelectedController = input.SelectedController; RuntimeTable = input.RuntimeTable;
        CameraLightingApplied = cameraApplied; SourceHighBitSwappedRows = sourceHighBitSwappedRows;
    }
    public long FrameNumber { get; }
    public MapLightingBinding Binding { get; }
    public MapLightingSourceIdentity SourceIdentity { get; }
    public string SourceProvenance => $"{SourceIdentity.CameraResourceUri}#{SourceIdentity.CameraObjectId:X8}@{SourceIdentity.CameraSha256};" +
        $"{SourceIdentity.DirectionalResourceUri}#{SourceIdentity.DirectionalObjectId:X8}@{SourceIdentity.DirectionalSha256};" +
        $"View={Binding.ViewIdentity};CameraOwner={Binding.CameraOwnerIdentity};LightOwner={Binding.LightOwnerIdentity};frame={FrameNumber}";
    public ReadOnlyMemory<byte> PreparedCameraPrefix => camera;
    public ReadOnlyMemory<byte> LocalLightData => lightData;
    public DeferredSceneInputs SceneInputs { get; }
    public SkylightParameters LightingSkylightParameters { get; }
    public CameraLightingController SelectedController { get; }
    public MapLightingRuntimeTable RuntimeTable { get; }
    public SkylightClockResult Clock { get; }
    public SkylightParameters NextSkylightParameters => Clock.Parameters;
    public bool CameraLightingApplied { get; }
    public bool SourceHighBitSwappedRows { get; }
}

/// <summary>Per-frame original Camera source→lighting→clock tail→CBC770 local
/// LightData→Deferred inputs. This explicit provider never discovers an active
/// View/publisher, initializes opaque fields or enables the main map.</summary>
public static class MapLightingProvider
{
    public static MapLightingPacket Prepare(MapLightingSource source,
        MapLightingFrameInputs input)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(source.Camera); ArgumentNullException.ThrowIfNull(source.Directional);
        ArgumentNullException.ThrowIfNull(source.Ambient); ArgumentNullException.ThrowIfNull(input.Binding);
        ArgumentNullException.ThrowIfNull(input.SelectedController);
        ArgumentNullException.ThrowIfNull(input.ResolvedSkylightParameters);
        ArgumentNullException.ThrowIfNull(input.AdditionalSceneInputs);
        MapLightingBinding binding = input.Binding;
        if (input.FrameNumber < 1 || new[] { binding.ViewIdentity, binding.CameraOwnerIdentity,
            binding.LightOwnerIdentity, binding.DeferredShaderIdentity }.Any(string.IsNullOrWhiteSpace) ||
            binding.DeferredCombination < 0 || binding.DirectionalShaderCapacity is < 1 or > 4)
            throw new InvalidDataException("Map lighting requires explicit active View, Camera/Light owners, shader row/capacity and positive frame identity.");
        if (input.PriorPreparedCameraPrefix.Length != PreparedCameraLighting.PrefixByteLength ||
            input.SourceInitializedLightData.Length != CameraDirectionalLightData.LightDataBytes)
            throw new InvalidDataException("Map lighting requires the complete prior3C0 Camera and source-initialized10C0 LightData templates.");
        if (input.RuntimeTable != MapLightingRuntimeTable.Absent)
            throw new NotSupportedException("Map lighting requires explicitly resolved runtime table absence; nonnull table RGB/Fog/weather preparation is unresolved.");
        if (input.CameraScope != MapLightingCameraScope.LightingPrefix)
            throw new NotSupportedException("This provider produces only the lighting3C0 prefix; independent weather outputs outside that prefix need their original producer.");
        if (binding.BasePath != MapLightingBasePath.UnmodifiedOrdinary)
            throw new NotSupportedException("Additional/external light modifiers require their selected original producer before Deferred projection.");
        if (input.AdditionalSceneInputs.Lighting is not null || input.AdditionalSceneInputs.Camera is not null ||
            (input.SelectedBuiltinAmbientCameraRate is not null && input.AdditionalSceneInputs.Ambient is not null) ||
            (input.SelectedSharedFogOwnerEnabled is not null && input.AdditionalSceneInputs.Fog is not null))
            throw new InvalidDataException("Map lighting has conflicting explicit scene writer selections.");
        MapLightingSourceIdentity identity = source.Identity;
        if (string.IsNullOrWhiteSpace(identity.CameraResourceUri) || string.IsNullOrWhiteSpace(identity.DirectionalResourceUri) ||
            !Hash(identity.CameraSha256) || !Hash(identity.DirectionalSha256) ||
            source.Camera.Source.Camera.Kind != DokObjectKind.Inline ||
            source.Camera.Source.Camera.ClassId != CameraSkylightSource.PerspectiveCameraClassId)
            throw new InvalidDataException("Map lighting requires retained typed Camera/Directional resource identities and decoded SHA256 hashes.");

        byte[] copied = PreparedCameraLightingSource.ApplyResolvedInputsToPrefix(input.SelectedController,
            input.ResolvedSkylightParameters, input.PriorPreparedCameraPrefix.Span);
        PreparedCameraLightingPacket lighting = PreparedCameraLighting.PrepareWithoutTransmittanceTable(
            input.SelectedController, input.ResolvedSkylightParameters, copied, input.FrameDelta);
        SkylightClockResult clock = SkylightClock.Advance(input.ResolvedSkylightParameters,
            input.SelectedController.AutoDateTimeStepType, input.SelectedController.TimeSpanPerDay, input.FrameDelta);
        byte[] camera = SkylightClock.ApplyToPreparedPrefix(lighting.Prefix.Span, clock);
        CameraDirectionalLightDataPacket local = CameraDirectionalLightData.Apply(
            input.SourceInitializedLightData.Span, camera, input.ContextFlags3E4);
        ReadOnlySpan<byte> prefix = local.Bytes.AsSpan(0, 0x1B0);
        int count = Trim(prefix, binding.DirectionalShaderCapacity);
        DeferredSceneInputs scene = input.AdditionalSceneInputs with
        {
            Lighting = DeferredSceneConstants.ReadPreparedBaseLighting(prefix, count),
            Camera = PreparedCameraSceneSource.ReadCompletePrefix(camera),
            Ambient = input.SelectedBuiltinAmbientCameraRate is float rate
                ? DeferredAmbientSource.Prepare(source.Ambient, rate) : input.AdditionalSceneInputs.Ambient,
            Fog = input.SelectedSharedFogOwnerEnabled is bool fogEnabled
                ? DeferredSceneConstants.ReadSharedFog(prefix, fogEnabled) : input.AdditionalSceneInputs.Fog
        };
        return new(input, identity, camera, local.Bytes, scene, clock, local.Applied, local.SourceHighBitSwappedRows);
    }

    // D77008..D77121 checks only trailing RGB, retaining at least the first row.
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
        if (!float.IsFinite(value)) throw new InvalidDataException("Map lighting consumed a nonfinite source LightData color.");
        return value;
    }
    private static bool Hash(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
