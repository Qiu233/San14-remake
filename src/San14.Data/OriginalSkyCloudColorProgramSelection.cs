using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Exact original pass0 contract for low-cost levels0/1/2 with
/// ray4, null Scene and the actual nonnull Cirrus owner. Renderer feature0
/// and all other retained axes remain bounded to this source scope.</summary>
public sealed class OriginalSkyCloudColorProgramSelection
{
    private readonly byte[] requested;
    private OriginalSkyCloudColorProgramSelection(LoadedShaderContainer shader,G2ShaderCombination combination,
        int row,uint level,byte[] requested,int vertex,int pixel,string pixelSha)
    {Shader=shader;Combination=combination;VariantRow=row;LowCostLevel=level;this.requested=requested.ToArray();
        VertexProgram=vertex;PixelProgram=pixel;PixelSourceSha256=pixelSha;}
    public LoadedShaderContainer Shader{get;}
    public G2ShaderCombination Combination{get;}
    public int VariantRow{get;}
    public uint LowCostLevel{get;}
    public int VertexProgram{get;}
    public int PixelProgram{get;}
    public int VertexConstantByteLength=>160;
    public int PixelConstantByteLength=>512;
    public string PixelSourceSha256{get;}
    public ReadOnlyMemory<byte> RequestedVariantBytes=>requested.ToArray();
    public const string VertexSourceSha256="7C7B170B5B42F4FE227CBDD60E411A7E000432684E48128B4A3457FAB779EAF1";
    public const string FullCostPixelSourceSha256="5C080FC73AA9C556C60D9BFCAAA87889478C115AFBCC406C235774BF62D91884";
    public const string LowCostPixelSourceSha256="412C03E12CCC037BF8604BE0DB51D55DB138C4225D447C628BB7F84B6EF64A18";

    public static OriginalSkyCloudColorProgramSelection ReadCurrentSourceLevel(LoadedShaderContainer shader,uint sourceLowCostLevel)
    {
        ArgumentNullException.ThrowIfNull(shader);OriginalSkyAppliedSourceCloudSelectionOwner.RequireShader(shader);
        var tables=shader.ReadG1Bindings();
        if(tables.Variants.RecordWidth!=36||tables.Variants.Records.Count!=288||tables.PassCount!=6||tables.Features.Count!=2)
            throw new InvalidDataException("Cloud original binding table shape changed.");
        // D77E3D..53 performs an unsigned clamp of prepared30 to2.
        uint level=Math.Min(sourceLowCostLevel,2u);
        byte[] requested=tables.Variants.Records[0].RawBytes.ToArray();
        foreach(var word in new[]{(0,level),(4,4u),(8,1u),(12,1u),(16,0u),(20,0u)})
            BinaryPrimitives.WriteUInt32LittleEndian(requested.AsSpan(word.Item1,4),word.Item2);
        var row=tables.Variants.Match(requested)??throw new InvalidDataException("Cloud exact source request has no original variant row.");
        var combination=tables.Combinations.Rows[checked(row.Index*(int)tables.PassCount)];
        if(combination.Stages.Count(s=>s.Entry is not null)!=2||
            combination.Stages.Any(s=>s.Entry is not null&&s.StageCode is not(0 or 1)))
            throw new InvalidDataException("Cloud pass0 requires its exact vertex/pixel source pair.");
        int vertex=combination.Stages.Single(s=>s.StageCode==1).Entry!.ProgramIndex;
        int pixel=combination.Stages.Single(s=>s.StageCode==0).Entry!.ProgramIndex;
        string pixelSha=level==2?LowCostPixelSourceSha256:FullCostPixelSourceSha256;
        CheckProgram(vertex,VertexSourceSha256);CheckProgram(pixel,pixelSha);
        var vd=shader.ReadResourceDefinitions(vertex);var pd=shader.ReadResourceDefinitions(pixel);
        if(vd.Buffers.Count!=1||vd.Buffers[0].ByteSize!=160||pd.Buffers.Count!=1||pd.Buffers[0].ByteSize!=512||
            pd.Bindings.Count(b=>b.InputType==2)!=7||pd.Bindings.Count(b=>b.InputType==3)!=7)
            throw new InvalidDataException("Cloud actual source pair differs from VS160/PS512/seven-texture RDEF.");
        return new(shader,combination,row.Index,level,requested,vertex,pixel,pixelSha);
        void CheckProgram(int program,string expected)
        {
            ReadOnlySpan<byte> bytes=shader.Container.GetProgramBytes(shader.Source.Bytes,program);
            if(Convert.ToHexString(SHA256.HashData(bytes))!=expected)
                throw new InvalidDataException("Cloud current low-cost pair differs from its independently verified original program.");
        }
    }
    public static OriginalSkyCloudColorProgramSelection ReadExplicitAppliedCombination(LoadedShaderContainer shader,int combination)
    {
        for(uint level=0;level<3;level++)
        {
            var selected=ReadCurrentSourceLevel(shader,level);
            if(selected.Combination.Index==combination)return selected;
        }
        throw new NotSupportedException("Cloud applied color combination requires verified low-cost0/1/2, ray4, nullScene and Cirrus axes.");
    }
}
