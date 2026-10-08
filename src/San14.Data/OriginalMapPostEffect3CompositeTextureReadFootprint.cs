using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Finite source PS3 non-Tone texture mip requests. Native full SRVs
/// are retained. Resolving requests needs the actual view mip count from its
/// owner, not a guessed declaration extent for an external texture parameter.</summary>
public sealed class OriginalMapPostEffect3CompositeTextureReadFootprint
{
    private const string ShaderSha="CCE8D4EF931F3A86F337E499BEE0F6C6F87BEA4AD06BF951E4981D8883780822";
    private OriginalMapPostEffect3CompositeTextureReadFootprint(OriginalMapPostEffect3ParameterCompositeUpdate init,
        OriginalMapPostEffect3Binding binding,bool starBranch,int[] requests)
    {
        var cb=init.ConstantBuffer;FrameNumber=init.FrameNumber;ContextOwnerIdentity=init.OwnerIdentity;SourceSha256=cb.SourceSha256;
        NativeBinding=binding;StarBurstBranchActive=starBranch;RequestedMipLevels=Array.AsReadOnly(requests);
        RequiredConstantBufferBytesSha256=new ReadOnlyDictionary<int,string>(new Dictionary<int,string>
            {{16,Convert.ToHexString(SHA256.HashData(cb.Bytes.Span))}});
        SourceEvidence="PS3-full-DXBC-SHA="+ShaderSha+";all texture samples are explicit SampleLevel,no derivativeLOD;"+
            "t0 Scene31:literal0/s8 plus literal2 onlyifCB16+60>bits37480000;"+
            "t2 LFOutputBloom48:one unconditional literal0/s7;t3 LFOutputStarBurst49:four literal0/s7 inside same ordered predicate;"+
            "actual owner view-mip-count required for native explicitLOD clamp;original fullSRV and alias identity retained;notGPUcompletion";
    }
    public int SourcePassIndex=>44;
    public string ProgramSha256=>ShaderSha;
    public long FrameNumber{get;}
    public string ContextOwnerIdentity{get;}
    public string SourceSha256{get;}
    public OriginalMapPostEffect3Binding NativeBinding{get;}
    public int ParameterIndex=>NativeBinding.ParameterIndex;
    public uint SourceRegister=>checked((uint)NativeBinding.RegisterIndex);
    public IReadOnlyDictionary<int,string> RequiredConstantBufferBytesSha256{get;}
    public IReadOnlyList<int> RequestedMipLevels{get;}
    /// <summary>Before actual view count resolution, these are source requests;
    /// a logical external parameter does not establish physical mip availability.</summary>
    public IReadOnlyList<int> RequiredMipLevels=>RequestedMipLevels;
    public bool RequiresTextureRead=>RequestedMipLevels.Count!=0;
    public bool StarBurstBranchActive{get;}
    public bool HasImplicitDerivativeLod=>false;
    public bool RetainsOriginalFullShaderResourceView=>true;
    public bool EstablishesGpuCompletion=>false;
    public uint RequiredSamplerRegister=>SourceRegister==0?8u:7u;
    public uint RequiredSamplerFilter=>SourceRegister==0?0u:0x14u;
    public uint RequiredSamplerMipLodBiasBits=>0;
    public uint RequiredSamplerMinimumLodBits=>0;
    public uint RequiredSamplerMaximumLodBits=>0x7F7FFFFF;
    public string SourceEvidence{get;}

    /// <summary>The device owner must verify this count against the actual
    /// bound native full SRV. Explicit integer SampleLevel with the required
    /// source MIP_POINT/min0/maxFLTMAX sampler clamps to the last view level.
    /// No missing mip is fabricated, initialized or inserted into the view.</summary>
    public IReadOnlyList<int> ResolveActualMipLevels(uint actualFullShaderResourceViewMipCount)
    {
        if(actualFullShaderResourceViewMipCount is <1 or >32)
            throw new InvalidDataException("Composite source mip resolution needs the actual positive complete view mip count.");
        int last=checked((int)actualFullShaderResourceViewMipCount-1);
        return Array.AsReadOnly(RequestedMipLevels.Select(m=>Math.Min(m,last)).Distinct().Order().ToArray());
    }

    public static OriginalMapPostEffect3CompositeTextureReadFootprint Prepare(
        OriginalMapPostEffect3AllocationPlan allocation,OriginalMapPostEffect3ParameterCompositeUpdate actualCompositeInit,int register)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(actualCompositeInit);
        if(register is not(0 or 2 or 3))throw new NotSupportedException("Composite non-Tone source footprint supports only actual PS3 registers0/2/3;Tone1 uses its texel-region proof.");
        OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);var cb=actualCompositeInit.ConstantBuffer;
        if(cb.FrameNumber<=0||cb.FrameNumber!=actualCompositeInit.FrameNumber||cb.OwnerIdentity!=actualCompositeInit.OwnerIdentity||
            cb.OwnerIdentity!=allocation.ContextOwnerIdentity||cb.SourceSha256!=allocation.Source.Sha256||
            cb.Declaration.Index!=16||cb.Declaration.NameHash!=0x8EF18D14||cb.WriterRva!=checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(
                cb.Locale,"OriginalMapPostEffect3CompositeTextureReadFootprint.cs",0x1153390))||cb.Bytes.Length!=160)
            throw new InvalidDataException("Composite mip footprint requires its actual owned160B source CB16.");
        var program=allocation.Source.GetProgram(0,3);
        if(program.Sha256!=ShaderSha||Convert.ToHexString(SHA256.HashData(program.Bytes.Span))!=ShaderSha||
            allocation.Source.Passes[44].ProgramReferences.Single(p=>p.StageCode==0).StageIndex!=3)
            throw new InvalidDataException("Composite exact explicitSampleLevel shader differs.");
        var raw=program.Container.Parts.Single(p=>p.TokenHeader is not null).PayloadRange.Slice(program.Bytes.Span);
        // Actual ordered lt threshold and branch. The complete source hash
        // fixes all sixteen SampleLevel reads and the separate Tone1 Load.
        uint[] predicate=[0x08000031,0x00100082,0,0x00004001,0x37480000,0x0020803A,2,3];
        for(int i=0;i<predicate.Length;i++)if(BinaryPrimitives.ReadUInt32LittleEndian(raw[((545+i)*4)..])!=predicate[i])
            throw new InvalidDataException("Composite actual star texture mip predicate changed.");
        var binding=allocation.Passes[44].ActiveBindings.Last(b=>b.Table==OriginalMapPostEffect3BindingTable.TextureParameters&&
            b.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead&&b.RegisterIndex==register&&(b.StageMask&4)!=0);
        int parameter=register switch{0=>31,2=>48,3=>49,_=>throw new InvalidDataException()};
        if(binding.ParameterIndex!=parameter||binding.MipSelector!=-1||binding.SourceRowIndex!=402+register)
            throw new InvalidDataException("Composite actual last ordered native full-SRV binding differs.");
        float sunScaleY=BinaryPrimitives.ReadSingleLittleEndian(cb.Bytes.Span[60..]);
        // Original LT is ordered:NaN disables this branch just as IEEE > does.
        bool star=sunScaleY>BitConverter.UInt32BitsToSingle(0x37480000);
        int[] requests=register switch{0=>star?[0,2]:[0],2=>[0],3=>star?[0]:[],_=>throw new InvalidDataException()};
        return new(actualCompositeInit,binding,star,requests);
    }
}
