using Godot;
using San14.Data;

public sealed record SdfFinalDrawResult(SdfFinalDecision Decision,bool PixelsDrawn,uint NextCpuState40);

/// <summary>Independent completed-input SDFOCC graphics stage on borrowed
/// RS3. Executes original VS0/PS1, writes RG only, preserves BA and never
/// clears. It does not create SDF/cluster/half-resolution intermediates.</summary>
public sealed class SdfFinalProducerOwner : IAsyncDisposable
{
    public SdfFinalProgramSource Source { get; }
    public ShaderSpirv VertexTranslation { get; }
    public AdaptedVertexShader VertexAdapter { get; }
    public ShaderSpirv PixelTranslation { get; }
    public TranslatedShaderBindings PixelBindings { get; }
    public DeferredExtraDeviceLayer Target { get; }
    public IReadOnlyList<DeferredDeviceBinding> Inputs { get; }
    public byte[] ConstantBytes=>_constantBytes.ToArray();
    private byte[] _constantBytes;
    private readonly Rid _pipeline,_set,_cb;
    private readonly IReadOnlyList<Rid> _owned;
    private Task? _disposal;
    private SdfFinalProducerOwner(SdfFinalProgramSource source,ShaderSpirv vertex,
        AdaptedVertexShader adapter,ShaderSpirv pixel,TranslatedShaderBindings bindings,
        DeferredExtraDeviceLayer target,DeferredDeviceBinding[] inputs,byte[] constants,
        Rid pipeline,Rid set,Rid cb,List<Rid> owned)
        =>(Source,VertexTranslation,VertexAdapter,PixelTranslation,PixelBindings,Target,Inputs,_constantBytes,_pipeline,_set,_cb,_owned)=
            (source,vertex,adapter,pixel,bindings,target,Array.AsReadOnly(inputs),constants,pipeline,set,cb,owned.AsReadOnly());

    public static Task<SdfFinalProducerOwner> BuildAsync(SdfFinalProgramSource source,
        SdfFinalSelection explicitGraphicsSelection,DeferredExtraDeviceLayer borrowedRs3,
        IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> explicitConstants,
        IReadOnlyList<DeferredDeviceBinding> explicitInputs,DeferredRasterState explicitRaster,
        Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(borrowedRs3);
        ArgumentNullException.ThrowIfNull(explicitConstants);ArgumentNullException.ThrowIfNull(explicitInputs);
        ArgumentNullException.ThrowIfNull(explicitRaster);ArgumentNullException.ThrowIfNull(compiler);
        RequireDraw(SdfFinalProducerPlan.Select(explicitGraphicsSelection));
        if(borrowedRs3.TextureObjectId!=DeferredExtraLayerPlan.RsObjectId || borrowedRs3.Slice!=3 ||
            explicitRaster.EnableBlend || explicitRaster.Cull!=RenderingDevice.PolygonCullMode.Disabled)
            throw new InvalidDataException("SDF final requires caller selected RS3 and explicit no-blend/no-cull coverage state.");
        byte[] constants=Snapshot(explicitConstants);
        var inputs=explicitInputs.Select(i=>i??throw new InvalidDataException("Null SDF final source input.")).ToArray();
        (string Kind,uint Register)[] expected=[("Sampler",0),("Sampler",1),("Texture",0),("Texture",1),("Texture",4),("Texture",5),("Texture",6)];
        if(inputs.Length!=7 || !inputs.Select(i=>(i.DescriptorKind,i.SourceBindPoint)).OrderBy(i=>i.DescriptorKind).ThenBy(i=>i.SourceBindPoint)
            .SequenceEqual(expected.OrderBy(i=>i.Kind).ThenBy(i=>i.Register)))
            throw new InvalidDataException("SDF final requires both original samplers and all five original input images.");
        foreach(var i in inputs)
            if(!i.Resource.IsValid || string.IsNullOrWhiteSpace(i.SourceResourceUri) ||
                (i.DescriptorKind=="Texture")!=i.OwnerTexture.HasValue ||
                i.OwnerTexture is Rid parent && (!parent.IsValid || parent==borrowedRs3.BorrowedStorage) ||
                i.Resource==borrowedRs3.TargetView || i.Resource==borrowedRs3.BorrowedStorage)
                throw new InvalidDataException("SDF final input is missing or aliases its borrowed RS3 target.");
        var vertex=compiler.Compile(source.Shader.Container.GetProgramBytes(source.Shader.Source.Bytes,0),1);
        var pixel=compiler.Compile(source.Shader.Container.GetProgramBytes(source.Shader.Source.Bytes,1));
        if(vertex.SourceProgramSha256!=SdfFinalProducerPlan.VertexSha256 || pixel.SourceProgramSha256!=SdfFinalProducerPlan.PixelSha256)
            throw new InvalidDataException("SDF final original program bytes changed after source selection.");
        var adapter=VertexShaderAdapter.Adapt(vertex.Bytes,0,0,0);
        var vb=TranslatedShaderBindingReader.Read(source.Shader.ReadResourceDefinitions(0),adapter.Bytes);
        var pb=SdfFinalProducerPlan.ReadTranslatedPixelBindings(source,pixel.Bytes);
        var outputs=pb.Interface.StageVariables.Where(v=>v.StorageClass==3 && v.Location is not null).ToArray();
        if(vb.Bindings.Count!=0 || vb.Interface.StageVariables.Any(v=>v.StorageClass==1 && v.Location is not null) ||
            outputs.Length!=1 || outputs[0].Location!=0 || outputs[0].Type is not
                {Kind:"vector",ComponentCount:4,ComponentType:{Kind:"float",Bits:32}})
            throw new InvalidDataException("SDF final translated VertexID/output contract differs.");
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var owned=new List<Rid>();
            Rid Own(Rid r){if(!r.IsValid)throw new InvalidDataException("SDF final device allocation failed.");owned.Add(r);return r;}
            try
            {
                Validate(rd,borrowedRs3,inputs);
                using var stages=new RDShaderSpirV();
                stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex,adapter.Bytes);
                stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment,pixel.Bytes);
                Rid shader=Own(rd.ShaderCreateFromSpirV(stages,"OriginalSdfOccFinalGraphics"));
                Rid cb=Own(rd.UniformBufferCreate(64,constants));
                var uniforms=new Godot.Collections.Array<RDUniform>();Rid set;
                try
                {
                    foreach(var d in pb.Bindings)
                    {
                        var type=d.DescriptorKind switch{"UniformBuffer"=>RenderingDevice.UniformType.UniformBuffer,
                            "Texture"=>RenderingDevice.UniformType.Texture,"Sampler"=>RenderingDevice.UniformType.Sampler,
                            _=>throw new InvalidDataException("Unsupported SDF final descriptor.")};
                        var uniform=new RDUniform{Binding=checked((int)d.Device.Binding!.Value),UniformType=type};
                        uniform.AddId(d.DescriptorKind=="UniformBuffer"?cb:inputs.Single(i=>i.DescriptorKind==d.DescriptorKind &&
                            i.SourceBindPoint==d.Source.BindPoint).Resource);uniforms.Add(uniform);
                    }
                    set=Own(rd.UniformSetCreate(uniforms,shader,0));
                    if(!rd.UniformSetIsValid(set))throw new InvalidDataException("SDF final complete descriptor set rejected.");
                }
                finally{foreach(var u in uniforms)u.Dispose();}
                using var raster=new RDPipelineRasterizationState{CullMode=explicitRaster.Cull};
                using var multi=new RDPipelineMultisampleState();
                using var depth=new RDPipelineDepthStencilState{EnableDepthTest=false,EnableDepthWrite=false};
                using var color=new RDPipelineColorBlendStateAttachment{EnableBlend=false,WriteR=true,WriteG=true,WriteB=false,WriteA=false};
                using var blend=new RDPipelineColorBlendState{Attachments=[color]};
                Rid pipeline=Own(rd.RenderPipelineCreate(shader,rd.FramebufferGetFormat(borrowedRs3.Framebuffer),-1,
                    RenderingDevice.RenderPrimitive.Triangles,raster,multi,depth,blend));
                if(!rd.RenderPipelineIsValid(pipeline))throw new InvalidDataException("SDF final RG-only pipeline rejected.");
                return new SdfFinalProducerOwner(source,vertex,adapter,pixel,pb,borrowedRs3,inputs,constants,pipeline,set,cb,owned);
            }
            catch{for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);throw;}
        });
    }
    private static byte[] Snapshot(IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> supplied)
    {
        if(supplied.Count!=1 || !supplied.TryGetValue(0,out var bytes) || bytes.Length!=64)
            throw new InvalidDataException("SDF final requires the full original64-byte cb0 matrix, including its retained last row.");
        return bytes.ToArray();
    }
    private static void RequireDraw(SdfFinalDecision decision)
    {
        if(decision.Action!=SdfFinalAction.Draw)throw new NotSupportedException(decision.Reason);
    }
    private static void Validate(RenderingDevice rd,DeferredExtraDeviceLayer t,IReadOnlyList<DeferredDeviceBinding> inputs)
    {
        if(!rd.TextureIsValid(t.BorrowedStorage) || !rd.TextureIsValid(t.TargetView) || !rd.FramebufferIsValid(t.Framebuffer))
            throw new InvalidDataException("SDF final borrowed target was released.");
        using(var f=rd.TextureGetFormat(t.BorrowedStorage))
            if(f.Format!=RenderingDevice.DataFormat.B8G8R8A8Unorm || f.TextureType!=RenderingDevice.TextureType.Type2DArray ||
                f.ArrayLayers!=5 || f.Mipmaps!=1 || !f.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.ColorAttachmentBit))
                throw new InvalidDataException("SDF final RS backing differs from original five-layer BGRA8 source.");
        foreach(var input in inputs.Where(i=>i.DescriptorKind=="Texture"))
        {
            if(!rd.TextureIsValid(input.Resource) || !rd.TextureIsValid(input.OwnerTexture!.Value))
                throw new InvalidDataException("SDF final completed input was released.");
            using var f=rd.TextureGetFormat(input.Resource);
            if(f.TextureType!=RenderingDevice.TextureType.Type2D || !f.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit) ||
                f.Format.ToString().Contains("Uint",StringComparison.Ordinal) || f.Format.ToString().Contains("Sint",StringComparison.Ordinal))
                throw new InvalidDataException("SDF final original image declaration requires a2D float view.");
        }
    }
    public Task UpdateConstantsAsync(IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> explicitConstants)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);var bytes=Snapshot(explicitConstants);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {ObjectDisposedException.ThrowIf(_disposal is not null,this);if(rd.BufferUpdate(_cb,0,64,bytes)!=Error.Ok)
            throw new InvalidDataException("SDF final full CB update rejected.");_constantBytes=bytes;return true;});
    }
    public Task<SdfFinalDrawResult> DrawAsync(SdfFinalSelection explicitGraphicsSelection,uint explicitCpuState40)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        var decision=SdfFinalProducerPlan.Select(explicitGraphicsSelection);
        if(decision.Action==SdfFinalAction.Preserve)return Task.FromResult(new SdfFinalDrawResult(decision,false,explicitCpuState40));
        RequireDraw(decision);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            ObjectDisposedException.ThrowIf(_disposal is not null,this);Validate(rd,Target,Inputs);
            if(!rd.RenderPipelineIsValid(_pipeline) || !rd.UniformSetIsValid(_set))throw new InvalidDataException("SDF final resources were released.");
            long list=rd.DrawListBegin(Target.Framebuffer,RenderingDevice.DrawFlags.DefaultAll);
            try{rd.DrawListBindRenderPipeline(list,_pipeline);rd.DrawListBindUniformSet(list,_set,0);rd.DrawListDraw(list,false,1,3);}
            finally{rd.DrawListEnd();}
            return new SdfFinalDrawResult(decision,true,SdfFinalProducerPlan.AfterCompletedFinalMethod(explicitCpuState40));
        });
    }
    public ValueTask DisposeAsync()=>new(_disposal??=TerrainDeviceTexture.OnRenderThreadAsync(rd=>
    {for(int i=_owned.Count-1;i>=0;i--)rd.FreeRid(_owned[i]);return true;}));
}
