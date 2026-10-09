using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The source PB2SM SV_VertexID quad and original four-MRT pixel
/// program. Only final raster Y is adapted for Godot's positive viewport;
/// source TEXCOORD0, zone topology and pixel arithmetic are retained.</summary>
public sealed class TerritoryShaderProgram : IAsyncDisposable
{
    public LoadedShaderContainer Source { get; }
    public string VertexSourceSha256 => TerritoryResources.VertexSha256;
    public string PixelSourceSha256 => TerritoryResources.PixelSha256;
    public ShaderSpirv VertexTranslation { get; }
    public AdaptedVertexShader VertexAdapter { get; }
    public ShaderSpirv PixelTranslation { get; }
    public TranslatedShaderBindings VertexBindings { get; }
    public TranslatedShaderBindings PixelBindings { get; }
    public IReadOnlyList<uint> ColorOutputLocations { get; }
    public Rid ShaderRid { get; }
    private Task? _disposal;

    private TerritoryShaderProgram(LoadedShaderContainer source, ShaderSpirv vertex,
        AdaptedVertexShader adapter, ShaderSpirv pixel,
        TranslatedShaderBindings vertexBindings, TranslatedShaderBindings pixelBindings, Rid shader)
    {
        (Source,VertexTranslation,VertexAdapter,PixelTranslation,VertexBindings,PixelBindings,ShaderRid)=
            (source,vertex,adapter,pixel,vertexBindings,pixelBindings,shader);
        ColorOutputLocations=Array.AsReadOnly(pixelBindings.Interface.StageVariables
            .Where(v=>v.StorageClass==3).Select(v=>v.Location!.Value).Order().ToArray());
    }

    public static async Task<TerritoryShaderProgram> BuildAsync(LoadedShaderContainer source,Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(compiler);
        var snapshot=source.Source.Bytes.ToArray();
        var canonical=source with{Source=source.Source with{Bytes=snapshot},Container=G2ShaderContainerReader.Read(snapshot)};
        TerritoryResources.ValidateShader(canonical);
        var combination=canonical.ReadG1ResourceBindings(0).Combination;
        if(combination.Stages.Count(s=>s.Entry is not null)!=2||
            combination.Stages.Count(s=>s.StageCode==1&&s.Entry?.ProgramIndex==0)!=1||
            combination.Stages.Count(s=>s.StageCode==0&&s.Entry?.ProgramIndex==1)!=1)
            throw new InvalidDataException("Territory requires the original PB2SM VS0/PS1 combination.");
        var vertex=compiler.Compile(canonical.Container.GetProgramBytes(snapshot,0),1);
        var pixel=compiler.Compile(canonical.Container.GetProgramBytes(snapshot,1));
        var raw=SpirvShaderInterfaceReader.Read(vertex.Bytes);
        if(raw.StageVariables.Any(v=>v.StorageClass==1&&v.Location is not null)||
            raw.StageVariables.Count(v=>v.StorageClass==1&&v.BuiltIn==42)!=1)
            throw new InvalidDataException("Territory source quad requires its single VertexIndex input and no vertex buffers.");
        var adapter=raw.StageVariables.Any(v=>v.BuiltIn==4424)
            ?VertexShaderAdapter.Adapt(vertex.Bytes,0,0,0)
            :VertexShaderAdapter.AdaptPositionInput(vertex.Bytes);
        var vb=TranslatedShaderBindingReader.Read(canonical.ReadResourceDefinitions(0),adapter.Bytes);
        var pb=TranslatedShaderBindingReader.Read(canonical.ReadResourceDefinitions(1),pixel.Bytes);
        var colors=pb.Interface.StageVariables.Where(v=>v.StorageClass==3).OrderBy(v=>v.Location).ToArray();
        if(vb.Bindings.Count!=0||pb.Bindings.Count!=13||pb.Bindings.Any(b=>b.Device.DescriptorSet!=0)||
            !colors.Select(v=>v.Location).SequenceEqual(new uint?[]{0,1,2,3})||
            colors.Any(v=>v.Type is not{Kind:"vector",ComponentCount:4,ComponentType:{Kind:"float",Bits:32}}))
            throw new InvalidDataException("Territory original four-float4 MRT or thirteen-descriptor interface differs.");
        foreach(var input in pb.Interface.StageVariables.Where(v=>v.StorageClass==1&&v.Location is not null))
        {
            var outputs=vb.Interface.StageVariables.Where(v=>v.StorageClass==3&&v.Location==input.Location).ToArray();
            if(outputs.Length!=1||!Compatible(outputs[0].Type,input.Type))
                throw new InvalidDataException("Territory original TEXCOORD does not satisfy its source pixel input.");
        }
        Rid shader=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            using var stages=new RDShaderSpirV();
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex,adapter.Bytes);
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment,pixel.Bytes);
            Rid value=rd.ShaderCreateFromSpirV(stages,"OriginalTerritoryPB2SM");
            if(!value.IsValid)throw new InvalidDataException("RenderingDevice rejected original territory VS0/PS1.");
            return value;
        });
        return new(canonical,vertex,adapter,pixel,vb,pb,shader);
    }

    private static bool Compatible(SpirvInterfaceType a,SpirvInterfaceType b)=>a.Kind==b.Kind&&
        (a.Kind=="vector"?a.ComponentCount>=b.ComponentCount&&Compatible(a.ComponentType!,b.ComponentType!):
         a.Kind is "float" or "int"&&a.Bits==b.Bits&&a.Signed==b.Signed);
    public ValueTask DisposeAsync()=>new(_disposal??=TerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(ShaderRid);return true;}));
    internal void RequireLive()=>ObjectDisposedException.ThrowIf(_disposal is not null,this);
}
