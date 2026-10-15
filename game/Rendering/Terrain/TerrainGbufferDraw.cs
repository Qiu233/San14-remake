using System.Buffers.Binary;
using Godot;
using San14.Data;

/// <summary>Explicit own raster state. Recovering the original state is a separate input.</summary>
public sealed record TerrainGbufferRasterState(RenderingDevice.PolygonCullMode Cull,
    bool DepthTest, bool DepthWrite, RenderingDevice.CompareOperator DepthCompare)
{
    /// <summary>Null retains the backend default used by existing callers.
    /// Source production may supply the current inherited frontend winding.</summary>
    public RenderingDevice.PolygonFrontFace? FrontFace { get; init; }
    /// <summary>Five explicit RGBA nibbles, target0 in the low four bits.
    /// Null preserves existing backend callers' all-channel writes. This is
    /// device input; a current source recipe must establish production values.</summary>
    public uint? PackedColorWriteMasks { get; init; }
    /// <summary>Explicit device state. Supplying this does not prove that the
    /// original Landscape renderer selected these values in the current frame.</summary>
    public TerrainGbufferStencilState? Stencil { get; init; }
}

public sealed record TerrainGbufferStencilFace(RenderingDevice.CompareOperator Compare,
    RenderingDevice.StencilOperation Fail, RenderingDevice.StencilOperation DepthFail,
    RenderingDevice.StencilOperation Pass, uint Reference, uint ReadMask, uint WriteMask)
{
    internal void Validate()
    {
        if ((uint)Compare > 7 || (uint)Fail > 7 || (uint)DepthFail > 7 || (uint)Pass > 7 ||
            Reference > 255 || ReadMask > 255 || WriteMask > 255)
            throw new InvalidDataException("Terrain stencil requires supported device operations and eight-bit reference/masks.");
    }
}
public sealed record TerrainGbufferStencilState(TerrainGbufferStencilFace Front, TerrainGbufferStencilFace Back)
{
    internal void Validate()
    { ArgumentNullException.ThrowIfNull(Front); ArgumentNullException.ThrowIfNull(Back); Front.Validate(); Back.Validate(); }
    internal void Apply(RDPipelineDepthStencilState depth)
    {
        Validate(); depth.EnableStencil = true;
        depth.FrontOpCompare = Front.Compare; depth.BackOpCompare = Back.Compare;
        depth.FrontOpFail = Front.Fail; depth.BackOpFail = Back.Fail;
        depth.FrontOpDepthFail = Front.DepthFail; depth.BackOpDepthFail = Back.DepthFail;
        depth.FrontOpPass = Front.Pass; depth.BackOpPass = Back.Pass;
        depth.FrontOpReference = Front.Reference; depth.BackOpReference = Back.Reference;
        depth.FrontOpCompareMask = Front.ReadMask; depth.BackOpCompareMask = Back.ReadMask;
        depth.FrontOpWriteMask = Front.WriteMask; depth.BackOpWriteMask = Back.WriteMask;
    }
}

/// <summary>One original patch drawn by its original VS/PS into the five MRTs.
/// Caller owns program, textures and framebuffer, which must outlive this draw.</summary>
public sealed class TerrainGbufferDraw : IAsyncDisposable
{
    public TerrainGbufferConstantPacket VertexConstants { get; private set; }
    public TerrainGbufferConstantPacket PixelConstants { get; private set; }
    public int VertexCount { get; }
    public int IndexCount { get; }
    public int InstanceCount { get; }
    private readonly Rid _pipeline, _vertices, _indices, _pixelSet, _vertexSet, _framebuffer;
    private readonly Rid _vertexConstantsBuffer, _pixelConstantsBuffer;
    private readonly TerrainGbufferProgram _program;
    public TerrainGbufferProgram Program => _program;
    private readonly IReadOnlyList<DeferredDeviceBinding> _pixelSamplerBindings;
    internal IReadOnlyList<DeferredDeviceBinding> RequireActualPixelSamplerBindings()
    {ObjectDisposedException.ThrowIf(_retiring,this);return _pixelSamplerBindings;}
    private readonly RenderDeviceResources _resources;
    private volatile bool _retiring;

    private TerrainGbufferDraw(TerrainGbufferConstantPacket vertex, TerrainGbufferConstantPacket pixel,
        int vertexCount, int indexCount, int instanceCount, Rid pipeline, Rid vertices, Rid indices, Rid pixelSet, Rid vertexSet,
        Rid framebuffer, RenderDeviceResources resources, TerrainGbufferProgram program, Rid vertexConstantsBuffer, Rid pixelConstantsBuffer,
        IReadOnlyList<DeferredDeviceBinding> pixelSamplerBindings)
        => (VertexConstants, PixelConstants, VertexCount, IndexCount, InstanceCount, _pipeline, _vertices, _indices,
            _pixelSet, _vertexSet, _framebuffer, _resources, _program, _vertexConstantsBuffer, _pixelConstantsBuffer,_pixelSamplerBindings) =
            (vertex, pixel, vertexCount, indexCount, instanceCount, pipeline, vertices, indices, pixelSet,
                vertexSet, framebuffer, resources, program, vertexConstantsBuffer, pixelConstantsBuffer,pixelSamplerBindings);

    public static Task<TerrainGbufferDraw> BuildAsync(TerrainGbufferProgram program,
        TerrainGbufferTextures textures, RenderTargetFrame frame, TerrainPatchInput patch,
        TerrainGbufferDrawInputs inputs, TerrainGbufferRasterState state)
        => BuildAsync(program, textures, frame, new[] { patch }, inputs, state);

    /// <summary>One compatible shader/grid group, retaining each 56-byte source instance.
    /// Detail and coarse groups are submitted separately by the caller.</summary>
    public static async Task<TerrainGbufferDraw> BuildAsync(TerrainGbufferProgram program,
        TerrainGbufferTextures textures, RenderTargetFrame frame, IReadOnlyList<TerrainPatchInput> patches,
        TerrainGbufferDrawInputs inputs, TerrainGbufferRasterState state)
    {
        ArgumentNullException.ThrowIfNull(program); ArgumentNullException.ThrowIfNull(textures);
        ArgumentNullException.ThrowIfNull(frame); ArgumentNullException.ThrowIfNull(patches);
        ArgumentNullException.ThrowIfNull(inputs); ArgumentNullException.ThrowIfNull(state);
        if (!ReferenceEquals(program.Source, textures.Source) || frame.ColorViews.Count != 5)
            throw new InvalidDataException("Terrain draw source or five-MRT contract differs.");
        state.Stencil?.Validate();
        if (state.PackedColorWriteMasks is uint packedMasks && (packedMasks & ~0xFFFFFu) != 0)
            throw new InvalidDataException("Terrain color writes require exactly five RGBA nibbles.");
        var vertex = program.Preparation.Stages.VertexConstants.Pack(inputs);
        var pixel = program.Preparation.Stages.PixelConstants.Pack(inputs, program.Source.Palette);
        if (patches.Count == 0 || patches.Any(p => p is null || p.GridSize != patches[0].GridSize ||
            (p.EdgeFlags & 0xFFFF0000) != 0 || !float.IsFinite(p.ControlFrequency) || p.ControlFrequency <= 0 ||
            !float.IsFinite(p.ControlCount) || p.ControlCount <= 1 ||
            !Finite(p.Origin) || !Finite(p.Extent) || p.Extent.X <= 0 || p.Extent.Z <= 0 ||
            !float.IsFinite(p.UvBounds.X) || !float.IsFinite(p.UvBounds.Y) || !float.IsFinite(p.UvBounds.Z) || !float.IsFinite(p.UvBounds.W)))
            throw new InvalidDataException("Terrain batch requires finite instances with one supported source grid.");
        int n = checked((int)patches[0].GridSize), vertexCount = checked(n * n), instanceCount = patches.Count;
        byte[] positions = TerrainPatchInputMath.EncodePositionStream(n), instance = new byte[checked(instanceCount * 56)];
        for (int i = 0; i < instanceCount; i++) patches[i].EncodeInstance().CopyTo(instance, i * 56);
        ushort[] sourceIndices = TerrainPatchInputMath.TriangleIndices(n);
        byte[] indexBytes = new byte[sourceIndices.Length * 2];
        for (int i = 0; i < sourceIndices.Length; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(indexBytes.AsSpan(i * 2, 2), sourceIndices[i]);
        return await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var resources = new RenderDeviceResources("Original terrain draw allocation failed.");
            Rid Own(Rid rid) => resources.Own(rid);
            try
            {
                if (state.Stencil is not null)
                {
                    if (!frame.DepthView.IsValid) throw new InvalidDataException("Terrain stencil requires its actual depth/stencil attachment.");
                    using var depthFormat = rd.TextureGetFormat(frame.DepthView);
                    if (depthFormat.Format != RenderingDevice.DataFormat.D32SfloatS8Uint ||
                        !depthFormat.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit))
                        throw new InvalidDataException("Terrain stencil requires the actual D32SfloatS8Uint attachment.");
                }
                Rid vertexBuffer = Own(rd.VertexBufferCreate((uint)positions.Length, positions));
                Rid instanceBuffer = Own(rd.VertexBufferCreate((uint)instance.Length, instance));
                Rid indexBuffer = Own(rd.IndexBufferCreate((uint)sourceIndices.Length, RenderingDevice.IndexBufferFormat.Uint16, indexBytes));
                Rid indices = Own(rd.IndexArrayCreate(indexBuffer, 0, (uint)sourceIndices.Length));
                using var position = new RDVertexAttribute { Location = 1, Binding = 0, Offset = 0, Stride = 12,
                    Format = RenderingDevice.DataFormat.R32G32B32Sfloat, Frequency = RenderingDevice.VertexFrequency.Vertex };
                using var origin = new RDVertexAttribute { Location = 2, Binding = 1, Offset = 0, Stride = 56,
                    Format = RenderingDevice.DataFormat.R32G32B32Sfloat, Frequency = RenderingDevice.VertexFrequency.Instance };
                using var scale = new RDVertexAttribute { Location = 3, Binding = 1, Offset = 12, Stride = 56,
                    Format = RenderingDevice.DataFormat.R32G32B32Sfloat, Frequency = RenderingDevice.VertexFrequency.Instance };
                using var uv = new RDVertexAttribute { Location = 4, Binding = 1, Offset = 24, Stride = 56,
                    Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat, Frequency = RenderingDevice.VertexFrequency.Instance };
                using var flags = new RDVertexAttribute { Location = 5, Binding = 1, Offset = 40, Stride = 56,
                    Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat, Frequency = RenderingDevice.VertexFrequency.Instance };
                long format = rd.VertexFormatCreate([position, origin, scale, uv, flags]);
                Rid vertices = Own(rd.VertexArrayCreate((uint)vertexCount, format, [vertexBuffer, instanceBuffer]));
                Rid vb = Own(rd.UniformBufferCreate((uint)vertex.Bytes.Length, vertex.Bytes));
                Rid pb = Own(rd.UniformBufferCreate((uint)pixel.Bytes.Length, pixel.Bytes));
                var pixelSamplerBindings=new List<DeferredDeviceBinding>();
                Rid Uniforms(TranslatedShaderBindings bindings, uint set, Rid cb, bool vertexStage)
                {
                    var uniforms = new Godot.Collections.Array<RDUniform>();
                    try
                    {
                        foreach (var binding in bindings.Bindings)
                        {
                            var uniform = new RDUniform { Binding = checked((int)binding.Device.Binding!.Value),
                                UniformType = binding.DescriptorKind switch {
                                    "UniformBuffer" => RenderingDevice.UniformType.UniformBuffer,
                                    "Sampler" => RenderingDevice.UniformType.Sampler,
                                    "Texture" => RenderingDevice.UniformType.Texture,
                                    _ => throw new InvalidDataException("Unsupported terrain descriptor kind.") } };
                            uniforms.Add(uniform);
                            Rid resource;
                            if (binding.ConstantBuffer is not null) resource = cb;
                            else
                            {
                                TerrainGbufferDeviceTexture texture = vertexStage
                                    ? binding.Source.BindPoint == 8 ? textures.Height : throw new InvalidDataException("Unknown terrain vertex texture register.")
                                    : textures.Pixel.TryGetValue(binding.Source.BindPoint, out var found) ? found
                                        : throw new InvalidDataException("Missing original terrain texture register.");
                                resource = binding.DescriptorKind == "Sampler" ? texture.Sampler : texture.Texture;
                                if(!vertexStage&&binding.DescriptorKind=="Sampler")
                                    pixelSamplerBindings.Add(new("Sampler",binding.Source.BindPoint,resource,null,
                                        texture.SourceUri+";actual-original-terrain-PS-uniform-set;descriptor52="+
                                        Convert.ToHexString(texture.Recipe.EncodeD3D11Descriptor())));
                            }
                            uniform.AddId(resource);
                        }
                        Rid result = Own(rd.UniformSetCreate(uniforms, program.ShaderRid, set));
                        if (!rd.UniformSetIsValid(result)) throw new InvalidDataException("Terrain uniform set is invalid.");
                        return result;
                    }
                    finally { foreach (var u in uniforms) u.Dispose(); }
                }
                Rid pixelSet = Uniforms(program.PixelBindings, 0, pb, false), vertexSet = Uniforms(program.VertexBindings, 1, vb, true);
                using var raster = new RDPipelineRasterizationState { CullMode = state.Cull };
                if (state.FrontFace is { } frontFace) raster.FrontFace = frontFace;
                using var multisample = new RDPipelineMultisampleState();
                using var depth = new RDPipelineDepthStencilState {
                    EnableDepthTest = state.DepthTest, EnableDepthWrite = state.DepthWrite, DepthCompareOperator = state.DepthCompare };
                state.Stencil?.Apply(depth);
                var attachments = new Godot.Collections.Array<RDPipelineColorBlendStateAttachment>();
                for (int i = 0; i < 5; i++)
                {
                    uint mask = ((state.PackedColorWriteMasks ?? 0xFFFFFu) >> (i * 4)) & 15;
                    attachments.Add(new() { EnableBlend = false,
                        WriteR = (mask & 1) != 0, WriteG = (mask & 2) != 0,
                        WriteB = (mask & 4) != 0, WriteA = (mask & 8) != 0 });
                }
                using var blend = new RDPipelineColorBlendState { Attachments = attachments };
                Rid pipeline;
                try { pipeline = Own(rd.RenderPipelineCreate(program.ShaderRid, rd.FramebufferGetFormat(frame.Framebuffer), format,
                    RenderingDevice.RenderPrimitive.Triangles, raster, multisample, depth, blend)); }
                finally { foreach (var a in attachments) a.Dispose(); }
                if (!rd.RenderPipelineIsValid(pipeline)) throw new InvalidDataException("Terrain draw pipeline is invalid.");
                return new TerrainGbufferDraw(vertex, pixel, vertexCount, sourceIndices.Length, instanceCount,
                    pipeline, vertices, indices, pixelSet, vertexSet, frame.Framebuffer, resources, program, vb, pb,
                    Array.AsReadOnly(pixelSamplerBindings.ToArray()));
            }
            catch (Exception failure)
            {
                resources.ReleaseAfterConstructionFailure(rd, failure, "Terrain-draw-partial");
                throw;
            }
        });
    }

    /// <summary>Update source camera/material inputs without recreating the
    /// shader, texture uploads, geometry or pipeline. Await before drawing.
    /// A changed LOD/instance batch is built separately with shared resources.</summary>
    public Task UpdateConstantsAsync(TerrainGbufferDrawInputs inputs)
    {
        ObjectDisposedException.ThrowIf(_retiring, this);
        var vertex = _program.Preparation.Stages.VertexConstants.Pack(inputs);
        var pixel = _program.Preparation.Stages.PixelConstants.Pack(inputs, _program.Source.Palette);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd => {
            ObjectDisposedException.ThrowIf(_retiring, this);
            if(rd.BufferUpdate(_vertexConstantsBuffer,0,(uint)vertex.Bytes.Length,vertex.Bytes)!=Error.Ok ||
                rd.BufferUpdate(_pixelConstantsBuffer,0,(uint)pixel.Bytes.Length,pixel.Bytes)!=Error.Ok)
                throw new InvalidDataException("Device rejected original terrain constant updates.");
            VertexConstants=vertex; PixelConstants=pixel; return true;
        });
    }

    public Task DrawAsync(RenderingDevice.DrawFlags actions, Color[] clearColors, float clearDepth, uint clearStencil)
    {
        ObjectDisposedException.ThrowIf(_retiring, this);
        Color[] colors = clearColors.ToArray();
        return TerrainDeviceTexture.OnRenderThreadAsync(rd => {
            ObjectDisposedException.ThrowIf(_retiring, this);
            long list = rd.DrawListBegin(_framebuffer, actions, colors, clearDepth, clearStencil);
            try {
                rd.DrawListBindRenderPipeline(list, _pipeline);
                rd.DrawListBindUniformSet(list, _pixelSet, 0); rd.DrawListBindUniformSet(list, _vertexSet, 1);
                rd.DrawListBindVertexArray(list, _vertices); rd.DrawListBindIndexArray(list, _indices);
                rd.DrawListDraw(list, true, (uint)InstanceCount);
            }
            finally { rd.DrawListEnd(); }
            return true;
        });
    }
    public ValueTask DisposeAsync()
    {
        _retiring = true;
        return _resources.DisposeAsync();
    }
    private static bool Finite(System.Numerics.Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
