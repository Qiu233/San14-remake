using Godot;
using San14.Data;

/// <summary>One actual field factory's device storage. Allocation never claims
/// that capture, integration, or filtering has written its contents. Retire
/// ReflectionUpstreamOwner and other borrowers before disposing this owner.</summary>
public sealed class ReflectionCaptureFieldResources : IAsyncDisposable
{
    public ReflectionCaptureFieldDefinition Source { get; }
    public ReflectionCaptureFieldResourcePlan Plan { get; }
    public Rid Radiance { get; }
    public Rid CaptureAtlas { get; }
    public Rid SphericalHarmonics { get; }
    public Rid SkyVisibility { get; }
    public Rid ProbeShadowMap { get; }
    public ReflectionCaptureFieldInstance Instance { get; }
    private readonly IReadOnlyList<Rid> owned;
    private Task? disposal;

    private ReflectionCaptureFieldResources(ReflectionCaptureFieldDefinition source,
        ReflectionCaptureFieldResourcePlan plan, Rid radiance, Rid capture,
        Rid coefficients, Rid sky, Rid shadow, IReadOnlyList<Rid> allocated)
    {
        (Source, Plan, Radiance, CaptureAtlas, SphericalHarmonics, SkyVisibility, ProbeShadowMap, owned) =
            (source, plan, radiance, capture, coefficients, sky, shadow, allocated);
        Instance = new(source);
    }

    /// <summary>Called by the owner of a genuinely constructed field instance,
    /// after normal typed resource lookup. It does not enumerate or activate
    /// library definitions, build a Scene registry, or publish a ready probe.</summary>
    public static Task<ReflectionCaptureFieldResources> AllocateAsync(RenderResourceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var source = descriptor.ReflectionCaptureField ??
            throw new InvalidDataException("reflection.capture.field.typed_resource");
        var plan = ReflectionCaptureFieldResourcePlan.Prepare(source);
        if (source.ProbeFieldShadowMapSize == 0)
            throw new InvalidDataException("reflection.capture.field.shadow_extent");
        if (plan.SphericalHarmonicsBytes > uint.MaxValue || plan.SkyVisibilityBytes > uint.MaxValue)
            throw new InvalidDataException("reflection.capture.field.buffer_extent");
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var owned = new List<Rid>();
            Rid Own(Rid value)
            {
                if (!value.IsValid) throw new InvalidDataException("reflection.capture.field.device_storage");
                owned.Add(value); return value;
            }
            Rid Atlas(uint layers, uint levels)
            {
                using var format = new RDTextureFormat
                {
                    Format = RenderingDevice.DataFormat.B10G11R11UfloatPack32,
                    Width = plan.AtlasWidth, Height = plan.AtlasHeight,
                    Depth = 1, ArrayLayers = layers, Mipmaps = levels,
                    TextureType = RenderingDevice.TextureType.Type2DArray,
                    Samples = RenderingDevice.TextureSamples.Samples1,
                    UsageBits = RenderingDevice.TextureUsageBits.SamplingBit |
                        RenderingDevice.TextureUsageBits.ColorAttachmentBit | RenderingDevice.TextureUsageBits.StorageBit |
                        (levels > 1 ? RenderingDevice.TextureUsageBits.CanCopyFromBit : 0)
                };
                using var view = new RDTextureView();
                return Own(rd.TextureCreate(format, view));
            }
            try
            {
                var radiance = Atlas(plan.AtlasLayers, plan.RadianceMipLevels);
                var coefficients = Own(rd.StorageBufferCreate((uint)plan.SphericalHarmonicsBytes));
                // RDEF declares u2/u4 as UAV buffers, but the original screen
                // shader only reads their source DWORDs. The existing adapter
                // preserves those reads with Godot storage-buffer descriptors.
                var sky = Own(rd.StorageBufferCreate((uint)plan.SkyVisibilityBytes));
                var capture = Atlas(plan.CaptureArrayLayers, 1);
                // F377D0 -> F37980 also owns the field88 single-sample
                // native4E shadow target. Native format4E maps to D32_FLOAT;
                // allocation is separate from a submitted capture or clear.
                using var shadowFormat = new RDTextureFormat
                {
                    Format = RenderingDevice.DataFormat.D32Sfloat,
                    Width = source.ProbeFieldShadowMapSize, Height = source.ProbeFieldShadowMapSize,
                    Depth = 1, ArrayLayers = 1, Mipmaps = 1,
                    TextureType = RenderingDevice.TextureType.Type2D,
                    Samples = RenderingDevice.TextureSamples.Samples1,
                    UsageBits = RenderingDevice.TextureUsageBits.SamplingBit |
                        RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit
                };
                using var shadowView = new RDTextureView();
                var shadow = Own(rd.TextureCreate(shadowFormat, shadowView));
                return new ReflectionCaptureFieldResources(source, plan, radiance,
                    capture, coefficients, sky, shadow, owned.AsReadOnly());
            }
            catch { for (int i = owned.Count - 1; i >= 0; --i) rd.FreeRid(owned[i]); throw; }
        });
    }

    /// <summary>Geometry for this field's freshly loaded source values. A
    /// caller with subsequent field writes must supply its actual current
    /// geometry instead; no frame is recovered from a Main camera or Scene.</summary>
    public ReflectionComputeProbeGeometry FreshGeometry() => new(Source.FieldDimension.X,
        Source.FieldDimension.Y, Source.FieldDimension.Z, Source.CellSize,
        Source.TileSize, Source.ViewRange, Source.WorldPosition);

    public ReflectionComputeProbeGeometry CurrentGeometry()
    { RequireCurrent(); return Instance.CaptureCurrentGeometry(); }

    public ReflectionComputeAtlasLayout AtlasLayout => new(Plan.AtlasWidth,
        Plan.AtlasHeight, Plan.CaptureArrayLayers, Plan.RadianceMipLevels);

    /// <summary>The selected original screen programs consume this field's
    /// actual storage: CS10 t9/u2/u4; CS11 t9/t10. Current depth/GBufs and the
    /// actual sampler owners remain borrowed from the prepared current View.</summary>
    internal IReadOnlyList<DeferredDeviceBinding> BindScreenInputs(uint pass,
        IReadOnlyList<DeferredDeviceBinding> borrowed)
    {
        RequireCurrent();
        ArgumentNullException.ThrowIfNull(borrowed);
        string identity = Source.SourceProvenance;
        DeferredDeviceBinding[] fields = pass switch
        {
            4 => [new("Texture", 9, CaptureAtlas, CaptureAtlas, identity + "/capture-octmap"),
                new("ImageBuffer", 2, SphericalHarmonics, null, identity + "/spherical-harmonics"),
                new("ImageBuffer", 4, SkyVisibility, null, identity + "/sky-visibility")],
            5 => [new("Texture", 9, CaptureAtlas, CaptureAtlas, identity + "/capture-octmap"),
                new("Texture", 10, Radiance, Radiance, identity + "/radiance")],
            _ => throw new InvalidDataException("reflection.capture.field.screen_pass")
        };
        var result = new List<DeferredDeviceBinding>(fields.Length + borrowed.Count);
        result.AddRange(fields);
        foreach (var input in borrowed)
        {
            if (input is null || fields.Any(field => field.DescriptorKind == input.DescriptorKind &&
                field.SourceBindPoint == input.SourceBindPoint))
                throw new InvalidDataException("reflection.capture.field.borrowed_binding");
            result.Add(input);
        }
        return result.AsReadOnly();
    }

    internal void RequireCurrent()
    { ObjectDisposedException.ThrowIf(disposal is not null, this); Instance.RequireCurrent(); }

    public ValueTask DisposeAsync()
    {
        Instance.Dispose();
        return new(disposal ??= RetireAsync());
    }
    private Task RetireAsync() => TerrainDeviceTexture.OnRenderThreadAsync(rd =>
    {
        for (int i = owned.Count - 1; i >= 0; --i) rd.FreeRid(owned[i]);
        return true;
    });
}
