using Godot;
using San14.Data;

/// <summary>A closed first-use admission of the original unspecified61 value.
/// This is not a completed texture lease and supplies no pixel readiness.</summary>
public sealed class MapPostEffect3FreshUndefinedEyeExposureRead
{
    internal MapPostEffect3FreshUndefinedEyeExposureRead(PostProcessResourceBank bank,
        MapPostEffect3FreshUndefinedEyeExposureSource source,MapPostEffect3DeviceTexture texture)
        =>(Bank,Source,Texture)=(bank,source,texture);
    internal PostProcessResourceBank Bank{get;}
    internal MapPostEffect3DeviceTexture Texture{get;}
    public MapPostEffect3FreshUndefinedEyeExposureSource Source{get;}
    public Rid StorageRid=>Texture.StorageRid;
    public long FrameNumber=>Source.FrameNumber;
    public string ContextOwnerIdentity=>Source.ContextOwnerIdentity;
    public uint NativeMipLevel=>0;
    public uint NativeViewFormat=>41;
    public Vector3I Extent=>new(2,1,1);
    public bool EstablishesPriorPixelReadiness=>false;
    public bool InitialPixelValuesAreKnown=>false;
    public string SourceEvidence=>Source.SourceEvidence;
    public void RequireCurrent()=>Bank.RequireFreshUndefinedEyeExposureCurrent(this);
}

public sealed partial class PostProcessResourceBank
{
    public MapPostEffect3FreshUndefinedEyeExposureRead CaptureFreshUndefinedEyeExposureRead(
        MapPostEffect3FreshUndefinedEyeExposureSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);source.RequireCurrent();
            var result=new MapPostEffect3FreshUndefinedEyeExposureRead(this,source,Require(Textures,61,0,"fresh source-undefined exposure"));
            RequireFreshUndefinedEyeExposureCurrent(result);return result;
        }
    }
    internal void RequireFreshUndefinedEyeExposureCurrent(MapPostEffect3FreshUndefinedEyeExposureRead read)
    {
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);read.Source.RequireCurrent();
            RequireFreshUndefinedEyeExposureIdentity(read);
            if(textureContents.Keys.Any(k=>k.Parameter==61))
                throw new InvalidDataException("Exposure61 already has a submitted writer; fresh undefined source admission cannot supply retained history.");
        }
    }
    private void RequireFreshUndefinedEyeExposureIdentity(MapPostEffect3FreshUndefinedEyeExposureRead read)
    {
        var t=Require(Textures,61,0,"source-undefined exposure");
        if(!ReferenceEquals(read.Bank,this)||!ReferenceEquals(read.Source.Allocation,Source)||
            !ReferenceEquals(read.Texture,t)||Textures[61].Count!=1||read.StorageRid!=t.StorageRid||
            t.Source.InstanceCount!=1||t.InstanceIndex!=0||t.StorageFormat!=RenderingDevice.DataFormat.R32Sfloat||
            t.Shape.Dimension!=MapPostEffect3TextureDimension.Texture2D||t.Shape.Width!=2||t.Shape.Height!=1||
            t.Shape.Depth!=1||t.Shape.MipLevels!=1||t.Shape.Formats.UnorderedAccessFormat!=41||!t.Shape.AllowsUnorderedAccess)
            throw new InvalidDataException("Fresh undefined exposure admission belongs to another actual physical allocation or source owner.");
    }
    private void RequireFreshUndefinedEyeExposureCompleted(MapPostEffect3FreshUndefinedEyeExposureRead read,
        MapPostEffect3ComputeCompletedPass completed)
    {
        read.Source.RequireCurrent();RequireFreshUndefinedEyeExposureIdentity(read);
        var write=completed.Writes.SingleOrDefault(w=>w.ParameterIndex==61);
        if(completed.SourcePassIndex!=104||completed.FrameNumber!=read.FrameNumber||
            completed.ContextOwnerIdentity!=read.ContextOwnerIdentity||completed.ProgramSha256!=read.Source.ProgramSha256||
            write is null||write.PhysicalAllocationParameterIndex!=61||write.InstanceIndex!=0||write.StorageRid!=read.StorageRid||
            write.WriterRevision!=1||write.MipLevel!=0||write.MipLevelCount!=1||write.NativeViewFormat!=41||write.Extent!=read.Extent||
            !textureContents.TryGetValue((61,0,0),out var current)||current.Revision!=1||current.Frame!=read.FrameNumber||
            current.Pass!=104||current.Complete)
            throw new InvalidDataException("Undefined exposure publication does not follow its exact actual first source104 writer.");
    }
}
