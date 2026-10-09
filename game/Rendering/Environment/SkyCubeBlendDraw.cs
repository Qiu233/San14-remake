using Godot;
using San14.Data;
using System.Buffers.Binary;

/// <summary>Original four-vertex interpolation draw with caller-selected inputs,
/// face matrix and sampler descriptors. Inputs and program remain caller-owned.</summary>
public sealed class SkyCubeBlendDraw : IAsyncDisposable
{
    public byte[] VertexConstantBytes { get; }
    public byte[] PixelConstantBytes { get; }
    public byte[] SourceSamplerDescriptorA { get; }
    public byte[] SourceSamplerDescriptorB { get; }
    public CubeRenderDeviceSurface Target { get; }
    private readonly Rid _pipeline,_vertexSet,_pixelSet,_vertexBuffer,_pixelBuffer;
    private readonly List<Rid> _owned;
    private readonly object _lifetimeGate=new();
    private Task? _disposal;

    private SkyCubeBlendDraw(byte[] vc,byte[] pc,byte[] sa,byte[] sb,CubeRenderDeviceSurface target,
        Rid pipeline,Rid vs,Rid ps,Rid vcb,Rid pcb,List<Rid> owned)
        =>(VertexConstantBytes,PixelConstantBytes,SourceSamplerDescriptorA,SourceSamplerDescriptorB,Target,
            _pipeline,_vertexSet,_pixelSet,_vertexBuffer,_pixelBuffer,_owned)
            =(vc,pc,sa,sb,target,pipeline,vs,ps,vcb,pcb,owned);

    internal static (byte[] Vertex,byte[] Pixel) PrepareConstants(float explicitInterpolation,
        ReadOnlyMemory<byte> explicitRowMajorFaceMatrix)
    {
        ValidateConstants(explicitInterpolation,explicitRowMajorFaceMatrix.Span);
        byte[] vc=explicitRowMajorFaceMatrix.ToArray(),pc=new byte[16];
        // Native AAC720 preparation writes scalar.x and explicitly zeros y/z/w.
        BinaryPrimitives.WriteSingleLittleEndian(pc,explicitInterpolation);
        return(vc,pc);
    }

    internal static void ValidateConstants(float explicitInterpolation,ReadOnlySpan<byte> explicitRowMajorFaceMatrix)
    {
        if(!float.IsFinite(explicitInterpolation)||explicitRowMajorFaceMatrix.Length!=64)
            throw new InvalidDataException("Cube blend requires a finite scalar and explicit 64-byte face matrix.");
        for(int i=0;i<64;i+=4)if(!float.IsFinite(BinaryPrimitives.ReadSingleLittleEndian(explicitRowMajorFaceMatrix.Slice(i,4))))
            throw new InvalidDataException("Cube blend face matrix contains a nonfinite value.");
    }

    public static Task<SkyCubeBlendDraw> BuildAsync(SkyCubeBlendProgram program,
        CubeRenderTextureDevice inputA,CubeRenderTextureDevice inputB,
        CubeRenderTextureDevice target,uint surfaceId,float explicitInterpolation,
        ReadOnlyMemory<byte> explicitRowMajorFaceMatrix,ReadOnlyMemory<byte> samplerDescriptorA,
        ReadOnlyMemory<byte> samplerDescriptorB)
    {
        ArgumentNullException.ThrowIfNull(program);ArgumentNullException.ThrowIfNull(inputA);
        ArgumentNullException.ThrowIfNull(inputB);ArgumentNullException.ThrowIfNull(target);
        if(ReferenceEquals(inputA,target)||ReferenceEquals(inputB,target)||surfaceId>=target.Surfaces.Count||
            !float.IsFinite(explicitInterpolation)||explicitRowMajorFaceMatrix.Length!=64)
            throw new InvalidDataException("Cube blend requires distinct input/target resources, a finite scalar and explicit 64-byte face matrix.");
        var(vc,pc)=PrepareConstants(explicitInterpolation,explicitRowMajorFaceMatrix);
        byte[] sa=samplerDescriptorA.ToArray(),sb=samplerDescriptorB.ToArray();
        var surface=target.Surfaces[(int)surfaceId];
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var owned=new List<Rid>();
            Rid Own(Rid rid)
            {if(!rid.IsValid)throw new InvalidDataException("Cube blend device allocation failed.");owned.Add(rid);return rid;}
            try
            {
                if(!rd.TextureIsValid(inputA.TextureRid)||!rd.TextureIsValid(inputB.TextureRid)||!rd.FramebufferIsValid(surface.Framebuffer))
                    throw new InvalidDataException("Cube blend input or target is no longer live.");
                Rid vcb=Own(rd.UniformBufferCreate(64,vc)),pcb=Own(rd.UniformBufferCreate(16,pc));
                Rid samplerA=Own(DeviceSampler.Create(rd,sa)),samplerB=Own(DeviceSampler.Create(rd,sb));
                Rid Set(TranslatedShaderBindings bindings,uint set,Rid cb)
                {
                    var uniforms=new Godot.Collections.Array<RDUniform>();
                    try
                    {
                        foreach(var binding in bindings.Bindings)
                        {
                            RenderingDevice.UniformType type;Rid input;
                            if(binding.DescriptorKind=="UniformBuffer"&&binding.Source.BindPoint==0)
                                (type,input)=(RenderingDevice.UniformType.UniformBuffer,cb);
                            else if(binding.DescriptorKind=="Sampler"&&binding.Source.BindPoint is 2 or 4)
                                (type,input)=(RenderingDevice.UniformType.Sampler,binding.Source.BindPoint==2?samplerA:samplerB);
                            else if(binding.DescriptorKind=="Texture"&&binding.Source.Dimension==9&&binding.Source.BindPoint is 2 or 4)
                                (type,input)=(RenderingDevice.UniformType.Texture,binding.Source.BindPoint==2?inputA.TextureRid:inputB.TextureRid);
                            else throw new InvalidDataException("Cube blend source descriptor is unsupported.");
                            var u=new RDUniform{Binding=checked((int)binding.Device.Binding!.Value),UniformType=type};
                            u.AddId(input);uniforms.Add(u);
                        }
                        Rid result=Own(rd.UniformSetCreate(uniforms,program.ShaderRid,set));
                        if(!rd.UniformSetIsValid(result))throw new InvalidDataException("Cube blend uniform set is invalid.");
                        return result;
                    }
                    finally{foreach(var u in uniforms)u.Dispose();}
                }
                Rid vs=Set(program.VertexBindings,1,vcb),ps=Set(program.PixelBindings,0,pcb);
                using var raster=new RDPipelineRasterizationState{CullMode=RenderingDevice.PolygonCullMode.Disabled};
                using var multi=new RDPipelineMultisampleState();
                using var depth=new RDPipelineDepthStencilState{EnableDepthTest=false,EnableDepthWrite=false};
                using var color=new RDPipelineColorBlendStateAttachment{EnableBlend=false};
                using var blend=new RDPipelineColorBlendState{Attachments=[color]};
                Rid pipeline=Own(rd.RenderPipelineCreate(program.ShaderRid,rd.FramebufferGetFormat(surface.Framebuffer),-1,
                    RenderingDevice.RenderPrimitive.TriangleStrips,raster,multi,depth,blend));
                if(!rd.RenderPipelineIsValid(pipeline))throw new InvalidDataException("Cube blend pipeline is invalid.");
                return new SkyCubeBlendDraw(vc,pc,sa,sb,surface,pipeline,vs,ps,vcb,pcb,owned);
            }
            catch{for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);throw;}
        });
    }

    public Task DrawAsync()
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            long list=rd.DrawListBegin(Target.Framebuffer);
            try{rd.DrawListBindRenderPipeline(list,_pipeline);rd.DrawListBindUniformSet(list,_pixelSet,0);
                rd.DrawListBindUniformSet(list,_vertexSet,1);rd.DrawListDraw(list,false,1,4);}
            finally{rd.DrawListEnd();}
            return true;
        });
    }

    /// <summary>Update the current caller's complete source constants and
    /// submit the original draw in one render-thread callback. Texture views,
    /// samplers, uniform sets and pipeline remain this draw's loaded bindings.</summary>
    public Task DrawCurrentAsync(float explicitInterpolation,ReadOnlyMemory<byte> explicitRowMajorFaceMatrix)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        var(vc,pc)=PrepareConstants(explicitInterpolation,explicitRowMajorFaceMatrix);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            if(rd.BufferUpdate(_vertexBuffer,0,64,vc)!=Error.Ok||
                rd.BufferUpdate(_pixelBuffer,0,16,pc)!=Error.Ok)
                throw new InvalidDataException("Device rejected the current cube blend constants.");
            vc.CopyTo(VertexConstantBytes,0);pc.CopyTo(PixelConstantBytes,0);
            long list=rd.DrawListBegin(Target.Framebuffer);
            try{rd.DrawListBindRenderPipeline(list,_pipeline);rd.DrawListBindUniformSet(list,_pixelSet,0);
                rd.DrawListBindUniformSet(list,_vertexSet,1);rd.DrawListDraw(list,false,1,4);}
            finally{rd.DrawListEnd();}
            return true;
        });
    }

    public ValueTask DisposeAsync()
    {
        lock(_lifetimeGate)
        {
            if(_disposal is null||_disposal.IsFaulted||_disposal.IsCanceled)
                _disposal=TerrainDeviceTexture.OnRenderThreadAsync(rd=>
                {
                    // Remove only successfully released resources. A failed
                    // retirement keeps remaining borrowers for scene cleanup.
                    while(_owned.Count>0){rd.FreeRid(_owned[^1]);_owned.RemoveAt(_owned.Count-1);}
                    return true;
                });
            return new(_disposal);
        }
    }
}
