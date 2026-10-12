using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed class SkyCloudMainPass
{
    internal SkyCloudMainPass(LoadedShaderContainer shader,uint pass,G2ShaderCombination combination)
    {
        Shader=shader;Pass=pass;Combination=combination;
        if(combination.Stages.Count(stage=>stage.Entry is not null)!=2 ||
            combination.Stages.Any(stage=>stage.Entry is not null&&stage.StageCode is not(0 or 1)))
            throw new NotSupportedException("Main Cloud requires one vertex and one pixel program.");
        VertexProgram=combination.Stages.Single(stage=>stage.StageCode==1).Entry!.ProgramIndex;
        PixelProgram=combination.Stages.Single(stage=>stage.StageCode==0).Entry!.ProgramIndex;
        VertexDefinitions=shader.ReadResourceDefinitions(VertexProgram);
        PixelDefinitions=shader.ReadResourceDefinitions(PixelProgram);
    }
    public LoadedShaderContainer Shader{get;}
    public uint Pass{get;}
    public G2ShaderCombination Combination{get;}
    public int VertexProgram{get;}
    public int PixelProgram{get;}
    public DxbcResourceDefinitions VertexDefinitions{get;}
    public DxbcResourceDefinitions PixelDefinitions{get;}
    public byte[] CopyVertexBytes()=>Shader.Container.GetProgramBytes(Shader.Source.Bytes,VertexProgram).ToArray();
    public byte[] CopyPixelBytes()=>Shader.Container.GetProgramBytes(Shader.Source.Bytes,PixelProgram).ToArray();
}

/// <summary>The Main color, history and composite programs resolved from one
/// current renderer feature and one current Cloud variant.</summary>
public sealed class SkyCloudMainProgramSelection
{
    private SkyCloudMainProgramSelection(SkyCloudProgramResources resources,int feature,int variant)
    {
        Resources=resources;FeatureIndex=feature;VariantRow=variant;
        SkyCloudMainPass Select(uint pass)=>new(resources.Shader,pass,
            resources.Bindings.ResolveCombination([(uint)feature,0,0,0,0,0,(uint)variant],pass));
        Color=Select(1);History=Select(2);Composite=Select(4);
    }
    public SkyCloudProgramResources Resources{get;}
    public int FeatureIndex{get;}
    public int VariantRow{get;}
    public SkyCloudMainPass Color{get;}
    public SkyCloudMainPass History{get;}
    public SkyCloudMainPass Composite{get;}
    internal static SkyCloudMainProgramSelection Import(SkyCloudProgramResources resources,int feature,uint level,uint rays,bool earth)
    {
        var tables=resources.Bindings;
        var selected=tables.Features[feature].Feature.Words;
        if((selected[6],selected[7]) is not ((7,2) or (0,1)))
            throw new NotSupportedException("Main Cloud requires a supported current renderer/profile interface.");
        byte[] request=tables.Variants.Records[0].RawBytes.ToArray();
        foreach(var edit in new[]{(0,level),(4,rays),(8,earth?0u:1u),(12,1u),(16,0u),(20,1u),(32,0u)})
            BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(edit.Item1),edit.Item2);
        var variant=tables.Variants.Match(request)??throw new NotSupportedException("Main Cloud current binding variant is absent.");
        return new(resources,feature,variant.Index);
    }
}
