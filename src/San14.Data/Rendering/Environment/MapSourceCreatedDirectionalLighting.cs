namespace San14.Data;

public sealed partial class MapSourceCreatedLightingPacket
{
    private readonly DirectionalLightConstructionPacket? directionalConstruction;
    private readonly DirectionalProviderCallerCurrentFrame? directionalProviderFrame;
    private readonly bool directionalCameraApplied, directionalRowsSwapped;

    internal MapSourceCreatedLightingPacket(DirectionalLightConstructionPacket source,
        MapSourceCreatedLightingFrameInputs input, byte[] camera,
        CameraDirectionalLightDataPacket local, DeferredSceneInputs scene,
        DirectionalProviderCallerCurrentFrame? providerFrame=null)
    {
        var present = input.PresentSeasonCamera!;
        var actualCamera = present.AcceptedSeasonCamera.Inputs.MainFrame.Camera;
        directionalConstruction = source;
        directionalProviderFrame=providerFrame;
        SourceLightClassId = DirectionalLightSource.SourceClass;
        FrameNumber = input.FrameNumber; Binding = input.Binding;
        string provenance = MapSourceCreatedDirectionalLightingSource.DescribeConstruction(source);
        SourceIdentity = MapSourceCreatedDirectionalLightingSource.CreateIdentity(actualCamera.Source, source);
        SourceProvenance = provenance + (providerFrame is null?"":";"+providerFrame.SourceProvenance) + $";View={Binding.ViewIdentity};CameraOwner={Binding.CameraOwnerIdentity};" +
            $"LightOwner={Binding.LightOwnerIdentity};frame={FrameNumber};" + present.SourceProvenance;
        this.camera = camera.ToArray(); lightData = local.Bytes.ToArray(); SceneInputs = scene;
        Optics = CameraOpticsEncoding.DecodePrefix(actualCamera.Source, camera);
        Clock = present.Clock; SelectedController = present.Controller;
        LightingSkylightParameters = input.LightingSkylightParameters; RuntimeTable = input.RuntimeTable;
        ContextFlags3E4 = input.ContextFlags3E4; CameraContextApplied = (input.ContextFlags3E4 & 3) == 1;
        directionalCameraApplied = local.Applied; directionalRowsSwapped = local.SourceHighBitSwappedRows;
        acceptedInputs = MapSourceCreatedLighting.SnapshotInputs(input);
    }

}

public static partial class MapSourceCreatedLighting
{
    /// <summary>Prepare one independently constructed Directional using the
    /// actual same-frame Main seasonal Camera. The packet identifies the exact
    /// source object; provider scheduling and current world publication remain
    /// with the owner that selected that object.</summary>
    public static MapSourceCreatedLightingPacket PrepareDirectional(
        DirectionalLightConstructionPacket source, MapSourceCreatedLightingFrameInputs input)
        =>PrepareDirectionalCore(source,input,null);

    public static MapSourceCreatedLightingPacket PrepareDirectional(
        DirectionalLightConstructionPacket registeredRoot,MapSourceCreatedLightingFrameInputs input,
        DirectionalProviderCallerCurrentFrame providerFrame)
        =>PrepareDirectionalCore(registeredRoot,input,providerFrame??throw new ArgumentNullException(nameof(providerFrame)));

    private static MapSourceCreatedLightingPacket PrepareDirectionalCore(
        DirectionalLightConstructionPacket source,MapSourceCreatedLightingFrameInputs input,
        DirectionalProviderCallerCurrentFrame? providerFrame)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Binding); ArgumentNullException.ThrowIfNull(input.AdditionalSceneInputs);
        var binding = input.Binding;
        if (input.FrameNumber <= 0 || new[] { binding.ViewIdentity, binding.CameraOwnerIdentity,
            binding.LightOwnerIdentity, binding.DeferredShaderIdentity }.Any(string.IsNullOrWhiteSpace) ||
            binding.DirectionalShaderCapacity is < 1 or > 4 || binding.DeferredCombination < 0 ||
            binding.BasePath != MapLightingBasePath.UnmodifiedOrdinary ||
            input.RuntimeTable != MapLightingRuntimeTable.Present || input.PresentSeasonCamera is not { } present ||
            input.CameraScope != MapLightingCameraScope.LightingPrefix || source.ContextFlags3E4 != input.ContextFlags3E4)
            throw new InvalidDataException("Directional lighting requires its actual complete constructor and same-frame seasonal Main Camera inputs.");
        present.ValidateInputs(input);
        if (input.AdditionalSceneInputs.Lighting is not null || input.AdditionalSceneInputs.Camera is not null ||
            input.SelectedBuiltinAmbientCameraRate is not null && input.AdditionalSceneInputs.Ambient is not null ||
            input.SelectedSharedFogOwnerEnabled is not null && input.AdditionalSceneInputs.Fog is not null)
            throw new InvalidDataException("Directional lighting has conflicting current scene writers.");
        byte[] camera = present.PreparedCameraPrefix.ToArray();
        CameraDirectionalLightDataPacket local;
        if(providerFrame is not null)
        {
            if(!ReferenceEquals(providerFrame.RegisteredRoot,source)||providerFrame.Caller.FrameNumber!=input.FrameNumber)
                throw new InvalidDataException("Directional provider frame does not belong to this physical root and accepted frame.");
            providerFrame.Caller.RequireSameMain(present.AcceptedSeasonCamera.Inputs.MainFrame);
            providerFrame.RequireCamera(camera);
            local=new(providerFrame.Bytes.ToArray(),providerFrame.Applied,providerFrame.SourceHighBitSwappedRows);
        }
        else local=ApplyDirectionalCamera(source, camera, input.ContextFlags3E4);
        var prefix = local.Bytes.AsSpan(0, 0x1B0);
        var scene = Snapshot(input.AdditionalSceneInputs) with
        {
            Lighting = DeferredSceneConstants.ReadPreparedBaseLighting(prefix, Trim(prefix, binding.DirectionalShaderCapacity)),
            Camera = PreparedCameraSceneSource.ReadCompletePrefix(camera),
            Ambient = input.SelectedBuiltinAmbientCameraRate is float rate
                ? DeferredSceneConstants.ReadAmbientModifier(local.Bytes.AsSpan(0x1B0, 0x178), rate)
                : input.AdditionalSceneInputs.Ambient,
            Fog = input.SelectedSharedFogOwnerEnabled is bool enabled
                ? DeferredSceneConstants.ReadSharedFog(prefix, enabled) : input.AdditionalSceneInputs.Fog
        };
        return new(source, input, camera, local, scene,providerFrame);
    }

    internal static CameraDirectionalLightDataPacket ApplyDirectionalCamera(
        DirectionalLightConstructionPacket source, ReadOnlySpan<byte> camera, uint flags)
    {
        if (source.Bytes.Span[0x10B0] == 2)
        {
            var local = MapSourceCreatedCameraLightData.ApplySourceMode2PreparedCamera(source.Bytes.Span, camera, flags);
            return new(local.Bytes, false, false);
        }
        return CameraDirectionalLightData.Apply(source.Bytes.Span, camera, flags);
    }
}
