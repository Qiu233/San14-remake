using Godot;
using San14.Data;

/// <summary>Caller-owned resource, identified by its original descriptor kind
/// and register. Texture parents make target alias checks explicit. Samplers
/// and typed buffers have no texture parent. No null input is synthesized.</summary>
public sealed record OriginalDeferredDeviceBinding(string DescriptorKind, uint SourceBindPoint,
    Rid Resource, Rid? OwnerTexture, string SourceResourceUri);

public sealed record OriginalDeferredDrawTarget(Rid Framebuffer, IReadOnlyList<Rid> OwnerTextures,
    string SourceResourceUri)
{
    /// <summary>Explicit borrowed D32/S8 parent used by an early read-only
    /// stencil pass. It must also occur in OwnerTextures.</summary>
    public Rid? DepthStencilTexture { get; init; }
}

/// <summary>The source consumer supplies the eight-bit reference and read
/// mask. Equal or Always comparison keeps both faces on every outcome;
/// this reads the existing stencil and never produces it.</summary>
public sealed record OriginalDeferredStencilState(uint Reference, uint CompareMask = 255)
{
    public RenderingDevice.CompareOperator Compare { get; init; } = RenderingDevice.CompareOperator.Equal;
}

public sealed record OriginalDeferredRasterState(RenderingDevice.PolygonCullMode Cull,
    bool EnableBlend);

/// <summary>Explicit current frontend color writes. An alpha producer can
/// preserve the same target's RGB without modifying its original shader.</summary>
public sealed record OriginalDeferredColorWriteMask(bool Red, bool Green, bool Blue, bool Alpha)
{
    public static OriginalDeferredColorWriteMask All { get; } = new(true, true, true, true);
    public static OriginalDeferredColorWriteMask AlphaOnly { get; } = new(false, false, false, true);
}

/// <summary>Full original pixel-program submission with every source CB,
/// sampler, image and typed-buffer explicitly supplied. Owns only the CBs,
/// uniform set and pipeline. Does not infer active native inputs or state.</summary>
public sealed class OriginalDeferredDraw : IAsyncDisposable
{
    public OriginalDeferredPixelProgram Program { get; }
    public OriginalDeferredDrawTarget Target { get; }
    public OriginalDeferredColorWriteMask ColorWrites { get; }
    public IReadOnlyList<OriginalDeferredDeviceBinding> Inputs { get; }
    public IReadOnlyDictionary<uint, byte[]> ConstantBytes =>
        _constants.ToDictionary(p => p.Key, p => p.Value.ToArray());
    private readonly Dictionary<uint, byte[]> _constants;
    private readonly Dictionary<uint, Rid> _buffers;
    private readonly IReadOnlyList<Rid> _owned;
    private readonly Rid _pipeline, _uniformSet;
    private Task? _disposal;

    private OriginalDeferredDraw(OriginalDeferredPixelProgram program, OriginalDeferredDrawTarget target,
        IReadOnlyList<OriginalDeferredDeviceBinding> inputs, Dictionary<uint, byte[]> constants,
        Dictionary<uint, Rid> buffers, Rid pipeline, Rid set, List<Rid> owned,
        OriginalDeferredColorWriteMask colorWrites)
        => (Program, Target, Inputs, _constants, _buffers, _pipeline, _uniformSet, _owned, ColorWrites) =
            (program, target, inputs, constants, buffers, pipeline, set, owned.AsReadOnly(), colorWrites);

    public static Task<OriginalDeferredDraw> BuildAsync(OriginalDeferredPixelProgram program,
        OriginalDeferredDrawTarget explicitTarget, IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> explicitConstants,
        IReadOnlyList<OriginalDeferredDeviceBinding> explicitInputs, OriginalDeferredRasterState explicitRaster,
        OriginalDeferredStencilState? explicitStencil = null,
        OriginalDeferredColorWriteMask? explicitColorWrites = null)
    {
        ArgumentNullException.ThrowIfNull(program); ArgumentNullException.ThrowIfNull(explicitTarget);
        ArgumentNullException.ThrowIfNull(explicitConstants); ArgumentNullException.ThrowIfNull(explicitInputs);
        ArgumentNullException.ThrowIfNull(explicitRaster);
        var colorWrites = explicitColorWrites ?? OriginalDeferredColorWriteMask.All;
        if(explicitRaster.EnableBlend)
            throw new InvalidDataException("Deferred draw currently requires explicit blend disable; a complete source blend recipe is not yet supported.");
        if (!explicitTarget.Framebuffer.IsValid || explicitTarget.OwnerTextures is null ||
            explicitTarget.OwnerTextures.Count == 0 || explicitTarget.OwnerTextures.Any(r => !r.IsValid) ||
            string.IsNullOrWhiteSpace(explicitTarget.SourceResourceUri))
            throw new InvalidDataException("Deferred draw requires an explicit live target and its texture parents.");
        var target = explicitTarget with { OwnerTextures = Array.AsReadOnly(explicitTarget.OwnerTextures.ToArray()) };
        if (explicitStencil is not null &&
            (explicitStencil.Reference > byte.MaxValue || explicitStencil.CompareMask > byte.MaxValue ||
             explicitStencil.Compare is not (RenderingDevice.CompareOperator.Equal or RenderingDevice.CompareOperator.NotEqual or RenderingDevice.CompareOperator.Always) ||
             target.DepthStencilTexture is not Rid stencilParent || !stencilParent.IsValid ||
             !target.OwnerTextures.Contains(stencilParent)))
            throw new InvalidDataException("Early deferred stencil requires its complete eight-bit comparison and explicit borrowed depth/stencil parent.");
        var declarations = program.DescriptorBindings.Bindings;
        var outputs=program.DescriptorBindings.Interface.StageVariables.Where(v=>v.StorageClass==3 && v.Location is not null).ToArray();
        if(outputs.Length!=1 || outputs[0].Location!=0 ||
            outputs[0].Type is not {Kind:"vector",ComponentCount:4,ComponentType:{Kind:"float",Bits:32}})
            throw new InvalidDataException("Deferred draw requires the verified single float4 color output.");
        if (declarations.Any(d => d.Device.DescriptorSet != 0))
            throw new InvalidDataException("Deferred draw requires the verified fragment set0 interface.");
        var cbs = declarations.Where(d => d.DescriptorKind == "UniformBuffer").ToArray();
        var constants = SnapshotConstants(cbs, explicitConstants);
        var inputs = explicitInputs.Select(i => i ?? throw new InvalidDataException("Null deferred resource input.")).ToArray();
        var expected = declarations.Where(d => d.DescriptorKind != "UniformBuffer").ToArray();
        if (inputs.Length != expected.Length || inputs.Select(i => (i.DescriptorKind, i.SourceBindPoint)).Distinct().Count() != inputs.Length)
            throw new InvalidDataException("Deferred resources must exactly match every original non-CB declaration.");
        foreach (var declaration in expected)
        {
            var input = inputs.SingleOrDefault(i => i.DescriptorKind == declaration.DescriptorKind && i.SourceBindPoint == declaration.Source.BindPoint);
            if (input is null || !input.Resource.IsValid || string.IsNullOrWhiteSpace(input.SourceResourceUri) ||
                (input.DescriptorKind == "Texture") != input.OwnerTexture.HasValue ||
                input.OwnerTexture is Rid parent && (!parent.IsValid || target.OwnerTextures.Contains(parent)) ||
                target.OwnerTextures.Contains(input.Resource))
                throw new InvalidDataException("Missing, invalid or target-aliasing deferred source resource.");
        }
        // Godot rejects a sampled texture that is also attached, even for
        // an Equal/Keep depth-stencil pass. A separately proved exact backend
        // depth transport must supply a disjoint sampled parent. Do not let
        // driver ERROR output occur after a superficially successful draw.
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var owned = new List<Rid>(); var buffers = new Dictionary<uint, Rid>();
            Rid Own(Rid rid) { if (!rid.IsValid) throw new InvalidDataException("Deferred device allocation failed."); owned.Add(rid); return rid; }
            try
            {
                if (!rd.FramebufferIsValid(target.Framebuffer) || target.OwnerTextures.Any(t => !rd.TextureIsValid(t)))
                    throw new InvalidDataException("Deferred output target is no longer live.");
                if (explicitStencil is not null)
                {
                    using var stencilFormat = rd.TextureGetFormat(target.DepthStencilTexture!.Value);
                    if (stencilFormat.Format != RenderingDevice.DataFormat.D32SfloatS8Uint)
                        throw new InvalidDataException("Early deferred stencil requires the source D32/S8 attachment, not a sampled R32 color alias.");
                }
                foreach (var declaration in expected.Where(d => d.DescriptorKind == "Texture"))
                {
                    var input = inputs.Single(i => i.DescriptorKind == "Texture" && i.SourceBindPoint == declaration.Source.BindPoint);
                    if (!rd.TextureIsValid(input.Resource) || !rd.TextureIsValid(input.OwnerTexture!.Value))
                        throw new InvalidDataException("Deferred source image or parent is no longer live.");
                    using var format = rd.TextureGetFormat(input.Resource);
                    var type = declaration.Source.Dimension switch {
                        4 => RenderingDevice.TextureType.Type2D, 5 => RenderingDevice.TextureType.Type2DArray,
                        8 => RenderingDevice.TextureType.Type3D, 9 => RenderingDevice.TextureType.Cube,
                        10 => RenderingDevice.TextureType.CubeArray,
                        _ => throw new InvalidDataException("Unsupported original deferred image dimension.") };
                    string nativeFormat=format.Format.ToString();
                    // Combined depth/stencil storage contains "Uint" for
                    // stencil, but its sampled depth view returns float.
                    // Classify the selected view, not that name substring.
                    bool depthView=nativeFormat is "D16Unorm" or "X8D24UnormPack32" or "D32Sfloat" or
                        "D16UnormS8Uint" or "D24UnormS8Uint" or "D32SfloatS8Uint";
                    bool isUint = !depthView && nativeFormat.Contains("Uint", StringComparison.Ordinal);
                    bool isSint = !depthView && nativeFormat.Contains("Sint", StringComparison.Ordinal);
                    if (format.TextureType != type || !format.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit) ||
                        isSint || isUint != (declaration.Source.ReturnType == 4))
                        throw new InvalidDataException("Deferred device image differs from the original dimension/return type.");
                }
                foreach (var cb in constants) buffers.Add(cb.Key, Own(rd.UniformBufferCreate((uint)cb.Value.Length, cb.Value)));
                var uniforms = new Godot.Collections.Array<RDUniform>();
                Rid set;
                try
                {
                    foreach (var declaration in declarations)
                    {
                        var type = declaration.DescriptorKind switch {
                            "UniformBuffer" => RenderingDevice.UniformType.UniformBuffer,
                            "Texture" => RenderingDevice.UniformType.Texture,
                            "TextureBuffer" => RenderingDevice.UniformType.TextureBuffer,
                            "Sampler" => RenderingDevice.UniformType.Sampler,
                            _ => throw new InvalidDataException("Unsupported original deferred descriptor kind.") };
                        Rid resource = declaration.DescriptorKind == "UniformBuffer" ? buffers[declaration.Source.BindPoint] :
                            inputs.Single(i => i.DescriptorKind == declaration.DescriptorKind && i.SourceBindPoint == declaration.Source.BindPoint).Resource;
                        var uniform = new RDUniform { Binding = checked((int)declaration.Device.Binding!.Value), UniformType = type };
                        uniform.AddId(resource); uniforms.Add(uniform);
                    }
                    set = Own(rd.UniformSetCreate(uniforms, program.ShaderRid, 0));
                    if (!rd.UniformSetIsValid(set)) throw new InvalidDataException("Deferred uniform set was rejected.");
                }
                finally { foreach (var uniform in uniforms) uniform.Dispose(); }
                using var raster = new RDPipelineRasterizationState { CullMode = explicitRaster.Cull };
                using var multi = new RDPipelineMultisampleState();
                using var depth = new RDPipelineDepthStencilState { EnableDepthTest = false, EnableDepthWrite = false };
                if (explicitStencil is not null)
                {
                    depth.EnableStencil = true;
                    depth.FrontOpCompare = depth.BackOpCompare = explicitStencil.Compare;
                    depth.FrontOpFail = depth.FrontOpPass = depth.FrontOpDepthFail =
                        depth.BackOpFail = depth.BackOpPass = depth.BackOpDepthFail = RenderingDevice.StencilOperation.Keep;
                    depth.FrontOpCompareMask = depth.BackOpCompareMask = explicitStencil.CompareMask;
                    // Native -1 retains its preceding write mask. All three
                    // operations keep the value, so this backend mask is an
                    // unused normalization, not a recovered native store.
                    depth.FrontOpWriteMask = depth.BackOpWriteMask = byte.MaxValue;
                    depth.FrontOpReference = depth.BackOpReference = explicitStencil.Reference;
                }
                using var color = new RDPipelineColorBlendStateAttachment {
                    EnableBlend = explicitRaster.EnableBlend,
                    WriteR = colorWrites.Red, WriteG = colorWrites.Green,
                    WriteB = colorWrites.Blue, WriteA = colorWrites.Alpha };
                using var blend = new RDPipelineColorBlendState { Attachments = [color] };
                Rid pipeline = Own(rd.RenderPipelineCreate(program.ShaderRid, rd.FramebufferGetFormat(target.Framebuffer), -1,
                    RenderingDevice.RenderPrimitive.Triangles, raster, multi, depth, blend));
                if (!rd.RenderPipelineIsValid(pipeline)) throw new InvalidDataException("Deferred draw pipeline was rejected.");
                return new OriginalDeferredDraw(program, target, Array.AsReadOnly(inputs), constants, buffers, pipeline, set, owned, colorWrites);
            }
            catch { for (int i = owned.Count - 1; i >= 0; i--) rd.FreeRid(owned[i]); throw; }
        });
    }

    private static Dictionary<uint, byte[]> SnapshotConstants(IReadOnlyList<OriginalTranslatedShaderBinding> cbs,
        IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> explicitConstants)
    {
        if (explicitConstants.Count != cbs.Count) throw new InvalidDataException("Deferred draw requires every complete original CB.");
        var result = new Dictionary<uint, byte[]>();
        foreach (var cb in cbs)
        {
            if (!explicitConstants.TryGetValue(cb.Source.BindPoint, out var bytes) || bytes.Length != cb.ConstantBuffer!.ByteSize)
                throw new InvalidDataException("Deferred CB is absent or differs from its original RDEF byte length.");
            result.Add(cb.Source.BindPoint, bytes.ToArray());
        }
        return result;
    }

    public Task UpdateConstantsAsync(IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> explicitConstants)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null, this); ArgumentNullException.ThrowIfNull(explicitConstants);
        var snapshot = SnapshotConstants(Program.DescriptorBindings.Bindings.Where(d => d.DescriptorKind == "UniformBuffer").ToArray(), explicitConstants);
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd => {
            ObjectDisposedException.ThrowIf(_disposal is not null, this);
            foreach (var cb in snapshot)
            {
                if (rd.BufferUpdate(_buffers[cb.Key], 0, (uint)cb.Value.Length, cb.Value) != Error.Ok)
                    throw new InvalidDataException("Device rejected a complete original deferred CB update.");
                _constants[cb.Key] = cb.Value;
            }
            return true;
        });
    }

    /// <summary>Temporary source viewport overrides are installed for this
    /// draw only. The original postdraw literal is restored even if submission
    /// throws; absent overrides retain the caller's prior bytes unchanged.</summary>
    public Task DrawAsync(OriginalDeferredTranslucencyViewportInputs? temporaryViewport=null,
        Action? requireCurrentSource=null)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null, this);
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd => {
            ObjectDisposedException.ThrowIf(_disposal is not null, this);
            requireCurrentSource?.Invoke();
            OriginalDeferredSceneConstantPacket? restore=null;
            if(temporaryViewport is not null)
            {
                var installed=ApplyCurrentGlobals(_constants[0],new(TranslucencyViewport:temporaryViewport));
                restore=ApplyCurrentGlobals(installed.Bytes,
                    new(TranslucencyViewport:OriginalDeferredRefractionMath.AfterInstalledViewportDraw()));
                UpdateOneConstant(rd,0,installed.Bytes);
            }
            try
            {
                long list = rd.DrawListBegin(Target.Framebuffer);
                try { rd.DrawListBindRenderPipeline(list, _pipeline); rd.DrawListBindUniformSet(list, _uniformSet, 0); rd.DrawListDraw(list, false, 1, 3); }
                finally { rd.DrawListEnd(); }
            }
            finally { if(restore is not null)UpdateOneConstant(rd,0,restore.Bytes); }
            return true;
        });
    }

    private OriginalDeferredSceneConstantPacket ApplyCurrentGlobals(ReadOnlySpan<byte> complete,
        OriginalDeferredSceneInputs inputs) => Program.SourceSelection is { } selected
            ? OriginalDeferredSelectedSceneConstants.Apply(selected,complete,inputs)
            : OriginalDeferredSceneConstants.Apply(Program.DescriptorBindings.OriginalDefinitions,complete,inputs);

    private void UpdateOneConstant(RenderingDevice rd,uint bindPoint,byte[] bytes)
    {
        if(rd.BufferUpdate(_buffers[bindPoint],0,(uint)bytes.Length,bytes)!=Error.Ok)
            throw new InvalidDataException("Device rejected original deferred named-constant update.");
        _constants[bindPoint]=bytes;
    }

    /// <summary>Explicit diagnostic readback of this draw's own CB. Ordinary
    /// frame submission never reads back GPU constants.</summary>
    public Task<byte[]> ReadConstantBufferAsync(uint sourceBindPoint)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        if(!_buffers.TryGetValue(sourceBindPoint,out var buffer))throw new InvalidDataException("Deferred source CB register is absent.");
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {ObjectDisposedException.ThrowIf(_disposal is not null,this);return rd.BufferGetData(buffer);});
    }

    public ValueTask DisposeAsync() => new(_disposal ??= OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd => {
        for (int i = _owned.Count - 1; i >= 0; i--) rd.FreeRid(_owned[i]); return true;
    }));
}
