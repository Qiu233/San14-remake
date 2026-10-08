using Godot;
using San14.Data;

/// <summary>RD color output exposed to Godot without CPU readback. Shared SRV
/// views require a byte-preserving GPU copy to an ordinary texture because
/// Texture2Drd cannot wrap them. The frame must outlive this wrapper. This seam
/// not a recovered tone/output conversion or a model-depth compositor.</summary>
public sealed class OriginalRenderOutputPresentation : IAsyncDisposable
{
    public OriginalRenderTargetDeviceTexture Source { get; }
    public Texture2Drd Texture { get; }
    public Rid PresentedRid { get; }
    public bool UsesGpuCopy { get; }
    private Task? _disposal;
    private OriginalRenderOutputPresentation(OriginalRenderTargetDeviceTexture source,Texture2Drd texture,Rid rid,bool copy)
        =>(Source,Texture,PresentedRid,UsesGpuCopy)=(source,texture,rid,copy);

    public static async Task<OriginalRenderOutputPresentation> BuildAsync(OriginalRenderTargetDeviceTexture source,
        bool forceGpuCopy = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.EffectiveSource.Kind!=OriginalRenderTextureKind.Texture2D || source.ExplicitExtent.X<=0 || source.ExplicitExtent.Y<=0)
            throw new InvalidDataException("Presentation requires an explicit ordinary 2D color output.");
        bool copy=forceGpuCopy || source.ShaderViewRid!=source.StorageRid;
        Rid presented=await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            if(!rd.TextureIsValid(source.StorageRid)||!rd.TextureIsValid(source.ShaderViewRid))
                throw new InvalidDataException("Presentation output or parent is no longer live.");
            using var format=rd.TextureGetFormat(source.ShaderViewRid);
            // Texture2Drd's image-format bridge cannot wrap packed R11G11B10.
            // A raw scene target remains valid for RD shaders; present the
            // completed source final output instead of attaching that target.
            if(format.Format==RenderingDevice.DataFormat.B10G11R11UfloatPack32)
                throw new NotSupportedException("Packed native107 scene storage requires a separate supported final presentation.");
            if(format.TextureType!=RenderingDevice.TextureType.Type2D || format.Width!=source.ExplicitExtent.X ||
                format.Height!=source.ExplicitExtent.Y || format.ArrayLayers!=1 ||
                !format.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit) ||
                format.Format.ToString().StartsWith("D",StringComparison.Ordinal))
                throw new InvalidDataException("Presentation output does not satisfy the source color shape.");
            if(!copy)return source.StorageRid;
            if(!format.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.CanCopyFromBit))
                throw new InvalidDataException("Presentation snapshot requires explicit GPU-copy permission on the source.");
            using var copyFormat=new RDTextureFormat{Format=source.ShaderFormat,Width=(uint)source.ExplicitExtent.X,
                Height=(uint)source.ExplicitExtent.Y,Depth=1,ArrayLayers=1,Mipmaps=1,TextureType=RenderingDevice.TextureType.Type2D,
                UsageBits=RenderingDevice.TextureUsageBits.SamplingBit|RenderingDevice.TextureUsageBits.CanCopyToBit|RenderingDevice.TextureUsageBits.CanCopyFromBit};
            using var view=new RDTextureView();Rid rid=rd.TextureCreate(copyFormat,view);
            if(!rid.IsValid)throw new InvalidDataException("Presentation image allocation failed.");
            return rid;
        });
        var texture=new Texture2Drd();
        try
        {
            texture.TextureRdRid=presented;
            bool attached=await OriginalTerrainDeviceTexture.OnRenderThreadAsync(_=>texture.TextureRdRid==presented);
            if(!attached||texture.GetWidth()!=source.ExplicitExtent.X||texture.GetHeight()!=source.ExplicitExtent.Y)
                throw new InvalidDataException("RenderingServer did not attach the native output shape.");
            return new(source,texture,presented,copy);
        }
        catch
        {
            try
            {
                texture.TextureRdRid=default;
                await OriginalTerrainDeviceTexture.OnRenderThreadAsync(_=>true);
            }
            finally
            {
                try { if(copy)await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(presented);return true;}); }
                finally { texture.Dispose(); }
            }
            throw;
        }
    }

    public Task RefreshAsync()
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            ObjectDisposedException.ThrowIf(_disposal is not null,this);
            if(!rd.TextureIsValid(Source.ShaderViewRid)||!rd.TextureIsValid(PresentedRid))
                throw new InvalidDataException("Presentation output or image is no longer live.");
            if(UsesGpuCopy && rd.TextureCopy(Source.ShaderViewRid,PresentedRid,Vector3.Zero,Vector3.Zero,
                new Vector3(Source.ExplicitExtent.X,Source.ExplicitExtent.Y,1),0,0,0,0)!=Error.Ok)
                throw new InvalidDataException("Device rejected the source-view presentation copy.");
            return true;
        });
    }

    public ValueTask DisposeAsync()=>new(_disposal??=DetachAsync());
    private async Task DetachAsync()
    {
        try
        {
            Texture.TextureRdRid=default;
            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(_=>true);
        }
        finally
        {
            try { if(UsesGpuCopy)await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(PresentedRid);return true;}); }
            finally { Texture.Dispose(); }
        }
    }
}
