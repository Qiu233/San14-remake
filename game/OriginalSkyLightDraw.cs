using Godot;
using San14.Data;

public sealed record OriginalSkyLightDeviceInput(uint SourceBindPoint, uint SourceDimension,
    Rid ShaderView, Rid OwnerTexture, ReadOnlyMemory<byte> SamplerDescriptor, string SourceResourceUri);

/// <summary>Native11973B0's four-VertexID strip and 32/192-byte independent
/// stage buffers. Complete light/camera values, LUT/cube and samplers are
/// caller-supplied. Fixed no-depth/no-blend is this backend adapter's explicit
/// coverage state, not an observation of the native active pipeline.</summary>
public sealed class OriginalSkyLightDraw : IAsyncDisposable
{
    public byte[] VertexConstantBytes { get; }
    public byte[] PixelConstantBytes { get; }
    public OriginalCubeRenderDeviceSurface Target { get; }
    private readonly IReadOnlyList<Rid> _owned;
    private readonly Rid _pipeline, _vertexSet, _pixelSet;
    private Task? _disposal;

    private OriginalSkyLightDraw(byte[] vertex, byte[] pixel, OriginalCubeRenderDeviceSurface target,
        Rid pipeline, Rid vs, Rid ps, List<Rid> owned)
        => (VertexConstantBytes, PixelConstantBytes, Target, _pipeline, _vertexSet, _pixelSet, _owned)
            = (vertex, pixel, target, pipeline, vs, ps, owned.AsReadOnly());

    public static Task<OriginalSkyLightDraw> BuildAsync(OriginalSkyLightProgram program,
        OriginalCubeRenderTextureDevice target, uint surfaceId, OriginalSkyLightDrawInputs inputs,
        ReadOnlyMemory<byte> vertexTemplate, ReadOnlyMemory<byte> pixelTemplate,
        IReadOnlyList<OriginalSkyLightDeviceInput> explicitTextures)
    {
        ArgumentNullException.ThrowIfNull(program); ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(inputs); ArgumentNullException.ThrowIfNull(explicitTextures);
        if (surfaceId >= target.Surfaces.Count || target.Surfaces[(int)surfaceId].Source.MipLevel != 0 ||
            inputs.Vertex.MipInfo.W != target.Surfaces[(int)surfaceId].Source.Face)
            throw new InvalidDataException("Sky light requires an explicit base target and matching native face index.");
        byte[] vc = OriginalSkyLightConstants.Vertex(program.VertexBindings.OriginalDefinitions, vertexTemplate.Span, inputs.Vertex);
        byte[] pc = OriginalSkyLightConstants.Pixel(program.PixelBindings.OriginalDefinitions, pixelTemplate.Span, inputs.Pixel);
        var textures = explicitTextures.Select(t => t with { SamplerDescriptor = t.SamplerDescriptor.ToArray() }).ToArray();
        if (textures.Length != 2 || textures.Select(t => t.SourceBindPoint).Distinct().Count() != 2)
            throw new InvalidDataException("Sky light requires t0 LUT and t3 cube exactly once.");
        foreach (var b in program.PixelBindings.Bindings.Where(b => b.DescriptorKind == "Texture"))
        {
            var t = textures.SingleOrDefault(t => t.SourceBindPoint == b.Source.BindPoint);
            if (t is null || t.SourceDimension != b.Source.Dimension || t.SamplerDescriptor.Length != 52 ||
                string.IsNullOrWhiteSpace(t.SourceResourceUri) || t.OwnerTexture == target.TextureRid ||
                target.Surfaces.Any(s => s.TargetView == t.ShaderView))
                throw new InvalidDataException("Sky light source type, sampler, provenance or read/write overlap differs.");
        }
        var surface = target.Surfaces[(int)surfaceId];
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var owned = new List<Rid>();
            Rid Own(Rid r) { if (!r.IsValid) throw new InvalidDataException("Sky light device allocation failed."); owned.Add(r); return r; }
            try
            {
                if (!rd.FramebufferIsValid(surface.Framebuffer)) throw new InvalidDataException("Sky light target is no longer live.");
                var samplers = new Dictionary<uint, Rid>();
                foreach (var t in textures)
                {
                    if (!rd.TextureIsValid(t.ShaderView) || !rd.TextureIsValid(t.OwnerTexture))
                        throw new InvalidDataException("Sky light input is no longer live.");
                    using var f = rd.TextureGetFormat(t.ShaderView);
                    if ((t.SourceDimension == 4 && f.TextureType != RenderingDevice.TextureType.Type2D) ||
                        (t.SourceDimension == 9 && f.TextureType != RenderingDevice.TextureType.Cube) ||
                        t.SourceDimension is not (4 or 9)) throw new InvalidDataException("Sky light device texture dimension differs.");
                    samplers.Add(t.SourceBindPoint, Own(OriginalDeviceSampler.Create(rd, t.SamplerDescriptor.Span)));
                }
                Rid vcb = Own(rd.UniformBufferCreate(32, vc)), pcb = Own(rd.UniformBufferCreate(192, pc));
                Rid Set(OriginalTranslatedShaderBindings bindings, uint set, Rid cb)
                {
                    var uniforms = new Godot.Collections.Array<RDUniform>();
                    try
                    {
                        foreach (var binding in bindings.Bindings)
                        {
                            RenderingDevice.UniformType type; Rid input;
                            if (binding.DescriptorKind == "UniformBuffer" && binding.Source.BindPoint == 0)
                                (type, input) = (RenderingDevice.UniformType.UniformBuffer, cb);
                            else if (binding.DescriptorKind == "Sampler" && samplers.TryGetValue(binding.Source.BindPoint, out var sampler))
                                (type, input) = (RenderingDevice.UniformType.Sampler, sampler);
                            else if (binding.DescriptorKind == "Texture")
                                (type, input) = (RenderingDevice.UniformType.Texture, textures.Single(t => t.SourceBindPoint == binding.Source.BindPoint).ShaderView);
                            else throw new InvalidDataException("Sky light descriptor is unsupported.");
                            var u = new RDUniform { Binding = checked((int)binding.Device.Binding!.Value), UniformType = type };
                            u.AddId(input); uniforms.Add(u);
                        }
                        Rid result = Own(rd.UniformSetCreate(uniforms, program.ShaderRid, set));
                        if (!rd.UniformSetIsValid(result)) throw new InvalidDataException("Sky light uniform set is invalid.");
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
                Rid pipeline = Own(rd.RenderPipelineCreate(program.ShaderRid, rd.FramebufferGetFormat(surface.Framebuffer), -1,
                    RenderingDevice.RenderPrimitive.TriangleStrips, raster, multi, depth, blend));
                if (!rd.RenderPipelineIsValid(pipeline)) throw new InvalidDataException("Sky light pipeline is invalid.");
                return new OriginalSkyLightDraw(vc, pc, surface, pipeline, vs, ps, owned);
            }
            catch { for (int i = owned.Count - 1; i >= 0; i--) rd.FreeRid(owned[i]); throw; }
        });
    }

    public Task DrawAsync()
    {
        ObjectDisposedException.ThrowIf(_disposal is not null, this);
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            ObjectDisposedException.ThrowIf(_disposal is not null, this);
            long list = rd.DrawListBegin(Target.Framebuffer);
            try { rd.DrawListBindRenderPipeline(list, _pipeline); rd.DrawListBindUniformSet(list, _pixelSet, 0);
                rd.DrawListBindUniformSet(list, _vertexSet, 1); rd.DrawListDraw(list, false, 1, 4); }
            finally { rd.DrawListEnd(); } return true;
        });
    }
    public ValueTask DisposeAsync() => new(_disposal ??= OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
    { for (int i = _owned.Count - 1; i >= 0; i--) rd.FreeRid(_owned[i]); return true; }));
}
