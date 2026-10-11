using Godot;
using San14.Data;

/// <summary>Original11971F0 VCLOUD native pass0 premultiplied cloud layer.
/// Loads a previously generated WORK face and blends One/InverseSrcAlpha,
/// Add for both color/alpha, as decoded from native0x551100. The retained
/// color write mask and complete source-selected CB layouts stay explicit.</summary>
public sealed class SkyCloudColorDraw : IAsyncDisposable
{
    public SkyCloudConstantPacket Constants { get; private set; }
    public CubeRenderDeviceSurface Target { get; }
    public IReadOnlyList<Sky2DeviceInput> Inputs { get; }
    public uint ExplicitColorWriteMask { get; }
    private long submittedDrawCount;
    public long SubmittedDrawCount => Interlocked.Read(ref submittedDrawCount);
    private readonly SkyCloudProgram _program;
    private readonly Rid _pipeline, _vertexSet, _pixelSet, _vertexBuffer, _pixelBuffer;
    private readonly IReadOnlyList<Rid> _owned;
    private Task? _disposal;
    private readonly object disposalGate=new();
    private int releasedResources;

    private SkyCloudColorDraw(SkyCloudProgram program,
        SkyCloudConstantPacket constants, CubeRenderDeviceSurface target,
        IReadOnlyList<Sky2DeviceInput> inputs, uint mask, Rid pipeline, Rid vs, Rid ps,
        Rid vcb, Rid pcb, List<Rid> owned)
        => (_program, Constants, Target, Inputs, ExplicitColorWriteMask,
            _pipeline, _vertexSet, _pixelSet, _vertexBuffer, _pixelBuffer, _owned)
            = (program, constants, target, inputs, mask, pipeline, vs, ps, vcb, pcb, owned.AsReadOnly());

    public static Task<SkyCloudColorDraw> BuildAsync(SkyCloudProgram program,
        CubeRenderTextureDevice target, uint surfaceId,
        ReadOnlyMemory<byte> explicitVertexConstants, ReadOnlyMemory<byte> explicitPixelConstants,
        uint explicitMaximumRaySamples, IReadOnlyList<Sky2DeviceInput> explicitInputs,
        uint explicitColorWriteMask)
    {
        ArgumentNullException.ThrowIfNull(program); ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(explicitInputs);
        if (program.Operation != SkyCloudOperation.Color || surfaceId >= target.Surfaces.Count ||
            target.Surfaces[(int)surfaceId].Source.MipLevel != 0 || explicitColorWriteMask > 15)
            throw new InvalidDataException("Cloud color requires native pass0, an explicit base WORK face and four-bit retained write mask.");
        var packet = SkyCloudConstants.ReadColor(program.VertexBindings.SourceDefinitions,
            program.PixelBindings.SourceDefinitions, explicitVertexConstants.Span,
            explicitPixelConstants.Span, explicitMaximumRaySamples);
        var inputs = explicitInputs.Select(i => i with { SourceSamplerDescriptor = i.SourceSamplerDescriptor.ToArray() }).ToArray();
        var sourceTextures=program.PixelBindings.Bindings.Where(b=>b.DescriptorKind=="Texture").ToArray();
        if (inputs.Length != sourceTextures.Length || inputs.Select(i => i.SourceBindPoint).Distinct().Count() != sourceTextures.Length ||
            sourceTextures.Length is not (5 or 7))
            throw new InvalidDataException("Cloud color requires every exact selected original texture input.");
        foreach (var b in program.PixelBindings.Bindings.Where(b => b.DescriptorKind == "Texture"))
        {
            var i = inputs.SingleOrDefault(i => i.SourceBindPoint == b.Source.BindPoint);
            if (i is null || i.SourceDimension != b.Source.Dimension || i.SourceSamplerDescriptor.Length != 52 ||
                string.IsNullOrWhiteSpace(i.SourceResourceUri) || i.OwnerTexture == target.TextureRid ||
                target.Surfaces.Any(s => s.TargetView == i.ShaderView))
                throw new InvalidDataException("Cloud color texture dimensions, source, sampler or read/write overlap differs.");
        }
        var surface = target.Surfaces[(int)surfaceId];
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var owned = new List<Rid>();
            Rid Own(Rid r) { if (!r.IsValid) throw new InvalidDataException("Cloud color device allocation failed."); owned.Add(r); return r; }
            try
            {
                if (!rd.FramebufferIsValid(surface.Framebuffer)) throw new InvalidDataException("Cloud color target is no longer live.");
                var samplers = new Dictionary<uint, Rid>();
                foreach (var i in inputs)
                {
                    if (!rd.TextureIsValid(i.ShaderView) || !rd.TextureIsValid(i.OwnerTexture))
                        throw new InvalidDataException("Cloud color source is no longer live.");
                    using var format = rd.TextureGetFormat(i.ShaderView);
                    var required = i.SourceDimension switch { 4 => RenderingDevice.TextureType.Type2D,
                        8 => RenderingDevice.TextureType.Type3D, _ => throw new InvalidDataException("Unsupported cloud color source dimension.") };
                    if (format.TextureType != required || format.ArrayLayers != 1 || format.Format.ToString().Contains("Uint") ||
                        format.Format.ToString().Contains("Sint")) throw new InvalidDataException("Cloud color view is not the original floating texture shape.");
                    samplers.Add(i.SourceBindPoint, Own(DeviceSampler.Create(rd, i.SourceSamplerDescriptor.Span)));
                }
                Rid vcb = Own(rd.UniformBufferCreate(160, packet.VertexBytes)), pcb = Own(rd.UniformBufferCreate(checked((uint)packet.PixelBytes.Length), packet.PixelBytes));
                Rid Set(TranslatedShaderBindings bindings, uint set, Rid cb)
                {
                    var uniforms = new Godot.Collections.Array<RDUniform>();
                    try
                    {
                        foreach (var b in bindings.Bindings)
                        {
                            RenderingDevice.UniformType type; Rid input;
                            if (b.DescriptorKind == "UniformBuffer" && b.Source.BindPoint == 0)
                                (type, input) = (RenderingDevice.UniformType.UniformBuffer, cb);
                            else if (b.DescriptorKind == "Sampler" && samplers.TryGetValue(b.Source.BindPoint, out var sampler))
                                (type, input) = (RenderingDevice.UniformType.Sampler, sampler);
                            else if (b.DescriptorKind == "Texture")
                                (type, input) = (RenderingDevice.UniformType.Texture, inputs.Single(i => i.SourceBindPoint == b.Source.BindPoint).ShaderView);
                            else throw new InvalidDataException("Cloud color descriptor is unsupported.");
                            var u = new RDUniform { Binding = checked((int)b.Device.Binding!.Value), UniformType = type };
                            u.AddId(input); uniforms.Add(u);
                        }
                        Rid result = Own(rd.UniformSetCreate(uniforms, program.ShaderRid, set));
                        if (!rd.UniformSetIsValid(result)) throw new InvalidDataException("Cloud color uniform set is invalid.");
                        return result;
                    }
                    finally { foreach (var u in uniforms) u.Dispose(); }
                }
                Rid vs = Set(program.VertexBindings, 1, vcb), ps = Set(program.PixelBindings, 0, pcb);
                using var raster = new RDPipelineRasterizationState { CullMode = RenderingDevice.PolygonCullMode.Disabled };
                using var multi = new RDPipelineMultisampleState();
                using var depth = new RDPipelineDepthStencilState { EnableDepthTest = false, EnableDepthWrite = false };
                using var color = new RDPipelineColorBlendStateAttachment {
                    EnableBlend = true, SrcColorBlendFactor = RenderingDevice.BlendFactor.One,
                    DstColorBlendFactor = RenderingDevice.BlendFactor.OneMinusSrcAlpha,
                    ColorBlendOp = RenderingDevice.BlendOperation.Add,
                    SrcAlphaBlendFactor = RenderingDevice.BlendFactor.One,
                    DstAlphaBlendFactor = RenderingDevice.BlendFactor.OneMinusSrcAlpha,
                    AlphaBlendOp = RenderingDevice.BlendOperation.Add,
                    WriteR = (explicitColorWriteMask & 1) != 0, WriteG = (explicitColorWriteMask & 2) != 0,
                    WriteB = (explicitColorWriteMask & 4) != 0, WriteA = (explicitColorWriteMask & 8) != 0 };
                using var blend = new RDPipelineColorBlendState { Attachments = [color] };
                Rid pipeline = Own(rd.RenderPipelineCreate(program.ShaderRid, rd.FramebufferGetFormat(surface.Framebuffer), -1,
                    RenderingDevice.RenderPrimitive.TriangleStrips, raster, multi, depth, blend));
                if (!rd.RenderPipelineIsValid(pipeline)) throw new InvalidDataException("Cloud color pipeline is invalid.");
                return new SkyCloudColorDraw(program, packet, surface, Array.AsReadOnly(inputs),
                    explicitColorWriteMask, pipeline, vs, ps, vcb, pcb, owned);
            }
            catch { for (int i = owned.Count - 1; i >= 0; i--) rd.FreeRid(owned[i]); throw; }
        });
    }

    public Task UpdateConstantsAsync(ReadOnlyMemory<byte> vertex, ReadOnlyMemory<byte> pixel, uint maximumRaySamples)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null, this);
        var packet = SkyCloudConstants.ReadColor(_program.VertexBindings.SourceDefinitions,
            _program.PixelBindings.SourceDefinitions, vertex.Span, pixel.Span, maximumRaySamples);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            ObjectDisposedException.ThrowIf(_disposal is not null, this);
            if (rd.BufferUpdate(_vertexBuffer, 0, 160, packet.VertexBytes) != Error.Ok ||
                rd.BufferUpdate(_pixelBuffer, 0, checked((uint)packet.PixelBytes.Length), packet.PixelBytes) != Error.Ok)
                throw new InvalidDataException("Device rejected cloud color update.");
            Constants = packet; return true;
        });
    }

    public Task DrawAsync()
    {
        ObjectDisposedException.ThrowIf(_disposal is not null, this);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            ObjectDisposedException.ThrowIf(_disposal is not null, this);
            // DefaultAll retains the already drawn Sky2/Scene color.
            long list = rd.DrawListBegin(Target.Framebuffer, RenderingDevice.DrawFlags.DefaultAll);
            try { rd.DrawListBindRenderPipeline(list, _pipeline); rd.DrawListBindUniformSet(list, _pixelSet, 0);
                rd.DrawListBindUniformSet(list, _vertexSet, 1); rd.DrawListDraw(list, false, 1, 4); }
            finally { rd.DrawListEnd(); }
            Interlocked.Increment(ref submittedDrawCount);
            return true;
        });
    }
    public ValueTask DisposeAsync()
    {
        lock(disposalGate)
        {
            if(_disposal is null||_disposal.IsFaulted||_disposal.IsCanceled)
                _disposal=TerrainDeviceTexture.OnRenderThreadAsync(rd=>
                {
                    while(releasedResources<_owned.Count)
                    {rd.FreeRid(_owned[_owned.Count-1-releasedResources]);releasedResources++;}
                    return true;
                });
            return new(_disposal);
        }
    }
}
