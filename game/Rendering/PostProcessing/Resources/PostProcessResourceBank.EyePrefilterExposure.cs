using Godot;
using San14.Data;

/// <summary>A closed PS34 texel0 source load. This is not a completed texture
/// lease. Auto first use preserves unspecified values rather than seeding61.</summary>
public sealed class MapPostEffect3EyePrefilterExposureRead:IMapPostEffect3SourceTextureReadFootprintLease
{
    internal MapPostEffect3EyePrefilterExposureRead(PostProcessResourceBank bank,
        MapPostEffect3EyePrefilterExposureSource source,MapPostEffect3DeviceTexture texture)
        =>(Bank,Source,Texture)=(bank,source,texture);
    internal PostProcessResourceBank Bank{get;}
    internal MapPostEffect3DeviceTexture Texture{get;}
    public MapPostEffect3EyePrefilterExposureSource Source{get;}
    public long FrameNumber=>Source.FrameNumber;public string ContextOwnerIdentity=>Source.ContextOwnerIdentity;
    public int ParameterIndex=>61;public int InstanceIndex=>0;public Rid StorageRid=>Texture.StorageRid;
    public Rid ShaderViewRid=>Texture.NativeFormatViews[41];public uint NativeViewFormat=>41;
    public RenderingDevice.DataFormat DeviceFormat=>RenderingDevice.DataFormat.R32Sfloat;public Vector3I Extent=>new(2,1,1);
    public string SourceEvidence=>Source.SourceEvidence+";actual-same-bank-owned61-SRV/source84-footprint-only";
    public void RequireCurrent()=>Bank.RequireEyePrefilterExposureCurrent(this);
    public void RequireReadableSourceFootprint(int pass,uint register,string shader,
        IReadOnlyList<MapPostEffect3ComputeConstantBufferReadReceipt> cbs)
    {RequireCurrent();Source.RequireConsumer(pass,register,shader,cbs.Where(c=>c.HasCompleteWriterContents)
        .ToDictionary(c=>c.ParameterIndex,c=>c.CompleteBytesSha256!));}
}

public sealed partial class PostProcessResourceBank
{
    public MapPostEffect3EyePrefilterExposureRead CaptureEyePrefilterExposureRead(
        MapPostEffect3TextureParameterState table,MapPostEffect3EyePrefilterExposureSource source)
    {
        ArgumentNullException.ThrowIfNull(source);source.RequireCurrentTextureTable(table);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            var result=new MapPostEffect3EyePrefilterExposureRead(this,source,Require(Textures,61,0,"Eye first prefilter exposure"));
            RequireEyePrefilterExposureCurrent(result);return result;
        }
    }
    internal void RequireEyePrefilterExposureCurrent(MapPostEffect3EyePrefilterExposureRead read)
    {
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);read.Source.RequireCurrent();
            var texture=Require(Textures,61,0,"Eye first prefilter exposure");
            if(!ReferenceEquals(read.Bank,this)||!ReferenceEquals(read.Source.Allocation,Source)||!ReferenceEquals(read.Texture,texture)||
                Textures[61].Count!=1||texture.Source.InstanceCount!=1||texture.InstanceIndex!=0||
                texture.StorageFormat!=RenderingDevice.DataFormat.R32Sfloat||texture.Shape.Formats.ShaderResourceFormat0!=41||
                texture.Shape.Dimension!=MapPostEffect3TextureDimension.Texture2D||texture.Shape.Width!=2||
                texture.Shape.Height!=1||texture.Shape.Depth!=1||texture.Shape.MipLevels!=1||
                read.Source.AdmitsFreshUnspecifiedValueDependency&&textureContents.Keys.Any(k=>k.Parameter==61))
                throw new InvalidDataException("Eye prefilter exposure needs exact original61; auto undefined admission cannot replace written/retained history.");
        }
    }
}
