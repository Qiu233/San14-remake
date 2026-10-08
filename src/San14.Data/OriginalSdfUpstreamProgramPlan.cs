using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalSdfUpstreamProgramSource(LoadedShaderContainer Shader,uint[] Dimensions,uint Pass,
    G2ShaderCombination Combination,int ProgramIndex,DxbcResourceDefinitions Definitions,string ProgramSha256);

/// <summary>Original SDFOCC compute programs with caller-resolved source
/// dimensions and pass. This contract never infers the native submission order.</summary>
public static class OriginalSdfUpstreamProgramPlan
{
    public static OriginalSdfUpstreamProgramSource Read(LoadedShaderContainer shader,
        IReadOnlyList<uint> explicitDimensions,uint explicitPass)
    {
        ArgumentNullException.ThrowIfNull(shader);ArgumentNullException.ThrowIfNull(explicitDimensions);
        if(shader.Source.FileId!=OriginalSdfFinalProducerPlan.ShaderFileId ||
            shader.Source.DecodedSha256!=OriginalSdfFinalProducerPlan.ShaderSha256 ||
            Hash(shader.Source.Bytes)!=OriginalSdfFinalProducerPlan.ShaderSha256)
            throw new InvalidDataException("SDF upstream requires exact original FD20CB1A bytes.");
        var dimensions=explicitDimensions.ToArray();var tables=shader.ReadG1Bindings();
        if(tables.PassCount!=5 || !tables.RawDimensionCounts.SequenceEqual(new uint[]{0,0,0,0,0,0,4}) || explicitPass>3)
            throw new InvalidDataException("SDF upstream source dimensions/pass differ.");
        var row=tables.ResolveCombination(dimensions,explicitPass);
        var active=row.Stages.Where(s=>s.Entry is not null).ToArray();
        if(active.Length!=1 || active[0].StageCode!=5)throw new InvalidDataException("SDF upstream row must be compute only.");
        int program=active[0].Entry!.ProgramIndex;
        if(program is <2 or >7)throw new InvalidDataException("SDF compute row is outside original programs2..7.");
        var bytes=shader.Container.GetProgramBytes(shader.Source.Bytes,program);
        return new(shader,dimensions,explicitPass,row,program,shader.ReadResourceDefinitions(program),Hash(bytes));
    }

    public static OriginalTranslatedShaderBindings ReadTranslatedBindings(OriginalSdfUpstreamProgramSource source,ReadOnlySpan<byte> spirv)
    {
        var module=SpirvShaderInterfaceReader.Read(spirv);
        if(module.EntryPoints.Count!=1 || module.EntryPoints[0].ExecutionModel!=5 || module.EntryPoints[0].Name!="main" ||
            module.ExecutionModes.Count(m=>m.Mode==17 && m.Operands.SequenceEqual(new uint[]{8,8,1}))!=1)
            throw new InvalidDataException("Original SDF compute entry/local8x8x1 changed.");
        var result=new List<OriginalTranslatedShaderBinding>();var matched=new HashSet<int>();
        foreach(var v in module.Descriptors)
        {
            string name=v.Name??throw new InvalidDataException("Unnamed SDF compute descriptor.");
            uint register;uint kind;
            if(name.StartsWith("cb0_",StringComparison.Ordinal)){register=0;kind=0;}
            else if(name.Length>1 && uint.TryParse(name[1..],out register))kind=name[0] switch{'s'=>3u,'t'=>2u,'u'=>4u,_=>99u};
            else throw new InvalidDataException("Unknown SDF compute descriptor register.");
            var candidates=source.Definitions.Bindings.Where(b=>b.BindPoint==register &&
                (b.InputType==kind || kind==2 && b.InputType==5)).ToArray();
            if(candidates.Length!=1 || candidates[0].BindCount!=1 || !matched.Add(candidates[0].Index) || v.DescriptorSet!=0 || v.Binding is null)
                throw new InvalidDataException("SDF compute descriptor does not uniquely match the original RDEF.");
            var b=candidates[0];DxbcRdefBuffer? cb=null;string descriptor;
            if(kind==0)
            {
                cb=source.Definitions.Buffers.Single(z=>z.Name.Text==b.Name.Text);
                if(v.StorageClass!=2 || v.Type is not{Kind:"struct",Block:true} || v.RequiredBufferBytes!=cb.ByteSize)
                    throw new InvalidDataException("SDF compute constant layout changed.");
                descriptor="UniformBuffer";
            }
            else if(kind==3)
            {
                if(v.StorageClass!=0 || v.Type.Kind!="sampler")throw new InvalidDataException("SDF compute sampler changed.");
                descriptor="Sampler";
            }
            else
            {
                uint dimension=b.Dimension switch{1=>5u,4=>1u,8=>2u,_=>throw new NotSupportedException("SDF compute resource dimension unsupported.")};
                bool component=b.ReturnType switch{4 or 6=>v.Type.ComponentType is{Kind:"int",Bits:32,Signed:false},
                    5=>v.Type.ComponentType is{Kind:"float",Bits:32},_=>false};
                if(v.StorageClass!=0 || v.Type.Kind!="image" || v.Type.Dimension!=dimension || v.Type.Arrayed!=false ||
                    v.Type.Multisampled!=false || v.Type.SampledCode!=(kind==4?2u:1u) || v.Type.ImageFormat!=0 || !component)
                    throw new InvalidDataException("SDF compute image layout changed.");
                descriptor=kind==4?(dimension==5?"ImageBuffer":"Image"):(dimension==5?"TextureBuffer":"Texture");
            }
            result.Add(new(v,b,cb,descriptor));
        }
        if(matched.Count!=source.Definitions.Bindings.Count)throw new InvalidDataException("SDF compute RDEF declarations missing from translation.");
        return new(module,source.Definitions,result.AsReadOnly());
    }
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
}
