using Godot;
using San14.Data;

/// <summary>Original VCLOUD pass5 draws four VertexIDs into one R8 PatternMap.
/// Inputs are complete source-sized packets plus typed 3D/3D/2D bindings;
/// no sky RGB, color table or missing noise is substituted.</summary>
public sealed class SkyCloudPatternDraw : IAsyncDisposable
{
    public SkyCloudConstantPacket Constants { get; private set; }
    public SkyPatternMapDevice Target { get; }
    public IReadOnlyList<Sky2DeviceInput> Inputs { get; }
    private readonly SkyCloudProgram _program;
    private readonly Rid _pipeline, _vertexSet, _pixelSet, _vertexBuffer, _pixelBuffer;
    private readonly IReadOnlyList<Rid> _owned;
    private Task? _disposal;

    private SkyCloudPatternDraw(SkyCloudProgram program,
        SkyCloudConstantPacket constants, SkyPatternMapDevice target,
        IReadOnlyList<Sky2DeviceInput> inputs, Rid pipeline, Rid vs, Rid ps,
        Rid vcb, Rid pcb, List<Rid> owned)
        => (_program, Constants, Target, Inputs, _pipeline, _vertexSet, _pixelSet, _vertexBuffer, _pixelBuffer, _owned)
            = (program, constants, target, inputs, pipeline, vs, ps, vcb, pcb, owned.AsReadOnly());

    public static Task<SkyCloudPatternDraw> BuildAsync(SkyCloudProgram program,
        SkyPatternMapDevice target, ReadOnlyMemory<byte> explicitVertexConstants,
        ReadOnlyMemory<byte> explicitPixelConstants, uint explicitMaximumRaySamples,
        IReadOnlyList<Sky2DeviceInput> explicitInputs)
    {
        ArgumentNullException.ThrowIfNull(program); ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(explicitInputs);
        var packet = SkyCloudConstants.Read(program.VertexBindings.SourceDefinitions,
            program.PixelBindings.SourceDefinitions, explicitVertexConstants.Span,
            explicitPixelConstants.Span, explicitMaximumRaySamples);
        var inputs = explicitInputs.Select(i => i with { SourceSamplerDescriptor = i.SourceSamplerDescriptor.ToArray() }).ToArray();
        if (inputs.Length != 3 || inputs.Select(i => i.SourceBindPoint).Distinct().Count() != 3)
            throw new InvalidDataException("Cloud draw requires exactly three original texture inputs.");
        foreach (var b in program.PixelBindings.Bindings.Where(b => b.DescriptorKind == "Texture"))
        {
            var i = inputs.SingleOrDefault(i => i.SourceBindPoint == b.Source.BindPoint);
            if (i is null || i.SourceDimension != b.Source.Dimension || i.SourceSamplerDescriptor.Length != 52 ||
                string.IsNullOrWhiteSpace(i.SourceResourceUri) || i.OwnerTexture == target.TextureRid || i.ShaderView == target.TextureRid)
                throw new InvalidDataException("Cloud texture dimensions, provenance, sampler or read/write overlap differ.");
        }
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var owned = new List<Rid>();
            Rid Own(Rid r) { if (!r.IsValid) throw new InvalidDataException("Cloud device allocation failed."); owned.Add(r); return r; }
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
                Rid vcb = Own(rd.UniformBufferCreate(160, packet.VertexBytes)), pcb = Own(rd.UniformBufferCreate(192, packet.PixelBytes));
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
                Rid vs = Set(program.VertexBindings, 1, vcb), ps = Set(program.PixelBindings, 0, pcb);
                using var raster = new RDPipelineRasterizationState { CullMode = RenderingDevice.PolygonCullMode.Disabled };
                using var multi = new RDPipelineMultisampleState();
                using var depth = new RDPipelineDepthStencilState { EnableDepthTest = false, EnableDepthWrite = false };
                using var color = new RDPipelineColorBlendStateAttachment { EnableBlend = false };
                using var blend = new RDPipelineColorBlendState { Attachments = [color] };
                Rid pipeline = Own(rd.RenderPipelineCreate(program.ShaderRid, rd.FramebufferGetFormat(target.Framebuffer), -1,
                    RenderingDevice.RenderPrimitive.TriangleStrips, raster, multi, depth, blend));
                if (!rd.RenderPipelineIsValid(pipeline)) throw new InvalidDataException("Cloud pipeline is invalid.");
                return new SkyCloudPatternDraw(program, packet, target, Array.AsReadOnly(inputs), pipeline, vs, ps, vcb, pcb, owned);
            }
            catch { for (int i = owned.Count - 1; i >= 0; i--) rd.FreeRid(owned[i]); throw; }
        });
    }

    public Task UpdateConstantsAsync(ReadOnlyMemory<byte> vertex, ReadOnlyMemory<byte> pixel, uint maximumRaySamples)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null, this);
        var packet = SkyCloudConstants.Read(_program.VertexBindings.SourceDefinitions,
            _program.PixelBindings.SourceDefinitions, vertex.Span, pixel.Span, maximumRaySamples);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            ObjectDisposedException.ThrowIf(_disposal is not null, this);
            if (rd.BufferUpdate(_vertexBuffer, 0, 160, packet.VertexBytes) != Error.Ok ||
                rd.BufferUpdate(_pixelBuffer, 0, 192, packet.PixelBytes) != Error.Ok)
                throw new InvalidDataException("Device rejected cloud constant update.");
            Constants = packet; return true;
        });
    }

    public Task DrawAsync()
    {
        ObjectDisposedException.ThrowIf(_disposal is not null, this);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            ObjectDisposedException.ThrowIf(_disposal is not null, this);
            long list = rd.DrawListBegin(Target.Framebuffer);
            try { rd.DrawListBindRenderPipeline(list, _pipeline); rd.DrawListBindUniformSet(list, _pixelSet, 0);
                rd.DrawListBindUniformSet(list, _vertexSet, 1); rd.DrawListDraw(list, false, 1, 4); }
            finally { rd.DrawListEnd(); } return true;
        });
    }
    public ValueTask DisposeAsync() => new(_disposal ??= TerrainDeviceTexture.OnRenderThreadAsync(rd =>
    { for (int i = _owned.Count - 1; i >= 0; i--) rd.FreeRid(_owned[i]); return true; }));
}
