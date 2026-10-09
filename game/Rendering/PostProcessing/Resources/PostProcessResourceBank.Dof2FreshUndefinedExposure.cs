using Godot;
using San14.Data;

/// <summary>Exact fresh source55/56 consumer of unspecified original owned61.
/// Deliberately not a completed mip lease. No upload/clear/ready publication.</summary>
public sealed class MapPostEffect3DepthOfField2FreshUndefinedExposureRead:IMapPostEffect3ComputeTextureLease
{
    internal MapPostEffect3DepthOfField2FreshUndefinedExposureRead(PostProcessResourceBank bank,
        MapPostEffect3DepthOfField2FreshUndefinedExposureSource source,MapPostEffect3DeviceTexture texture)
        =>(Bank,Source,Texture)=(bank,source,texture);
    internal PostProcessResourceBank Bank{get;}
    internal MapPostEffect3DeviceTexture Texture{get;}
    public MapPostEffect3DepthOfField2FreshUndefinedExposureSource Source{get;}
    public long FrameNumber=>Source.FrameNumber;
    public string ContextOwnerIdentity=>Source.ContextOwnerIdentity;
    public int ParameterIndex=>61;
    public int InstanceIndex=>0;
    public Rid StorageRid=>Texture.StorageRid;
    public Rid ShaderViewRid=>Texture.NativeFormatViews[41];
    public uint NativeViewFormat=>41;
    public RenderingDevice.DataFormat DeviceFormat=>RenderingDevice.DataFormat.R32Sfloat;
    public Vector3I Extent=>new(2,1,1);
    public bool InitialPixelValuesAreKnown=>false;
    public bool EstablishesPriorPixelReadiness=>false;
    public bool ProvesNativeInitialBytesEqualBackendBytes=>false;
    public string SourceEvidence=>Source.SourceEvidence+";actual-same-bank-owned61-source-undefined-SRV/texel0-only";
    public void RequireCurrent()=>Bank.RequireDepthOfField2FreshUndefinedExposureCurrent(this);
    internal void RequireConsumer(MapPostEffect3DepthOfField2GraphicsSource proof,int pass,uint register,
        string shader,IReadOnlyList<MapPostEffect3ComputeConstantBufferReadReceipt> cbs)
    {
        RequireCurrent();
        Source.RequireConsumer(proof,pass,register,shader,cbs.Where(c=>c.HasCompleteWriterContents)
            .ToDictionary(c=>c.ParameterIndex,c=>c.CompleteBytesSha256!));
    }
}

public sealed partial class PostProcessResourceBank
{
    public MapPostEffect3DepthOfField2FreshUndefinedExposureRead CaptureDepthOfField2FreshUndefinedExposureRead(
        MapPostEffect3TextureParameterState actualTable,
        MapPostEffect3DepthOfField2FreshUndefinedExposureSource source)
    {
        ArgumentNullException.ThrowIfNull(source);source.RequireCurrentTextureTable(actualTable);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            var result=new MapPostEffect3DepthOfField2FreshUndefinedExposureRead(this,source,
                Require(Textures,61,0,"fresh DOF source-undefined exposure"));
            RequireDepthOfField2FreshUndefinedExposureCurrent(result);return result;
        }
    }
    internal void RequireDepthOfField2FreshUndefinedExposureCurrent(MapPostEffect3DepthOfField2FreshUndefinedExposureRead read)
    {
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);read.Source.RequireCurrent();
            var texture=Require(Textures,61,0,"fresh DOF source-undefined exposure");
            if(!ReferenceEquals(read.Bank,this)||!ReferenceEquals(read.Source.Allocation,Source)||
                !ReferenceEquals(read.Texture,texture)||Textures[61].Count!=1||texture.Source.InstanceCount!=1||
                texture.InstanceIndex!=0||texture.StorageFormat!=RenderingDevice.DataFormat.R32Sfloat||
                texture.Shape.Dimension!=MapPostEffect3TextureDimension.Texture2D||
                texture.Shape.Width!=2||texture.Shape.Height!=1||texture.Shape.Depth!=1||texture.Shape.MipLevels!=1||
                texture.Shape.Formats.ShaderResourceFormat0!=41||!texture.NativeFormatViews.ContainsKey(41)||
                textureContents.Keys.Any(k=>k.Parameter==61))
                throw new InvalidDataException("Fresh DOF exposure needs exact original unwritten61 allocation; a prior writer needs real retained-history admission.");
        }
    }
}
