using Godot;
using San14.Data;
using System.Buffers.Binary;

public sealed record Sky2DeviceInput(uint SourceBindPoint,uint SourceDimension,Rid ShaderView,
    Rid OwnerTexture,ReadOnlyMemory<byte> SourceSamplerDescriptor,string SourceResourceUri)
{
    /// <summary>Full source view with the untrimmed original header sampler.
    /// Bind point stays explicit; missing sampler metadata is an error.</summary>
    public static Sky2DeviceInput FromRetainedSource(uint bindPoint,SkyTextureDevice texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        var sampler=San14.Data.Formats.G1TextureSamplerReader.ReadRetainedSource(texture.Image);
        return new(bindPoint,texture.Image.TextureSystem==2?8u:4u,texture.TextureRid,texture.TextureRid,
            sampler.EncodeD3D11Descriptor(),texture.Source.Source.ResourceUri+"#image="+texture.Image.Index);
    }
}

/// <summary>1197B00 retains the caller's write mask and blends both RGB and
/// alpha with ONE / SRC_ALPHA / ADD. Null preserves the standalone replacement
/// adapter used by earlier shader diagnostics; native scene composition must
/// supply this record explicitly.</summary>
public sealed record Sky2NativeComposition(uint ColorWriteMask);

/// <summary>Original Sky2 four-corner VB draw. The caller supplies complete
/// source-sized constants and every typed texture/sampler; no missing input is
/// replaced with a cleared texture or a guessed atmospheric table.</summary>
public sealed class Sky2Draw : IAsyncDisposable
{
    public byte[] VertexConstantBytes { get; private set; }
    public byte[] PixelConstantBytes { get; private set; }
    public byte[] VertexStreamBytes { get; }
    public IReadOnlyList<Sky2DeviceInput> Inputs { get; }
    public CubeRenderDeviceSurface Target { get; }
    public Sky2NativeComposition? NativeComposition { get; }
    private readonly Rid _pipeline,_vertices,_vertexSet,_pixelSet;
    private readonly Rid _vertexBuffer,_pixelBuffer;
    private readonly Sky2Program _program;
    private readonly byte[] _vertexTemplate,_pixelTemplate;
    private readonly IReadOnlyList<Rid> _owned;
    private Task? _disposal;

    private Sky2Draw(Sky2Program program,byte[] vc,byte[] pc,byte[] geometry,IReadOnlyList<Sky2DeviceInput> inputs,
        CubeRenderDeviceSurface target,Rid pipeline,Rid vertices,Rid vs,Rid ps,Rid vcb,Rid pcb,List<Rid> owned,
        Sky2NativeComposition? composition)
    {
        (VertexConstantBytes,PixelConstantBytes,VertexStreamBytes,Inputs,Target,_pipeline,_vertices,_vertexSet,_pixelSet,_owned)
            =(vc,pc,geometry,inputs,target,pipeline,vertices,vs,ps,owned.AsReadOnly());
        (_program,_vertexBuffer,_pixelBuffer,_vertexTemplate,_pixelTemplate)=(program,vcb,pcb,vc.ToArray(),pc.ToArray());
        NativeComposition=composition;
    }

    /// <summary>Prepare the named packets against this selected program's
    /// original definitions before allocating the draw. Templates retain padding.</summary>
    public static Task<Sky2Draw> BuildAsync(Sky2Program program,
        CubeRenderTextureDevice target,uint surfaceId,Sky2DrawInputs explicitConstants,
        ReadOnlyMemory<byte> explicitVertexTemplate,ReadOnlyMemory<byte> explicitPixelTemplate,
        IReadOnlyList<Sky2DeviceInput> explicitInputs,
        Sky2NativeComposition? explicitNativeComposition=null)
    {
        ArgumentNullException.ThrowIfNull(program);ArgumentNullException.ThrowIfNull(explicitConstants);
        var vertex=Sky2Constants.Vertex(program.VertexBindings.SourceDefinitions,
            explicitVertexTemplate.Span,explicitConstants.Vertex);
        var pixel=Sky2Constants.Pixel(program.PixelBindings.SourceDefinitions,
            explicitPixelTemplate.Span,explicitConstants.Pixel);
        return BuildAsync(program,target,surfaceId,vertex.Bytes,pixel.Bytes,explicitInputs,explicitNativeComposition);
    }

    public static Task<Sky2Draw> BuildAsync(Sky2Program program,
        CubeRenderTextureDevice target,uint surfaceId,ReadOnlyMemory<byte> explicitVertexConstants,
        ReadOnlyMemory<byte> explicitPixelConstants,IReadOnlyList<Sky2DeviceInput> explicitInputs,
        Sky2NativeComposition? explicitNativeComposition=null)
    {
        ArgumentNullException.ThrowIfNull(program);ArgumentNullException.ThrowIfNull(target);ArgumentNullException.ThrowIfNull(explicitInputs);
        if(surfaceId>=target.Surfaces.Count||explicitVertexConstants.Length!=program.VertexConstantByteLength||
            explicitPixelConstants.Length!=program.PixelConstantByteLength)
            throw new InvalidDataException("Sky2 requires an explicit Cube surface and complete constants matching the selected original programs.");
        if(explicitNativeComposition is {ColorWriteMask: >15})
            throw new InvalidDataException("Native Sky2 composition requires the explicit four-bit retained write mask.");
        program.ValidateNullTexturePacket(explicitPixelConstants.Span);
        var sourceTextures=program.PixelBindings.Bindings.Where(b=>b.DescriptorKind=="Texture").ToArray();
        var inputs=explicitInputs.Select(i=>i is null?throw new InvalidDataException("Sky2 texture input is null."):
            i with{SourceSamplerDescriptor=i.SourceSamplerDescriptor.ToArray()}).ToArray();
        if(inputs.Length!=sourceTextures.Length||inputs.Select(i=>i.SourceBindPoint).Distinct().Count()!=inputs.Length)
            throw new InvalidDataException("Sky2 texture inputs must exactly match the original source registers.");
        foreach(var binding in sourceTextures)
        {
            var input=inputs.SingleOrDefault(i=>i.SourceBindPoint==binding.Source.BindPoint);
            if(input is null||input.SourceDimension!=binding.Source.Dimension||input.SourceSamplerDescriptor.Length!=52||
                string.IsNullOrWhiteSpace(input.SourceResourceUri)||input.OwnerTexture==target.TextureRid||
                target.Surfaces.Any(s=>s.TargetView==input.ShaderView))
                throw new InvalidDataException("Sky2 input source dimension, sampler, provenance or read/write resource differs.");
        }
        byte[] vc=explicitVertexConstants.ToArray(),pc=explicitPixelConstants.ToArray(),geometry=new byte[32];
        // Exact native rdata four POSITION.xy vertices; stride8, strip order.
        float[] corners=[-1,1,-1,-1,1,1,1,-1];
        for(int i=0;i<corners.Length;i++)BinaryPrimitives.WriteSingleLittleEndian(geometry.AsSpan(i*4,4),corners[i]);
        var surface=target.Surfaces[(int)surfaceId];
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var owned=new List<Rid>();
            Rid Own(Rid rid){if(!rid.IsValid)throw new InvalidDataException("Sky2 device allocation failed.");owned.Add(rid);return rid;}
            try
            {
                if(!rd.FramebufferIsValid(surface.Framebuffer))throw new InvalidDataException("Sky2 target is no longer live.");
                var samplers=new Dictionary<uint,Rid>();
                foreach(var input in inputs)
                {
                    if(!rd.TextureIsValid(input.ShaderView)||!rd.TextureIsValid(input.OwnerTexture))
                        throw new InvalidDataException("Sky2 input view or parent texture is no longer live.");
                    using var format=rd.TextureGetFormat(input.ShaderView);
                    var required=input.SourceDimension switch {
                        4=>RenderingDevice.TextureType.Type2D,8=>RenderingDevice.TextureType.Type3D,
                        _=>throw new InvalidDataException("Unsupported original Sky2 texture dimension.")};
                    string encoding=format.Format.ToString();
                    if(format.TextureType!=required||format.ArrayLayers!=1||
                        encoding.Contains("Uint",StringComparison.Ordinal)||encoding.Contains("Sint",StringComparison.Ordinal))
                        throw new InvalidDataException("Sky2 device view does not satisfy the original float texture dimension.");
                    samplers.Add(input.SourceBindPoint,Own(DeviceSampler.Create(rd,input.SourceSamplerDescriptor.Span)));
                }
                Rid vb=Own(rd.VertexBufferCreate(32,geometry));
                using var position=new RDVertexAttribute{Location=program.PositionLocation,Binding=0,Offset=0,Stride=8,
                    Format=RenderingDevice.DataFormat.R32G32Sfloat,Frequency=RenderingDevice.VertexFrequency.Vertex};
                long vertexFormat=rd.VertexFormatCreate([position]);Rid vertices=Own(rd.VertexArrayCreate(4,vertexFormat,[vb]));
                Rid vcb=Own(rd.UniformBufferCreate((uint)vc.Length,vc)),pcb=Own(rd.UniformBufferCreate((uint)pc.Length,pc));
                Rid Set(TranslatedShaderBindings bindings,uint set,Rid cb)
                {
                    var uniforms=new Godot.Collections.Array<RDUniform>();
                    try
                    {
                        foreach(var binding in bindings.Bindings)
                        {
                            RenderingDevice.UniformType type;Rid resource;
                            if(binding.DescriptorKind=="UniformBuffer"&&binding.Source.BindPoint==0)
                                (type,resource)=(RenderingDevice.UniformType.UniformBuffer,cb);
                            else if(binding.DescriptorKind=="Sampler"&&samplers.TryGetValue(binding.Source.BindPoint,out var sampler))
                                (type,resource)=(RenderingDevice.UniformType.Sampler,sampler);
                            else if(binding.DescriptorKind=="Texture")
                                (type,resource)=(RenderingDevice.UniformType.Texture,inputs.Single(i=>i.SourceBindPoint==binding.Source.BindPoint).ShaderView);
                            else throw new InvalidDataException("Unsupported original Sky2 descriptor.");
                            var u=new RDUniform{Binding=checked((int)binding.Device.Binding!.Value),UniformType=type};
                            u.AddId(resource);uniforms.Add(u);
                        }
                        Rid result=Own(rd.UniformSetCreate(uniforms,program.ShaderRid,set));
                        if(!rd.UniformSetIsValid(result))throw new InvalidDataException("Sky2 uniform set is invalid.");
                        return result;
                    }
                    finally{foreach(var u in uniforms)u.Dispose();}
                }
                Rid vs=Set(program.VertexBindings,1,vcb),ps=Set(program.PixelBindings,0,pcb);
                using var raster=new RDPipelineRasterizationState{CullMode=RenderingDevice.PolygonCullMode.Disabled};
                using var multi=new RDPipelineMultisampleState();
                using var depth=new RDPipelineDepthStencilState{EnableDepthTest=false,EnableDepthWrite=false};
                uint mask=explicitNativeComposition?.ColorWriteMask??15;
                using var color=new RDPipelineColorBlendStateAttachment{
                    EnableBlend=explicitNativeComposition is not null,
                    SrcColorBlendFactor=RenderingDevice.BlendFactor.One,
                    DstColorBlendFactor=RenderingDevice.BlendFactor.SrcAlpha,
                    ColorBlendOp=RenderingDevice.BlendOperation.Add,
                    SrcAlphaBlendFactor=RenderingDevice.BlendFactor.One,
                    DstAlphaBlendFactor=RenderingDevice.BlendFactor.SrcAlpha,
                    AlphaBlendOp=RenderingDevice.BlendOperation.Add,
                    WriteR=(mask&1)!=0,WriteG=(mask&2)!=0,WriteB=(mask&4)!=0,WriteA=(mask&8)!=0};
                using var blend=new RDPipelineColorBlendState{Attachments=[color]};
                Rid pipeline=Own(rd.RenderPipelineCreate(program.ShaderRid,rd.FramebufferGetFormat(surface.Framebuffer),vertexFormat,
                    RenderingDevice.RenderPrimitive.TriangleStrips,raster,multi,depth,blend));
                if(!rd.RenderPipelineIsValid(pipeline))throw new InvalidDataException("Sky2 pipeline is invalid.");
                return new Sky2Draw(program,vc,pc,geometry,Array.AsReadOnly(inputs),surface,pipeline,vertices,vs,ps,vcb,pcb,owned,explicitNativeComposition);
            }
            catch{for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);throw;}
        });
    }

    /// <summary>Update the complete named state while retaining geometry,
    /// texture bindings and pipeline. Await this before submitting the draw.</summary>
    public Task UpdateConstantsAsync(Sky2DrawInputs explicitConstants)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        ArgumentNullException.ThrowIfNull(explicitConstants);
        byte[] vc=Sky2Constants.Vertex(_program.VertexBindings.SourceDefinitions,
            _vertexTemplate,explicitConstants.Vertex).Bytes;
        byte[] pc=Sky2Constants.Pixel(_program.PixelBindings.SourceDefinitions,
            _pixelTemplate,explicitConstants.Pixel).Bytes;
        _program.ValidateNullTexturePacket(pc);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            ObjectDisposedException.ThrowIf(_disposal is not null,this);
            if(rd.BufferUpdate(_vertexBuffer,0,(uint)vc.Length,vc)!=Error.Ok||rd.BufferUpdate(_pixelBuffer,0,(uint)pc.Length,pc)!=Error.Ok)
                throw new InvalidDataException("Device rejected an original Sky2 constant update.");
            VertexConstantBytes=vc;PixelConstantBytes=pc;return true;
        });
    }

    public Task DrawAsync()
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            ObjectDisposedException.ThrowIf(_disposal is not null,this);
            long list=rd.DrawListBegin(Target.Framebuffer,RenderingDevice.DrawFlags.DefaultAll);
            try{rd.DrawListBindRenderPipeline(list,_pipeline);rd.DrawListBindVertexArray(list,_vertices);
                rd.DrawListBindUniformSet(list,_pixelSet,0);rd.DrawListBindUniformSet(list,_vertexSet,1);rd.DrawListDraw(list,false,1,4);}
            finally{rd.DrawListEnd();}return true;
        });
    }

    public ValueTask DisposeAsync()=>new(_disposal??=TerrainDeviceTexture.OnRenderThreadAsync(rd=>
    {for(int i=_owned.Count-1;i>=0;i--)rd.FreeRid(_owned[i]);return true;}));
}
