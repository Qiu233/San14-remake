using Godot;
using San14.Data;

public interface IOriginalMapPostEffect3Composite2FreshExposureInvocationLease
{
    bool RequiresFreshUndefinedExposure(OriginalMapPostEffect3Composite2BeforeBindings before);
    IOriginalMapPostEffect3ComputeTextureLease CaptureFreshUndefinedExposure(
        OriginalMapPostEffect3Composite2BeforeBindings before,OriginalMapPostEffect3Composite2ReadFootprint proof);
}
public sealed class OriginalMapPostEffect3Composite2FreshUndefinedExposureRead:IOriginalMapPostEffect3SourceTextureReadFootprintLease
    //Closed fresh contract remains distinct from prior104 history below.
{
    internal OriginalMapPostEffect3Composite2FreshUndefinedExposureRead(OriginalMapPostEffect3ResourceBank bank,
        OriginalMapPostEffect3Composite2AddBloomExposureSource source,OriginalMapPostEffect3DeviceTexture texture)
        =>(Bank,Source,Texture)=(bank,source,texture);
    internal OriginalMapPostEffect3ResourceBank Bank{get;} internal OriginalMapPostEffect3DeviceTexture Texture{get;}
    public OriginalMapPostEffect3Composite2AddBloomExposureSource Source{get;}
    public long FrameNumber=>Source.FrameNumber;public string ContextOwnerIdentity=>Source.ContextOwnerIdentity;
    public int ParameterIndex=>61;public int InstanceIndex=>0;public Rid StorageRid=>Texture.StorageRid;
    public Rid ShaderViewRid=>Texture.NativeFormatViews[41];public uint NativeViewFormat=>41;
    public RenderingDevice.DataFormat DeviceFormat=>RenderingDevice.DataFormat.R32Sfloat;public Vector3I Extent=>new(2,1,1);
    public string SourceEvidence=>Source.SourceEvidence+";actual-same-bank-owned61/source100/101-texel1-only-SRV";
    public void RequireCurrent()=>Bank.RequireComposite2FreshExposureCurrent(this);
    public void RequireReadableSourceFootprint(int pass,uint register,string shader,IReadOnlyList<OriginalMapPostEffect3ComputeConstantBufferReadReceipt> cbs)
    {RequireCurrent();Source.RequireConsumer(pass,register,shader,cbs.Where(c=>c.HasCompleteWriterContents).ToDictionary(c=>c.ParameterIndex,c=>c.CompleteBytesSha256!));}
}
public interface IOriginalMapPostEffect3Composite2RetainedExposureInvocationLease
{
    bool RequiresRetainedExposure(OriginalMapPostEffect3Composite2BeforeBindings before);
    IOriginalMapPostEffect3ComputeMipTextureLease CaptureRetainedExposure(
        OriginalMapPostEffect3Composite2BeforeBindings before,OriginalMapPostEffect3Composite2ReadFootprint proof);
}
public sealed partial class OriginalMapPostEffect3ResourceBank
{
    public OriginalMapPostEffect3Composite2FreshUndefinedExposureRead CaptureComposite2FreshUndefinedExposure(
        OriginalMapPostEffect3TextureParameterState table,OriginalMapPostEffect3Composite2AddBloomExposureSource source)
    {
        source.RequireCurrentTextureTable(table);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            var result=new OriginalMapPostEffect3Composite2FreshUndefinedExposureRead(this,source,Require(Textures,61,0,"cold AddBloom exposure"));
            RequireComposite2FreshExposureCurrent(result);return result;
        }
    }
    internal void RequireComposite2FreshExposureCurrent(OriginalMapPostEffect3Composite2FreshUndefinedExposureRead read)
    {
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);read.Source.RequireCurrent();var texture=Require(Textures,61,0,"cold AddBloom exposure");
            if(!ReferenceEquals(read.Bank,this)||!ReferenceEquals(read.Source.Allocation,Source)||!ReferenceEquals(read.Texture,texture)||
                Textures[61].Count!=1||texture.Source.InstanceCount!=1||texture.InstanceIndex!=0||
                texture.StorageFormat!=RenderingDevice.DataFormat.R32Sfloat||texture.Shape.Formats.ShaderResourceFormat0!=41||
                texture.Shape.Dimension!=OriginalMapPostEffect3TextureDimension.Texture2D||texture.Shape.Width!=2||texture.Shape.Height!=1||
                texture.Shape.Depth!=1||texture.Shape.MipLevels!=1||textureContents.Keys.Any(k=>k.Parameter==61))
                throw new InvalidDataException("Cold AddBloom exposure cannot replace an actual written or retained61 generation.");
        }
    }
}
