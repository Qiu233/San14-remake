using System.Buffers.Binary;
using Godot;

/// <summary>Original three-vertex cube copy/filter pass with explicit source view,
/// sampler and target. The caller owns the cubes, program and filter buffers.</summary>
public sealed class SkyCubeDraw : IAsyncDisposable
{
    public byte[] ConstantBytes { get; }
    public byte[] SourceSamplerDescriptor { get; }
    public CubeRenderDeviceSurface Target { get; }
    //Exact immutable bindings captured when the real uniform sets were
    //created. These diagnostics grant no texture-content capability.
    public CubeRenderTextureDevice InputOwner { get; }
    public SkyCubeProgram Program { get; }
    public Rid InputShaderViewRid { get; }
    public Rid InputSamplerRid { get; }
    public Rid ConstantBufferRid { get; }
    public Rid PixelUniformSetRid => _pixelSet;
    public uint InputFirstMipLevel { get; }
    public uint InputMipLevelCount { get; }
    public SkyFilterSampleBuffers? FilterSamples { get; }
    private readonly byte[] _samplerCreationDescriptor;
    public ReadOnlyMemory<byte> SamplerCreationDescriptor => _samplerCreationDescriptor.ToArray();
    private readonly Rid _pipeline, _pixelSet, _vertexSet;
    private readonly IReadOnlyList<Rid> _owned;
    private Task? _disposal;

    private SkyCubeDraw(byte[] constants, byte[] descriptor, CubeRenderDeviceSurface target,
        Rid pipeline, Rid pixelSet, Rid vertexSet, List<Rid> owned,
        CubeRenderTextureDevice inputOwner,SkyCubeProgram program,Rid inputView,Rid sampler,Rid cb,
        uint firstMip,uint mipCount,SkyFilterSampleBuffers? samples)
    {
        (ConstantBytes, SourceSamplerDescriptor, Target, _pipeline, _pixelSet, _vertexSet, _owned)
            = (constants, descriptor, target, pipeline, pixelSet, vertexSet, owned.AsReadOnly());
        (InputOwner,Program,InputShaderViewRid,InputSamplerRid,ConstantBufferRid,InputFirstMipLevel,InputMipLevelCount,FilterSamples)
            =(inputOwner,program,inputView,sampler,cb,firstMip,mipCount,samples);
        _samplerCreationDescriptor=descriptor.ToArray();
    }
    public void RequireCurrentDiagnosticBindings()
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        if(!InputShaderViewRid.IsValid||!InputSamplerRid.IsValid||!ConstantBufferRid.IsValid||!_pixelSet.IsValid||
            !SourceSamplerDescriptor.AsSpan().SequenceEqual(_samplerCreationDescriptor))
            throw new InvalidDataException("Cube draw diagnostic lost its actual current created sampler/uniform bindings.");
    }

    public static Task<SkyCubeDraw> BuildAsync(SkyCubeProgram program,
        CubeRenderTextureDevice source, CubeRenderTextureDevice target, uint surfaceId,
        ReadOnlyMemory<byte> explicitSourceSamplerDescriptor, SkyFilterSampleBuffers? filterSamples = null,
        CubeShaderView? sourceView = null)
    {
        ArgumentNullException.ThrowIfNull(program); ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(target);
        if (surfaceId >= target.Surfaces.Count || sourceView is not null && !ReferenceEquals(sourceView.Owner, source))
            throw new InvalidDataException("Sky cube source view owner and target surface must be explicit.");
        var surface = target.Surfaces[(int)surfaceId];
        uint firstMip = sourceView?.MostDetailedMip ?? 0;
        uint viewLevels = sourceView?.MipLevels ?? source.Source.LevelNum;
        if (ReferenceEquals(source, target) && surface.Source.MipLevel >= firstMip &&
            surface.Source.MipLevel - firstMip < viewLevels)
            throw new InvalidDataException("Sky cube draw cannot sample a mip that it writes.");
        if (program.Operation==SkyCubeOperation.Copy && (filterSamples is not null ||
            surface.Source.MipLevel != 0 && (sourceView is null || viewLevels != 1 || firstMip != surface.Source.MipLevel - 1)))
            throw new InvalidDataException("Later cube copy mips require the explicit one-mip view of the preceding level.");
        if (program.Operation==SkyCubeOperation.Filter && (filterSamples is null || surface.Source.MipLevel == 0 ||
            surface.Source.MipLevel != filterSamples.Source.DestinationMip || filterSamples.Source.SampleCount != program.FilterSampleCount ||
            firstMip != 0 || viewLevels != source.Source.LevelNum))
            throw new InvalidDataException("Sky filter target mip and explicit sample buffers must match the selected original program.");
        byte[] descriptor = explicitSourceSamplerDescriptor.ToArray(), constants = new byte[16];
        // Native 1198CA0 retains WORK78's source width for base copy and filter.
        // Only its internal later-mip copy loop replaces it with target extent.
        float workWidth = program.Operation == SkyCubeOperation.Copy && surface.Source.MipLevel != 0
            ? surface.Source.Extent : source.Source.Width;
        BinaryPrimitives.WriteSingleLittleEndian(constants, workWidth);
        if(filterSamples is not null)BinaryPrimitives.WriteSingleLittleEndian(constants.AsSpan(4),filterSamples.Source.WeightSum);
        BinaryPrimitives.WriteSingleLittleEndian(constants.AsSpan(12), surface.Source.Face);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var owned = new List<Rid>();
            Rid Own(Rid rid)
            {
                if (!rid.IsValid) throw new InvalidDataException("Cube-copy device allocation failed.");
                owned.Add(rid); return rid;
            }
            try
            {
                Rid inputCube = sourceView?.TextureRid ?? source.TextureRid;
                if (!rd.TextureIsValid(inputCube) || !rd.FramebufferIsValid(surface.Framebuffer))
                    throw new InvalidDataException("Cube-copy input or target is no longer live.");
                Rid cb = Own(rd.UniformBufferCreate(16,constants)), sampler = Own(DeviceSampler.Create(rd,descriptor));
                Rid UniformSet(San14.Data.TranslatedShaderBindings bindings, uint set)
                {
                    var uniforms = new Godot.Collections.Array<RDUniform>();
                    try
                    {
                        foreach (var binding in bindings.Bindings)
                        {
                            Rid input;
                            RenderingDevice.UniformType type;
                            if (binding.DescriptorKind == "UniformBuffer" && binding.Source.BindPoint == 0)
                                (input,type) = (cb,RenderingDevice.UniformType.UniformBuffer);
                            else if (binding.DescriptorKind == "Sampler" && binding.Source.BindPoint == 2)
                                (input,type) = (sampler,RenderingDevice.UniformType.Sampler);
                            else if (binding.DescriptorKind == "Texture" && binding.Source.BindPoint == 2 && binding.Source.Dimension == 9)
                                (input,type) = (inputCube,RenderingDevice.UniformType.Texture);
                            else if(binding.DescriptorKind == "TextureBuffer" && filterSamples is not null &&
                                binding.Source.BindPoint is 17 or 18 && binding.Source.Dimension == 1)
                                (input,type) = (binding.Source.BindPoint==17?filterSamples.Directions:filterSamples.Weights,
                                    RenderingDevice.UniformType.TextureBuffer);
                            else throw new InvalidDataException("Cube-copy has an unsupported source descriptor.");
                            var uniform = new RDUniform { Binding = checked((int)binding.Device.Binding!.Value), UniformType=type };
                            uniform.AddId(input); uniforms.Add(uniform);
                        }
                        Rid result = Own(rd.UniformSetCreate(uniforms,program.ShaderRid,set));
                        if (!rd.UniformSetIsValid(result)) throw new InvalidDataException("Cube-copy uniform set is invalid.");
                        return result;
                    }
                    finally { foreach (var u in uniforms) u.Dispose(); }
                }
                Rid vertexSet=UniformSet(program.VertexBindings,1), pixelSet=UniformSet(program.PixelBindings,0);
                using var raster=new RDPipelineRasterizationState { CullMode=RenderingDevice.PolygonCullMode.Disabled };
                using var multisample=new RDPipelineMultisampleState();
                using var depth=new RDPipelineDepthStencilState { EnableDepthTest=false, EnableDepthWrite=false };
                using var color=new RDPipelineColorBlendStateAttachment { EnableBlend=false };
                using var blend=new RDPipelineColorBlendState { Attachments=[color] };
                Rid pipeline=Own(rd.RenderPipelineCreate(program.ShaderRid,rd.FramebufferGetFormat(surface.Framebuffer),-1,
                    RenderingDevice.RenderPrimitive.TriangleStrips,raster,multisample,depth,blend));
                if (!rd.RenderPipelineIsValid(pipeline)) throw new InvalidDataException("Cube-copy pipeline is invalid.");
                return new SkyCubeDraw(constants,descriptor,surface,pipeline,pixelSet,vertexSet,owned,
                    source,program,inputCube,sampler,cb,firstMip,viewLevels,filterSamples);
            }
            catch { for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]); throw; }
        });
    }

    public Task DrawAsync()
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            SubmitDraw(rd);
            return true;
        });
    }

    /// <summary>Submit an already ordered source schedule in one render-thread
    /// callback. Every draw keeps its own list boundary and resource transitions.</summary>
    internal static Task DrawOrderedBatchAsync(IReadOnlyList<SkyCubeDraw> orderedDraws)
    {
        var draws=orderedDraws.ToArray();
        foreach(var draw in draws)ObjectDisposedException.ThrowIf(draw._disposal is not null,draw);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            foreach(var draw in draws)draw.SubmitDraw(rd);
            return true;
        });
    }

    private void SubmitDraw(RenderingDevice rd)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        long list=rd.DrawListBegin(Target.Framebuffer);
        try
        {
            rd.DrawListBindRenderPipeline(list,_pipeline); rd.DrawListBindUniformSet(list,_pixelSet,0);
            rd.DrawListBindUniformSet(list,_vertexSet,1); rd.DrawListDraw(list,false,1,3);
        }
        finally { rd.DrawListEnd(); }
    }

    public ValueTask DisposeAsync() => new(_disposal ??= TerrainDeviceTexture.OnRenderThreadAsync(rd =>
    { for(int i=_owned.Count-1;i>=0;i--)rd.FreeRid(_owned[i]); return true; }));
}
