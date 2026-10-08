using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public enum OriginalMapLightingRuntimeTable { Unresolved, Absent, Present }
public enum OriginalMapLightingBasePath { UnmodifiedOrdinary, AdditionalModifiers }
public enum OriginalMapLightingCameraScope { LightingPrefix, CompleteCameraWeatherOutputs }

/// <summary>Caller-selected active associations. None of these identities is
/// inferred from a group mask, Camera order bit, cached pointer or source name.
/// DeferredShaderIdentity is the selected container's ResourceUri@DecodedSha256.</summary>
public sealed record OriginalMapLightingBinding(string ViewIdentity, string CameraOwnerIdentity,
    string LightOwnerIdentity, string DeferredShaderIdentity, int DeferredCombination,
    int DirectionalShaderCapacity, OriginalMapLightingBasePath BasePath);

public sealed record OriginalMapLightingSourceIdentity(string CameraResourceUri, string CameraSha256,
    uint CameraObjectId, string DirectionalResourceUri, string DirectionalSha256,
    uint DirectionalObjectId, uint DeclaredGroupMask);

/// <summary>Selected original declarations; selecting them does not select an
/// active publisher. Source-initialized complete LightData remains an explicit
/// per-frame input, including its quaternion, Hemi, occurrence and ambient words.</summary>
public sealed record OriginalMapLightingSource(LoadedPreparedCameraLightingSource Camera,
    LoadedOriginalDirectionalLightDeclaration Directional, OriginalDeferredAmbientSourceParameters Ambient)
{
    public OriginalMapLightingSourceIdentity Identity => new(Camera.Source.Resource.Source.ResourceUri,
        Camera.Source.Resource.Source.DecodedSha256, Camera.Source.Camera.ObjectId,
        Directional.Source.ResourceUri, Directional.Source.DecodedSha256,
        Directional.Declaration.ObjectId, Directional.Declaration.SourceGroupMask);

    public static OriginalMapLightingSource Load(OriginalGameRepository repository,
        string cameraArchive, uint cameraFileId, uint cameraObjectId,
        string directionalArchive, uint directionalFileId, uint directionalObjectId)
    {
        ArgumentNullException.ThrowIfNull(repository);
        LoadedObjects camera = repository.LoadObjects(cameraArchive, cameraFileId);
        LoadedObjects light = repository.LoadObjects(directionalArchive, directionalFileId);
        DokObject[] matches = light.Document.Objects.Where(o => o.ObjectId == directionalObjectId).ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException("Map lighting requires one explicitly selected Directional source object.");
        return new(OriginalPreparedCameraLightingSource.ReadPerspective(camera, cameraObjectId),
            new(OriginalDirectionalLightSource.Read(matches[0]), light.Source),
            OriginalDeferredAmbientSource.Read(matches[0]));
    }
}

/// <summary>Source state used before the original auto-clock tail. The next
/// frame receives NextSkylightParameters and PreparedCameraPrefix explicitly.
/// Null ambient/Fog selections retain the corresponding additional inputs and
/// ultimately the caller's complete CB0 template.</summary>
public sealed record OriginalMapLightingFrameInputs(long FrameNumber, OriginalMapLightingBinding Binding,
    OriginalCameraLightingController SelectedController, OriginalSkylightParameters ResolvedSkylightParameters,
    ReadOnlyMemory<byte> PriorPreparedCameraPrefix, ReadOnlyMemory<byte> SourceInitializedLightData,
    uint ContextFlags3E4, float FrameDelta, OriginalMapLightingRuntimeTable RuntimeTable,
    OriginalMapLightingCameraScope CameraScope, float? SelectedBuiltinAmbientCameraRate,
    bool? SelectedSharedFogOwnerEnabled, OriginalDeferredSceneInputs AdditionalSceneInputs);

public sealed class OriginalMapLightingPacket
{
    private readonly byte[] camera, lightData;
    internal OriginalMapLightingPacket(OriginalMapLightingFrameInputs input, OriginalMapLightingSourceIdentity source,
        byte[] camera, byte[] lightData, OriginalDeferredSceneInputs sceneInputs,
        OriginalSkylightClockResult clock, bool cameraApplied, bool sourceHighBitSwappedRows)
    {
        FrameNumber = input.FrameNumber; Binding = input.Binding; SourceIdentity = source;
        this.camera = camera; this.lightData = lightData; SceneInputs = sceneInputs; Clock = clock;
        LightingSkylightParameters = input.ResolvedSkylightParameters;
        SelectedController = input.SelectedController; RuntimeTable = input.RuntimeTable;
        CameraLightingApplied = cameraApplied; SourceHighBitSwappedRows = sourceHighBitSwappedRows;
    }
    public long FrameNumber { get; }
    public OriginalMapLightingBinding Binding { get; }
    public OriginalMapLightingSourceIdentity SourceIdentity { get; }
    public string SourceProvenance => $"{SourceIdentity.CameraResourceUri}#{SourceIdentity.CameraObjectId:X8}@{SourceIdentity.CameraSha256};" +
        $"{SourceIdentity.DirectionalResourceUri}#{SourceIdentity.DirectionalObjectId:X8}@{SourceIdentity.DirectionalSha256};" +
        $"View={Binding.ViewIdentity};CameraOwner={Binding.CameraOwnerIdentity};LightOwner={Binding.LightOwnerIdentity};frame={FrameNumber}";
    public ReadOnlyMemory<byte> PreparedCameraPrefix => camera;
    public ReadOnlyMemory<byte> LocalLightData => lightData;
    public OriginalDeferredSceneInputs SceneInputs { get; }
    public OriginalSkylightParameters LightingSkylightParameters { get; }
    public OriginalCameraLightingController SelectedController { get; }
    public OriginalMapLightingRuntimeTable RuntimeTable { get; }
    public OriginalSkylightClockResult Clock { get; }
    public OriginalSkylightParameters NextSkylightParameters => Clock.Parameters;
    public bool CameraLightingApplied { get; }
    public bool SourceHighBitSwappedRows { get; }
}

/// <summary>Per-frame original Camera source→lighting→clock tail→CBC770 local
/// LightData→Deferred inputs. This explicit provider never discovers an active
/// View/publisher, initializes opaque fields or enables the main map.</summary>
public static class OriginalMapLightingProvider
{
    public static OriginalMapLightingPacket Prepare(OriginalMapLightingSource source,
        OriginalMapLightingFrameInputs input)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(source.Camera); ArgumentNullException.ThrowIfNull(source.Directional);
        ArgumentNullException.ThrowIfNull(source.Ambient); ArgumentNullException.ThrowIfNull(input.Binding);
        ArgumentNullException.ThrowIfNull(input.SelectedController);
        ArgumentNullException.ThrowIfNull(input.ResolvedSkylightParameters);
        ArgumentNullException.ThrowIfNull(input.AdditionalSceneInputs);
        OriginalMapLightingBinding binding = input.Binding;
        if (input.FrameNumber < 1 || new[] { binding.ViewIdentity, binding.CameraOwnerIdentity,
            binding.LightOwnerIdentity, binding.DeferredShaderIdentity }.Any(string.IsNullOrWhiteSpace) ||
            binding.DeferredCombination < 0 || binding.DirectionalShaderCapacity is < 1 or > 4)
            throw new InvalidDataException("Map lighting requires explicit active View, Camera/Light owners, shader row/capacity and positive frame identity.");
        if (input.PriorPreparedCameraPrefix.Length != OriginalPreparedCameraLighting.PrefixByteLength ||
            input.SourceInitializedLightData.Length != OriginalCameraDirectionalLightData.LightDataBytes)
            throw new InvalidDataException("Map lighting requires the complete prior3C0 Camera and source-initialized10C0 LightData templates.");
        if (input.RuntimeTable != OriginalMapLightingRuntimeTable.Absent)
            throw new NotSupportedException("Map lighting requires explicitly resolved runtime table absence; nonnull table RGB/Fog/weather preparation is unresolved.");
        if (input.CameraScope != OriginalMapLightingCameraScope.LightingPrefix)
            throw new NotSupportedException("This provider produces only the lighting3C0 prefix; independent weather outputs outside that prefix need their original producer.");
        if (binding.BasePath != OriginalMapLightingBasePath.UnmodifiedOrdinary)
            throw new NotSupportedException("Additional/external light modifiers require their selected original producer before Deferred projection.");
        if (input.AdditionalSceneInputs.Lighting is not null || input.AdditionalSceneInputs.Camera is not null ||
            (input.SelectedBuiltinAmbientCameraRate is not null && input.AdditionalSceneInputs.Ambient is not null) ||
            (input.SelectedSharedFogOwnerEnabled is not null && input.AdditionalSceneInputs.Fog is not null))
            throw new InvalidDataException("Map lighting has conflicting explicit scene writer selections.");
        OriginalMapLightingSourceIdentity identity = source.Identity;
        if (string.IsNullOrWhiteSpace(identity.CameraResourceUri) || string.IsNullOrWhiteSpace(identity.DirectionalResourceUri) ||
            !Hash(identity.CameraSha256) || !Hash(identity.DirectionalSha256) ||
            source.Camera.Source.Camera.Kind != DokObjectKind.Inline ||
            source.Camera.Source.Camera.ClassId != OriginalCameraSkylightSource.PerspectiveCameraClassId)
            throw new InvalidDataException("Map lighting requires retained typed Camera/Directional resource identities and decoded SHA256 hashes.");

        byte[] copied = OriginalPreparedCameraLightingSource.ApplyResolvedInputsToPrefix(input.SelectedController,
            input.ResolvedSkylightParameters, input.PriorPreparedCameraPrefix.Span);
        OriginalPreparedCameraLightingPacket lighting = OriginalPreparedCameraLighting.PrepareWithoutTransmittanceTable(
            input.SelectedController, input.ResolvedSkylightParameters, copied, input.FrameDelta);
        OriginalSkylightClockResult clock = OriginalSkylightClock.Advance(input.ResolvedSkylightParameters,
            input.SelectedController.AutoDateTimeStepType, input.SelectedController.TimeSpanPerDay, input.FrameDelta);
        byte[] camera = OriginalSkylightClock.ApplyToPreparedPrefix(lighting.Prefix.Span, clock);
        OriginalCameraDirectionalLightDataPacket local = OriginalCameraDirectionalLightData.Apply(
            input.SourceInitializedLightData.Span, camera, input.ContextFlags3E4);
        ReadOnlySpan<byte> prefix = local.Bytes.AsSpan(0, 0x1B0);
        int count = Trim(prefix, binding.DirectionalShaderCapacity);
        OriginalDeferredSceneInputs scene = input.AdditionalSceneInputs with
        {
            Lighting = OriginalDeferredSceneConstants.ReadPreparedBaseLighting(prefix, count),
            Camera = OriginalPreparedCameraSceneSource.ReadCompletePrefix(camera),
            Ambient = input.SelectedBuiltinAmbientCameraRate is float rate
                ? OriginalDeferredAmbientSource.Prepare(source.Ambient, rate) : input.AdditionalSceneInputs.Ambient,
            Fog = input.SelectedSharedFogOwnerEnabled is bool fogEnabled
                ? OriginalDeferredSceneConstants.ReadSharedFog(prefix, fogEnabled) : input.AdditionalSceneInputs.Fog
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
