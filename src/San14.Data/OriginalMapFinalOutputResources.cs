using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalMapFinalOutputProgramSource(string SourceResourceUri,
    int VertexProgramIndex,int PixelProgramIndex,ReadOnlyMemory<byte> VertexBytes,
    ReadOnlyMemory<byte> PixelBytes,string VertexSha256,string PixelSha256);
public sealed record OriginalMapFinalOutputSpkProgram(int Index,uint StageCode,
    int SourceOffset,ReadOnlyMemory<byte> Bytes,string Sha256);
public sealed record LoadedOriginalMapFinalOutputResources(LoadedRenderGraph Graph,
    CompiledGraphNode HdrNode,CompiledGraphNode ApplyNode,LoadedObjects TextureObjects,
    LoadedObjects Parameters,DokObject Parameter,LoadedObjects PostEffect,DokObject PostEffectObject,
    OriginalRenderTextureDefinition SceneTexture,OriginalRenderTextureDefinition HdrTexture,
    OriginalRenderTextureDefinition ExposureTexture,uint ParameterFlags,
    IReadOnlyList<OriginalMapFinalOutputSpkProgram> SpkPrograms,LoadedShaderContainer AuxiliaryShader,
    OriginalRenderTextureDefinition OutputTexture,LoadedOriginalMapExposureSource StockScreen);

/// <summary>Exact node192/193 source documents. The caller supplies the native
/// branch context separately: these source references never choose an active
/// camera route, manufacture effect inputs or identify a swapchain.</summary>
public static class OriginalMapFinalOutputResources
{
    public const uint HdrFactory=0x28916AD0,ApplyFactory=0xB2D30063;
    public const uint AuxiliaryShaderId=0x40BC6503;
    public const string AuxiliaryShaderSha256="E1FD1C006B4A58BDD4012BE10068553A9BE9AF24A400BE2AB99A15FBF5BE0078";
    public const string PostEffectSha256="37D6A0845B404715D11C394B25ECC3426F74B1D16B12A1675EDEA5E529680C69";
    public const string SpkSha256="D0D7AABAABB391CD4E3EA823E8751AFD592A6F662C2A319E6937F62F9CBD12B3";

    public static LoadedOriginalMapFinalOutputResources Read(OriginalGameRepository repository,
        LoadedRenderGraph graph,LoadedObjects textureObjects,LoadedObjects parameters,LoadedObjects postEffect)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(textureObjects);ArgumentNullException.ThrowIfNull(parameters);ArgumentNullException.ThrowIfNull(postEffect);
        if(graph.Document.GraphKind!=OriginalCompiledGraphKind.RenderGraph||graph.Document.Nodes.Count<=193)
            throw new InvalidDataException("Final map output requires the explicit original node192/193 render graph.");
        var hdr=graph.Document.Nodes[192];var apply=graph.Document.Nodes[193];
        if(hdr.Call.FactoryClassId!=HdrFactory||apply.Call.FactoryClassId!=ApplyFactory||
            !hdr.Call.ConnectedNodeIndices.Contains(193u))
            throw new InvalidDataException("Final map output source is outside the node192 HDR -> node193 Apply contract.");
        byte B(CompiledGraphNode n,uint hash)=>Field(n,hash,0x01000001,1).RawValue[0];
        uint W(CompiledGraphNode n,uint hash)=>BinaryPrimitives.ReadUInt32LittleEndian(Field(n,hash,0x05000001,4).RawValue);
        if(B(hdr,0x05BE6503)!=1||W(hdr,0x42820CA6)!=0||W(hdr,0xF1BA6952)!=0||B(hdr,0x9075EDA0)!=0||
            B(apply,0x9075EDA0)!=0||W(apply,0x72DB7EBD)!=0xE1B6A687||W(apply,0xCA398DA4)!=0)
            throw new InvalidDataException("Final map source requires HDR mode1, view0/E1, scalar surface0 and no composite GBuffers.");
        uint effectId=W(apply,0x17916AA9),paramId=W(apply,0xA773922C);
        if(effectId!=0xA1C83E34||paramId!=0x9C288AE0)
            throw new InvalidDataException("Final map source PostEffect/parameter keys differ from the recovered source.");
        var effect=Object(postEffect,effectId,0x1D8D8BB6);var parameter=Object(parameters,paramId,0x33429E29);
        var flags=Property(parameter,0x72B09F90,5,1,4);
        uint parameterFlags=BinaryPrimitives.ReadUInt32LittleEndian(flags.RawValue);
        var payload=Property(effect,0x7AE38475,1,116348,116348).RawValue.ToArray();
        if(postEffect.Source.DecodedSha256!=PostEffectSha256||Hash(postEffect.Source.Bytes)!=PostEffectSha256)
            throw new InvalidDataException("Final map PostEffect resource differs from its recovered source identity.");
        var spk=ReadSpk(payload);
        var scene=OriginalRenderTextureResources.Read(textureObjects,W(apply,0x51332A47));
        var target=OriginalRenderTextureResources.Read(textureObjects,W(hdr,0xD208055E));
        var exposure=OriginalRenderTextureResources.Read(textureObjects,0x6C5630BB);
        if(scene.Object.ObjectId!=0x064B746B||scene.Kind!=OriginalRenderTextureKind.Texture2D||
            target.Object.ObjectId!=0x881E16F9||target.Kind!=OriginalRenderTextureKind.Texture2D||
            target.PixelFormat!=0x6B||target.ColorSpace!=0||target.OutputColorSpace!=0||
            target.LevelNum!=1||target.MinMipLevel!=0||target.MultiSample!=0||
            exposure.Width!=2||exposure.Height!=1)
            throw new InvalidDataException("Final map scene/HDR/Exposure texture declarations differ from the source contract.");
        var auxiliary=repository.LoadShaderContainer("system",AuxiliaryShaderId);ValidateAuxiliary(auxiliary);
        var output=OriginalRenderTextureResources.Read(textureObjects,0x13BF771D);
        if(output.Kind!=OriginalRenderTextureKind.Texture2D||output.PixelFormat!=1||output.ColorSpace!=1||output.OutputColorSpace!=1)
            throw new InvalidDataException("Final output requires the exact source Output BGRA8 sRGB target/view declaration.");
        return new(graph,hdr,apply,textureObjects,parameters,parameter,postEffect,effect,scene,target,exposure,
            parameterFlags,spk,auxiliary,output,OriginalMapExposureSource.Load(repository,"SC"));
    }

    public static LoadedOriginalMapFinalOutputResources Load(OriginalGameRepository repository)
        =>Read(repository,repository.LoadRenderGraph("system",0x024496EE),repository.LoadObjects("system",0x93FB3280),
            repository.LoadObjects("system",0x908AB6E2),repository.LoadObjects("system",0xA1C83E34));

    public static OriginalMapFinalOutputProgramSource AuxiliaryProgram(LoadedOriginalMapFinalOutputResources source,int explicitCombination)
    {
        ArgumentNullException.ThrowIfNull(source);ValidateAuxiliary(source.AuxiliaryShader);
        var combination=source.AuxiliaryShader.ReadG1ResourceBindings(explicitCombination).Combination;
        var stages=combination.Stages.Where(s=>s.Entry is not null).ToArray();
        if(stages.Length!=2||stages.Count(s=>s.StageCode==1)!=1||stages.Count(s=>s.StageCode==0)!=1)
            throw new InvalidDataException("Final auxiliary source requires one exact original VS/PS pair.");
        int vi=stages.Single(s=>s.StageCode==1).Entry!.ProgramIndex,pi=stages.Single(s=>s.StageCode==0).Entry!.ProgramIndex;
        var vs=source.AuxiliaryShader.Container.GetProgramBytes(source.AuxiliaryShader.Source.Bytes,vi).ToArray();
        var ps=source.AuxiliaryShader.Container.GetProgramBytes(source.AuxiliaryShader.Source.Bytes,pi).ToArray();
        return new(source.AuxiliaryShader.Source.ResourceUri,vi,pi,vs,ps,Hash(vs),Hash(ps));
    }

    public static OriginalMapFinalOutputProgramSource SpkProgram(LoadedOriginalMapFinalOutputResources source,
        int explicitVertexProgram,int explicitPixelProgram)
    {
        ArgumentNullException.ThrowIfNull(source);
        var vs=source.SpkPrograms.SingleOrDefault(p=>p.StageCode==1&&p.Index==explicitVertexProgram)
            ??throw new InvalidDataException("Selected source PostEffect vertex program is absent.");
        var ps=source.SpkPrograms.SingleOrDefault(p=>p.StageCode==0&&p.Index==explicitPixelProgram)
            ??throw new InvalidDataException("Selected source PostEffect pixel program is absent.");
        return new(source.PostEffect.Source.ResourceUri+"#SPK",vs.Index,ps.Index,vs.Bytes.ToArray(),ps.Bytes.ToArray(),vs.Sha256,ps.Sha256);
    }

    public static IReadOnlyList<OriginalMapFinalOutputSpkProgram> ReadSpk(ReadOnlySpan<byte> source)
    {
        if(source.Length!=116348||Hash(source)!=SpkSha256||!source[..8].SequenceEqual("_SPK4100"u8)||
            U(source,8)!=10||U(source,12)!=source.Length||U(source,16)!=100||U(source,0x4C)!=8||
            U(source,0x50)!=30||U(source,0x54)!=0x57EC)
            throw new InvalidDataException("PostEffect source is outside the recovered SPK4100 directory contract.");
        int cursor=0x57EC;var result=new List<OriginalMapFinalOutputSpkProgram>();
        for(int stage=0;stage<2;stage++)
            for(int index=0;index<(stage==0?30:8);index++)
            {
                if(stage==1){Require(source,cursor,8);if(U(source,cursor)!=4)throw new InvalidDataException("Source PostEffect VS directory prefix differs.");cursor+=8;}
                Require(source,cursor,4);int length=checked((int)U(source,cursor));cursor+=4;Require(source,cursor,length);
                byte[] bytes=source.Slice(cursor,length).ToArray();var dxbc=DxbcContainerReader.Read(bytes);
                var programs=dxbc.Parts.Where(p=>p.TokenHeader is not null).ToArray();
                if(programs.Length!=1||(programs[0].TokenHeader!.RawVersionToken>>16)!=(uint)stage)
                    throw new InvalidDataException("PostEffect program stage differs from its original directory.");
                result.Add(new(index,(uint)stage,cursor,bytes,Hash(bytes)));cursor+=length;
            }
        if(cursor!=source.Length)throw new InvalidDataException("PostEffect program directory does not consume the complete source.");
        return result.AsReadOnly();
    }

    private static void ValidateAuxiliary(LoadedShaderContainer source)
    {
        if(source.Source.FileId!=AuxiliaryShaderId||source.Source.DecodedSha256!=AuxiliaryShaderSha256||
            Hash(source.Source.Bytes)!=AuxiliaryShaderSha256||source.Container.Programs.Count!=10)
            throw new InvalidDataException("Final auxiliary G1S differs from its recovered complete source.");
    }
    private static TaskGraphArgument Field(CompiledGraphNode node,uint hash,uint packed,int bytes)
    {
        var fields=node.Call.Arguments.Where(p=>p.NameHash==hash&&p.DescriptorWord==packed).ToArray();
        return fields.Length==1&&fields[0].RawValue.Length==bytes?fields[0]:
            throw new InvalidDataException($"Final source node{node.Index} has no unique typed field{hash:X8}.");
    }
    private static DokObject Object(LoadedObjects source,uint key,uint type)
    {
        var objects=source.Document.Objects.Where(o=>o.ObjectId==key).ToArray();
        return objects.Length==1&&objects[0].ClassId==type&&objects[0].Kind==DokObjectKind.Inline?objects[0]:
            throw new InvalidDataException($"Final source object{key:X8} has no exact inline class in{source.Source.ResourceUri}.");
    }
    private static DokProperty Property(DokObject obj,uint hash,uint type,uint count,int length)
    {
        var properties=obj.Properties.Where(p=>p.NameHash==hash).ToArray();
        return properties.Length==1&&properties[0].TypeCode==type&&properties[0].ElementCount==count&&properties[0].RawValue.Length==length?properties[0]:
            throw new InvalidDataException($"Final source object{obj.ObjectId:X8} has no unique typed property{hash:X8}.");
    }
    private static uint U(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset,4));
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    private static void Require(ReadOnlySpan<byte> source,int at,int count)
    {if(at<0||count<0||(long)at+count>source.Length)throw new InvalidDataException("PostEffect program exceeds its source bounds.");}
}
