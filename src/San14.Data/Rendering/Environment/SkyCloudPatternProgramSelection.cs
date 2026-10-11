using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Current Pattern draw inputs to the Cloud renderer's shader axes.
/// Fog mode belongs to the selected base light, not an arbitrary Main light.
/// Retained axes and actual texture presence are supplied by their owners.</summary>
public readonly record struct SkyCloudPatternProgramInputs(uint LowCostLevel,uint RayExponent,
    bool EarthTexturePresent,bool SkyDomeLightGroupNonzero,bool CirrusGroupPresent,uint LightFogMode);

public sealed class SkyCloudPatternProgramSelection
{
    private readonly byte[] requested;
    private SkyCloudPatternProgramSelection(SkyCloudProgramResources resources,int feature,int variant,byte[] request)
    {
        Resources=resources;FeatureIndex=feature;VariantRow=variant;requested=request.ToArray();
        Combination=resources.Bindings.ResolveCombination([(uint)feature,0,0,0,0,0,(uint)variant],5);
        if(Combination.Stages.Count(stage=>stage.Entry is not null)!=2||
            Combination.Stages.Any(stage=>stage.Entry is not null&&stage.StageCode is not(0 or 1)))
            throw new NotSupportedException("Cloud Pattern requires one vertex and one pixel program.");
        VertexProgram=Combination.Stages.Single(stage=>stage.StageCode==1).Entry!.ProgramIndex;
        PixelProgram=Combination.Stages.Single(stage=>stage.StageCode==0).Entry!.ProgramIndex;
        VertexDefinitions=Shader.ReadResourceDefinitions(VertexProgram);
        PixelDefinitions=Shader.ReadResourceDefinitions(PixelProgram);
    }
    public SkyCloudProgramResources Resources{get;}
    public LoadedShaderContainer Shader=>Resources.Shader;
    public int FeatureIndex{get;}
    public int VariantRow{get;}
    public G2ShaderCombination Combination{get;}
    public int VertexProgram{get;}
    public int PixelProgram{get;}
    public DxbcResourceDefinitions VertexDefinitions{get;}
    public DxbcResourceDefinitions PixelDefinitions{get;}
    public ReadOnlyMemory<byte> RequestedVariantBytes=>requested.ToArray();
    public byte[] CopyVertexBytes()=>Shader.Container.GetProgramBytes(Shader.Source.Bytes,VertexProgram).ToArray();
    public byte[] CopyPixelBytes()=>Shader.Container.GetProgramBytes(Shader.Source.Bytes,PixelProgram).ToArray();

    internal static byte[] PrepareVariant(ReadOnlySpan<byte> retained,SkyCloudPatternProgramInputs current)
    {
        if(retained.Length!=36)throw new InvalidDataException("Pattern selection needs the current nine-word Cloud variant.");
        byte[] request=retained.ToArray();
        uint[] edits=[Math.Min(current.LowCostLevel,2u),Math.Clamp(current.RayExponent,4u,6u),
            current.EarthTexturePresent&&current.SkyDomeLightGroupNonzero?0u:1u,current.CirrusGroupPresent?1u:0u,0];
        for(int word=0;word<edits.Length;word++)
            BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(word*4,4),edits[word]);
        return request;
    }
    internal static SkyCloudPatternProgramSelection Import(SkyCloudProgramResources resources,int feature,int variant,byte[] request)
        =>new(resources,feature,variant,request);
}
