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
    private readonly RenderDeviceResources resources;
    private volatile bool retiring;

    private SkyCubeBlendDraw(byte[] vc,byte[] pc,byte[] sa,byte[] sb,CubeRenderDeviceSurface target,
        Rid pipeline,Rid vs,Rid ps,Rid vcb,Rid pcb,RenderDeviceResources resources)
        =>(VertexConstantBytes,PixelConstantBytes,SourceSamplerDescriptorA,SourceSamplerDescriptorB,Target,
            _pipeline,_vertexSet,_pixelSet,_vertexBuffer,_pixelBuffer,this.resources)
            =(vc,pc,sa,sb,target,pipeline,vs,ps,vcb,pcb,resources);

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
            var resources=new RenderDeviceResources("Cube blend device allocation failed.");
            Rid Own(Rid rid)=>resources.Own(rid);
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
                return new SkyCubeBlendDraw(vc,pc,sa,sb,surface,pipeline,vs,ps,vcb,pcb,resources);
            }
            catch(Exception failure)
            {
                resources.ReleaseAfterConstructionFailure(rd,failure,"Sky-cube-blend-draw-partial");
                throw;
            }
        });
    }

    public Task DrawAsync()
    {
        ObjectDisposedException.ThrowIf(retiring,this);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            ObjectDisposedException.ThrowIf(retiring,this);
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
        ObjectDisposedException.ThrowIf(retiring,this);
        var(vc,pc)=PrepareConstants(explicitInterpolation,explicitRowMajorFaceMatrix);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            ObjectDisposedException.ThrowIf(retiring,this);
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
        retiring=true;
        return resources.DisposeAsync();
    }
}
