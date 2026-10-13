using San14.Data.Formats;

namespace San14.Data;

public enum Sky2Operation { Atmosphere, Contribution }

/// <summary>CPU preparation of a selected Sky2 pass and its captured texture bindings.
/// Device upload and retirement belong to the client renderer.</summary>
public sealed class Sky2ProgramPreparation
{
    private readonly Sky2NullTextureBinding[] nullBindings;
    private readonly Sky2LoadedNullTexturePacketInterface? nullPacketInterface;
    public LoadedShaderContainer Source { get; }
    public G2ShaderCombination Combination { get; }
    public Sky2Operation Operation { get; }
    public ShaderSpirv VertexTranslation { get; }
    public AdaptedVertexShader VertexAdapter { get; }
    public ShaderSpirv PixelTranslation { get; }
    public TranslatedShaderBindings VertexBindings { get; }
    public TranslatedShaderBindings PixelBindings { get; }
    public uint PositionLocation { get; }
    public Sky2NullTextureSpirv? NullTextureAdapter { get; }
    public Sky2TextureBindingSnapshot TextureBindings { get; }
    public Sky2ConstantLayout VertexConstants { get; }
    public Sky2ConstantLayout PixelConstants { get; }
    private Sky2ProgramPreparation(LoadedShaderContainer source,G2ShaderCombination combination,Sky2Operation operation,
        ShaderSpirv vertex,AdaptedVertexShader adapter,ShaderSpirv pixel,
        TranslatedShaderBindings vb,TranslatedShaderBindings pb,uint location,
        Sky2NullTextureSpirv? nullAdapter,Sky2NullTextureBinding[] nullTokens,
        Sky2TextureBindingSnapshot textureBindings,Sky2LoadedNullTexturePacketInterface? loadedNullInterface)
    {
        (Source,Combination,Operation,VertexTranslation,VertexAdapter,PixelTranslation,VertexBindings,PixelBindings,PositionLocation,NullTextureAdapter)
            =(source,combination,operation,vertex,adapter,pixel,vb,pb,location,nullAdapter);
        nullBindings=nullTokens;TextureBindings=textureBindings;nullPacketInterface=loadedNullInterface;
        VertexConstants=Sky2ConstantLayout.ImportVertex(vb.SourceDefinitions);
        PixelConstants=Sky2ConstantLayout.ImportPixel(pb.SourceDefinitions);
    }
    public void ValidateNullTexturePacket(ReadOnlySpan<byte> packet)
    {
        TextureBindings.RequireCurrent();
        nullPacketInterface?.Validate(nullBindings,PixelBindings.SourceDefinitions,packet);
    }
    public static void RequireUnusedMoonColor(DxbcResourceDefinitions definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var moon=definitions.Buffers.Single().Variables.Single(v=>v.Name.Text=="g_vMoonRGB");
        if((moon.Flags&2)!=0)
            throw new InvalidDataException("A shader reading Moon color cannot consume the nonfinite zero-light amplitude.");
    }
    public static Sky2ProgramPreparation Prepare(LoadedShaderContainer source,int explicitCombinationIndex,
        Vkd3dShaderCompiler compiler,Sky2Operation operation,Sky2TextureBindingSnapshot textureBindings)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(textureBindings);textureBindings.RequireCurrent();
        var tables=source.ReadG1Bindings();
        var combination=source.ReadCombination(explicitCombinationIndex);
        if(operation is not (Sky2Operation.Atmosphere or Sky2Operation.Contribution) ||
            tables.PassCount!=2 || combination.Index%tables.PassCount!=(uint)operation)
            throw new InvalidDataException("Sky2 operation must match the explicitly selected original pass.");
        if(combination.Stages.Any(s=>s.Entry is not null&&s.StageCode is not (0 or 1)))
            throw new InvalidDataException("Sky2 source requires vertex and pixel stages only.");
        var vs=combination.Stages.Single(s=>s.StageCode==1&&s.Entry is not null);
        var ps=combination.Stages.Single(s=>s.StageCode==0&&s.Entry is not null);
        var vertexBytes=source.Container.GetProgramBytes(source.Source.Bytes,vs.Entry!.ProgramIndex);
        var pixelBytes=source.Container.GetProgramBytes(source.Source.Bytes,ps.Entry!.ProgramIndex);
        var vertex=compiler.Compile(vertexBytes,1);var pixel=compiler.Compile(pixelBytes);
        var adapter=VertexShaderAdapter.AdaptPositionInput(vertex.Bytes);
        var vb=TranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(vs.Entry.ProgramIndex),adapter.Bytes);
        var pb=TranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(ps.Entry.ProgramIndex),pixel.Bytes);
        var position=vb.Interface.StageVariables.Where(v=>v.StorageClass==1&&v.Location is not null).ToArray();
        if(position.Length!=1||position[0].Type is not {Kind:"vector",ComponentCount:2,ComponentType:{Kind:"float",Bits:32}}||
            vb.Bindings.Count!=1||
            vb.Bindings.Any(b=>b.Device.DescriptorSet!=1)||pb.Bindings.Any(b=>b.Device.DescriptorSet!=0))
            throw new InvalidDataException("Sky2 vertex input or stage descriptor layout differs from the original pair.");
        foreach(var stage in new[]{vb,pb})
        {
            var cb=stage.Bindings.Single(b=>b.DescriptorKind=="UniformBuffer");
            if(cb.Source.BindPoint!=0||cb.ConstantBuffer is not {} buffer||
                cb.Device.RequiredBufferBytes!=buffer.ByteSize)
                throw new InvalidDataException("Sky2 requires a complete source constant buffer at slot zero.");
        }
        Sky2NullTextureSpirv? nullAdapter=null;
        var sampledSlots=pb.Bindings.Where(b=>b.DescriptorKind=="Texture").Select(b=>b.Source.BindPoint).ToHashSet();
        var nullTokens=new[]{SkySamplingRole.Earth,SkySamplingRole.MilkyWay,SkySamplingRole.Moon}
            .Where(role=>sampledSlots.Contains(SkySamplingResources.PixelRegister(role))&&textureBindings.GetTexture(role) is null)
            .Select(textureBindings.RequireNull).ToArray();
        if(nullTokens.Length>0)
        {
            nullAdapter=Sky2NullTextureAdapter.Adapt(pixel,pb,nullTokens);
            var finalInterface=nullAdapter.Interface;
            pb=new(finalInterface,pb.SourceDefinitions,Array.AsReadOnly(pb.Bindings
                .Where(b=>b.DescriptorKind is not ("Texture" or "Sampler") || !nullAdapter.NullPixelRegisters.Contains(b.Source.BindPoint))
                .Select(b=>b with{Device=finalInterface.Descriptors.Single(v=>v.VariableId==b.Device.VariableId)}).ToArray()));
        }
        // Complete source/interface import before acquiring the shader RID.
        var loadedNullInterface=nullAdapter is null?null:
            Sky2NullTextureAdapter.ReadLoadedPacketInterface(nullAdapter,pb.SourceDefinitions);
        return new(source,combination,operation,vertex,adapter,pixel,vb,pb,position[0].Location!.Value,
            nullAdapter,nullTokens,textureBindings,loadedNullInterface);
    }
}
