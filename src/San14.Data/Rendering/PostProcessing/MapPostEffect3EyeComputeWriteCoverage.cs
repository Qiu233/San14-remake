using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Source CS50/51 buffer or CS52 two-texel store domain. Publication
/// must additionally join actual dispatch/bindings/current CB bytes, prior
/// histogram or exposure pixels where required, source image pixels and GPU
/// completion. A dispatched command alone grants none of those contents.</summary>
public sealed class MapPostEffect3EyeComputeWriteCoverage
{
    private readonly MapPostEffect3EyeComputeExecutionRecipe execution;
    private MapPostEffect3EyeComputeWriteCoverage(MapPostEffect3EyeComputeExecutionRecipe execution,
        MapPostEffect3Binding output,MapPostEffect3DispatchExtent local,
        uint byteCount,uint width,uint height,MapPostEffect3Binding? textureRead,
        MapPostEffect3PixelRectangle? textureRegion,MapPostEffect3Binding? bufferRead,
        IReadOnlyDictionary<int,string> cbs)
    {
        this.execution=execution;OutputBinding=output;LocalSize=local;ByteCount=byteCount;TextureWidth=width;TextureHeight=height;
        RequiredInputTextureBinding=textureRead;RequiredInputTexturePixelRegion=textureRegion;RequiredInputBufferBinding=bufferRead;
        RequiredConstantBufferBytesSha256=cbs;
        SourceEvidence=execution.SourceEvidence+";exact-native-CS50/51/52-DXBC-hash/store/atomic/control-domain;"+
            "no-AutoExposure-skip;histogram64uint256B;CS52-literal-two-texel-writes;prior-u0-read-only-positive-adaptation-rates;"+
            "source-domain-only;actual-GPU/inputs/current-CB/revisions-required";
    }
    public long FrameNumber=>execution.FrameNumber;
    public string ContextOwnerIdentity=>execution.ContextOwnerIdentity;
    public string SourceSha256=>execution.SourceSha256;
    public string Locale=>execution.Locale;
    public int SourcePassIndex=>execution.SourcePassIndex;
    public string ProgramSha256=>execution.ProgramSha256;
    public MapPostEffect3Binding OutputBinding{get;}
    public MapPostEffect3BindingTable Table=>OutputBinding.Table;
    public int ParameterIndex=>OutputBinding.ParameterIndex;
    public uint ByteOffset=>0;
    public uint ByteCount{get;}
    public uint TextureWidth{get;}
    public uint TextureHeight{get;}
    public uint TextureMipLevel=>0;
    public MapPostEffect3DispatchExtent LocalSize{get;}
    public MapPostEffect3DispatchExtent ActualDispatch=>execution.ActualDispatch;
    public MapPostEffect3Binding? RequiredInputTextureBinding{get;}
    public MapPostEffect3PixelRectangle? RequiredInputTexturePixelRegion{get;}
    public MapPostEffect3Binding? RequiredInputBufferBinding{get;}
    public uint RequiredInputBufferByteOffset=>0;
    public uint RequiredInputBufferByteCount=>RequiredInputBufferBinding is null?0u:256u;
    public bool RequiresPriorHistogramBufferContents=>SourcePassIndex==103;
    public bool RequiresPriorExposureTexelContents=>execution.PriorExposureTexelLoadExecutes;
    public MapPostEffect3PixelRectangle? RequiredPriorExposureRegion=>RequiresPriorExposureTexelContents?new(0,0,1,1):null;
    public bool PriorExposureTexelLoadExecutes=>execution.PriorExposureTexelLoadExecutes;
    public bool AllowsFreshUninitializedExposureStorage=>SourcePassIndex==104&&!PriorExposureTexelLoadExecutes;
    public IReadOnlyDictionary<int,string> RequiredConstantBufferBytesSha256{get;}
    public string? SourceKnownCompleteBufferBytesSha256=>SourcePassIndex==102?Convert.ToHexString(SHA256.HashData(new byte[256])):null;
    public bool EstablishesGpuCompletion=>false;
    public bool EstablishesCompleteResourceContents=>false;
    public string SourceEvidence{get;}
    public void RequireCurrent()=>execution.RequireCurrent();
    public void RequirePreparedFrame(MapPostEffect3EyeAdaptation2Frame actual)=>execution.RequirePreparedFrame(actual);

    public static MapPostEffect3EyeComputeWriteCoverage Prepare(MapPostEffect3AllocationPlan allocation,
        MapPostEffect3EyeComputeExecutionRecipe execution)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(execution);execution.RequireCurrent();
        MapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if(execution.SourceSha256!=allocation.Source.Sha256||execution.ContextOwnerIdentity!=allocation.ContextOwnerIdentity)
            throw new InvalidDataException("Eye compute coverage belongs to another current module.");
        int pass=execution.SourcePassIndex;
        var program=MapPostEffect3Functor1FootprintSource.RequireProgram(allocation,pass,5,pass-52);
        var local=pass switch{102=>new MapPostEffect3DispatchExtent(64,1,1),103=>new(8,8,1),104=>new(1,1,1),_=>throw new InvalidDataException("Unknown Eye compute coverage pass.")};
        CheckTokens(program,pass,local);
        MapPostEffect3Binding Binding(MapPostEffect3BindingTable table,MapPostEffect3BindingRole role,int register)
            =>execution.OrderedEffectiveBindings.LastOrDefault(b=>b.Table==table&&b.Role==role&&b.RegisterIndex==register&&(b.StageMask&1)!=0)
                ??throw new InvalidDataException("Eye compute exact shader resource has no current native effective binding.");
        var histogram=MapPostEffect3AllocationBufferShapeReader.Read(allocation.BufferAllocations.Single(b=>b.Parameter.Index==8));
        if(histogram.Dimension!=MapPostEffect3BufferDimension.Typed||histogram.ByteCount!=256||histogram.ViewFormat!=42||
            histogram.ViewElementCount!=64||!histogram.CreatesShaderResourceView||!histogram.CreatesUnorderedAccessView)
            throw new InvalidDataException("Eye actual histogram must be native buffer8 R32_UINT64/256B with source SRV/UAV views.");
        MapPostEffect3Binding output,readBuffer,readTexture;
        var hashes=new Dictionary<int,string>();
        if(pass!=102)
        {
            var cb=execution.ExposureConstants!;
            hashes.Add(24,Convert.ToHexString(SHA256.HashData(cb.Bytes.Span)));
        }
        if(pass is 102 or 103)
        {
            output=Binding(MapPostEffect3BindingTable.BufferParameters,MapPostEffect3BindingRole.UnorderedAccessReadWrite,0);
            if(output.ParameterIndex!=8)throw new InvalidDataException("Eye CS50/51 output histogram binding differs.");
            if(pass==102)return new(execution,output,local,256,0,0,null,null,null,new ReadOnlyDictionary<int,string>(hashes));
            readTexture=Binding(MapPostEffect3BindingTable.TextureParameters,MapPostEffect3BindingRole.ShaderResourceRead,0);
            if(readTexture.ParameterIndex!=79)throw new InvalidDataException("Eye CS51 source texture79 binding differs.");
            var cb=execution.ExposureConstants!.Bytes.Span;
            uint width=ExactUnsignedExtent(BinaryPrimitives.ReadSingleLittleEndian(cb[40..]));
            uint height=ExactUnsignedExtent(BinaryPrimitives.ReadSingleLittleEndian(cb[44..]));
            uint coveredWidth=Math.Min(width,checked(execution.ActualDispatch.X*16u));
            uint coveredHeight=Math.Min(height,checked(execution.ActualDispatch.Y*16u));
            var region=new MapPostEffect3PixelRectangle(0,0,checked((int)coveredWidth),checked((int)coveredHeight));
            return new(execution,output,local,256,0,0,readTexture,region,null,new ReadOnlyDictionary<int,string>(hashes));
        }
        output=Binding(MapPostEffect3BindingTable.TextureParameters,MapPostEffect3BindingRole.UnorderedAccessReadWrite,0);
        readBuffer=Binding(MapPostEffect3BindingTable.BufferParameters,MapPostEffect3BindingRole.ShaderResourceRead,0);
        if(output.ParameterIndex!=61||readBuffer.ParameterIndex!=8)
            throw new InvalidDataException("Eye CS52 original exposure/histogram binding differs.");
        var shape=MapPostEffect3AllocationShapeReader.Read(allocation.TextureAllocations.Single(t=>t.Parameter.Index==61));
        if(shape.Dimension!=MapPostEffect3TextureDimension.Texture2D||shape.Width!=2||shape.Height!=1||shape.MipLevels!=1||!shape.AllowsUnorderedAccess)
            throw new InvalidDataException("Eye actual exposure output is not its original two-texel 2D mip0 texture61.");
        return new(execution,output,local,0,2,1,null,null,readBuffer,new ReadOnlyDictionary<int,string>(hashes));
    }
    private static uint ExactUnsignedExtent(float value)
    {
        if(!float.IsFinite(value)||value<0||value>=2147483648f)
            throw new NotSupportedException("Eye CS51 source FTou bound is limited to finite nonnegative int-range extents.");
        return (uint)value;
    }
    private static void CheckTokens(MapPostEffect3Program program,int pass,MapPostEffect3DispatchExtent local)
    {
        var part=program.Container.Parts.Single(p=>p.TokenHeader is not null);var b=part.PayloadRange.Slice(program.Bytes.Span);
        int count=checked((int)BinaryPrimitives.ReadUInt32LittleEndian(b[4..]));
        if(b.Length!=count*4)throw new InvalidDataException("Eye CS source token extent differs.");
        int groups=0,stores=0,atomics=0,returns=0,loads=0;
        for(int word=2;word<count;)
        {
            uint token=BinaryPrimitives.ReadUInt32LittleEndian(b[(word*4)..]);int op=(int)(token&2047);
            int n=op==53?checked((int)BinaryPrimitives.ReadUInt32LittleEndian(b[((word+1)*4)..])):(int)((token>>24)&127);
            if(n<=0||word+n>count)throw new InvalidDataException("Eye CS token instruction span differs.");
            if(op==155)
            {if(n!=4||Word(b,word+1)!=local.X||Word(b,word+2)!=local.Y||Word(b,word+3)!=local.Z)throw new InvalidDataException("Eye exact local-size declaration changed.");groups++;}
            if(op==164)stores++;
            if(op==173)atomics++;
            if(op==163)loads++;
            if(op==62)returns++;
            if(op is 13 or 63 or 48 or 76)throw new InvalidDataException("Eye CS coverage has an unclosed discard/early-return/loop domain.");
            word+=n;
        }
        if(groups!=1||returns!=1||stores!=(pass==102?1:pass==104?2:0)||loads!=(pass==104?1:0))
            throw new InvalidDataException("Eye exact original store/load/final-return domain differs.");
        // CS51's source atomic opcode is checked against its frozen exact
        // bytecode by the audit. Group shared64 counters start zero, each lane
        // unconditionally atomic-adds its own final counter to u0[lane].
        _=atomics;
    }
    private static uint Word(ReadOnlySpan<byte> bytes,int word)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[(word*4)..]);
}
