using System.Security.Cryptography;
using System.Text;

namespace San14.Data;

/// <summary>One actual seasonal Directional and the independent Main Camera
/// declaration. This source owns neither a provider selection nor a GPU resource
/// lifetime; those belong to the world and resource owners respectively.</summary>
public sealed class OriginalMapSourceCreatedDirectionalLightingSource
{
    private OriginalMapSourceCreatedDirectionalLightingSource(
        LoadedOriginalMapCameraConstructionSource camera, OriginalDirectionalLightConstructionPacket construction)
    {
        if (camera.Camera.ObjectId != OriginalMainMapCameraAdapter.MainCameraObjectId)
            throw new InvalidDataException("Seasonal world lighting requires the actual Main38D Camera declaration.");
        CameraConstructionSource = OriginalMapCameraConstructionSource.ReadPerspective(camera.Resource, camera.Camera.ObjectId);
        var lighting = OriginalPreparedCameraLightingSource.ReadPerspective(CameraConstructionSource.Resource, camera.Camera.ObjectId);
        Controller = lighting.Controller;
        InitialSkylightParameters = lighting.Source.Parameters;
        Construction = construction;
        ConstructionProvenance = DescribeConstruction(construction);
        Identity = CreateIdentity(CameraConstructionSource, construction);
    }

    public OriginalMapSourceCreatedLightingIdentity Identity { get; }
    public LoadedOriginalMapCameraConstructionSource CameraConstructionSource { get; }
    public OriginalCameraLightingController Controller { get; }
    public OriginalSkylightParameters InitialSkylightParameters { get; }
    public OriginalDirectionalLightConstructionPacket Construction { get; }
    public string ConstructionProvenance { get; }
    public ReadOnlyMemory<byte> CreatedLightData => Construction.Bytes;

    public static OriginalMapSourceCreatedDirectionalLightingSource FromMainCamera(
        LoadedOriginalMapCameraConstructionSource camera, OriginalDirectionalLightConstructionPacket construction)
    {
        ArgumentNullException.ThrowIfNull(camera); ArgumentNullException.ThrowIfNull(construction);
        return new(camera, construction);
    }

    public OriginalMapSourceCreatedLightingPacket PrepareFrame(OriginalMapSourceCreatedLightingFrameInputs input)
        =>PrepareFrameCore(input,null);

    public OriginalMapSourceCreatedLightingPacket PrepareFrame(OriginalMapSourceCreatedLightingFrameInputs input,
        OriginalDirectionalProviderCallerCurrentFrame providerFrame)
        =>PrepareFrameCore(input,providerFrame??throw new ArgumentNullException(nameof(providerFrame)));

    private OriginalMapSourceCreatedLightingPacket PrepareFrameCore(OriginalMapSourceCreatedLightingFrameInputs input,
        OriginalDirectionalProviderCallerCurrentFrame? providerFrame)
    {
        ArgumentNullException.ThrowIfNull(input);
        var main = input.PresentSeasonCamera?.AcceptedSeasonCamera.Inputs.MainFrame.Camera.Source;
        if (main is null || main.Camera.ObjectId != Identity.CameraObjectId ||
            main.Resource.Source.ResourceUri != Identity.CameraResourceUri ||
            main.Resource.Source.DecodedSha256 != Identity.CameraSha256)
            throw new InvalidDataException("Directional source needs its own same-frame Main Camera declaration.");
        var frame = providerFrame is null?OriginalMapSourceCreatedLighting.PrepareDirectional(Construction,input):
            OriginalMapSourceCreatedLighting.PrepareDirectional(Construction,input,providerFrame);
        if (frame.SourceIdentity != Identity)
            throw new InvalidDataException("Directional frame changed its source-created owner identity.");
        return frame;
    }

    internal static string DescribeConstruction(OriginalDirectionalLightConstructionPacket source) =>
        $"{source.SourceUri}#{source.SourceObjectId:X8}@{source.SourceSha256};" +
        $"Directional-CBFAB0/CB65D0/CB9CC0;retained={source.RetainedTemplateProvenance};" +
        $"constructor={source.ConstructorSha256};prepared={source.PreparedSha256};" +
        string.Join(';', source.ResourceBindings.Select(b =>
            $"resource={b.SourceObjectKey:X8}:{b.DataHandle:X16}:{b.OwnerProvenance}"));

    internal static OriginalMapSourceCreatedLightingIdentity CreateIdentity(
        LoadedOriginalMapCameraConstructionSource camera, OriginalDirectionalLightConstructionPacket source)
    {
        string provenance = DescribeConstruction(source);
        return new(0, OriginalDefaultView0Source.ViewParameterObjectId,
            camera.Resource.Source.ResourceUri, camera.Resource.Source.DecodedSha256, camera.Camera.ObjectId,
            source.SourceUri, source.SourceSha256, source.SourceObjectId, source.Metadata.SourceGroupMask,
            0, source.PreparedSha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(provenance))));
    }
}
