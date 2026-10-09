using Godot;
using San14.Data;

public sealed record ModelDeviceBinding(string DescriptorKind, uint SourceBindPoint,
    Rid Resource, Rid? OwnerTexture, string SourceResourceUri);
public sealed record ModelStageInputs(IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> Constants,
    IReadOnlyList<ModelDeviceBinding> Resources);
/// <summary>Color/depth attachment views and every underlying texture parent
/// are explicit. Shared views cannot hide an input/output storage alias.</summary>
public sealed record ModelDrawTarget(Rid Framebuffer, IReadOnlyList<Rid> ColorTextures,
    Rid? DepthTexture, IReadOnlyList<Rid> OwnerTextures, string SourceResourceUri)
{
    public Army48DeviceFramebufferPass? Army48DevicePass {get;init;}
}
public sealed record ModelBlendAttachment(bool EnableBlend,
    RenderingDevice.BlendFactor SourceColor, RenderingDevice.BlendFactor DestinationColor,
    RenderingDevice.BlendOperation ColorOperation, RenderingDevice.BlendFactor SourceAlpha,
    RenderingDevice.BlendFactor DestinationAlpha, RenderingDevice.BlendOperation AlphaOperation,
    bool WriteR, bool WriteG, bool WriteB, bool WriteA);
public sealed record ModelRasterInputs(RenderingDevice.PolygonCullMode Cull,
    RenderingDevice.PolygonFrontFace FrontFace, bool DepthTest, bool DepthWrite,
    RenderingDevice.CompareOperator DepthCompare, IReadOnlyList<ModelBlendAttachment> Attachments)
{
    public ModelNativeStencilRaster? NativeStencil { get; init; }
}
public readonly record struct ModelConstantUpdateStatistics(int BufferUpdates, long UpdatedBytes)
{
    public static ModelConstantUpdateStatistics operator +(ModelConstantUpdateStatistics a,
        ModelConstantUpdateStatistics b) => new(a.BufferUpdates + b.BufferUpdates, a.UpdatedBytes + b.UpdatedBytes);
}

/// <summary>One original model draw using the original VS and optional PS, complete source
/// buffers, all original descriptors, and caller supplied raster state. Owns
/// CBs, uniform sets and pipeline; program, geometry, resources and targets
/// must outlive it. Does not infer the original active variant or lighting.</summary>
public sealed class ModelDraw : IAsyncDisposable
{
    public ModelShaderProgram Program { get; }
    public ModelGeometryDevice Geometry { get; }
    public int DrawIndex { get; }
    public ModelDrawTarget Target { get; }
    public IReadOnlyList<ModelDeviceBinding> VertexResources { get; }
    public IReadOnlyList<ModelDeviceBinding> PixelResources { get; }
    public IReadOnlyDictionary<uint, byte[]> VertexConstants => _vertexConstants.ToDictionary(p => p.Key, p => p.Value.ToArray());
    public IReadOnlyDictionary<uint, byte[]> PixelConstants => _pixelConstants.ToDictionary(p => p.Key, p => p.Value.ToArray());
    private readonly Dictionary<uint, byte[]> _vertexConstants, _pixelConstants;
    private readonly Dictionary<uint, Rid> _vertexBuffers, _pixelBuffers;
    private readonly Rid _pipeline;
    private readonly Rid? _vertexSet, _pixelSet;
    private readonly ModelDeviceDrawArrays _arrays;
    private readonly IReadOnlyList<Rid> _owned;
    private Task? _disposal;

    private ModelDraw(ModelShaderProgram program, ModelGeometryDevice geometry,
        int drawIndex, ModelDrawTarget target, ModelDeviceBinding[] vertexResources,
        ModelDeviceBinding[] pixelResources, Dictionary<uint, byte[]> vertexConstants,
        Dictionary<uint, byte[]> pixelConstants, Dictionary<uint, Rid> vertexBuffers,
        Dictionary<uint, Rid> pixelBuffers, Rid pipeline, Rid? vertexSet, Rid? pixelSet,
        ModelDeviceDrawArrays arrays, List<Rid> owned)
        => (Program, Geometry, DrawIndex, Target, VertexResources, PixelResources, _vertexConstants,
            _pixelConstants, _vertexBuffers, _pixelBuffers, _pipeline, _vertexSet, _pixelSet, _arrays, _owned) =
            (program, geometry, drawIndex, target, Array.AsReadOnly(vertexResources), Array.AsReadOnly(pixelResources),
                vertexConstants, pixelConstants, vertexBuffers, pixelBuffers, pipeline, vertexSet, pixelSet, arrays, owned.AsReadOnly());

    public static Task<ModelDraw> BuildAsync(ModelShaderProgram program,
        ModelGeometryDevice geometry, int drawIndex, ModelDrawTarget explicitTarget,
        ModelStageInputs explicitVertex, ModelStageInputs explicitPixel,
        ModelRasterInputs explicitRaster)
        => BuildCoreAsync(program, geometry, drawIndex, explicitTarget, explicitVertex, explicitPixel, explicitRaster, false);

    /// <summary>Original Army node48 retains six attachments and selects a
    /// retained device subpass from its unchanged PS's actual output mask.</summary>
    public static Task<ModelDraw> BuildArmy48Async(ModelShaderProgram program,
        ModelGeometryDevice geometry, int drawIndex, ModelDrawTarget explicitTarget,
        ModelStageInputs explicitVertex, ModelStageInputs explicitPixel,
        ModelRasterInputs explicitRaster, Army48TargetBinding actualArmyTarget)
    {
        ArgumentNullException.ThrowIfNull(actualArmyTarget); actualArmyTarget.RequireCurrent();
        Army48GraphicsContract.RequireProgram(program);
        actualArmyTarget.Frame.RequireArmyDeviceDrawTarget(explicitTarget,program.ColorOutputLocations);
        if (!explicitTarget.ColorTextures.SequenceEqual(actualArmyTarget.Frame.ColorViews) ||
            explicitTarget.DepthTexture != actualArmyTarget.Frame.DepthView ||
            explicitRaster.Attachments is not { Count: 6 })
            throw new InvalidDataException("Army pipeline must retain all six node48 views and actual color states.");
        return BuildCoreAsync(program, geometry, drawIndex, explicitTarget, explicitVertex, explicitPixel, explicitRaster, true);
    }

    private static Task<ModelDraw> BuildCoreAsync(ModelShaderProgram program,
        ModelGeometryDevice geometry, int drawIndex, ModelDrawTarget explicitTarget,
        ModelStageInputs explicitVertex, ModelStageInputs explicitPixel,
        ModelRasterInputs explicitRaster, bool army48)
    {
        ArgumentNullException.ThrowIfNull(program); ArgumentNullException.ThrowIfNull(geometry);
        program.RequireLive(); geometry.RequireLive();
        ArgumentNullException.ThrowIfNull(explicitTarget); ArgumentNullException.ThrowIfNull(explicitRaster);
        ArgumentNullException.ThrowIfNull(explicitVertex); ArgumentNullException.ThrowIfNull(explicitPixel);
        if (!geometry.Draws.TryGetValue(drawIndex, out var arrays))
            throw new InvalidDataException("Original model draw index is absent from the uploaded source.");
        if (!explicitTarget.Framebuffer.IsValid || explicitTarget.ColorTextures is null ||
            explicitTarget.ColorTextures.Count != (army48 ? 6 : program.ColorAttachmentCount) ||
            explicitTarget.ColorTextures.Any(t => !t.IsValid) ||
            explicitTarget.ColorTextures.Distinct().Count() != explicitTarget.ColorTextures.Count ||
            explicitTarget.OwnerTextures is null || explicitTarget.OwnerTextures.Count == 0 ||
            explicitTarget.OwnerTextures.Any(t=>!t.IsValid) ||
            explicitTarget.OwnerTextures.Distinct().Count()!=explicitTarget.OwnerTextures.Count ||
            string.IsNullOrWhiteSpace(explicitTarget.SourceResourceUri) ||
            explicitTarget.DepthTexture is Rid depth && (!depth.IsValid || explicitTarget.ColorTextures.Contains(depth)))
            throw new InvalidDataException("Original model draw target has invalid attachment identities.");
        if ((explicitRaster.DepthTest || explicitRaster.DepthWrite || explicitRaster.NativeStencil is not null) && explicitTarget.DepthTexture is null)
            throw new InvalidDataException("Original model depth state requires an explicit depth target.");
        if (program.ColorAttachmentCount == 0 && explicitTarget.DepthTexture is null)
            throw new InvalidDataException("A model with no color output requires an actual depth-only target.");
        if (explicitRaster.Attachments is null || explicitRaster.Attachments.Count != (army48 ? 6 : program.ColorAttachmentCount) ||
            explicitRaster.Attachments.Any(a => a is null))
            throw new InvalidDataException("Original model draw requires one complete explicit blend state per source output.");
        var target = explicitTarget with { ColorTextures = Array.AsReadOnly(explicitTarget.ColorTextures.ToArray()),
            OwnerTextures = Array.AsReadOnly(explicitTarget.OwnerTextures.ToArray()) };
        var attachments = explicitRaster.Attachments.ToArray();
        var pixelBindings = program.PixelStage?.Bindings;
        var vertexConstants = SnapshotConstants(program.VertexBindings, explicitVertex.Constants);
        var pixelConstants = SnapshotConstants(pixelBindings, explicitPixel.Constants);
        var vertexResources = SnapshotResources(program.VertexBindings, explicitVertex.Resources, target);
        var pixelResources = SnapshotResources(pixelBindings, explicitPixel.Resources, target);
        ValidateVertexInputs(program, geometry.Packet.Layouts.Single(l => l.Index == arrays.LayoutIndex));
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            program.RequireLive(); geometry.RequireLive();
            var owned = new List<Rid>(); var vertexBuffers = new Dictionary<uint, Rid>(); var pixelBuffers = new Dictionary<uint, Rid>();
            Rid Own(Rid value)
            { if (!value.IsValid) throw new InvalidDataException("Original model draw allocation failed."); owned.Add(value); return value; }
            try
            {
                if (!rd.FramebufferIsValid(target.Framebuffer) || target.ColorTextures.Any(t => !rd.TextureIsValid(t)) ||
                    target.OwnerTextures.Any(t=>!rd.TextureIsValid(t)) ||
                    target.DepthTexture is Rid depthParent && !rd.TextureIsValid(depthParent))
                    throw new InvalidDataException("Original model draw target is no longer live.");
                ValidateDeviceTextures(rd, program.VertexBindings, vertexResources);
                ValidateDeviceTextures(rd, pixelBindings, pixelResources);
                if (program.ColorAttachmentCount == 0)
                {
                    using var format = rd.TextureGetFormat(target.DepthTexture!.Value);
                    if (!format.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit))
                        throw new InvalidDataException("A model with no color output must bind a real depth attachment.");
                }
                if (explicitRaster.NativeStencil is not null)
                {
                    using var format = rd.TextureGetFormat(target.DepthTexture!.Value);
                    if (format.Format != RenderingDevice.DataFormat.D32SfloatS8Uint ||
                        !format.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit))
                        throw new InvalidDataException("Native model stencil requires the actual D32SfloatS8Uint attachment.");
                }
                foreach (var output in pixelBindings?.Interface.StageVariables.Where(v => v.StorageClass == 3)
                    ?? Enumerable.Empty<San14.Data.Formats.SpirvInterfaceVariable>())
                {
                    using var shape = rd.TextureGetFormat(target.ColorTextures[checked((int)output.Location!.Value)]);
                    var scalar = output.Type.Kind == "vector" ? output.Type.ComponentType! : output.Type;
                    bool uintFormat = shape.Format.ToString().Contains("Uint", StringComparison.Ordinal);
                    bool sintFormat = shape.Format.ToString().Contains("Sint", StringComparison.Ordinal);
                    if (!shape.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.ColorAttachmentBit) ||
                        uintFormat != (scalar is { Kind: "int", Signed: false }) ||
                        sintFormat != (scalar is { Kind: "int", Signed: true }))
                        throw new InvalidDataException("Original model color attachment differs from its source output scalar type.");
                }
                foreach (var cb in vertexConstants) vertexBuffers.Add(cb.Key, Own(rd.UniformBufferCreate((uint)cb.Value.Length, cb.Value)));
                foreach (var cb in pixelConstants) pixelBuffers.Add(cb.Key, Own(rd.UniformBufferCreate((uint)cb.Value.Length, cb.Value)));
                Rid? Uniforms(TranslatedShaderBindings? bindings, uint set,
                    Dictionary<uint, Rid> buffers, ModelDeviceBinding[] resources)
                {
                    if (bindings is null || bindings.Bindings.Count == 0) return null;
                    var uniforms = new Godot.Collections.Array<RDUniform>();
                    try
                    {
                        foreach (var declaration in bindings.Bindings)
                        {
                            var uniform = new RDUniform
                            {
                                Binding = checked((int)declaration.Device.Binding!.Value),
                                UniformType = declaration.DescriptorKind switch
                                {
                                    "UniformBuffer" => RenderingDevice.UniformType.UniformBuffer,
                                    "Texture" => RenderingDevice.UniformType.Texture,
                                    "TextureBuffer" => RenderingDevice.UniformType.TextureBuffer,
                                    "Sampler" => RenderingDevice.UniformType.Sampler,
                                    _ => throw new InvalidDataException("Unsupported original model descriptor kind.")
                                }
                            };
                            uniforms.Add(uniform);
                            uniform.AddId(declaration.DescriptorKind == "UniformBuffer" ? buffers[declaration.Source.BindPoint] :
                                resources.Single(r => r.DescriptorKind == declaration.DescriptorKind && r.SourceBindPoint == declaration.Source.BindPoint).Resource);
                        }
                        Rid value = Own(rd.UniformSetCreate(uniforms, program.ShaderRid, set));
                        if (!rd.UniformSetIsValid(value)) throw new InvalidDataException("Original model uniform set is invalid.");
                        return value;
                    }
                    finally { foreach (var uniform in uniforms) uniform.Dispose(); }
                }
                Rid? vertexSet = Uniforms(program.VertexBindings, 1, vertexBuffers, vertexResources);
                Rid? pixelSet = Uniforms(pixelBindings, 0, pixelBuffers, pixelResources);
                using var raster = new RDPipelineRasterizationState { CullMode = explicitRaster.Cull, FrontFace = explicitRaster.FrontFace };
                using var multi = new RDPipelineMultisampleState();
                using var depth = new RDPipelineDepthStencilState
                { EnableDepthTest = explicitRaster.DepthTest, EnableDepthWrite = explicitRaster.DepthWrite, DepthCompareOperator = explicitRaster.DepthCompare };
                if (explicitRaster.NativeStencil is { } stencil)
                {
                    depth.EnableStencil = true;
                    depth.FrontOpFail = depth.BackOpFail = RenderingDevice.StencilOperation.Keep;
                    depth.FrontOpDepthFail = depth.BackOpDepthFail = RenderingDevice.StencilOperation.Keep;
                    depth.FrontOpPass = depth.BackOpPass = RenderingDevice.StencilOperation.Replace;
                    depth.FrontOpCompare = depth.BackOpCompare = RenderingDevice.CompareOperator.Always;
                    depth.FrontOpCompareMask = depth.BackOpCompareMask = stencil.Source.DrawTimeState.ReadMask;
                    depth.FrontOpWriteMask = depth.BackOpWriteMask = stencil.Source.DrawTimeState.WriteMask;
                    depth.FrontOpReference = depth.BackOpReference = stencil.Source.DrawTimeState.Reference;
                }
                var blendAttachments = new Godot.Collections.Array<RDPipelineColorBlendStateAttachment>();
                try
                {
                    foreach (var a in attachments) blendAttachments.Add(new()
                    {
                        EnableBlend = a.EnableBlend, SrcColorBlendFactor = a.SourceColor, DstColorBlendFactor = a.DestinationColor,
                        ColorBlendOp = a.ColorOperation, SrcAlphaBlendFactor = a.SourceAlpha, DstAlphaBlendFactor = a.DestinationAlpha,
                        AlphaBlendOp = a.AlphaOperation, WriteR = a.WriteR, WriteG = a.WriteG, WriteB = a.WriteB, WriteA = a.WriteA
                    });
                    using var blend = new RDPipelineColorBlendState { Attachments = blendAttachments };
                    Rid pipeline = Own(rd.RenderPipelineCreate(program.ShaderRid, rd.FramebufferGetFormat(target.Framebuffer), arrays.VertexFormat,
                        RenderingDevice.RenderPrimitive.Triangles, raster, multi, depth, blend));
                    if (!rd.RenderPipelineIsValid(pipeline)) throw new InvalidDataException("Original model pipeline was rejected.");
                    return new ModelDraw(program, geometry, drawIndex, target, vertexResources, pixelResources,
                        vertexConstants, pixelConstants, vertexBuffers, pixelBuffers, pipeline, vertexSet, pixelSet, arrays, owned);
                }
                finally { foreach (var a in blendAttachments) a.Dispose(); }
            }
            catch { for (int i = owned.Count - 1; i >= 0; i--) rd.FreeRid(owned[i]); throw; }
        });
    }

    private static Dictionary<uint, byte[]> SnapshotConstants(TranslatedShaderBindings? bindings,
        IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> constants)
    {
        ArgumentNullException.ThrowIfNull(constants);
        if (bindings is null)
        {
            if (constants.Count != 0) throw new InvalidDataException("An absent source pixel stage cannot receive pixel constants.");
            return [];
        }
        var declarations = bindings.Bindings.Where(b => b.DescriptorKind == "UniformBuffer").ToArray();
        if (constants.Count != declarations.Length) throw new InvalidDataException("Original model stage requires every complete original CB.");
        var result = new Dictionary<uint, byte[]>();
        foreach (var declaration in declarations)
        {
            if (!constants.TryGetValue(declaration.Source.BindPoint, out var bytes) || bytes.Length != declaration.ConstantBuffer!.ByteSize)
                throw new InvalidDataException("Original model CB is absent or differs from its RDEF length.");
            result.Add(declaration.Source.BindPoint, bytes.ToArray());
        }
        return result;
    }

    private static ModelDeviceBinding[] SnapshotResources(TranslatedShaderBindings? bindings,
        IReadOnlyList<ModelDeviceBinding> resources, ModelDrawTarget target)
    {
        ArgumentNullException.ThrowIfNull(resources);
        if (bindings is null)
        {
            if (resources.Count != 0) throw new InvalidDataException("An absent source pixel stage cannot receive pixel resources.");
            return [];
        }
        var expected = bindings.Bindings.Where(b => b.DescriptorKind != "UniformBuffer").ToArray();
        var result = resources.ToArray();
        if (result.Any(r => r is null) || result.Length != expected.Length ||
            result.Select(r => (r.DescriptorKind, r.SourceBindPoint)).Distinct().Count() != result.Length)
            throw new InvalidDataException("Original model resources must exactly match all source declarations.");
        bool Target(Rid rid) => target.ColorTextures.Contains(rid) || target.DepthTexture == rid || target.OwnerTextures.Contains(rid);
        foreach (var declaration in expected)
        {
            var input = result.SingleOrDefault(r => r.DescriptorKind == declaration.DescriptorKind && r.SourceBindPoint == declaration.Source.BindPoint);
            if (input is null || !input.Resource.IsValid || string.IsNullOrWhiteSpace(input.SourceResourceUri) ||
                (input.DescriptorKind == "Texture") != input.OwnerTexture.HasValue || Target(input.Resource) ||
                input.OwnerTexture is Rid parent && (!parent.IsValid || Target(parent)))
                throw new InvalidDataException("Original model source resource is absent, invalid or aliases the output target.");
        }
        return result;
    }

    private static void ValidateDeviceTextures(RenderingDevice rd, TranslatedShaderBindings? bindings,
        ModelDeviceBinding[] resources)
    {
        if (bindings is null) return;
        foreach (var declaration in bindings.Bindings.Where(b => b.DescriptorKind == "Texture"))
        {
            var resource = resources.Single(r => r.DescriptorKind == "Texture" && r.SourceBindPoint == declaration.Source.BindPoint);
            if (!rd.TextureIsValid(resource.Resource) || !rd.TextureIsValid(resource.OwnerTexture!.Value))
                throw new InvalidDataException("Original model source image is no longer live.");
            using var format = rd.TextureGetFormat(resource.Resource);
            var type = declaration.Source.Dimension switch
            {
                4 => RenderingDevice.TextureType.Type2D, 5 => RenderingDevice.TextureType.Type2DArray,
                8 => RenderingDevice.TextureType.Type3D, 9 => RenderingDevice.TextureType.Cube,
                10 => RenderingDevice.TextureType.CubeArray,
                _ => throw new InvalidDataException("Unsupported original model source image dimension.")
            };
            bool uintFormat = format.Format.ToString().Contains("Uint", StringComparison.Ordinal);
            bool sintFormat = format.Format.ToString().Contains("Sint", StringComparison.Ordinal);
            if (format.TextureType != type || !format.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit) ||
                sintFormat || uintFormat != (declaration.Source.ReturnType == 4))
                throw new InvalidDataException("Original model image differs from its RDEF dimension/return type.");
        }
    }

    private static void ValidateVertexInputs(ModelShaderProgram program, ModelDeviceLayout layout)
    {
        var inputs = program.VertexBindings.Interface.StageVariables.Where(v => v.StorageClass == 1 && v.Location is not null).ToArray();
        if (inputs.Length != layout.Attributes.Count)
            throw new InvalidDataException("Original model layout must bind every translated VS input location exactly once.");
        foreach (var input in inputs)
        {
            var attribute = layout.Attributes.SingleOrDefault(a => a.Location == input.Location);
            var scalar = input.Type.Kind == "vector" ? input.Type.ComponentType! : input.Type;
            bool compatible = attribute?.RequiredShaderComponentType switch
            {
                3 => scalar.Kind == "float", 1 => scalar is { Kind: "int", Signed: false },
                2 => scalar is { Kind: "int", Signed: true }, _ => false
            };
            if (!compatible || scalar.Bits != 32)
                throw new InvalidDataException("Original model source storage differs from the VS scalar input type.");
        }
    }

    /// <summary>Full stage CB replacement supports source-written dynamic
    /// transforms/camera without rebuilding geometry, textures or pipelines.
    /// The caller uses each selected original program's actual field layout.</summary>
    public Task UpdateConstantsAsync(IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> vertex,
        IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> pixel)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null, this);
        var vs = SnapshotConstants(Program.VertexBindings, vertex);
        var ps = SnapshotConstants(Program.PixelStage?.Bindings, pixel);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            UpdateOnRenderThread(rd, vs, ps); return true;
        });
    }

    internal (Dictionary<uint, byte[]> Vertex, Dictionary<uint, byte[]> Pixel) SnapshotUpdate(
        IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> vertex, IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> pixel)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null, this);
        return (SnapshotConstants(Program.VertexBindings, vertex), SnapshotConstants(Program.PixelStage?.Bindings, pixel));
    }

    /// <summary>Only called inside the owning layer's accepted render-thread
    /// batch. Storage is the last successful owned GPU write, never caller data.
    /// Each CB gets one aligned dirty interval; unchanged CBs issue no update.</summary>
    internal ModelConstantUpdateStatistics UpdateOnRenderThread(RenderingDevice rd,
        Dictionary<uint, byte[]> vertex, Dictionary<uint, byte[]> pixel)
    {
        RequireDrawLive();
        ModelConstantUpdateStatistics result = default;
        void Update(Dictionary<uint, Rid> buffers, Dictionary<uint, byte[]> storage, Dictionary<uint, byte[]> next)
        {
            foreach (var cb in next)
            {
                byte[] prior = storage[cb.Key], desired = cb.Value;
                if (desired.Length != prior.Length) throw new InvalidDataException("Original model CB update extent changed.");
                int first = 0; while (first < desired.Length && desired[first] == prior[first]) first++;
                if (first == desired.Length) continue;
                int last = desired.Length; while (last > first && desired[last - 1] == prior[last - 1]) last--;
                int offset = first & ~3, end = Math.Min(desired.Length, (last + 3) & ~3);
                byte[] write = desired.AsSpan(offset, end - offset).ToArray();
                if (rd.BufferUpdate(buffers[cb.Key], (uint)offset, (uint)write.Length, write) != Error.Ok)
                    throw new InvalidDataException("Device rejected an original model dirty CB interval.");
                // Commit only this successful buffer write. If another CB fails,
                // retry compares against the bytes actually written to this one.
                write.CopyTo(prior.AsSpan(offset, write.Length));
                result += new ModelConstantUpdateStatistics(1, write.Length);
            }
        }
        Update(_vertexBuffers, _vertexConstants, vertex); Update(_pixelBuffers, _pixelConstants, pixel);
        return result;
    }

    internal void RequireDrawLive()
    { ObjectDisposedException.ThrowIf(_disposal is not null, this); Program.RequireLive(); Geometry.RequireLive(); }
    internal void DrawInList(RenderingDevice rd, long list)
    {
        RequireDrawLive();
        rd.DrawListBindRenderPipeline(list, _pipeline);
        if (_pixelSet is Rid ps) rd.DrawListBindUniformSet(list, ps, 0);
        if (_vertexSet is Rid vs) rd.DrawListBindUniformSet(list, vs, 1);
        rd.DrawListBindVertexArray(list, _arrays.Vertices); rd.DrawListBindIndexArray(list, _arrays.Indices);
        rd.DrawListDraw(list, true, 1);
    }

    public Task DrawAsync(RenderingDevice.DrawFlags explicitActions, Color[] explicitClearColors,
        float explicitClearDepth, uint explicitClearStencil)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null, this);
        ArgumentNullException.ThrowIfNull(explicitClearColors);
        Color[] colors = explicitClearColors.ToArray();
        bool clearsColor=(explicitActions & RenderingDevice.DrawFlags.ClearColorAll)!=0;
        if ((clearsColor && colors.Length != Target.ColorTextures.Count || !clearsColor &&
                colors.Length != 0 && colors.Length != Target.ColorTextures.Count) || !float.IsFinite(explicitClearDepth))
            throw new InvalidDataException("Original model draw clear inputs differ from its target contract.");
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            ObjectDisposedException.ThrowIf(_disposal is not null, this);
            Program.RequireLive(); Geometry.RequireLive();
            long list = rd.DrawListBegin(Target.Framebuffer, explicitActions, colors, explicitClearDepth, explicitClearStencil);
            try
            {
                DrawInList(rd, list);
            }
            finally { rd.DrawListEnd(); }
            return true;
        });
    }

    public ValueTask DisposeAsync() => new(_disposal ??= TerrainDeviceTexture.OnRenderThreadAsync(rd =>
    { for (int i = _owned.Count - 1; i >= 0; i--) rd.FreeRid(_owned[i]); return true; }));
}
