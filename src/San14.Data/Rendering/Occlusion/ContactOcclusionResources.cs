using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ContactOcclusionProgram(int Index,uint StageCode,ReadOnlyMemory<byte> Bytes,
    DxbcResourceDefinitions Definitions);

/// <summary>Static node63 resources. Current Camera, View population and light
/// admission belong to the accepted-frame owner, independently of this bank.</summary>
public sealed partial class ContactOcclusionResources
{
    private readonly ContactOcclusionProgram[] programs;
    private readonly byte[][] samplers;
    private readonly byte[] noise;
    private readonly RenderTextureFormats[][] targetCandidates;
    private readonly ModelNativeColorAttachment compositionBlend;

    private ContactOcclusionResources(DeferredEarlySubShadowDeclaration declaration,
        LoadedRenderGraphTargets targets,LoadedShaderContainer shader,
        ContactOcclusionProgram[] programs,byte[][] samplers,byte[] noise,
        RenderTextureFormats[][] candidates,ModelNativeColorAttachment blend)
    {
        Declaration=declaration;Targets=targets;PrimaryShader=shader;PrimaryBindings=shader.ReadG1Bindings();
        this.programs=programs;this.samplers=samplers;this.noise=noise;
        targetCandidates=candidates;compositionBlend=blend;
    }

    public DeferredEarlySubShadowDeclaration Declaration{get;}
    public LoadedRenderGraphTargets Targets{get;}
    public LoadedShaderContainer PrimaryShader{get;}
    public G1ShaderBindingTables PrimaryBindings{get;}
    public IReadOnlyList<ContactOcclusionProgram> Programs=>Array.AsReadOnly(programs);
    public ContactOcclusionProgram GetProgram(int index)=>programs.Single(value=>value.Index==index);

    public static ContactOcclusionResources Load(GameResourceRepository repository,string locale)
    {
        var graph=repository.LoadRenderGraph("system",0x024496EE);
        var objects=repository.LoadObjects("system",0x718CCB8B);
        var textures=repository.LoadObjects("system",0x93FB3280);
        var node=graph.Document.Nodes[63];
        if(node.Call.FactoryClassId!=0x406CB6FF)
            throw new NotSupportedException("The selected Main node has no contact occlusion producer.");
        byte[] Field(uint name,uint descriptor,int length)
        {
            var field=node.Call.Arguments.Single(value=>value.NameHash==name);
            if(field.DescriptorWord!=descriptor||field.RawValue.Length!=length)
                throw new InvalidDataException("Contact occlusion declaration field has an incompatible type.");
            return field.RawValue;
        }
        uint Word(uint name)=>BinaryPrimitives.ReadUInt32LittleEndian(Field(name,0x05000001,4));
        byte[] banks=Field(0x71550276,0x05000008,32);
        var declaration=new DeferredEarlySubShadowDeclaration(node,Word(0x72DB7EBD),
            Field(0x9075EDA0,0x01000001,1)[0],Word(0xB7CCDD1F),Word(0xF43A3947),
            Word(0xD7B0F360),Word(0xD7B0FEA3),Field(0x05D92ACC,0x01000001,1)[0],
            Array.AsReadOnly(Enumerable.Range(0,8).Select(index=>BinaryPrimitives.ReadUInt32LittleEndian(banks.AsSpan(index*4))).ToArray()));
        if(declaration.ViewObjectId!=DefaultView0Source.ViewObjectId||
            (declaration.ViewId&0x7F)!=0||(declaration.SourceFlags&1)!=0)
            throw new NotSupportedException("Contact occlusion requires its normal Main View and target scope producer.");
        var provider=objects.Document.Objects.Single(value=>value.ObjectId==declaration.PrimaryShaderObjectId);
        var reference=provider.Properties.Single(value=>value.NameHash==ModelShaderResources.G1SPropertyHash);
        if(reference.TypeCode!=5||reference.ElementCount!=1||reference.RawValue.Length!=4)
            throw new InvalidDataException("Contact occlusion provider needs its declared G1S reference.");
        var shader=repository.LoadShaderContainer("system",BinaryPrimitives.ReadUInt32LittleEndian(reference.RawValue));
        var programs=shader.Container.StageTables.SelectMany(table=>table.Entries.Select(entry=>(table.StageCode,entry.ProgramIndex)))
            .Distinct().OrderBy(value=>value.ProgramIndex).Select(value=>new ContactOcclusionProgram(value.ProgramIndex,
                value.StageCode,shader.Container.GetProgramBytes(shader.Source.Bytes,value.ProgramIndex).ToArray(),
                shader.ReadResourceDefinitions(value.ProgramIndex))).ToArray();
        var native=RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale);
        byte[] temporary=RenderSamplerSourceAdapters.RenderTextureKey(native,1);
        var depth=RenderTextureResources.Read(textures,declaration.DepthObjectId);
        var normals=RenderTextureResources.Read(textures,declaration.NormalObjectId);
        byte[] depthKey=RenderSamplerSourceAdapters.FinalRenderTextureKey(native,depth);
        byte[] normalKey=RenderSamplerSourceAdapters.FinalRenderTextureKey(native,normals);
        byte[] Descriptor(byte[] key,Func<ushort,ushort> change)
        {
            key=key.ToArray();BinaryPrimitives.WriteUInt16LittleEndian(key,change(BinaryPrimitives.ReadUInt16LittleEndian(key)));
            return RenderSamplerSourceAdapters.DecodeKey(native,key).EncodeD3D11Descriptor();
        }
        byte[][] samplers=[Descriptor(temporary,word=>(ushort)((word&0x3F0)|5)),
            Descriptor(normalKey,word=>(ushort)(((word&0x3FF)&0xFFF5)|5)),
            Descriptor(temporary,word=>(ushort)(word&0x3F0)),
            Descriptor(depthKey,word=>(ushort)((word&0x3F0)|0x1405)),
            Descriptor(temporary,word=>(ushort)((word&0x3F0)|0x1405)),
            Descriptor(temporary,word=>(ushort)(word&0x3FF)),
            Descriptor(depthKey,word=>(ushort)((word&0x3F0)|5))];
        var colors=ModelFrontendColorSource.Load(repository,locale);
        var state=colors.ApplyNativeFlags(colors.CreateFreshFrontendState(),1,0x0130101000000100UL);
        var colorSlots=state.Slots.Select((slot,index)=>index==0?slot with{RgbWriteMask=0,AlphaWriteEnabled=1}:slot).ToArray();
        var blend=colors.ReadDrawAttachments(colors.ReadCurrentFrontend(colorSlots),1).Single();
        return new(declaration,RenderGraphTargetResources.Read(graph,62,textures),shader,programs,samplers,
            CreateNoise(),CreateTargetCandidates(),blend);
    }

    private static byte[] CreateNoise()
    {
        // The four-by-four producer owns an independent deterministic LCG.
        // Two draws write R/G; BGRA's B/A lanes remain source-defined zero.
        byte[] bytes=new byte[4*4*4];uint state=1;
        byte Next(){state=unchecked(state*0x10DCD+1);return (byte)((state>>16)/65535f*255f);}
        for(int pixel=0;pixel<16;pixel++){bytes[pixel*4+2]=Next();bytes[pixel*4+1]=Next();}
        return bytes;
    }

    private static RenderTextureFormats[][] CreateTargetCandidates()
    {
        // Source zero-format rows44/84/46/85/39 are unavailable and skipped.
        // Duplicate aliases preserve the original ordered device selection.
        byte[][] pixels=[[2],[2],[42,114,9,0,10,1],[42,114,9,0,10,1],[9,0,10,1]];
        return pixels.Select(values=>values.Select(pixel=>RenderTextureFormatMath.Resolve(pixel,0,0)).ToArray()).ToArray();
    }
}
