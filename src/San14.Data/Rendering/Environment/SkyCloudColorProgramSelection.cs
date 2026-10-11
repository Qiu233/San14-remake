using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Exact original pass0 contract for low-cost levels0/1/2 with
/// ray4, null Scene and the actual nonnull Cirrus owner. Renderer feature0
/// and all other retained axes remain bounded to this source scope.</summary>
public sealed class SkyCloudColorProgramSelection
{
    private readonly byte[] requested;
    private SkyCloudColorProgramSelection(LoadedShaderContainer shader,G2ShaderCombination combination,
        int row,uint level,byte[] requested,int vertex,int pixel,DxbcResourceDefinitions vertexDefinitions,DxbcResourceDefinitions pixelDefinitions)
    {Shader=shader;Combination=combination;VariantRow=row;LowCostLevel=level;this.requested=requested.ToArray();
        VertexProgram=vertex;PixelProgram=pixel;VertexDefinitions=vertexDefinitions;PixelDefinitions=pixelDefinitions;}
    public LoadedShaderContainer Shader{get;}
    public G2ShaderCombination Combination{get;}
    public int VariantRow{get;}
    public uint LowCostLevel{get;}
    public int VertexProgram{get;}
    public int PixelProgram{get;}
    public DxbcResourceDefinitions VertexDefinitions{get;}
    public DxbcResourceDefinitions PixelDefinitions{get;}
    public int VertexConstantByteLength=>160;
    public int PixelConstantByteLength=>512;
    public ReadOnlyMemory<byte> RequestedVariantBytes=>requested.ToArray();

    internal static SkyCloudColorProgramSelection ImportLevel(SkyCloudProgramResources resources,uint level)
    {
        var shader=resources.Shader;
        var tables=resources.Bindings;
        // D77E3D..53 performs an unsigned clamp of prepared30 to2.
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
        var vd=shader.ReadResourceDefinitions(vertex);var pd=shader.ReadResourceDefinitions(pixel);
        if(vd.Buffers.Count!=1||vd.Buffers[0].ByteSize!=160||pd.Buffers.Count!=1||pd.Buffers[0].ByteSize!=512||
            pd.Bindings.Count(b=>b.InputType==2)!=7||pd.Bindings.Count(b=>b.InputType==3)!=7)
            throw new InvalidDataException("Cloud actual source pair differs from VS160/PS512/seven-texture RDEF.");
        return new(shader,combination,row.Index,level,requested,vertex,pixel,vd,pd);
    }
}
