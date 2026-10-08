using Godot;
using San14.Data;
using San14.Data.Formats;
using System.Security.Cryptography;

public enum OriginalSky2Operation { Atmosphere, Contribution }

/// <summary>Caller-selected original Sky2 source pair. Does not infer the active
/// native pass, variant, atmosphere tables or current camera/light constants.</summary>
public sealed class OriginalSky2Program : IAsyncDisposable
{
    public LoadedShaderContainer Source { get; }
    public G2ShaderCombination Combination { get; }
    public OriginalSky2Operation Operation { get; }
    public OriginalShaderSpirv VertexTranslation { get; }
    public OriginalAdaptedVertexShader VertexAdapter { get; }
    public OriginalShaderSpirv PixelTranslation { get; }
    public OriginalSky2NullTextureSpirv? NullTextureAdapter { get; }
    public OriginalTranslatedShaderBindings VertexBindings { get; }
    public OriginalTranslatedShaderBindings PixelBindings { get; }
    public uint PositionLocation { get; }
    public int VertexConstantByteLength => VertexBindings.OriginalDefinitions.Buffers.Single().ByteSize;
    public int PixelConstantByteLength => PixelBindings.OriginalDefinitions.Buffers.Single().ByteSize;
    public Rid ShaderRid { get; }
    private readonly OriginalSky2SourceNull2D[] sourceNullProofs;
    private readonly OriginalSky2LoadedNullTexturePacketInterface? nullPacketInterface;
    private Task? _disposal;

    public void ValidateNullTexturePacket(ReadOnlySpan<byte> pixelPacket)
    {
        nullPacketInterface?.Validate(sourceNullProofs,PixelBindings.OriginalDefinitions,pixelPacket);
    }

    private OriginalSky2Program(LoadedShaderContainer source,G2ShaderCombination combination,OriginalSky2Operation operation,
        OriginalShaderSpirv vertex,OriginalAdaptedVertexShader adapter,OriginalShaderSpirv pixel,
        OriginalTranslatedShaderBindings vb,OriginalTranslatedShaderBindings pb,uint location,Rid shader,
        OriginalSky2NullTextureSpirv? nullAdapter,OriginalSky2SourceNull2D[] nullProofs,
        OriginalSky2LoadedNullTexturePacketInterface? loadedNullInterface)
    {
        (Source,Combination,Operation,VertexTranslation,VertexAdapter,PixelTranslation,VertexBindings,PixelBindings,PositionLocation,ShaderRid,NullTextureAdapter)
            =(source,combination,operation,vertex,adapter,pixel,vb,pb,location,shader,nullAdapter);
        sourceNullProofs=nullProofs;
        nullPacketInterface=loadedNullInterface;
    }

    public static async Task<OriginalSky2Program> BuildAsync(LoadedShaderContainer source,int explicitCombinationIndex,
        Vkd3dShaderCompiler compiler,OriginalSky2Operation operation,
        IReadOnlyList<OriginalSky2SourceNull2D>? explicitFreshNullTextures=null)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(compiler);
        if(Convert.ToHexString(SHA256.HashData(source.Source.Bytes))!=
            "CE4CEC6271BDE59E40D5B97746A7A1877188E133550A0C608D41C353A4999424")
            throw new InvalidDataException("Sky2 programs require the verified complete 340AC335 source.");
        var combination=source.ReadG1ResourceBindings(explicitCombinationIndex).Combination;
        if(operation is not (OriginalSky2Operation.Atmosphere or OriginalSky2Operation.Contribution) ||
            (combination.Index%2==0)!=(operation==OriginalSky2Operation.Atmosphere))
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
        var adapter=OriginalVertexShaderAdapter.AdaptPositionInput(vertex.Bytes);
        var vb=OriginalTranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(vs.Entry.ProgramIndex),adapter.Bytes);
        var pb=OriginalTranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(ps.Entry.ProgramIndex),pixel.Bytes);
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
        OriginalSky2NullTextureSpirv? nullAdapter=null;
        var nullProofs=explicitFreshNullTextures?.ToArray()??[];
        if(nullProofs.Length>0)
        {
            nullAdapter=OriginalSky2NullTextureAdapter.Adapt(pixel,pb,nullProofs);
            var finalInterface=SpirvShaderInterfaceReader.Read(nullAdapter.Bytes);
            pb=new(finalInterface,pb.OriginalDefinitions,Array.AsReadOnly(pb.Bindings
                .Where(b=>b.DescriptorKind is not ("Texture" or "Sampler") || !nullAdapter.NullPixelRegisters.Contains(b.Source.BindPoint))
                .Select(b=>b with{Device=finalInterface.Descriptors.Single(v=>v.VariableId==b.Device.VariableId)}).ToArray()));
        }
        // Complete source/interface import before acquiring the shader RID.
        var loadedNullInterface=nullAdapter is null?null:
            OriginalSky2NullTextureAdapter.ReadLoadedPacketInterface(nullAdapter,pb.OriginalDefinitions);
        Rid shader=await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            using var stages=new RDShaderSpirV();
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex,adapter.Bytes);
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment,nullAdapter?.Bytes??pixel.Bytes);
            Rid rid=rd.ShaderCreateFromSpirV(stages,"OriginalSky2"+operation);
            if(!rid.IsValid)throw new InvalidDataException("Device rejected original Sky2 shader pair.");
            return rid;
        });
        return new(source,combination,operation,vertex,adapter,pixel,vb,pb,position[0].Location!.Value,shader,
            nullAdapter,nullProofs,loadedNullInterface);
    }

    public ValueTask DisposeAsync()=>new(_disposal??=OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
    {rd.FreeRid(ShaderRid);return true;}));
}
