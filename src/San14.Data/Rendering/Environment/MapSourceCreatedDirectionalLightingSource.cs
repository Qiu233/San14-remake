using System.Security.Cryptography;
using System.Text;

namespace San14.Data;

/// <summary>One actual seasonal Directional and the independent Main Camera
/// declaration. This source owns neither a provider selection nor a GPU resource
/// lifetime; those belong to the world and resource owners respectively.</summary>
public sealed class MapSourceCreatedDirectionalLightingSource
{
    private MapSourceCreatedDirectionalLightingSource(
        LoadedMapCameraConstructionSource camera, DirectionalLightConstructionPacket construction)
    {
        if (camera.Camera.ObjectId != MainMapCameraAdapter.MainCameraObjectId)
            throw new InvalidDataException("Seasonal world lighting requires the actual Main38D Camera declaration.");
        CameraConstructionSource = MapCameraConstructionSource.ReadPerspective(camera.Resource, camera.Camera.ObjectId);
        var lighting = PreparedCameraLightingSource.ReadPerspective(CameraConstructionSource.Resource, camera.Camera.ObjectId);
        Controller = lighting.Controller;
        InitialSkylightParameters = lighting.Source.Parameters;
        Construction = construction;
        ConstructionProvenance = DescribeConstruction(construction);
        Identity = CreateIdentity();
    }

    public MapSourceCreatedLightingIdentity Identity { get; }
    public LoadedMapCameraConstructionSource CameraConstructionSource { get; }
    public CameraLightingController Controller { get; }
    public SkylightParameters InitialSkylightParameters { get; }
    public DirectionalLightConstructionPacket Construction { get; }
    public string ConstructionProvenance { get; }
    public ReadOnlyMemory<byte> CreatedLightData => Construction.Bytes;

    public static MapSourceCreatedDirectionalLightingSource FromMainCamera(
        LoadedMapCameraConstructionSource camera, DirectionalLightConstructionPacket construction)
    {
        ArgumentNullException.ThrowIfNull(camera); ArgumentNullException.ThrowIfNull(construction);
        return new(camera, construction);
    }

    public MapSourceCreatedLightingPacket PrepareFrame(MapSourceCreatedLightingFrameInputs input)
        =>PrepareFrameCore(input,null);

    public MapSourceCreatedLightingPacket PrepareFrame(MapSourceCreatedLightingFrameInputs input,
        DirectionalProviderCallerCurrentFrame providerFrame)
        =>PrepareFrameCore(input,providerFrame??throw new ArgumentNullException(nameof(providerFrame)));

    private MapSourceCreatedLightingPacket PrepareFrameCore(MapSourceCreatedLightingFrameInputs input,
        DirectionalProviderCallerCurrentFrame? providerFrame)
    {
        ArgumentNullException.ThrowIfNull(input);
        var main = input.PresentSeasonCamera?.AcceptedSeasonCamera.Inputs.MainFrame.Camera.Source;
        if (main is null || main.Camera.ObjectId != Identity.CameraObjectId ||
            main.Resource.Source.ResourceUri != Identity.CameraResourceUri ||
            main.Resource.Source.DecodedSha256 != Identity.CameraSha256)
            throw new InvalidDataException("Directional source needs its own same-frame Main Camera declaration.");
        return providerFrame is null?MapSourceCreatedLighting.PrepareDirectional(this,input):
            MapSourceCreatedLighting.PrepareDirectional(this,input,providerFrame);
    }

    internal static string DescribeConstruction(DirectionalLightConstructionPacket source) =>
        $"{source.SourceUri}#{source.SourceObjectId:X8}@{source.SourceSha256};" +
        $"Directional-CBFAB0/CB65D0/CB9CC0;retained={source.RetainedTemplateProvenance};" +
        $"constructor={source.ConstructorSha256};prepared={source.PreparedSha256};" +
        string.Join(';', source.ResourceBindings.Select(b =>
            $"resource={b.SourceObjectKey:X8}:{b.DataHandle:X16}:{b.OwnerProvenance}"));

    private MapSourceCreatedLightingIdentity CreateIdentity()
    {
        var camera = CameraConstructionSource; var source = Construction;
        return new(0, DefaultView0Source.ViewParameterObjectId,
            camera.Resource.Source.ResourceUri, camera.Resource.Source.DecodedSha256, camera.Camera.ObjectId,
            source.SourceUri, source.SourceSha256, source.SourceObjectId, source.Metadata.SourceGroupMask,
            0, source.PreparedSha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ConstructionProvenance))));
    }
}
