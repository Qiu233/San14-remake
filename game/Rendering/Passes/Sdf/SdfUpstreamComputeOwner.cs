using Godot;
using San14.Data;

public sealed record SdfUpstreamDeviceBinding(string Kind,uint Register,Rid Resource,string SourceResourceUri);

/// <summary>Executes an original SDFOCC compute pass using complete explicit
/// current packets. Borrows all image/buffer/sampler descriptors and owns only
/// its shader, pipeline, descriptor set and full original constant buffer.</summary>
public sealed class SdfUpstreamComputeOwner : IAsyncDisposable
{
    public SdfUpstreamProgramSource Source {get;}
    public ShaderSpirv Translation {get;}
    public TranslatedShaderBindings Bindings {get;}
    public Vector2I Extent {get;}
    private readonly Rid _pipeline,_set;
    private readonly IReadOnlyList<Rid> _owned;
    private readonly IReadOnlyList<SdfUpstreamDeviceBinding> _inputs;
    private Task? _disposal;
    private SdfUpstreamComputeOwner(SdfUpstreamProgramSource source,ShaderSpirv translation,
        TranslatedShaderBindings bindings,Vector2I extent,Rid pipeline,Rid set,List<Rid> owned,
        SdfUpstreamDeviceBinding[] inputs)
        =>(Source,Translation,Bindings,Extent,_pipeline,_set,_owned,_inputs)=
            (source,translation,bindings,extent,pipeline,set,owned.AsReadOnly(),Array.AsReadOnly(inputs));

    public static Task<SdfUpstreamComputeOwner> BuildAsync(SdfUpstreamProgramSource source,
        ReadOnlyMemory<byte> explicitConstants,IReadOnlyList<SdfUpstreamDeviceBinding> explicitInputs,
        Vector2I explicitOutputExtent,Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(explicitInputs);ArgumentNullException.ThrowIfNull(compiler);
        var canonical=SdfUpstreamProgramPlan.Read(source.Shader,source.Dimensions,source.Pass);
        if(canonical.ProgramIndex!=source.ProgramIndex || canonical.ProgramSha256!=source.ProgramSha256)
            throw new InvalidDataException("SDF compute source packet changed after selection.");
        source=canonical;
        if(explicitOutputExtent.X<=0 || explicitOutputExtent.Y<=0)throw new InvalidDataException("SDF compute needs current nonzero extent.");
        var cb=source.Definitions.Buffers.Single(b=>b.Name.Text=="$Globals");
        if(explicitConstants.Length!=cb.ByteSize)throw new InvalidDataException("SDF compute needs the entire original constant packet.");
        var constants=explicitConstants.ToArray();var inputs=explicitInputs.ToArray();
        var target=cb.Variables.Single(v=>v.Name.Text=="vTargetSize");
        if(System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(constants.AsSpan(target.StartOffset))!=explicitOutputExtent.X ||
            System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(constants.AsSpan(target.StartOffset+4))!=explicitOutputExtent.Y)
            throw new InvalidDataException("Original SDF vTargetSize must equal the current output extent.");
        var translated=compiler.Compile(source.Shader.Container.GetProgramBytes(source.Shader.Source.Bytes,source.ProgramIndex));
        if(translated.SourceProgramSha256!=source.ProgramSha256)throw new InvalidDataException("SDF compute source changed.");
        var bindings=SdfUpstreamProgramPlan.ReadTranslatedBindings(source,translated.Bytes);
        var expected=bindings.Bindings.Where(b=>b.DescriptorKind!="UniformBuffer").ToArray();
        if(inputs.Length!=expected.Length || inputs.Any(i=>!i.Resource.IsValid || string.IsNullOrWhiteSpace(i.SourceResourceUri)) ||
            expected.Any(b=>inputs.Count(i=>i.Kind==b.DescriptorKind && i.Register==b.Source.BindPoint)!=1))
            throw new InvalidDataException("SDF compute complete current descriptor packet missing.");
        var writes=inputs.Where(i=>i.Kind is "Image" or "ImageBuffer").ToArray();
        if(writes.Select(i=>i.Resource).Distinct().Count()!=writes.Length ||
            writes.Any(w=>inputs.Any(i=>i.Resource==w.Resource && i.Kind is "Texture" or "TextureBuffer")))
            throw new InvalidDataException("SDF compute read/write resource alias rejected.");
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var owned=new List<Rid>();Rid Own(Rid r){if(!r.IsValid)throw new InvalidDataException("SDF compute allocation failed.");owned.Add(r);return r;}
            try
            {
                foreach(var input in inputs.Where(i=>i.Kind is "Image" or "Texture"))
                {
                    if(!rd.TextureIsValid(input.Resource))throw new InvalidDataException("SDF compute borrowed image released.");
                    using var f=rd.TextureGetFormat(input.Resource);
                    var declaration=bindings.Bindings.Single(b=>b.DescriptorKind==input.Kind && b.Source.BindPoint==input.Register).Source;
                    if(f.TextureType!=(declaration.Dimension==8?RenderingDevice.TextureType.Type3D:RenderingDevice.TextureType.Type2D) ||
                        !f.UsageBits.HasFlag(input.Kind=="Image"?RenderingDevice.TextureUsageBits.StorageBit:RenderingDevice.TextureUsageBits.SamplingBit) ||
                        f.Format.ToString().Contains("Uint",StringComparison.Ordinal) || f.Format.ToString().Contains("Sint",StringComparison.Ordinal))
                        throw new InvalidDataException("SDF compute original float texture dimension/usage differs.");
                    if(input.Kind=="Image" && (!f.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.StorageBit) ||
                        f.Width!=(uint)explicitOutputExtent.X || f.Height!=(uint)explicitOutputExtent.Y))
                        throw new InvalidDataException("SDF compute output extent/storage usage differs.");
                }
                using var stages=new RDShaderSpirV();stages.SetStageBytecode(RenderingDevice.ShaderStage.Compute,translated.Bytes);
                Rid shader=Own(rd.ShaderCreateFromSpirV(stages,"OriginalSdfCompute"+source.ProgramIndex));
                Rid buffer=Own(rd.UniformBufferCreate(checked((uint)cb.ByteSize),constants));
                var uniforms=new Godot.Collections.Array<RDUniform>();Rid set;
                try
                {
                    foreach(var b in bindings.Bindings)
                    {
                        var type=b.DescriptorKind switch{"UniformBuffer"=>RenderingDevice.UniformType.UniformBuffer,
                            "Sampler"=>RenderingDevice.UniformType.Sampler,"Texture"=>RenderingDevice.UniformType.Texture,
                            "Image"=>RenderingDevice.UniformType.Image,"TextureBuffer"=>RenderingDevice.UniformType.TextureBuffer,
                            "ImageBuffer"=>RenderingDevice.UniformType.ImageBuffer,_=>throw new InvalidDataException("Unknown SDF compute descriptor.")};
                        var u=new RDUniform{Binding=(int)b.Device.Binding!.Value,UniformType=type};
                        u.AddId(b.DescriptorKind=="UniformBuffer"?buffer:inputs.Single(i=>i.Kind==b.DescriptorKind && i.Register==b.Source.BindPoint).Resource);uniforms.Add(u);
                    }
                    set=Own(rd.UniformSetCreate(uniforms,shader,0));
                    if(!rd.UniformSetIsValid(set))throw new InvalidDataException("SDF compute descriptor set rejected.");
                }
                finally{foreach(var u in uniforms)u.Dispose();}
                Rid pipeline=Own(rd.ComputePipelineCreate(shader));
                if(!rd.ComputePipelineIsValid(pipeline))throw new InvalidDataException("Original SDF compute pipeline rejected.");
                return new SdfUpstreamComputeOwner(source,translated,bindings,explicitOutputExtent,pipeline,set,owned,inputs);
            }
            catch{for(int n=owned.Count-1;n>=0;n--)rd.FreeRid(owned[n]);throw;}
        });
    }
    internal void ValidateCurrentResources(RenderingDevice rd)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        if(!rd.ComputePipelineIsValid(_pipeline) || !rd.UniformSetIsValid(_set) ||
            _inputs.Where(i=>i.Kind is "Image" or "Texture").Any(i=>!rd.TextureIsValid(i.Resource)))
            throw new InvalidDataException("SDF compute current resources released.");
    }
    public Task DispatchAsync()=>TerrainDeviceTexture.OnRenderThreadAsync(rd=>
    {
        ValidateCurrentResources(rd);
        long list=rd.ComputeListBegin();
        try{rd.ComputeListBindComputePipeline(list,_pipeline);rd.ComputeListBindUniformSet(list,_set,0);
            rd.ComputeListDispatch(list,(uint)((Extent.X+7)/8),(uint)((Extent.Y+7)/8),1);}
        finally{rd.ComputeListEnd();}
        return true;
    });
    public ValueTask DisposeAsync()=>new(_disposal??=TerrainDeviceTexture.OnRenderThreadAsync(rd=>
    {for(int n=_owned.Count-1;n>=0;n--)rd.FreeRid(_owned[n]);return true;}));
}
