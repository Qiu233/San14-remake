using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Proved write domain of the six exact original Shaft CS programs.
/// It describes source coverage; successful GPU execution and current physical
/// storage/write revision are separately required by the resource bank.</summary>
public sealed class MapPostEffect3ShaftWriteCoverage
{
    private MapPostEffect3ShaftWriteCoverage(MapPostEffect3AllocationPlan allocation,
        MapPostEffect3ParameterLightShaftInitialization init,int pass,int parameter,string shader,
        MapPostEffect3TextureShape shape,MapPostEffect3DispatchExtent local,
        MapPostEffect3DispatchExtent dispatch)
        =>(FrameNumber,ContextOwnerIdentity,SourceSha256,SourcePassIndex,ParameterIndex,ProgramSha256,
            TextureWidth,TextureHeight,LocalSize,ActualDispatch,RequiredConstantBufferBytesSha256,SourceEvidence)=
            (init.FrameNumber,init.OwnerIdentity,init.SourceSha256,pass,parameter,shader,shape.Width,shape.Height,local,dispatch,
                Convert.ToHexString(SHA256.HashData(init.ConstantBuffer!.Bytes.Span)),
                "original-Shaft-CS"+(pass-1)+";exact-program-SHA;unconditional-complete-typed-UAV-mip0-stores;"+
                (pass is 4 or 5?"four-consecutive-pixels-per-thread-k0..3;":"final-store-DispatchThreadID.xy;")+init.SourceEvidence);
    public long FrameNumber{get;}
    public string ContextOwnerIdentity{get;}
    public string SourceSha256{get;}
    public int SourcePassIndex{get;}
    public int ParameterIndex{get;}
    public string ProgramSha256{get;}
    public uint TextureMipLevel=>0;
    public uint TextureWidth{get;}
    public uint TextureHeight{get;}
    public MapPostEffect3DispatchExtent ActualDispatch{get;}
    public MapPostEffect3DispatchExtent LocalSize{get;}
    public int RequiredConstantBufferParameterIndex=>1;
    public string RequiredConstantBufferBytesSha256{get;}
    public string SourceEvidence{get;}
    public bool EstablishesGpuCompletion=>false;

    public static MapPostEffect3ShaftWriteCoverage Prepare(MapPostEffect3AllocationPlan allocation,
        MapPostEffect3ParameterLightShaftInitialization init,int sourcePassIndex)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(init);
        MapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if(sourcePassIndex is <1 or >6||!init.BeforeHookAllowsPasses||!init.InitializationReturnedTrue||
            init.SourceSha256!=allocation.Source.Sha256||init.OwnerIdentity!=allocation.ContextOwnerIdentity||init.FrameNumber<=0||
            init.ConstantBuffer is not{} cb||cb.Declaration.Index!=1||cb.Bytes.Length!=144||cb.Locale!=init.Locale||
            cb.Writer!=MapPostEffect3ConstantBufferWriter.LightShaft||
            cb.FrameNumber!=init.FrameNumber||cb.OwnerIdentity!=init.OwnerIdentity||cb.SourceSha256!=init.SourceSha256)
            throw new InvalidDataException("Shaft coverage requires this actual enabled source Init and pass1..6.");
        var links=allocation.Passes[sourcePassIndex];int shaderIndex=sourcePassIndex-1;
        if(links.Pass.ProgramReferences.Where(p=>p.StageIndex is not null).ToArray() is not [{StageCode:5}]||
            links.Pass.ProgramReferences.Single(p=>p.StageIndex is not null).StageIndex!=shaderIndex||
            links.DispatchExtent is not{} dispatch)
            throw new InvalidDataException("Shaft coverage selected another source program/dispatch.");
        var program=allocation.Source.GetProgram(5,shaderIndex);string sha=ShaderHashes[shaderIndex];
        if(program.Sha256!=sha||Convert.ToHexString(SHA256.HashData(program.Bytes.Span))!=sha)
            throw new InvalidDataException("Shaft complete-store program differs from its audited original source.");
        var part=program.Container.Parts.Single(p=>p.TokenHeader is not null);
        var raw=part.PayloadRange.Slice(program.Bytes.Span);
        var local=sourcePassIndex switch{4=>new MapPostEffect3DispatchExtent(1,64,1),
            5=>new(64,1,1),_=>new(16,16,1)};
        int localWord=sourcePassIndex==1?36:sourcePassIndex is 4 or 5?15:22;
        Check(raw,localWord,[0x0400009B,local.X,local.Y,local.Z]);
        int output=sourcePassIndex is 1 or 3 or 5?8:9;
        uint uavRegister=output==8?0u:1u;
        if(sourcePassIndex is not (4 or 5))
        {
            int at=sourcePassIndex switch{1=>300,2=>394,_=>307};
            Check(raw,at,[0x060000A4,0x0011E0F2,uavRegister,0x00020546,0x00100E46,0,0x0100003E]);
            if(sourcePassIndex is 3 or 6)
            {
                // Fixed i=0; signed i>=4 break; i++; endloop. No dependent
                // conditional exits or stores. Final typed store follows it.
                Check(raw,104,[0x05000036,0x00100012,2,0x00004001,0,0x01000030,
                    0x07000021,0x00100022,2,0x0010000A,2,0x00004001,4,0x03040003,0x0010001A,2]);
                Check(raw,279,[0x0700001E,0x00100012,2,0x0010000A,2,0x00004001,1,0x01000016]);
            }
        }
        else
        {
            // Original CS3/4 coordinates are (4*x,y)/(x,4*y). Four
            // unconditional stores cover offsets0,1,2,3 along that axis.
            Check(raw,27,sourcePassIndex==4?
                [0x04000036,0x00100022,0,0x0002001A,0x06000029,0x00100012,0,0x0002000A,0x00004001,2]:
                [0x04000036,0x00100012,0,0x0002000A,0x06000029,0x00100022,0,0x0002001A,0x00004001,2]);
            Check(raw,233,[0x070000A4,0x0011E0F2,uavRegister,0x00100546,0,0x00100E46,2]);
            Check(raw,366,[0x070000A4,0x0011E0F2,uavRegister,0x00100E46,10,0x00100E46,1]);
            Check(raw,625,[0x070000A4,0x0011E0F2,uavRegister,0x00100E46,3,0x00100E46,1,
                0x070000A4,0x0011E0F2,uavRegister,0x00100E46,4,0x00100E46,2,0x0100003E]);
        }
        var outputRows=links.ActiveBindings.Where(r=>r.Table==MapPostEffect3BindingTable.TextureParameters&&
            r.Role==MapPostEffect3BindingRole.UnorderedAccessReadWrite).ToArray();
        if(outputRows.Length!=2||outputRows.Any(r=>r.ParameterIndex!=output||r.RegisterIndex!=uavRegister||
            r.MipSelector!=-1||r.SliceSelector!=-1))
            throw new InvalidDataException("Shaft source complete store selects another UAV subresource.");
        var shape=MapPostEffect3AllocationShapeReader.Read(allocation.TextureAllocations.Single(a=>a.Parameter.Index==output));
        ulong widthCoverage=(ulong)dispatch.X*local.X*(sourcePassIndex==4?4u:1u),
            heightCoverage=(ulong)dispatch.Y*local.Y*(sourcePassIndex==5?4u:1u);
        if(shape.Dimension!=MapPostEffect3TextureDimension.Texture2D||shape.MipLevels!=1||!shape.AllowsUnorderedAccess||
            widthCoverage<shape.Width||heightCoverage<shape.Height||dispatch.Z!=1||local.Z!=1)
            throw new NotSupportedException("Original Shaft dispatch does not prove all allocated target coordinates are written.");
        return new(allocation,init,sourcePassIndex,output,sha,shape,local,dispatch);
    }
    private static void Check(ReadOnlySpan<byte> raw,int word,uint[] expected)
    {for(int i=0;i<expected.Length;i++)if(BinaryPrimitives.ReadUInt32LittleEndian(raw.Slice((word+i)*4,4))!=expected[i])
        throw new InvalidDataException("Original Shaft audited store/control/coordinate operands changed.");}
    private static readonly string[] ShaderHashes=[
        "F4214366D2845314DEEB6CC0389D9E4AFA2904482ABFAD33695D72DEBCF19188",
        "0063F3BBCBD3E4DACAB8570CCD69511F1813F216FB177550FE81894A9FA9D41D",
        "923ACA4B10592E4EB713BCCC3321CCC3FD164D6F7BE67CC6CDE557D4FF37ACA3",
        "8F35919A50F26129EB7FDA25DACD8F24A9CA3E4411C5B256494F5CD540F45E1C",
        "987BBE89FF3A448CA6FA7AC1C2FD0BE80C97C059AB6D7B377F30A210D0E4CB83",
        "33B26C788559D292439AFE7007FA99853BCBCCEBE35389FCAB8D04AA56E28DE7"];
}
