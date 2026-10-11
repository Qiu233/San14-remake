using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Explicit source pass7 cube interpolation pair. Creation does not
/// select the active variant, interpolation scalar or rotating source cubes.</summary>
public sealed class SkyCubeBlendProgram : IAsyncDisposable
{
    public LoadedShaderContainer Source { get; }
    public G2ShaderCombination Combination { get; }
    public ShaderSpirv VertexTranslation { get; }
    public AdaptedVertexShader VertexAdapter { get; }
    public ShaderSpirv PixelTranslation { get; }
    public TranslatedShaderBindings VertexBindings { get; }
    public TranslatedShaderBindings PixelBindings { get; }
    public Rid ShaderRid { get; }
    private Task? _disposal;

    private SkyCubeBlendProgram(LoadedShaderContainer source,G2ShaderCombination combination,
        ShaderSpirv vertex,AdaptedVertexShader adapter,ShaderSpirv pixel,
        TranslatedShaderBindings vb,TranslatedShaderBindings pb,Rid shader)
        => (Source,Combination,VertexTranslation,VertexAdapter,PixelTranslation,VertexBindings,PixelBindings,ShaderRid)
            = (source,combination,vertex,adapter,pixel,vb,pb,shader);

    public static async Task<SkyCubeBlendProgram> BuildAsync(LoadedShaderContainer source,
        int explicitCombinationIndex,Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(compiler);
        var combination=SkyAmbientFilterSelection.FromExplicitBlendCombination(source,explicitCombinationIndex).BlendCombination;
        if(combination.Stages.Any(s=>s.Entry is not null && s.StageCode is not (0 or 1)))
            throw new InvalidDataException("Cube blend requires the explicit vertex/pixel pair.");
        var vs=combination.Stages.Single(s=>s.StageCode==1&&s.Entry is not null);
        var ps=combination.Stages.Single(s=>s.StageCode==0&&s.Entry is not null);
        var vertexBytes=source.Container.GetProgramBytes(source.Source.Bytes,vs.Entry!.ProgramIndex);
        var pixelBytes=source.Container.GetProgramBytes(source.Source.Bytes,ps.Entry!.ProgramIndex);
        var vertex=compiler.Compile(vertexBytes,1);var pixel=compiler.Compile(pixelBytes);
        var adapter=VertexShaderAdapter.Adapt(vertex.Bytes,0,0,0);
        var vb=TranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(vs.Entry.ProgramIndex),adapter.Bytes);
        var pb=TranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(ps.Entry.ProgramIndex),pixel.Bytes);
        if(vb.Bindings.Count!=1 || pb.Bindings.Count!=5 ||
            vb.Bindings.Any(b=>b.Device.DescriptorSet!=1) || pb.Bindings.Any(b=>b.Device.DescriptorSet!=0) ||
            vb.Interface.StageVariables.Any(v=>v.StorageClass==1&&v.Location is not null))
            throw new InvalidDataException("Cube blend descriptors or vertex-ID geometry differ from the source contract.");
        void CheckConstant(TranslatedShaderBindings bindings,int size,string name)
        {
            var b=bindings.Bindings.Single(b=>b.DescriptorKind=="UniformBuffer");
            if(b.Source.BindPoint!=0||b.Device.RequiredBufferBytes!=size||b.ConstantBuffer is not { } cb ||
                cb.ByteSize!=size||cb.Variables.Count!=1||cb.Variables[0].Name.Text!=name||
                cb.Variables[0].StartOffset!=0||cb.Variables[0].ByteSize!=size)
                throw new NotSupportedException("Godot cube blend draws require their fixed constant-buffer slots and field layouts.");
        }
        CheckConstant(vb,64,"g_mC2W");CheckConstant(pb,16,"g_vIntInfo");
        Rid shader=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            using var stages=new RDShaderSpirV();
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex,adapter.Bytes);
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment,pixel.Bytes);
            Rid rid=rd.ShaderCreateFromSpirV(stages,"OriginalSkyCubeBlend");
            if(!rid.IsValid)throw new InvalidDataException("Device rejected the original cube blend pair.");
            return rid;
        });
        return new(source,combination,vertex,adapter,pixel,vb,pb,shader);
    }

    public ValueTask DisposeAsync()=>new(_disposal??=TerrainDeviceTexture.OnRenderThreadAsync(rd=>
    {rd.FreeRid(ShaderRid);return true;}));
}
