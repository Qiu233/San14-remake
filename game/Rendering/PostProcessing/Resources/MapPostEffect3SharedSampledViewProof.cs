using Godot;
using San14.Data;

/// <summary>Effective view format backed by a retained actual shared-view
/// creation. TextureGetFormat reports the parent image's format in Godot even
/// when its RDTextureView.FormatOverride selects the native sRGB shader view.</summary>
public interface IMapPostEffect3SharedSampledViewLease
{
    MapPostEffect3SharedSampledViewProof RequireSharedSampledViewProof();
}

public sealed class MapPostEffect3SharedSampledViewProof
{
    private readonly RenderTargetFrame frame;
    private readonly RenderTargetDeviceTexture texture;
    private readonly uint logicalId;
    private readonly Action requireOwnerCurrent;
    private MapPostEffect3SharedSampledViewProof(RenderTargetFrame frame,
        uint logicalId,RenderTargetDeviceTexture texture,Action requireOwnerCurrent)
        =>(this.frame,this.logicalId,this.texture,this.requireOwnerCurrent)=
            (frame,logicalId,texture,requireOwnerCurrent);
    public Rid StorageRid=>texture.StorageRid;
    public Rid ShaderViewRid=>texture.ShaderViewRid;
    public RenderingDevice.DataFormat StorageFormat=>texture.StorageFormat;
    public RenderingDevice.DataFormat EffectiveShaderFormat=>texture.ShaderFormat;
    public string SourceEvidence=>texture.EffectiveSource.SourceProvenance+
        ";actual-OriginalRenderTargetFrame.Build-TextureCreateShared-RDTextureView.FormatOverride;"+
        "Godot-TextureGetFormat-retains-parent-format;source-native-BGRA8-UNORM-storage-and-sRGB-SRV";

    internal static MapPostEffect3SharedSampledViewProof CaptureWorkingScene(
        RenderTargetFrame actualFrame,RenderTargetDeviceTexture actualTexture,
        Action requireCurrent)
    {
        ArgumentNullException.ThrowIfNull(actualFrame);ArgumentNullException.ThrowIfNull(actualTexture);
        ArgumentNullException.ThrowIfNull(requireCurrent);requireCurrent();
        const uint scene=0x064B746B;
        var source=RenderTextureFormatMath.Resolve(actualTexture.EffectiveSource.PixelFormat,
            actualTexture.EffectiveSource.ColorSpace,actualTexture.EffectiveSource.OutputColorSpace);
        if(!ReferenceEquals(actualFrame.Textures[scene],actualTexture)||
            actualFrame.Source.ColorTargets is not [{Texture.Object.ObjectId:scene}]||
            source.TargetViewFormat!=87||source.ShaderViewFormat!=91||
            actualTexture.StorageFormat!=RenderingDevice.DataFormat.B8G8R8A8Unorm||
            actualTexture.TargetFormat!=actualTexture.StorageFormat||
            actualTexture.ShaderFormat!=RenderingDevice.DataFormat.B8G8R8A8Srgb||
            actualTexture.StorageRid==actualTexture.ShaderViewRid||
            !actualTexture.StorageRid.IsValid||!actualTexture.ShaderViewRid.IsValid)
            throw new NotSupportedException("Shared sampled view proof requires the actual source working Scene BGRA8 UNORM/sRGB creation.");
        return new(actualFrame,scene,actualTexture,requireCurrent);
    }
    /// <summary>Called on the render thread with the actual queried shared
    /// view. All effective format/RID fields are joined to the retained real
    /// frame creation; mere format compatibility cannot grant this proof.</summary>
    public void RequireCurrentNativeView(IMapPostEffect3ComputeTextureLease actualLease,
        RenderingDevice device,RDTextureFormat actualQueriedView)
    {
        requireOwnerCurrent();actualLease.RequireCurrent();
        if(!ReferenceEquals(frame.Textures[logicalId],texture)||
            actualLease.StorageRid!=StorageRid||actualLease.ShaderViewRid!=ShaderViewRid||
            actualLease.DeviceFormat!=EffectiveShaderFormat||actualLease.NativeViewFormat!=91||
            actualLease.Extent!=new Vector3I(texture.ExplicitExtent.X,texture.ExplicitExtent.Y,1)||
            !device.TextureIsValid(StorageRid)||!device.TextureIsValid(ShaderViewRid))
            throw new InvalidDataException("Shared sampled view proof lost its exact actual frame/lease/view owner.");
        using var storage=device.TextureGetFormat(StorageRid);
        foreach(var shape in new[]{storage,actualQueriedView})
            if(shape.TextureType!=RenderingDevice.TextureType.Type2D||shape.ArrayLayers!=1||shape.Depth!=1||
                shape.Mipmaps!=1||shape.Width!=actualLease.Extent.X||shape.Height!=actualLease.Extent.Y||
                shape.Format!=StorageFormat||!shape.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit))
                throw new InvalidDataException("Actual shared sRGB view parent metadata differs from its retained native creation.");
    }
}
