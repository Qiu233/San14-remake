using System.Security.Cryptography;
using Godot;
using San14.Data;

public sealed record OriginalSkyStarRasterState(RenderingDevice.PolygonCullMode Cull,
    bool DepthTest, bool DepthWrite, RenderingDevice.CompareOperator DepthCompare);

/// <summary>Original point stream drawn as four-vertex strip instances through
/// the original VS/GS body and PS. Target and star texture remain borrowed.
/// Native additive blend is fixed; retained write mask/raster state are explicit.</summary>
public sealed class OriginalSkyStarDraw : IAsyncDisposable
{
    public OriginalSkyStarConstantPacket Constants { get; private set; }
    public OriginalSkyStarGeometry Geometry { get; }
    public OriginalCubeRenderDeviceSurface Target { get; }
    public bool UsesAnalyticPixel { get; }
    private readonly OriginalSkyStarProgram _program;
    private readonly Rid _pipeline, _vertices, _vs, _gs, _ps, _vcb, _gcb, _pcb;
    private readonly IReadOnlyList<Rid> _owned;
    private Task? _disposal;
    private OriginalSkyStarDraw(OriginalSkyStarProgram program, OriginalSkyStarConstantPacket packet,
        OriginalSkyStarGeometry geometry, OriginalCubeRenderDeviceSurface target,
        Rid pipeline, Rid vertices, Rid vs, Rid gs, Rid ps, Rid vcb, Rid gcb, Rid pcb, List<Rid> owned, bool analytic)
        => (_program, Constants, Geometry, Target, _pipeline, _vertices, _vs, _gs, _ps, _vcb, _gcb, _pcb, _owned, UsesAnalyticPixel)
            = (program, packet, geometry, target, pipeline, vertices, vs, gs, ps, vcb, gcb, pcb, owned.AsReadOnly(), analytic);

    public static Task<OriginalSkyStarDraw> BuildAsync(OriginalSkyStarProgram program,
        OriginalCubeRenderTextureDevice work, uint surfaceId, OriginalSkyStarGeometry geometry,
        ReadOnlyMemory<byte> vertexConstants, ReadOnlyMemory<byte> geometryConstants, ReadOnlyMemory<byte> pixelConstants,
        OriginalSky2DeviceInput? starTexture, uint explicitColorWriteMask, OriginalSkyStarRasterState explicitRaster)
    {
        ArgumentNullException.ThrowIfNull(program); ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(geometry); ArgumentNullException.ThrowIfNull(explicitRaster);
        if (surfaceId >= work.Surfaces.Count || work.Surfaces[(int)surfaceId].Source.MipLevel != 0 ||
            geometry.PointCount == 0 || explicitColorWriteMask > 15 || explicitRaster.DepthTest || explicitRaster.DepthWrite ||
            starTexture is not null && (starTexture.SourceBindPoint != 0 || starTexture.SourceDimension != 4 ||
            starTexture.OriginalSamplerDescriptor.Length != 52 || string.IsNullOrWhiteSpace(starTexture.SourceResourceUri) ||
            starTexture.OwnerTexture == work.TextureRid || work.Surfaces.Any(s => s.TargetView == starTexture.ShaderView)))
            throw new InvalidDataException("Star draw needs a nonempty source stream, base WORK face, distinct2D texture and explicit mask/raster contract; this color-only backend has no depth attachment.");
        var retained = OriginalSkyStarGeometry.ReadExplicit(geometry.VertexBytes, geometry.PointCount,
            geometry.SourceResourceUri, geometry.SourceSha256);
        if (retained.VertexPayloadSha256 != geometry.VertexPayloadSha256)
            throw new InvalidDataException("Star geometry changed after its source read.");
        var packet = OriginalSkyStarConstants.Read(program.VertexDefinitions, program.GeometryDefinitions,
            program.PixelBindings.OriginalDefinitions, vertexConstants.Span, geometryConstants.Span, pixelConstants.Span);
        bool analytic = starTexture is null;
        if (analytic) OriginalSkyStarPixelAdapter.ValidateAnalyticPacket(packet.PixelBytes);
        var input = starTexture is null ? null : starTexture with { OriginalSamplerDescriptor = starTexture.OriginalSamplerDescriptor.ToArray() };
        Rid shader = analytic ? program.AnalyticShaderRid : program.ShaderRid;
        var target = work.Surfaces[(int)surfaceId];
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var owned = new List<Rid>();
            Rid Own(Rid r) { if (!r.IsValid) throw new InvalidDataException("Star device allocation failed."); owned.Add(r); return r; }
            try
            {
                if (!rd.FramebufferIsValid(target.Framebuffer) || input is not null && (!rd.TextureIsValid(input.ShaderView) || !rd.TextureIsValid(input.OwnerTexture)))
                    throw new InvalidDataException("Star borrowed target or input is no longer live.");
                if (input is not null)
                {
                    using var tf = rd.TextureGetFormat(input.ShaderView);
                    if (tf.TextureType != RenderingDevice.TextureType.Type2D || tf.ArrayLayers != 1 ||
                        tf.Format.ToString().Contains("Uint") || tf.Format.ToString().Contains("Sint"))
                        throw new InvalidDataException("Star input is not the original floating2D shape.");
                }
                Rid vb = Own(rd.VertexBufferCreate((uint)retained.VertexBytes.Length, retained.VertexBytes.ToArray()));
                using var position = new RDVertexAttribute { Location = 0, Binding = 0, Offset = 0, Stride = 20,
                    Format = RenderingDevice.DataFormat.R32G32B32Sfloat, Frequency = RenderingDevice.VertexFrequency.Instance };
                using var size = new RDVertexAttribute { Location = 1, Binding = 0, Offset = 12, Stride = 20,
                    Format = RenderingDevice.DataFormat.R32Sfloat, Frequency = RenderingDevice.VertexFrequency.Instance };
                using var colorAttribute = new RDVertexAttribute { Location = 2, Binding = 0, Offset = 16, Stride = 20,
                    Format = RenderingDevice.DataFormat.R8G8B8A8Unorm, Frequency = RenderingDevice.VertexFrequency.Instance };
                long format = rd.VertexFormatCreate([position, size, colorAttribute]);
                Rid vertices = Own(rd.VertexArrayCreate(4, format, [vb]));
                Rid vcb = Own(rd.UniformBufferCreate(352, packet.VertexBytes)), gcb = Own(rd.UniformBufferCreate(16, packet.GeometryBytes)),
                    pcb = Own(rd.UniformBufferCreate(16, packet.PixelBytes));
                Rid sampler = input is null ? default : Own(OriginalDeviceSampler.Create(rd, input.OriginalSamplerDescriptor.Span));
                Rid Set(uint set, IEnumerable<(uint Binding, RenderingDevice.UniformType Type, Rid Resource)> rows)
                {
                    var uniforms = new Godot.Collections.Array<RDUniform>();
                    try
                    {
                        foreach (var row in rows) { var u = new RDUniform { Binding = (int)row.Binding, UniformType = row.Type }; u.AddId(row.Resource); uniforms.Add(u); }
                        Rid result = Own(rd.UniformSetCreate(uniforms, shader, set));
                        if (!rd.UniformSetIsValid(result)) throw new InvalidDataException("Star uniform set is invalid."); return result;
                    }
                    finally { foreach (var u in uniforms) u.Dispose(); }
                }
                Rid vs = Set(1, [(0, RenderingDevice.UniformType.UniformBuffer, vcb)]),
                    gs = Set(2, [(0, RenderingDevice.UniformType.UniformBuffer, gcb)]);
                Rid ps = Set(0, program.PixelBindings.Bindings.Where(b => !analytic || b.DescriptorKind == "UniformBuffer").Select(b => (b.Device.Binding!.Value,
                    b.DescriptorKind switch { "UniformBuffer" => RenderingDevice.UniformType.UniformBuffer,
                        "Sampler" => RenderingDevice.UniformType.Sampler, "Texture" => RenderingDevice.UniformType.Texture,
                        _ => throw new InvalidDataException("Unsupported star descriptor.") },
                    b.DescriptorKind switch { "UniformBuffer" => pcb, "Sampler" => sampler, _ => input!.ShaderView })));
                using var raster = new RDPipelineRasterizationState { CullMode = explicitRaster.Cull };
                using var multi = new RDPipelineMultisampleState();
                using var depth = new RDPipelineDepthStencilState { EnableDepthTest = explicitRaster.DepthTest,
                    EnableDepthWrite = explicitRaster.DepthWrite, DepthCompareOperator = explicitRaster.DepthCompare };
                using var color = new RDPipelineColorBlendStateAttachment { EnableBlend = true,
                    SrcColorBlendFactor = RenderingDevice.BlendFactor.One, DstColorBlendFactor = RenderingDevice.BlendFactor.One,
                    ColorBlendOp = RenderingDevice.BlendOperation.Add, SrcAlphaBlendFactor = RenderingDevice.BlendFactor.One,
                    DstAlphaBlendFactor = RenderingDevice.BlendFactor.One, AlphaBlendOp = RenderingDevice.BlendOperation.Add,
                    WriteR = (explicitColorWriteMask & 1) != 0, WriteG = (explicitColorWriteMask & 2) != 0,
                    WriteB = (explicitColorWriteMask & 4) != 0, WriteA = (explicitColorWriteMask & 8) != 0 };
                using var blend = new RDPipelineColorBlendState { Attachments = [color] };
                Rid pipeline = Own(rd.RenderPipelineCreate(shader, rd.FramebufferGetFormat(target.Framebuffer), format,
                    RenderingDevice.RenderPrimitive.TriangleStrips, raster, multi, depth, blend));
                if (!rd.RenderPipelineIsValid(pipeline)) throw new InvalidDataException("Expanded star pipeline is invalid.");
                return new OriginalSkyStarDraw(program, packet, retained, target, pipeline, vertices, vs, gs, ps, vcb, gcb, pcb, owned, analytic);
            }
            catch { for (int i = owned.Count - 1; i >= 0; i--) rd.FreeRid(owned[i]); throw; }
        });
    }
    public Task UpdateConstantsAsync(ReadOnlyMemory<byte> vertex, ReadOnlyMemory<byte> geometry, ReadOnlyMemory<byte> pixel)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null, this);
        var packet = OriginalSkyStarConstants.Read(_program.VertexDefinitions, _program.GeometryDefinitions,
            _program.PixelBindings.OriginalDefinitions, vertex.Span, geometry.Span, pixel.Span);
        if (UsesAnalyticPixel) OriginalSkyStarPixelAdapter.ValidateAnalyticPacket(packet.PixelBytes);
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            ObjectDisposedException.ThrowIf(_disposal is not null, this);
            if (rd.BufferUpdate(_vcb, 0, 352, packet.VertexBytes) != Error.Ok || rd.BufferUpdate(_gcb, 0, 16, packet.GeometryBytes) != Error.Ok ||
                rd.BufferUpdate(_pcb, 0, 16, packet.PixelBytes) != Error.Ok) throw new InvalidDataException("Device rejected star packet update.");
            Constants = packet; return true;
        });
    }
    public Task DrawAsync()
    {
        ObjectDisposedException.ThrowIf(_disposal is not null, this);
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            ObjectDisposedException.ThrowIf(_disposal is not null, this);
            long list = rd.DrawListBegin(Target.Framebuffer, RenderingDevice.DrawFlags.DefaultAll);
            try { rd.DrawListBindRenderPipeline(list, _pipeline); rd.DrawListBindVertexArray(list, _vertices);
                rd.DrawListBindUniformSet(list, _ps, 0); rd.DrawListBindUniformSet(list, _vs, 1); rd.DrawListBindUniformSet(list, _gs, 2);
                rd.DrawListDraw(list, false, Geometry.PointCount, 4); }
            finally { rd.DrawListEnd(); } return true;
        });
    }
    public ValueTask DisposeAsync() => new(_disposal ??= OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
    { for (int i = _owned.Count - 1; i >= 0; i--) rd.FreeRid(_owned[i]); return true; }));
}
