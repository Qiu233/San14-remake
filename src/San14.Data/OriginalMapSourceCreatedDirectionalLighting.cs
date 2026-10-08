namespace San14.Data;

public sealed partial class OriginalMapSourceCreatedLightingPacket
{
    private readonly OriginalDirectionalLightConstructionPacket? directionalConstruction;
    private readonly OriginalDirectionalProviderCallerCurrentFrame? directionalProviderFrame;
    private readonly bool directionalCameraApplied, directionalRowsSwapped;

    internal OriginalMapSourceCreatedLightingPacket(OriginalDirectionalLightConstructionPacket source,
        OriginalMapSourceCreatedLightingFrameInputs input, byte[] camera,
        OriginalCameraDirectionalLightDataPacket local, OriginalDeferredSceneInputs scene,
        OriginalDirectionalProviderCallerCurrentFrame? providerFrame=null)
    {
        var present = input.PresentSeasonCamera!;
        var actualCamera = present.AcceptedSeasonCamera.Inputs.MainFrame.Camera;
        directionalConstruction = source; SourceLightClassId = OriginalDirectionalLightSource.SourceClass;
        directionalProviderFrame=providerFrame;
        constructedLightData = source.Bytes.ToArray();
        FrameNumber = input.FrameNumber; Binding = input.Binding;
        string provenance = OriginalMapSourceCreatedDirectionalLightingSource.DescribeConstruction(source);
        SourceIdentity = OriginalMapSourceCreatedDirectionalLightingSource.CreateIdentity(actualCamera.Source, source);
        SourceProvenance = provenance + (providerFrame is null?"":";"+providerFrame.SourceProvenance) + $";View={Binding.ViewIdentity};CameraOwner={Binding.CameraOwnerIdentity};" +
            $"LightOwner={Binding.LightOwnerIdentity};frame={FrameNumber};" + present.SourceProvenance;
        this.camera = camera.ToArray(); lightData = local.Bytes.ToArray(); SceneInputs = scene;
        Clock = present.Clock; SelectedController = present.Controller;
        LightingSkylightParameters = input.LightingSkylightParameters; RuntimeTable = input.RuntimeTable;
        ContextFlags3E4 = input.ContextFlags3E4; CameraContextApplied = (input.ContextFlags3E4 & 3) == 1;
        directionalCameraApplied = local.Applied; directionalRowsSwapped = local.SourceHighBitSwappedRows;
        acceptedInputs = OriginalMapSourceCreatedLighting.SnapshotInputs(input);
    }

    private void ValidateDirectionalCurrentSourceFrame()
    {
        if(directionalProviderFrame is { } provider)
        {
            if(!ReferenceEquals(provider.RegisteredRoot,directionalConstruction))
                throw new InvalidDataException("Directional provider current frame has another physical root constructor.");
            provider.RequireCamera(camera);
            provider.Caller.RequireSameMain(acceptedInputs.PresentSeasonCamera!.AcceptedSeasonCamera.Inputs.MainFrame);
        }
    }
}

public static partial class OriginalMapSourceCreatedLighting
{
    /// <summary>Prepare one independently constructed Directional using the
    /// actual same-frame Main seasonal Camera. The packet identifies the exact
    /// source object; provider scheduling and current world publication remain
    /// with the owner that selected that object.</summary>
    public static OriginalMapSourceCreatedLightingPacket PrepareDirectional(
        OriginalDirectionalLightConstructionPacket source, OriginalMapSourceCreatedLightingFrameInputs input)
        =>PrepareDirectionalCore(source,input,null);

    public static OriginalMapSourceCreatedLightingPacket PrepareDirectional(
        OriginalDirectionalLightConstructionPacket registeredRoot,OriginalMapSourceCreatedLightingFrameInputs input,
        OriginalDirectionalProviderCallerCurrentFrame providerFrame)
        =>PrepareDirectionalCore(registeredRoot,input,providerFrame??throw new ArgumentNullException(nameof(providerFrame)));

    private static OriginalMapSourceCreatedLightingPacket PrepareDirectionalCore(
        OriginalDirectionalLightConstructionPacket source,OriginalMapSourceCreatedLightingFrameInputs input,
        OriginalDirectionalProviderCallerCurrentFrame? providerFrame)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Binding); ArgumentNullException.ThrowIfNull(input.AdditionalSceneInputs);
        var binding = input.Binding;
        if (input.FrameNumber <= 0 || new[] { binding.ViewIdentity, binding.CameraOwnerIdentity,
            binding.LightOwnerIdentity, binding.DeferredShaderIdentity }.Any(string.IsNullOrWhiteSpace) ||
            binding.DirectionalShaderCapacity is < 1 or > 4 || binding.DeferredCombination < 0 ||
            binding.BasePath != OriginalMapLightingBasePath.UnmodifiedOrdinary ||
            input.RuntimeTable != OriginalMapLightingRuntimeTable.Present || input.PresentSeasonCamera is not { } present ||
            input.CameraScope != OriginalMapLightingCameraScope.LightingPrefix || source.ContextFlags3E4 != input.ContextFlags3E4)
            throw new InvalidDataException("Directional lighting requires its actual complete constructor and same-frame seasonal Main Camera inputs.");
        present.ValidateInputs(input);
        if (input.AdditionalSceneInputs.Lighting is not null || input.AdditionalSceneInputs.Camera is not null ||
            input.SelectedBuiltinAmbientCameraRate is not null && input.AdditionalSceneInputs.Ambient is not null ||
            input.SelectedSharedFogOwnerEnabled is not null && input.AdditionalSceneInputs.Fog is not null)
            throw new InvalidDataException("Directional lighting has conflicting current scene writers.");
        byte[] camera = present.PreparedCameraPrefix.ToArray();
        OriginalCameraDirectionalLightDataPacket local;
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
            Lighting = OriginalDeferredSceneConstants.ReadPreparedBaseLighting(prefix, Trim(prefix, binding.DirectionalShaderCapacity)),
            Camera = OriginalPreparedCameraSceneSource.ReadCompletePrefix(camera),
            Ambient = input.SelectedBuiltinAmbientCameraRate is float rate
                ? OriginalDeferredSceneConstants.ReadAmbientModifier(local.Bytes.AsSpan(0x1B0, 0x178), rate)
                : input.AdditionalSceneInputs.Ambient,
            Fog = input.SelectedSharedFogOwnerEnabled is bool enabled
                ? OriginalDeferredSceneConstants.ReadSharedFog(prefix, enabled) : input.AdditionalSceneInputs.Fog
        };
        return new(source, input, camera, local, scene,providerFrame);
    }

    internal static OriginalCameraDirectionalLightDataPacket ApplyDirectionalCamera(
        OriginalDirectionalLightConstructionPacket source, ReadOnlySpan<byte> camera, uint flags)
    {
        if (source.Bytes.Span[0x10B0] == 2)
        {
            var local = OriginalMapSourceCreatedCameraLightData.ApplySourceMode2PreparedCamera(source.Bytes.Span, camera, flags);
            return new(local.Bytes, false, false);
        }
        return OriginalCameraDirectionalLightData.Apply(source.Bytes.Span, camera, flags);
    }
}
