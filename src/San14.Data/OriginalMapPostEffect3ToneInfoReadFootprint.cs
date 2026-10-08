using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>PS3 reads only literal ToneInfo texel0,mip0 when the actual
/// Composite automatic-exposure word is nonzero. Keeps logical binding5;
/// physical resolution through the real Lens5->44 alias belongs to the bank.</summary>
public sealed class OriginalMapPostEffect3ToneInfoReadFootprint
{
    private const string ShaderSha="CCE8D4EF931F3A86F337E499BEE0F6C6F87BEA4AD06BF951E4981D8883780822";
    private OriginalMapPostEffect3ToneInfoReadFootprint(OriginalMapPostEffect3ParameterCompositeUpdate init,
        OriginalMapPostEffect3Binding actualBinding,bool reads)
    {
        var cb=init.ConstantBuffer;SourceSha256=cb.SourceSha256;ContextOwnerIdentity=init.OwnerIdentity;FrameNumber=init.FrameNumber;
        NativeBinding=actualBinding;RequiresTextureRead=reads;
        RequiredConstantBufferBytesSha256=new ReadOnlyDictionary<int,string>(new Dictionary<int,string>
            {{16,Convert.ToHexString(SHA256.HashData(cb.Bytes.Span))}});
        SourceEvidence="PS3-full-DXBC-SHA="+ShaderSha+";single t1 ld_indexable literal(0,0,mip0);"+
            "if_nz(ne(cb2[8].z,0))=actualComposite160B+136;else CB+140 ExposureValue;"+
            "real row403 logical LFOutputToneMapInfo5;only region1x1,not entire native4x1 or whole SRV ready";
    }
    public string SourceSha256{get;}
    public string ContextOwnerIdentity{get;}
    public long FrameNumber{get;}
    public int SourcePassIndex=>44;
    public string ProgramSha256=>ShaderSha;
    public int ParameterIndex=>NativeBinding.ParameterIndex;
    public int TextureMipLevel=>0;
    public uint TextureWidth=>4;
    public uint TextureHeight=>1;
    public uint SourceRegister=>1;
    public uint RegionX=>0;
    public uint RegionY=>0;
    public uint RegionWidth=>1;
    public uint RegionHeight=>1;
    public bool RequiresTextureRead{get;}
    public OriginalMapPostEffect3Binding NativeBinding{get;}
    public IReadOnlyDictionary<int,string> RequiredConstantBufferBytesSha256{get;}
    public string SourceEvidence{get;}
    public bool EstablishesGpuCompletion=>false;

    public static OriginalMapPostEffect3ToneInfoReadFootprint PrepareComposite(
        OriginalMapPostEffect3AllocationPlan allocation,OriginalMapPostEffect3ParameterCompositeUpdate actualCompositeInit)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(actualCompositeInit);
        OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);var cb=actualCompositeInit.ConstantBuffer;
        if(cb.SourceSha256!=allocation.Source.Sha256||cb.FrameNumber!=actualCompositeInit.FrameNumber||cb.FrameNumber<=0||
            cb.OwnerIdentity!=actualCompositeInit.OwnerIdentity||cb.OwnerIdentity!=allocation.ContextOwnerIdentity||
            cb.Declaration.Index!=16||cb.WriterRva!=checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(
                cb.Locale,"OriginalMapPostEffect3ToneInfoReadFootprint.cs",0x1153390))||cb.Bytes.Length!=160)
            throw new InvalidDataException("Composite ToneInfo footprint requires its actual owned source160B CB.");
        var program=allocation.Source.GetProgram(0,3);
        if(program.Sha256!=ShaderSha||Convert.ToHexString(SHA256.HashData(program.Bytes.Span))!=ShaderSha||
            allocation.Source.Passes[44].ProgramReferences.Single(p=>p.StageCode==0).StageIndex!=3)
            throw new InvalidDataException("Composite ToneInfo exact conditional read shader differs.");
        var binding=allocation.Passes[44].ActiveBindings.Last(b=>b.Table==OriginalMapPostEffect3BindingTable.TextureParameters&&
            b.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead&&b.RegisterIndex==1&&(b.StageMask&4)!=0);
        if(binding.SourceRowIndex!=403||binding.ParameterIndex!=5||binding.MipSelector!=-1)
            throw new InvalidDataException("Composite actual native ToneInfo logical binding differs.");
        var ownedToneInfo=OriginalMapPostEffect3AllocationShapeReader.Read(allocation.TextureAllocations.Single(a=>a.Parameter.Index==44));
        if(ownedToneInfo.Width!=4||ownedToneInfo.Height!=1||ownedToneInfo.MipLevels!=1)
            throw new InvalidDataException("Composite source ToneInfo aliased storage declaration differs.");
        float automatic=BinaryPrimitives.ReadSingleLittleEndian(cb.Bytes.Span[136..]);
        if(!float.IsFinite(automatic))throw new NotSupportedException("Composite source automatic exposure conversion must be a finite actual int-to-float value.");
        return new(actualCompositeInit,binding,automatic!=0);
    }
}
