using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Device resources for a prepared Sky2 pass. CPU shader and binding
/// preparation is shared with independent resource preparation tools.</summary>
public sealed class Sky2Program : IAsyncDisposable
{
    private readonly Sky2ProgramPreparation prepared;
    public LoadedShaderContainer Source => prepared.Source;
    public G2ShaderCombination Combination => prepared.Combination;
    public Sky2Operation Operation => prepared.Operation;
    public ShaderSpirv VertexTranslation => prepared.VertexTranslation;
    public AdaptedVertexShader VertexAdapter => prepared.VertexAdapter;
    public ShaderSpirv PixelTranslation => prepared.PixelTranslation;
    public TranslatedShaderBindings VertexBindings => prepared.VertexBindings;
    public TranslatedShaderBindings PixelBindings => prepared.PixelBindings;
    public uint PositionLocation => prepared.PositionLocation;
    public Sky2NullTextureSpirv? NullTextureAdapter => prepared.NullTextureAdapter;
    public Sky2TextureBindingSnapshot TextureBindings => prepared.TextureBindings;
    public Sky2ConstantLayout VertexConstants => prepared.VertexConstants;
    public Sky2ConstantLayout PixelConstants => prepared.PixelConstants;
    public int VertexConstantByteLength => VertexConstants.ByteLength;
    public int PixelConstantByteLength => PixelConstants.ByteLength;
    public Rid ShaderRid { get; }
    private Task? _disposal;
    private readonly object _disposalGate=new();
    private Sky2Program(Sky2ProgramPreparation prepared,Rid shader)
        => (this.prepared,ShaderRid)=(prepared,shader);
    public void RequireCurrent()
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        TextureBindings.RequireCurrent();
    }
    public void ValidateNullTexturePacket(ReadOnlySpan<byte> pixelPacket)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        prepared.ValidateNullTexturePacket(pixelPacket);
    }
    public static async Task<Sky2Program> BuildAsync(LoadedShaderContainer source,int explicitCombinationIndex,
        Vkd3dShaderCompiler compiler,Sky2Operation operation,Sky2TextureBindingSnapshot textureBindings)
    {
        var prepared=Sky2ProgramPreparation.Prepare(source,explicitCombinationIndex,compiler,operation,textureBindings);
        Rid shader=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            textureBindings.RequireCurrent();
            using var stages=new RDShaderSpirV();
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex,prepared.VertexAdapter.Bytes);
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment,prepared.NullTextureAdapter?.CopyBytes()??prepared.PixelTranslation.Bytes);
            Rid rid=rd.ShaderCreateFromSpirV(stages,"OriginalSky2"+operation);
            if(!rid.IsValid)throw new InvalidDataException("Device rejected prepared Sky2 shader pair.");
            return rid;
        });
        return new(prepared,shader);
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
