using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Exact source shader store coverage, distinct from a completed GPU
/// write. The device owner must join an actual successful dispatch receipt and
/// exact physical storage/side before publishing initialized contents.</summary>
public sealed class MapPostEffect3LensWriteCoverage
{
    private MapPostEffect3LensWriteCoverage(MapPostEffect3LensExecutionRecipe recipe,
        MapPostEffect3BindingTable table,int parameter,string shader,uint width,uint height,uint bytes,
        MapPostEffect3DispatchExtent local,string evidence,string? requiredCbSha=null,
        IReadOnlyDictionary<int,string>? requiredInputBufferSha=null)
        =>(FrameNumber,ContextOwnerIdentity,SourceSha256,SourcePassIndex,Table,ParameterIndex,ProgramSha256,
            TextureWidth,TextureHeight,ByteOffset,ByteCount,LocalSize,ActualDispatch,SourceEvidence,
            RequiredConstantBufferBytesSha256,RequiredInputBufferBytesSha256)=
            (recipe.FrameNumber,recipe.ContextOwnerIdentity,recipe.SourceSha256,recipe.SourcePassIndex,table,parameter,shader,
             width,height,0,bytes,local,recipe.ActualDispatch,evidence,requiredCbSha,
             new System.Collections.ObjectModel.ReadOnlyDictionary<int,string>(requiredInputBufferSha?.ToDictionary(p=>p.Key,p=>p.Value)??new()));
    public long FrameNumber{get;}
    public string ContextOwnerIdentity{get;}
    public string SourceSha256{get;}
    public int SourcePassIndex{get;}
    public MapPostEffect3BindingTable Table{get;}
    public int ParameterIndex{get;}
    public string ProgramSha256{get;}
    public uint TextureWidth{get;}
    public uint TextureHeight{get;}
    public uint ByteOffset{get;}
    public uint ByteCount{get;}
    public int TextureMipLevel=>0;
    public MapPostEffect3DispatchExtent LocalSize{get;}
    public MapPostEffect3DispatchExtent ActualDispatch{get;}
    public string SourceEvidence{get;}
    public int? RequiredConstantBufferParameterIndex=>RequiredConstantBufferBytesSha256 is null?null:6;
    public string? RequiredConstantBufferBytesSha256{get;}
    public IReadOnlyDictionary<int,string> RequiredInputBufferBytesSha256{get;}
    public bool EstablishesGpuCompletion=>false;

    public static MapPostEffect3LensWriteCoverage Prepare(
        MapPostEffect3AllocationPlan allocation,MapPostEffect3LensExecutionRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(recipe);
        if(!recipe.ExecutesPass||recipe.ContextOwnerIdentity!=allocation.ContextOwnerIdentity||recipe.SourceSha256!=allocation.Source.Sha256)
            throw new InvalidDataException("Lens write coverage requires its exact prepared source execution recipe.");
        if(recipe.SourcePassIndex==30)
        {
            const string sha="C65B00C5E62C72F6DC19AD5A1F2B6E2EDE87AF8BE36B4D3C2D91DA0F9873D464";
            var program=allocation.Source.GetProgram(5,28);
            if(program.Sha256!=sha||Convert.ToHexString(SHA256.HashData(program.Bytes.Span))!=sha)
                throw new InvalidDataException("GhostSprite complete-store shader differs from its exact source.");
            var part=program.Container.Parts.Single(p=>p.TokenHeader is not null);var raw=part.PayloadRange.Slice(program.Bytes.Span);
            if(raw.Length!=468*4||BinaryPrimitives.ReadUInt32LittleEndian(raw.Slice(4,4))!=468)
                throw new InvalidDataException("GhostSprite complete loop token extent changed.");
            // The exact hash fixes the whole single-invocation program. The
            // final loop has one UGE/breakc exit and no conditional stores.
            CheckWords(raw,259,[0x0600001C,0x00100082,0,0x0020801A,2,9]); // ftoi r0.w, cb2[9].y
            CheckWords(raw,265,[0x05000036,0x00100012,1,0x00004001,0]); // r1.x=0
            CheckWords(raw,270,[0x01000030,0x07000050,0x00100022,1,0x0010000A,1,0x0010003A,0,
                0x03040003,0x0010001A,1]); // loop, uge r1.y,r1.x,r0.w, breakc nz
            CheckWords(raw,441,[0x090000A8,0x0011E0F2,0,0x0010000A,1,0x00004001,0,0x00100E46,4,
                0x090000A8,0x0011E072,0,0x0010000A,1,0x00004001,16,0x00100796,1,
                0x0700001E,0x00100012,1,0x0010000A,1,0x00004001,1,0x01000016,0x0100003E]);
            var main=recipe.Initialization.ConstantBuffers.Single(c=>c.Declaration.Index==6);
            if(main.FrameNumber!=recipe.FrameNumber||main.OwnerIdentity!=recipe.ContextOwnerIdentity||main.SourceSha256!=recipe.SourceSha256||
                main.Locale!=recipe.Initialization.Locale||main.Writer!=MapPostEffect3ConstantBufferWriter.LensLegacy||main.Bytes.Length!=192||
                BinaryPrimitives.ReadUInt32LittleEndian(main.Bytes.Span.Slice(148,4))!=0x41800000||
                recipe.ActualDispatch!=new MapPostEffect3DispatchExtent(1,1,1))
                throw new NotSupportedException("GhostSprite complete16-row coverage needs its actual original Init count16 and single invocation.");
            var output=recipe.OrderedEffectiveBindings.Last(b=>b.Table==MapPostEffect3BindingTable.BufferParameters&&
                b.Role==MapPostEffect3BindingRole.UnorderedAccessReadWrite&&b.RegisterIndex==0);
            var shape=MapPostEffect3AllocationBufferShapeReader.Read(allocation.BufferAllocations.Single(a=>a.Parameter.Index==output.ParameterIndex));
            if(output.ParameterIndex!=2||shape.Dimension!=MapPostEffect3BufferDimension.Structured||shape.StructureByteStride!=28||shape.ByteCount!=448)
                throw new InvalidDataException("GhostSprite complete coverage differs from original buffer2 stride28×16.");
            var inputs=recipe.Initialization.BufferWrites.Where(w=>w.ParameterIndex is 0 or 1).OrderBy(w=>w.ParameterIndex).ToArray();
            if(inputs.Length!=2||inputs[0].ParameterIndex!=0||inputs[0].Bytes.Length!=64||inputs[1].ParameterIndex!=1||inputs[1].Bytes.Length!=448)
                throw new InvalidDataException("GhostSprite loop coverage needs both actual complete builtin Init buffer writers.");
            return new(recipe,MapPostEffect3BindingTable.BufferParameters,2,sha,0,0,448,new(1,1,1),
                "CS28 exact468words/hash;actualInit CB6+148=float16;one invocation;for i=0..15 unconditional " +
                "store_structured u0[i] byte0.xyzw16B + byte16.xyz12B;then iadd/endloop;all448B source-written;GPU values need no invented CPU bytes",
                Convert.ToHexString(SHA256.HashData(main.Bytes.Span)),
                inputs.ToDictionary(w=>w.ParameterIndex,w=>Convert.ToHexString(SHA256.HashData(w.Bytes.Span))));
        }
        if(recipe.SourcePassIndex==31)
        {
            const string sha="2BE29B14F5D4D9C037328C865F30CD73466E6013CA7D7262B32EF85DC401A7BF";
            CheckTokens(allocation.Source.GetProgram(5,29),sha,RawResetTokens);
            var output=recipe.OrderedEffectiveBindings.Last(b=>b.Table==MapPostEffect3BindingTable.BufferParameters&&
                b.Role==MapPostEffect3BindingRole.UnorderedAccessReadWrite&&b.RegisterIndex==1);
            var shape=MapPostEffect3AllocationBufferShapeReader.Read(allocation.BufferAllocations.Single(a=>a.Parameter.Index==output.ParameterIndex));
            if(output.ParameterIndex!=3||shape.Dimension!=MapPostEffect3BufferDimension.Raw||shape.ByteCount!=16||
                recipe.ActualDispatch!=new MapPostEffect3DispatchExtent(1,1,1))
                throw new NotSupportedException("GhostArguments full reset needs the actual16B raw buffer3 and one native invocation.");
            return new(recipe,MapPostEffect3BindingTable.BufferParameters,3,sha,0,0,16,new(1,1,1),
                "CS29 exact21words;DCL_RAW_UAV1;numthreads1,1,1;unconditional store_raw byte0 uint4(4,4,0,0);ret;"+
                "raw3 actual Width4*Height1*stride4=16B;indirect-capable declaration is not an indirect draw claim");
        }
        if(recipe.SourcePassIndex==32)
        {
            const string sha="0599505E6853EF7D12468B3FC73928079909A022CE05EB666658A5B536F55C42";
            CheckTokens(allocation.Source.GetProgram(5,30),sha,ClearTextureTokens);
            var output=recipe.OrderedEffectiveBindings.Last(b=>b.Table==MapPostEffect3BindingTable.TextureParameters&&
                b.Role==MapPostEffect3BindingRole.UnorderedAccessReadWrite&&b.RegisterIndex==0);
            var shape=MapPostEffect3AllocationShapeReader.Read(allocation.TextureAllocations.Single(a=>a.Parameter.Index==output.ParameterIndex));
            if(output.ParameterIndex!=41||output.MipSelector!=-1||shape.MipLevels!=1||shape.Dimension!=MapPostEffect3TextureDimension.Texture2D||
                (ulong)recipe.ActualDispatch.X*16<shape.Width||(ulong)recipe.ActualDispatch.Y*16<shape.Height||recipe.ActualDispatch.Z!=1)
                throw new NotSupportedException("ClearGhost full coverage needs its exact single-mip target and dispatch covering every coordinate.");
            return new(recipe,MapPostEffect3BindingTable.TextureParameters,41,sha,shape.Width,shape.Height,0,new(16,16,1),
                "CS30 exact23words;DCL_TYPED_UAV0 float4 Texture2D;numthreads16,16,1;unconditional store_uav_typed global-thread-id.xy float4(0);ret;"+
                "all in-range target coordinates are written;out-of-range invocations do not establish any additional contents");
        }
        throw new NotSupportedException("Lens source coverage is closed only for actual GhostSprite loop30, GhostArguments reset31 and ClearGhost32.");
    }
    private static void CheckTokens(MapPostEffect3Program program,string sha,uint[] tokens)
    {
        if(program.Sha256!=sha)throw new InvalidDataException("Lens complete store shader differs from its positively identified source.");
        var part=program.Container.Parts.Single(p=>p.TokenHeader is not null);var raw=part.PayloadRange.Slice(program.Bytes.Span);
        if(raw.Length!=tokens.Length*4||!raw[..8].SequenceEqual(TokenPrefix(tokens)))
            throw new InvalidDataException("Lens complete store token extent/header differs.");
        for(int i=0;i<tokens.Length;i++)if(BinaryPrimitives.ReadUInt32LittleEndian(raw.Slice(i*4,4))!=tokens[i])
            throw new InvalidDataException("Lens complete store control flow or operands changed.");
    }
    private static byte[] TokenPrefix(uint[] tokens)
    {byte[] b=new byte[8];BinaryPrimitives.WriteUInt32LittleEndian(b,tokens[0]);BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4),tokens[1]);return b;}
    private static void CheckWords(ReadOnlySpan<byte> source,int at,uint[] words)
    {for(int i=0;i<words.Length;i++)if(BinaryPrimitives.ReadUInt32LittleEndian(source.Slice((at+i)*4,4))!=words[i])
        throw new InvalidDataException("GhostSprite complete-store loop operands changed.");}
    private static readonly uint[] RawResetTokens=[0x00050050,21,
        0x0100086A,0x0300009D,0x0011E000,1,0x0400009B,1,1,1,
        0x0A0000A6,0x0011E0F2,1,0x00004001,0,0x00004002,4,4,0,0,0x0100003E];
    private static readonly uint[] ClearTextureTokens=[0x00050050,23,
        0x0100086A,0x0400189C,0x0011E000,0,0x00005555,0x0200005F,0x00020032,
        0x0400009B,16,16,1,0x090000A4,0x0011E0F2,0,0x00020546,0x00004002,0,0,0,0,0x0100003E];
}
