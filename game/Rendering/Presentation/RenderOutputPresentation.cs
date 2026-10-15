using Godot;
using San14.Data;

/// <summary>RD color output exposed to Godot without CPU readback. Shared SRV
/// views require a byte-preserving GPU copy to an ordinary texture because
/// Texture2Drd cannot wrap them. The frame must outlive this wrapper. This seam
/// not a recovered tone/output conversion or a model-depth compositor.</summary>
public sealed class RenderOutputPresentation : IAsyncDisposable
{
    public RenderTargetDeviceTexture Source { get; }
    public Texture2Drd Texture { get; }
    public Rid PresentedRid { get; }
    public bool UsesGpuCopy { get; }
    private readonly RenderDeviceResources resources;
    private readonly object lifetimeGate=new();
    private volatile bool retiring;
    private int retirementStage;
    private Task? _disposal;
    private RenderOutputPresentation(RenderTargetDeviceTexture source,Rid rid,bool copy,RenderDeviceResources owned)
    {
        (Source,PresentedRid,UsesGpuCopy,resources)=(source,rid,copy,owned);
        Texture=new Texture2Drd();
    }

    public static async Task<RenderOutputPresentation> BuildAsync(RenderTargetDeviceTexture source,
        bool forceGpuCopy = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.EffectiveSource.Kind!=RenderTextureKind.Texture2D || source.ExplicitExtent.X<=0 || source.ExplicitExtent.Y<=0)
            throw new InvalidDataException("Presentation requires an explicit ordinary 2D color output.");
        bool copy=forceGpuCopy || source.ShaderViewRid!=source.StorageRid;
        var resources=new RenderDeviceResources("Presentation image allocation failed.");
        Rid allocatedCopy=default;
        RenderOutputPresentation? owner=null;
        try
        {
            Rid presented=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
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
                using var view=new RDTextureView();
                allocatedCopy=resources.Own(rd.TextureCreate(copyFormat,view));
                return allocatedCopy;
            });
            owner=new(source,presented,copy,resources);
            owner.Texture.TextureRdRid=presented;
            bool attached=await TerrainDeviceTexture.OnRenderThreadAsync(_=>owner.Texture.TextureRdRid==presented);
            if(!attached||owner.Texture.GetWidth()!=source.ExplicitExtent.X||owner.Texture.GetHeight()!=source.ExplicitExtent.Y)
                throw new InvalidDataException("RenderingServer did not attach the native output shape.");
            return owner;
        }
        catch(Exception failure)
        {
            IAsyncDisposable[] completed=owner is not null?[owner]:allocatedCopy.IsValid?[resources]:[];
            var pending=NativeMapBorrowerRetirementPackage.CreatePending("Render-output-presentation-partial",completed,
                (failure as NativeMapBorrowerConstructionException)?.RetainedPackage);
            try{await NativeMapBorrowerRetirementPackage.DrainPendingAsync(pending);}
            catch(Exception retirementFailure)
            {throw new NativeMapBorrowerConstructionException(failure,retirementFailure,pending!);}
            throw;
        }
    }

    public Task RefreshAsync()
    {
        ObjectDisposedException.ThrowIf(retiring,this);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(!rd.TextureIsValid(Source.ShaderViewRid)||!rd.TextureIsValid(PresentedRid))
                throw new InvalidDataException("Presentation output or image is no longer live.");
            if(UsesGpuCopy && rd.TextureCopy(Source.ShaderViewRid,PresentedRid,Vector3.Zero,Vector3.Zero,
                new Vector3(Source.ExplicitExtent.X,Source.ExplicitExtent.Y,1),0,0,0,0)!=Error.Ok)
                throw new InvalidDataException("Device rejected the source-view presentation copy.");
            return true;
        });
    }

    public ValueTask DisposeAsync()
    {
        lock(lifetimeGate)
        {
            retiring=true;
            return new(_disposal is null||_disposal.IsFaulted||_disposal.IsCanceled?_disposal=DetachAsync():_disposal);
        }
    }
    private async Task DetachAsync()
    {
        if(retirementStage==0)
        {
            Texture.TextureRdRid=default;
            await TerrainDeviceTexture.OnRenderThreadAsync(_=>true);
            retirementStage=1;
        }
        if(retirementStage==1)
        {
            if(UsesGpuCopy)await resources.DisposeAsync();
            retirementStage=2;
        }
        if(retirementStage==2)
        {
            Texture.Dispose();
            retirementStage=3;
        }
    }
}
