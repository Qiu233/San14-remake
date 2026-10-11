using Godot;
using San14.Data;
using San14.Data.Formats;

public enum Sky2Operation { Atmosphere, Contribution }

/// <summary>Caller-selected original Sky2 source pair. Does not infer the active
/// native pass, variant, atmosphere tables or current camera/light constants.</summary>
public sealed class Sky2Program : IAsyncDisposable
{
    public LoadedShaderContainer Source { get; }
    public G2ShaderCombination Combination { get; }
    public Sky2Operation Operation { get; }
    public ShaderSpirv VertexTranslation { get; }
    public AdaptedVertexShader VertexAdapter { get; }
    public ShaderSpirv PixelTranslation { get; }
    public Sky2NullTextureSpirv? NullTextureAdapter { get; }
    public TranslatedShaderBindings VertexBindings { get; }
    public TranslatedShaderBindings PixelBindings { get; }
    public uint PositionLocation { get; }
    public int VertexConstantByteLength => VertexBindings.SourceDefinitions.Buffers.Single().ByteSize;
    public int PixelConstantByteLength => PixelBindings.SourceDefinitions.Buffers.Single().ByteSize;
    public Rid ShaderRid { get; }
    private readonly Sky2NullTextureBinding[] nullBindings;
    public Sky2TextureBindingSnapshot TextureBindings { get; }
    private readonly Sky2LoadedNullTexturePacketInterface? nullPacketInterface;
    private Task? _disposal;
    private readonly object _disposalGate=new();

    public void RequireCurrent()
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        TextureBindings.RequireCurrent();
    }

    public void ValidateNullTexturePacket(ReadOnlySpan<byte> pixelPacket)
    {
        RequireCurrent();
        nullPacketInterface?.Validate(nullBindings,PixelBindings.SourceDefinitions,pixelPacket);
    }

    private Sky2Program(LoadedShaderContainer source,G2ShaderCombination combination,Sky2Operation operation,
        ShaderSpirv vertex,AdaptedVertexShader adapter,ShaderSpirv pixel,
        TranslatedShaderBindings vb,TranslatedShaderBindings pb,uint location,Rid shader,
        Sky2NullTextureSpirv? nullAdapter,Sky2NullTextureBinding[] nullTokens,
        Sky2TextureBindingSnapshot textureBindings,Sky2LoadedNullTexturePacketInterface? loadedNullInterface)
    {
        (Source,Combination,Operation,VertexTranslation,VertexAdapter,PixelTranslation,VertexBindings,PixelBindings,PositionLocation,ShaderRid,NullTextureAdapter)
            =(source,combination,operation,vertex,adapter,pixel,vb,pb,location,shader,nullAdapter);
        nullBindings=nullTokens;
        TextureBindings=textureBindings;nullPacketInterface=loadedNullInterface;
    }

    public static async Task<Sky2Program> BuildAsync(LoadedShaderContainer source,int explicitCombinationIndex,
        Vkd3dShaderCompiler compiler,Sky2Operation operation,
        Sky2TextureBindingSnapshot textureBindings)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(textureBindings);textureBindings.RequireCurrent();
        var combination=source.ReadG1ResourceBindings(explicitCombinationIndex).Combination;
        if(operation is not (Sky2Operation.Atmosphere or Sky2Operation.Contribution) ||
            (combination.Index%2==0)!=(operation==Sky2Operation.Atmosphere))
            throw new InvalidDataException("Sky2 operation must match the explicitly selected original pass.");
        if(combination.Stages.Any(s=>s.Entry is not null&&s.StageCode is not (0 or 1)))
            throw new InvalidDataException("Sky2 source requires vertex and pixel stages only.");
        var vs=combination.Stages.Single(s=>s.StageCode==1&&s.Entry is not null);
        var ps=combination.Stages.Single(s=>s.StageCode==0&&s.Entry is not null);
        var vertexBytes=source.Container.GetProgramBytes(source.Source.Bytes,vs.Entry!.ProgramIndex);
        var pixelBytes=source.Container.GetProgramBytes(source.Source.Bytes,ps.Entry!.ProgramIndex);
        int vertexIndex=vs.Entry!.ProgramIndex,pixelIndex=ps.Entry!.ProgramIndex;
        int vertexBytesExpected=vertexIndex switch{0=>160,1 or 2=>144,
            _=>throw new InvalidDataException("Unknown original Sky2 vertex program.")};
        int pixelBytesExpected=pixelIndex switch{3 or 4 or 10 or 12 or 13=>256,
            5 or 6 or 11 or 14 or 15=>336,7 or 16=>64,8 or 9 or 17 or 18=>128,
            _=>throw new InvalidDataException("Unknown original Sky2 pixel program.")};
        int pixelDescriptorsExpected=pixelIndex switch{
            3 or 8 or 10 or 12 or 17=>11,5 or 11 or 14=>13,
            6 or 15=>7,4 or 7 or 9 or 13=>5,16 or 18=>3,
            _=>throw new InvalidDataException("Unknown original Sky2 texture inventory.")};
        var vertex=compiler.Compile(vertexBytes,1);var pixel=compiler.Compile(pixelBytes);
        var adapter=VertexShaderAdapter.AdaptPositionInput(vertex.Bytes);
        var vb=TranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(vs.Entry.ProgramIndex),adapter.Bytes);
        var pb=TranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(ps.Entry.ProgramIndex),pixel.Bytes);
        var position=vb.Interface.StageVariables.Where(v=>v.StorageClass==1&&v.Location is not null).ToArray();
        if(position.Length!=1||position[0].Type is not {Kind:"vector",ComponentCount:2,ComponentType:{Kind:"float",Bits:32}}||
            vb.Bindings.Count!=1||pb.Bindings.Count!=pixelDescriptorsExpected||
            vb.Bindings.Any(b=>b.Device.DescriptorSet!=1)||pb.Bindings.Any(b=>b.Device.DescriptorSet!=0))
            throw new InvalidDataException("Sky2 vertex input or stage descriptor layout differs from the original pair.");
        foreach(var pair in new[]{(bindings:vb,bytes:vertexBytesExpected),(bindings:pb,bytes:pixelBytesExpected)})
        {
            var cb=pair.bindings.Bindings.Single(b=>b.DescriptorKind=="UniformBuffer");
            if(cb.Source.BindPoint!=0||cb.ConstantBuffer?.ByteSize!=pair.bytes||cb.Device.RequiredBufferBytes!=pair.bytes)
                throw new InvalidDataException("Sky2 constant buffer size differs from the original source.");
        }
        Sky2NullTextureSpirv? nullAdapter=null;
        var sampledSlots=pb.Bindings.Where(b=>b.DescriptorKind=="Texture").Select(b=>b.Source.BindPoint).ToHashSet();
        var nullTokens=new[]{SkySamplingRole.Earth,SkySamplingRole.MilkyWay,SkySamplingRole.Moon}
            .Where(role=>sampledSlots.Contains(SkySamplingResources.PixelRegister(role))&&textureBindings.GetTexture(role) is null)
            .Select(textureBindings.RequireNull).ToArray();
        if(nullTokens.Length>0)
        {
            nullAdapter=Sky2NullTextureAdapter.Adapt(pixel,pb,nullTokens);
            var finalInterface=SpirvShaderInterfaceReader.Read(nullAdapter.Bytes);
            pb=new(finalInterface,pb.SourceDefinitions,Array.AsReadOnly(pb.Bindings
                .Where(b=>b.DescriptorKind is not ("Texture" or "Sampler") || !nullAdapter.NullPixelRegisters.Contains(b.Source.BindPoint))
                .Select(b=>b with{Device=finalInterface.Descriptors.Single(v=>v.VariableId==b.Device.VariableId)}).ToArray()));
        }
        // Complete source/interface import before acquiring the shader RID.
        var loadedNullInterface=nullAdapter is null?null:
            Sky2NullTextureAdapter.ReadLoadedPacketInterface(nullAdapter,pb.SourceDefinitions);
        Rid shader=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            textureBindings.RequireCurrent();
            using var stages=new RDShaderSpirV();
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex,adapter.Bytes);
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment,nullAdapter?.Bytes??pixel.Bytes);
            Rid rid=rd.ShaderCreateFromSpirV(stages,"OriginalSky2"+operation);
            if(!rid.IsValid)throw new InvalidDataException("Device rejected original Sky2 shader pair.");
            return rid;
        });
        return new(source,combination,operation,vertex,adapter,pixel,vb,pb,position[0].Location!.Value,shader,
            nullAdapter,nullTokens,textureBindings,loadedNullInterface);
    }

    public ValueTask DisposeAsync()
    {
        lock(_disposalGate)
        {
            if(_disposal is null||_disposal.IsFaulted||_disposal.IsCanceled)
                _disposal=TerrainDeviceTexture.OnRenderThreadAsync(rd=>
                {rd.FreeRid(ShaderRid);return true;});
            return new(_disposal);
        }
    }
}
