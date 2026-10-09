using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actual CS38's two-texel write inside native4x1 storage. The source
/// load of prior adaptation is retained, but its influence is proven absent
/// only when the actual CB96/100 comparison selects the fresh reduction.</summary>
public sealed class OriginalMapPostEffect3ToneInfoRegionWriteCoverage
{
    private const string ShaderSha="5F84FD6A28934662775F9D2D13DD52E1B9A3ED4A3FB7775676C1D851C89939C5";
    private OriginalMapPostEffect3ToneInfoRegionWriteCoverage(OriginalMapPostEffect3LensExecutionRecipe recipe,
        OriginalMapPostEffect3ParameterConstantBuffer main,OriginalMapPostEffect3Binding input)
    {
        SourceSha256=recipe.SourceSha256;ContextOwnerIdentity=recipe.ContextOwnerIdentity;FrameNumber=recipe.FrameNumber;
        ActualDispatch=recipe.ActualDispatch;
        RequiredConstantBufferBytesSha256=new ReadOnlyDictionary<int,string>(new Dictionary<int,string>
            {{6,Convert.ToHexString(SHA256.HashData(main.Bytes.Span))}});
        RequiredInputTextureParameters=Array.AsReadOnly(new[]{input.ParameterIndex});
        RequiredInputTextureBindings=Array.AsReadOnly(new[]{input});
        SourceEvidence="CS38-full-DXBC-SHA="+ShaderSha+";SHEX267 unconditional old u0(1,0) load;"+
            "SHEX249/260 ordered lt/or fromCB96/100;SHEX321 movc selects current reduction when both false;"+
            "SHEX330/366 thread0 literal stores(1,0)/(0,0);native texture44 storage4x1,region only2x1;"+
            "actual mainCB96/100 finite<=0;no prior-value dependency,not a skipped-load claim;"+
            "actual physical execution/input/CB joins required";
    }
    public string SourceSha256{get;}
    public string ContextOwnerIdentity{get;}
    public long FrameNumber{get;}
    public int SourcePassIndex=>41;
    public string ProgramSha256=>ShaderSha;
    public int ParameterIndex=>44;
    public int TextureMipLevel=>0;
    public uint TextureWidth=>4;
    public uint TextureHeight=>1;
    public uint RegionX=>0;
    public uint RegionY=>0;
    public uint RegionWidth=>2;
    public uint RegionHeight=>1;
    public OriginalMapPostEffect3DispatchExtent ActualDispatch{get;}
    public OriginalMapPostEffect3DispatchExtent LocalSize=>new(16,1,1);
    public IReadOnlyDictionary<int,string> RequiredConstantBufferBytesSha256{get;}
    public IReadOnlyList<int> RequiredInputTextureParameters{get;}
    public IReadOnlyList<OriginalMapPostEffect3Binding> RequiredInputTextureBindings{get;}
    public string SourceEvidence{get;}
    public bool OldTexelLoadExecuted=>true;
    public bool OutputDependsOnPriorTexel=>false;
    public bool EstablishesCompleteMipContents=>false;
    public bool EstablishesGpuCompletion=>false;

    public static OriginalMapPostEffect3ToneInfoRegionWriteCoverage Prepare(
        OriginalMapPostEffect3AllocationPlan allocation,OriginalMapPostEffect3LensExecutionRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(recipe);
        OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if(!recipe.ExecutesPass||recipe.SourcePassIndex!=41||recipe.SourceSha256!=allocation.Source.Sha256||
            recipe.ContextOwnerIdentity!=allocation.ContextOwnerIdentity||recipe.FrameNumber<=0||
            recipe.ActualDispatch!=new OriginalMapPostEffect3DispatchExtent(1,1,1))
            throw new InvalidDataException("ToneInfo region needs the actual owned CS38 single16-thread execution.");
        var program=allocation.Source.GetProgram(5,38);
        if(program.Sha256!=ShaderSha||Convert.ToHexString(SHA256.HashData(program.Bytes.Span))!=ShaderSha||
            allocation.Source.Passes[41].ProgramReferences.Single(p=>p.StageCode==5).StageIndex!=38)
            throw new InvalidDataException("ToneInfo original shader/store domain differs.");
        var raw=program.Container.Parts.Single(p=>p.TokenHeader is not null).PayloadRange.Slice(program.Bytes.Span);
        // These are complete original instructions, in addition to the pinned
        // complete program fixing loops, thread0 guard and comparison inputs.
        Check(raw,267,[0x8C0000A3,0x800000C2,0x00155543,0x00100042,0,0x00004002,1,0,0,0,0x0011EC96,0]);
        Check(raw,321,[0x09000037,0x00100012,0,0x0010001A,0,0x0010002A,0,0x0010000A,0]);
        Check(raw,330,[0x0A0000A4,0x0011E0F2,0,0x00004002,1,0,0,0,0x00100006,0]);
        Check(raw,366,[0x0A0000A4,0x0011E0F2,0,0x00004002,0,0,0,0,0x00100006,0]);
        var main=recipe.Initialization.ConstantBuffers.Single(c=>c.Declaration.Index==6);
        if(main.FrameNumber!=recipe.FrameNumber||main.OwnerIdentity!=recipe.ContextOwnerIdentity||main.SourceSha256!=recipe.SourceSha256||
            main.Locale!=recipe.Initialization.Locale||main.Writer!=OriginalMapPostEffect3ConstantBufferWriter.LensLegacy||main.Bytes.Length!=192||
            BinaryPrimitives.ReadSingleLittleEndian(main.Bytes.Span)!=(float)allocation.ContextWidth||
            BinaryPrimitives.ReadSingleLittleEndian(main.Bytes.Span[4..])!=(float)allocation.ContextHeight||
            allocation.ContextWidth>int.MaxValue||allocation.ContextHeight>int.MaxValue)
            throw new InvalidDataException("ToneInfo actual192B source CB and finite context loop bounds differ.");
        float up=BinaryPrimitives.ReadSingleLittleEndian(main.Bytes.Span[96..]),down=BinaryPrimitives.ReadSingleLittleEndian(main.Bytes.Span[100..]);
        if(!float.IsFinite(up)||!float.IsFinite(down)||up>0||down>0)
            throw new NotSupportedException("ToneInfo adaptation depends on prior texel1; this branch needs a real retained/source initialization producer.");
        var output=recipe.OrderedEffectiveBindings.Last(b=>b.Table==OriginalMapPostEffect3BindingTable.TextureParameters&&
            b.Role==OriginalMapPostEffect3BindingRole.UnorderedAccessReadWrite&&b.RegisterIndex==0&&(b.StageMask&1)!=0);
        var shape=OriginalMapPostEffect3AllocationShapeReader.Read(allocation.TextureAllocations.Single(a=>a.Parameter.Index==output.ParameterIndex));
        if(output.ParameterIndex!=44||output.MipSelector!=-1||shape.Width!=4||shape.Height!=1||shape.MipLevels!=1||
            shape.Dimension!=OriginalMapPostEffect3TextureDimension.Texture2D||!shape.AllowsUnorderedAccess)
            throw new InvalidDataException("ToneInfo region is not the original native4x1 single-mip UAV44.");
        var input=recipe.OrderedEffectiveBindings.Last(b=>b.Table==OriginalMapPostEffect3BindingTable.TextureParameters&&
            b.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead&&b.RegisterIndex==0&&(b.StageMask&1)!=0);
        return new(recipe,main,input);
    }
    private static void Check(ReadOnlySpan<byte> raw,int at,uint[] words)
    {for(int i=0;i<words.Length;i++)if(BinaryPrimitives.ReadUInt32LittleEndian(raw[((at+i)*4)..])!=words[i])
        throw new InvalidDataException("ToneInfo actual read/select/literal store operands changed.");}
}
