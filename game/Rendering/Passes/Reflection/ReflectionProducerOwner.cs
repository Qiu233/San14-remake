using Godot;
using San14.Data;

/// <summary>Source inspection and write gate for the active RefProbe producer.
/// It owns no device resources while the source execution contract is incomplete.
/// Apply never clears targets, invents intermediate pixels or allocates shaders.
/// The source conditional fallback remains the extra-layer owner's operation.</summary>
public sealed class ReflectionProducerOwner : IAsyncDisposable
{
    public LoadedReflectionProducerPlan Source { get; }
    private bool _disposed;
    private ReflectionProducerOwner(LoadedReflectionProducerPlan source)=>Source=source;

    public static Task<ReflectionProducerOwner> BuildAsync(LoadedReflectionProducerPlan source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Task.FromResult(new ReflectionProducerOwner(source));
    }

    /// <summary>Execute only the proved final shader stage with caller-produced
    /// PreviousResult images and three explicitly selected attachments. This
    /// does not make the whole active ReflectionProbe producer executable.</summary>
    public static Task<ReflectionFinalDraw> BuildFinalDrawAsync(LoadedReflectionProducerPlan source,
        Vkd3dShaderCompiler compiler,IReadOnlyList<ReflectionFinalAttachment> explicitAttachments,
        IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> explicitConstants,
        IReadOnlyList<DeferredDeviceBinding> explicitInputs,DeferredRasterState explicitRaster)
        =>ReflectionFinalDraw.BuildAsync(source,compiler,explicitAttachments,explicitConstants,explicitInputs,explicitRaster);

    public Task<DeferredReflectionDecision> ApplyAsync(DeferredReflectionState explicitSourceState)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        var decision=DeferredExtraLayerPlan.Reflection(explicitSourceState);
        if(decision.RequiresActiveProducer)Source.RequireExecutable();
        if(decision.RequiresTargetDescriptorFallback || decision.Steps.Any(s=>s.Action==DeferredExtraAction.ClearReflectionSlice))
            throw new InvalidOperationException("Selected original reflection fallback must use OriginalDeferredExtraLayerOwner.");
        return Task.FromResult(decision);
    }
    public ValueTask DisposeAsync(){_disposed=true;return ValueTask.CompletedTask;}
}

public sealed record ReflectionFinalAttachment(Rid ColorView,Rid OwnerTexture,string SourceResourceUri);

/// <summary>Original RefProbe pass12 VS1/PS6. Only the viewport/zero-base
/// vertex adapter is applied; pixel DXBC is translated unchanged. Borrowed
/// inputs and attachments must outlive this draw. It neither produces the
/// upstream compute results nor claims caller attachments resolve raw3/4/5.</summary>
public sealed class ReflectionFinalDraw : IAsyncDisposable
{
    public LoadedReflectionProducerPlan Source { get; }
    public ShaderSpirv VertexTranslation { get; }
    public AdaptedVertexShader VertexAdapter { get; }
    public ShaderSpirv PixelTranslation { get; }
    public TranslatedShaderBindings PixelBindings { get; }
    public IReadOnlyList<ReflectionFinalAttachment> Attachments { get; }
    public IReadOnlyList<DeferredDeviceBinding> Inputs { get; }
    public Rid Framebuffer { get; }
    private readonly Rid _pipeline,_uniformSet;
    private readonly IReadOnlyList<Rid> _owned;
    private Task? _disposal;
    private ReflectionFinalDraw(LoadedReflectionProducerPlan source,ShaderSpirv vertex,
        AdaptedVertexShader adapter,ShaderSpirv pixel,TranslatedShaderBindings bindings,
        ReflectionFinalAttachment[] attachments,DeferredDeviceBinding[] inputs,
        Rid framebuffer,Rid pipeline,Rid set,List<Rid> owned)
        =>(Source,VertexTranslation,VertexAdapter,PixelTranslation,PixelBindings,Attachments,Inputs,Framebuffer,_pipeline,_uniformSet,_owned)=
            (source,vertex,adapter,pixel,bindings,Array.AsReadOnly(attachments),Array.AsReadOnly(inputs),framebuffer,pipeline,set,owned.AsReadOnly());

    internal static Task<ReflectionFinalDraw> BuildAsync(LoadedReflectionProducerPlan source,
        Vkd3dShaderCompiler compiler,IReadOnlyList<ReflectionFinalAttachment> explicitAttachments,
        IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> explicitConstants,
        IReadOnlyList<DeferredDeviceBinding> explicitInputs,DeferredRasterState explicitRaster)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(explicitAttachments);ArgumentNullException.ThrowIfNull(explicitConstants);
        ArgumentNullException.ThrowIfNull(explicitInputs);ArgumentNullException.ThrowIfNull(explicitRaster);
        // The complete source RDEF contains no CB or sampler. Callers must
        // provide the empty CB set and exactly t8/t9, without default packets.
        if(explicitConstants.Count!=0 || explicitRaster.EnableBlend || explicitRaster.Cull!=RenderingDevice.PolygonCullMode.Disabled)
            throw new InvalidDataException("RefProbe final draw requires empty source CBs and explicit no-blend/no-cull coverage state.");
        var targets=explicitAttachments.ToArray();var inputs=explicitInputs.ToArray();
        if(targets.Length!=3 || targets.Any(t=>t is null || !t.ColorView.IsValid || !t.OwnerTexture.IsValid ||
            string.IsNullOrWhiteSpace(t.SourceResourceUri)) || targets.Select(t=>t.ColorView).Distinct().Count()!=3)
            throw new InvalidDataException("RefProbe final draw requires three explicit distinct color attachments and parents.");
        if(inputs.Length!=2 || inputs.Any(i=>i is null) ||
            !inputs.Select(i=>i.SourceBindPoint).Order().SequenceEqual(new uint[]{8,9}) ||
            inputs.Any(i=>i.DescriptorKind!="Texture" || !i.Resource.IsValid ||
                i.OwnerTexture is not Rid p || !p.IsValid || string.IsNullOrWhiteSpace(i.SourceResourceUri) ||
                targets.Any(t=>t.ColorView==i.Resource || t.OwnerTexture==p || t.OwnerTexture==i.Resource || t.ColorView==p)))
            throw new InvalidDataException("RefProbe final draw requires non-aliasing explicit t8/t9 source images.");
        var vs=source.FinalPass.Stages.Single(s=>s.StageCode==1);var ps=source.FinalPass.Stages.Single(s=>s.StageCode==0);
        var vertex=compiler.Compile(source.Shader.Container.GetProgramBytes(source.Shader.Source.Bytes,vs.ProgramIndex),1);
        var pixel=compiler.Compile(source.Shader.Container.GetProgramBytes(source.Shader.Source.Bytes,ps.ProgramIndex));
        if(vertex.SourceProgramSha256!=vs.ProgramSha256 || pixel.SourceProgramSha256!=ps.ProgramSha256)
            throw new InvalidDataException("RefProbe source program bytes changed after selection.");
        var adapter=VertexShaderAdapter.Adapt(vertex.Bytes,0,0,0);
        var vb=TranslatedShaderBindingReader.Read(vs.Definitions,adapter.Bytes);
        var pb=TranslatedShaderBindingReader.Read(ps.Definitions,pixel.Bytes);
        var outputs=pb.Interface.StageVariables.Where(v=>v.StorageClass==3 && v.Location is not null).ToArray();
        if(vb.Bindings.Count!=0 || vb.Interface.StageVariables.Any(v=>v.StorageClass==1 && v.Location is not null) ||
            pb.Bindings.Count!=2 || pb.Bindings.Any(b=>b.DescriptorKind!="Texture" || b.Device.DescriptorSet!=0) ||
            !outputs.Select(o=>o.Location).Order().SequenceEqual(new uint?[]{0,1,2}) ||
            outputs.Any(o=>o.Type is not {Kind:"vector",ComponentCount:3,ComponentType:{Kind:"float",Bits:32}}))
            throw new InvalidDataException("Translated RefProbe final VertexID/textures/three-RGB interface differs.");
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var owned=new List<Rid>();
            Rid Own(Rid r){if(!r.IsValid)throw new InvalidDataException("RefProbe final device allocation failed.");owned.Add(r);return r;}
            try
            {
                Validate(rd,targets,inputs);
                using var stages=new RDShaderSpirV();
                stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex,adapter.Bytes);
                stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment,pixel.Bytes);
                Rid shader=Own(rd.ShaderCreateFromSpirV(stages,"OriginalRefProbeFinalPass12"));
                Rid framebuffer=Own(rd.FramebufferCreate(new Godot.Collections.Array<Rid>(targets.Select(t=>t.ColorView))));
                if(!rd.FramebufferIsValid(framebuffer))throw new InvalidDataException("RefProbe final three-target framebuffer rejected.");
                var uniforms=new Godot.Collections.Array<RDUniform>();Rid set;
                try
                {
                    foreach(var binding in pb.Bindings)
                    {
                        var uniform=new RDUniform{Binding=checked((int)binding.Device.Binding!.Value),UniformType=RenderingDevice.UniformType.Texture};
                        uniform.AddId(inputs.Single(i=>i.SourceBindPoint==binding.Source.BindPoint).Resource);uniforms.Add(uniform);
                    }
                    set=Own(rd.UniformSetCreate(uniforms,shader,0));
                    if(!rd.UniformSetIsValid(set))throw new InvalidDataException("RefProbe final texture descriptors rejected.");
                }
                finally{foreach(var uniform in uniforms)uniform.Dispose();}
                using var raster=new RDPipelineRasterizationState{CullMode=explicitRaster.Cull};
                using var multi=new RDPipelineMultisampleState();
                using var depth=new RDPipelineDepthStencilState{EnableDepthTest=false,EnableDepthWrite=false};
                var colors=Enumerable.Range(0,3).Select(_=>new RDPipelineColorBlendStateAttachment{
                    EnableBlend=false,WriteR=true,WriteG=true,WriteB=true,WriteA=false}).ToArray();
                Rid pipeline;
                try
                {
                    using var blend=new RDPipelineColorBlendState{Attachments=new Godot.Collections.Array<RDPipelineColorBlendStateAttachment>(colors)};
                    pipeline=Own(rd.RenderPipelineCreate(shader,rd.FramebufferGetFormat(framebuffer),-1,
                        RenderingDevice.RenderPrimitive.Triangles,raster,multi,depth,blend));
                }
                finally{foreach(var color in colors)color.Dispose();}
                if(!rd.RenderPipelineIsValid(pipeline))throw new InvalidDataException("RefProbe final original pipeline rejected.");
                return new ReflectionFinalDraw(source,vertex,adapter,pixel,pb,targets,inputs,framebuffer,pipeline,set,owned);
            }
            catch{for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);throw;}
        });
    }

    private static void Validate(RenderingDevice rd,IReadOnlyList<ReflectionFinalAttachment> targets,
        IReadOnlyList<DeferredDeviceBinding> inputs)
    {
        uint width=0,height=0;
        foreach(var target in targets)
        {
            if(!rd.TextureIsValid(target.ColorView) || !rd.TextureIsValid(target.OwnerTexture))
                throw new InvalidDataException("RefProbe final borrowed attachment was released.");
            using var f=rd.TextureGetFormat(target.ColorView);
            if(f.Format!=RenderingDevice.DataFormat.B10G11R11UfloatPack32 ||
                !f.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.ColorAttachmentBit) ||
                f.Width==0 || f.Height==0 || f.Samples!=RenderingDevice.TextureSamples.Samples1 ||
                (width!=0 && (f.Width!=width || f.Height!=height)))
                throw new InvalidDataException("RefProbe final explicit attachment extent/format differs from source R11G11B10.");
            width=f.Width;height=f.Height;
        }
        foreach(var input in inputs)
        {
            if(!rd.TextureIsValid(input.Resource) || !rd.TextureIsValid(input.OwnerTexture!.Value))
                throw new InvalidDataException("RefProbe final intermediate image was released.");
            using var f=rd.TextureGetFormat(input.Resource);
            if(f.TextureType!=RenderingDevice.TextureType.Type2D || !f.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit) ||
                f.Width!=width || f.Height!=height || f.Samples!=RenderingDevice.TextureSamples.Samples1 ||
                f.Format.ToString().Contains("Uint",StringComparison.Ordinal) || f.Format.ToString().Contains("Sint",StringComparison.Ordinal))
                throw new InvalidDataException("RefProbe final intermediate must be a full-extent 2D floating sample view.");
        }
    }
    public Task DrawAsync()
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            ObjectDisposedException.ThrowIf(_disposal is not null,this);Validate(rd,Attachments,Inputs);
            if(!rd.FramebufferIsValid(Framebuffer) || !rd.RenderPipelineIsValid(_pipeline) || !rd.UniformSetIsValid(_uniformSet))
                throw new InvalidDataException("RefProbe final device resources were released.");
            long list=rd.DrawListBegin(Framebuffer,RenderingDevice.DrawFlags.DefaultAll);
            try{rd.DrawListBindRenderPipeline(list,_pipeline);rd.DrawListBindUniformSet(list,_uniformSet,0);rd.DrawListDraw(list,false,1,3);}
            finally{rd.DrawListEnd();}return true;
        });
    }
    public ValueTask DisposeAsync()=>new(_disposal??=TerrainDeviceTexture.OnRenderThreadAsync(rd=>
    {
        for(int i=_owned.Count-1;i>=0;i--)
        {
            // RD automatically retires the dependent framebuffer when a
            // borrowed color attachment is released. Preflight can reject
            // that caller lifetime violation without freeing this RID twice.
            if(_owned[i]==Framebuffer && !rd.FramebufferIsValid(Framebuffer))continue;
            rd.FreeRid(_owned[i]);
        }
        return true;
    }));
}
