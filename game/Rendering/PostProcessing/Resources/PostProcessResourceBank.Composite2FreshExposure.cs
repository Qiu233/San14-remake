using Godot;
using San14.Data;

public interface IMapPostEffect3Composite2FreshExposureInvocationLease
{
    bool RequiresFreshUndefinedExposure(MapPostEffect3Composite2BeforeBindings before);
    IMapPostEffect3ComputeTextureLease CaptureFreshUndefinedExposure(
        MapPostEffect3Composite2BeforeBindings before,MapPostEffect3Composite2ReadFootprint proof);
}
public sealed class MapPostEffect3Composite2FreshUndefinedExposureRead:IMapPostEffect3SourceTextureReadFootprintLease
    //Closed fresh contract remains distinct from prior104 history below.
{
    internal MapPostEffect3Composite2FreshUndefinedExposureRead(PostProcessResourceBank bank,
        MapPostEffect3Composite2AddBloomExposureSource source,MapPostEffect3DeviceTexture texture)
        =>(Bank,Source,Texture)=(bank,source,texture);
    internal PostProcessResourceBank Bank{get;} internal MapPostEffect3DeviceTexture Texture{get;}
    public MapPostEffect3Composite2AddBloomExposureSource Source{get;}
    public long FrameNumber=>Source.FrameNumber;public string ContextOwnerIdentity=>Source.ContextOwnerIdentity;
    public int ParameterIndex=>61;public int InstanceIndex=>0;public Rid StorageRid=>Texture.StorageRid;
    public Rid ShaderViewRid=>Texture.NativeFormatViews[41];public uint NativeViewFormat=>41;
    public RenderingDevice.DataFormat DeviceFormat=>RenderingDevice.DataFormat.R32Sfloat;public Vector3I Extent=>new(2,1,1);
    public string SourceEvidence=>Source.SourceEvidence+";actual-same-bank-owned61/source100/101-texel1-only-SRV";
    public void RequireCurrent()=>Bank.RequireComposite2FreshExposureCurrent(this);
    public void RequireReadableSourceFootprint(int pass,uint register,string shader,IReadOnlyList<MapPostEffect3ComputeConstantBufferReadReceipt> cbs)
    {RequireCurrent();Source.RequireConsumer(pass,register,shader,cbs.Where(c=>c.HasCompleteWriterContents).ToDictionary(c=>c.ParameterIndex,c=>c.CompleteBytesSha256!));}
}
public interface IMapPostEffect3Composite2RetainedExposureInvocationLease
{
    bool RequiresRetainedExposure(MapPostEffect3Composite2BeforeBindings before);
    IMapPostEffect3ComputeMipTextureLease CaptureRetainedExposure(
        MapPostEffect3Composite2BeforeBindings before,MapPostEffect3Composite2ReadFootprint proof);
}
public sealed partial class PostProcessResourceBank
{
    public MapPostEffect3Composite2FreshUndefinedExposureRead CaptureComposite2FreshUndefinedExposure(
        MapPostEffect3TextureParameterState table,MapPostEffect3Composite2AddBloomExposureSource source)
    {
        source.RequireCurrentTextureTable(table);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            var result=new MapPostEffect3Composite2FreshUndefinedExposureRead(this,source,Require(Textures,61,0,"cold AddBloom exposure"));
            RequireComposite2FreshExposureCurrent(result);return result;
        }
    }
    internal void RequireComposite2FreshExposureCurrent(MapPostEffect3Composite2FreshUndefinedExposureRead read)
    {
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);read.Source.RequireCurrent();var texture=Require(Textures,61,0,"cold AddBloom exposure");
            if(!ReferenceEquals(read.Bank,this)||!ReferenceEquals(read.Source.Allocation,Source)||!ReferenceEquals(read.Texture,texture)||
                Textures[61].Count!=1||texture.Source.InstanceCount!=1||texture.InstanceIndex!=0||
                texture.StorageFormat!=RenderingDevice.DataFormat.R32Sfloat||texture.Shape.Formats.ShaderResourceFormat0!=41||
                texture.Shape.Dimension!=MapPostEffect3TextureDimension.Texture2D||texture.Shape.Width!=2||texture.Shape.Height!=1||
                texture.Shape.Depth!=1||texture.Shape.MipLevels!=1||textureContents.Keys.Any(k=>k.Parameter==61))
                throw new InvalidDataException("Cold AddBloom exposure cannot replace an actual written or retained61 generation.");
        }
    }
}
