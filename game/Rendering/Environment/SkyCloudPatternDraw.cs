using Godot;
using San14.Data;

/// <summary>Original VCLOUD pass5 draws four VertexIDs into one R8 PatternMap.
/// Inputs are named material values plus actual sampled texture bindings;
/// no sky RGB, color table or missing noise is substituted.</summary>
public sealed class SkyCloudPatternDraw : IAsyncDisposable
{
    public SkyCloudPatternMaterial Material { get; private set; }
    public uint MaximumRaySamples { get; private set; }
    public SkyCloudConstantPacket Constants => _program.PatternLayout.Pack(Material, MaximumRaySamples);
    public SkyPatternMapDevice Target { get; }
    public IReadOnlyList<Sky2DeviceInput> Inputs { get; }
    private readonly SkyCloudProgram _program;
    private readonly Rid _pipeline, _vertexSet, _pixelSet, _vertexBuffer, _pixelBuffer;
    private readonly RenderDeviceResources resources;
    private volatile bool retiring;

    private SkyCloudPatternDraw(SkyCloudProgram program,
        SkyCloudPatternMaterial material, uint maximumRaySamples, SkyPatternMapDevice target,
        IReadOnlyList<Sky2DeviceInput> inputs, Rid pipeline, Rid vs, Rid ps,
        Rid vcb, Rid pcb, RenderDeviceResources resources)
        => (_program, Material, MaximumRaySamples, Target, Inputs, _pipeline, _vertexSet, _pixelSet, _vertexBuffer, _pixelBuffer, this.resources)
            = (program, material, maximumRaySamples, target, inputs, pipeline, vs, ps, vcb, pcb, resources);

    public static Task<SkyCloudPatternDraw> BuildAsync(SkyCloudProgram program,
        SkyPatternMapDevice target, ReadOnlyMemory<byte> explicitVertexConstants,
        ReadOnlyMemory<byte> explicitPixelConstants, uint explicitMaximumRaySamples,
        IReadOnlyList<Sky2DeviceInput> explicitInputs)
        => BuildAsync(program, target, program.PatternLayout.ReadMaterial(explicitVertexConstants.Span,
            explicitPixelConstants.Span), explicitMaximumRaySamples, explicitInputs);

    public static Task<SkyCloudPatternDraw> BuildAsync(SkyCloudProgram program,
        SkyPatternMapDevice target, SkyCloudPatternMaterial material, uint explicitMaximumRaySamples,
        IReadOnlyList<Sky2DeviceInput> explicitInputs)
    {
        ArgumentNullException.ThrowIfNull(program); ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(explicitInputs);
        var layout = program.PatternLayout;
        var packet = layout.Pack(material, explicitMaximumRaySamples);
        var inputs = layout.TextureBindings.Select(binding =>
        {
            var input = explicitInputs.SingleOrDefault(value => value.SourceBindPoint == binding.Slot) ??
                throw new InvalidDataException($"Pattern texture {binding.Name} has no actual device input.");
            return input with { SourceSamplerDescriptor = input.SourceSamplerDescriptor.ToArray() };
        }).ToArray();
        foreach (var b in program.PixelBindings.Bindings.Where(b => b.DescriptorKind == "Texture"))
        {
            var i = inputs.SingleOrDefault(i => i.SourceBindPoint == b.Source.BindPoint);
            if (i is null || i.SourceDimension != b.Source.Dimension || i.SourceSamplerDescriptor.Length != 52 ||
                string.IsNullOrWhiteSpace(i.SourceResourceUri) || i.OwnerTexture == target.TextureRid || i.ShaderView == target.TextureRid)
                throw new InvalidDataException("Cloud texture dimensions, provenance, sampler or read/write overlap differ.");
        }
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var resources = new RenderDeviceResources("Cloud device allocation failed.");
            Rid Own(Rid r) => resources.Own(r);
            try
            {
                if (!rd.FramebufferIsValid(target.Framebuffer)) throw new InvalidDataException("Cloud target is no longer live.");
                var samplers = new Dictionary<uint, Rid>();
                foreach (var i in inputs)
                {
                    if (!rd.TextureIsValid(i.ShaderView) || !rd.TextureIsValid(i.OwnerTexture))
                        throw new InvalidDataException("Cloud input is no longer live.");
                    using var format = rd.TextureGetFormat(i.ShaderView);
                    var required = i.SourceDimension switch { 4 => RenderingDevice.TextureType.Type2D,
                        8 => RenderingDevice.TextureType.Type3D, _ => throw new InvalidDataException("Unsupported cloud source dimension.") };
                    if (format.TextureType != required || format.ArrayLayers != 1 || format.Format.ToString().Contains("Uint") ||
                        format.Format.ToString().Contains("Sint")) throw new InvalidDataException("Cloud source view is not the original floating texture shape.");
                    samplers.Add(i.SourceBindPoint, Own(DeviceSampler.Create(rd, i.SourceSamplerDescriptor.Span)));
                }
                Rid vcb = Own(rd.UniformBufferCreate((uint)layout.VertexByteLength, packet.VertexBytes)),
                    pcb = Own(rd.UniformBufferCreate((uint)layout.PixelByteLength, packet.PixelBytes));
                Rid Set(TranslatedShaderBindings bindings, uint set, Rid cb, uint slot)
                {
                    var uniforms = new Godot.Collections.Array<RDUniform>();
                    try
                    {
                        foreach (var b in bindings.Bindings)
                        {
                            RenderingDevice.UniformType type; Rid input;
                            if (b.DescriptorKind == "UniformBuffer" && b.Source.BindPoint == slot)
                                (type, input) = (RenderingDevice.UniformType.UniformBuffer, cb);
                            else if (b.DescriptorKind == "Sampler")
                                (type, input) = (RenderingDevice.UniformType.Sampler, samplers[layout.ResolveSamplerTextureSlot(b.Source.BindPoint)]);
                            else if (b.DescriptorKind == "Texture")
                                (type, input) = (RenderingDevice.UniformType.Texture, inputs.Single(i => i.SourceBindPoint == b.Source.BindPoint).ShaderView);
                            else throw new InvalidDataException("Cloud descriptor is unsupported.");
                            var u = new RDUniform { Binding = checked((int)b.Device.Binding!.Value), UniformType = type };
                            u.AddId(input); uniforms.Add(u);
                        }
                        Rid result = Own(rd.UniformSetCreate(uniforms, program.ShaderRid, set));
                        if (!rd.UniformSetIsValid(result)) throw new InvalidDataException("Cloud uniform set is invalid.");
                        return result;
                    }
                    finally { foreach (var u in uniforms) u.Dispose(); }
                }
                Rid vs = Set(program.VertexBindings, 1, vcb, layout.VertexSlot), ps = Set(program.PixelBindings, 0, pcb, layout.PixelSlot);
                using var raster = new RDPipelineRasterizationState { CullMode = RenderingDevice.PolygonCullMode.Disabled };
                using var multi = new RDPipelineMultisampleState();
                using var depth = new RDPipelineDepthStencilState { EnableDepthTest = false, EnableDepthWrite = false };
                using var color = new RDPipelineColorBlendStateAttachment { EnableBlend = false };
                using var blend = new RDPipelineColorBlendState { Attachments = [color] };
                Rid pipeline = Own(rd.RenderPipelineCreate(program.ShaderRid, rd.FramebufferGetFormat(target.Framebuffer), -1,
                    RenderingDevice.RenderPrimitive.TriangleStrips, raster, multi, depth, blend));
                if (!rd.RenderPipelineIsValid(pipeline)) throw new InvalidDataException("Cloud pipeline is invalid.");
                return new SkyCloudPatternDraw(program, material, explicitMaximumRaySamples, target, Array.AsReadOnly(inputs), pipeline, vs, ps, vcb, pcb, resources);
            }
            catch (Exception failure)
            {
                resources.ReleaseAfterConstructionFailure(rd, failure, "Cloud-pattern-draw-partial");
                throw;
            }
        });
    }

    public Task UpdateConstantsAsync(ReadOnlyMemory<byte> vertex, ReadOnlyMemory<byte> pixel, uint maximumRaySamples)
    {
        ObjectDisposedException.ThrowIf(retiring, this);
        return UpdateMaterialAsync(_program.PatternLayout.ReadMaterial(vertex.Span, pixel.Span), maximumRaySamples);
    }

    public Task UpdateMaterialAsync(SkyCloudPatternMaterial material, uint maximumRaySamples)
    {
        ObjectDisposedException.ThrowIf(retiring, this);
        var packet = _program.PatternLayout.Pack(material, maximumRaySamples);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            ObjectDisposedException.ThrowIf(retiring, this);
            if (rd.BufferUpdate(_vertexBuffer, 0, (uint)packet.VertexBytes.Length, packet.VertexBytes) != Error.Ok ||
                rd.BufferUpdate(_pixelBuffer, 0, (uint)packet.PixelBytes.Length, packet.PixelBytes) != Error.Ok)
                throw new InvalidDataException("Device rejected cloud constant update.");
            Material = material; MaximumRaySamples = maximumRaySamples; return true;
        });
    }

    public Task DrawAsync()
    {
        ObjectDisposedException.ThrowIf(retiring, this);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            ObjectDisposedException.ThrowIf(retiring, this);
            long list = rd.DrawListBegin(Target.Framebuffer);
            try { rd.DrawListBindRenderPipeline(list, _pipeline); rd.DrawListBindUniformSet(list, _pixelSet, 0);
                rd.DrawListBindUniformSet(list, _vertexSet, 1); rd.DrawListDraw(list, false, 1, 4); }
            finally { rd.DrawListEnd(); } return true;
        });
    }
    public ValueTask DisposeAsync()
    {
        retiring = true;
        return resources.DisposeAsync();
    }
}
