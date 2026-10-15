using Godot;
using San14.Data;

public sealed record RenderTargetDeviceTexture(RenderTextureDefinition Source,
    Vector2I ExplicitExtent, Rid StorageRid, Rid ShaderViewRid,
    RenderingDevice.DataFormat StorageFormat, RenderingDevice.DataFormat ShaderFormat,
    RenderingDevice.DataFormat TargetFormat)
{
    /// <summary>The terminal owned RT definition. Source retains the logical
    /// graph name even when the original CPU getter resolves a proxy.</summary>
    public RenderTextureRuntimeDescriptor EffectiveSource { get; init; } =
        RenderTextureRuntimeDescriptor.ConstructDeclared(Source,
            "device-declared/" + Source.Source.Source.ResourceUri + "/" + Source.Object.ObjectId.ToString("X8"));
}

/// <summary>Allocates the explicitly resolved original attachments on the own RenderingDevice.
/// Every extent is supplied explicitly; source zero dimensions are not interpreted.
/// Does not clear, draw, populate contents, or claim original active-frame identity.</summary>
public sealed class RenderTargetFrame : IAsyncDisposable
{
    public LoadedRenderGraphTargets Source { get; }
    public RenderTextureResolutionFrame? Resolutions { get; }
    public IReadOnlyDictionary<uint, RenderTargetDeviceTexture> Textures { get; }
    public IReadOnlyList<Rid> ColorViews { get; }
    public Rid DepthView { get; }
    public Rid Framebuffer { get; }
    private readonly RenderDeviceResources _resources;
    private readonly Dictionary<uint,(Rid Framebuffer,Army48DeviceFramebufferPass Pass,RenderDeviceResources Resources)> _armyDevicePasses=[];
    private readonly object _armyDevicePassGate=new();
    private NativeMapBorrowerRetirementPackage? _pendingArmyVariant;
    private volatile bool _retiring;
    private Task? _disposal;

    /// <summary>Build another source attachment set over the same current
    /// physical textures. Only its surface views and framebuffer are owned;
    /// retire this frame before its storage owner. This is used by the original
    /// node48 six-target and node53 five-target scopes.</summary>
    public static Task<RenderTargetFrame> BuildAttachmentFrameAsync(
        LoadedRenderGraphTargets source, RenderTargetFrame storageOwner)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(storageOwner);
        var definitions = source.ColorTargets.Select(t => t.Texture)
            .Concat(source.DepthTarget is null ? [] : new[] { source.DepthTarget })
            .DistinctBy(t => t.Object.ObjectId).ToArray();
        if (definitions.Length == 0 || source.Graph.Source.ResourceUri != storageOwner.Source.Graph.Source.ResourceUri ||
            source.Graph.Source.DecodedSha256 != storageOwner.Source.Graph.Source.DecodedSha256)
            throw new InvalidDataException("Shared attachment frames require the same loaded render graph and physical storage.");
        var textures = new Dictionary<uint, RenderTargetDeviceTexture>();
        foreach (var definition in definitions)
        {
            if (!storageOwner.Textures.TryGetValue(definition.Object.ObjectId, out var actual) ||
                definition.Source.Source.ResourceUri != actual.Source.Source.Source.ResourceUri ||
                definition.Source.Source.DecodedSha256 != actual.Source.Source.Source.DecodedSha256 ||
                definition.Kind != actual.Source.Kind || definition.PixelFormat != actual.Source.PixelFormat ||
                definition.ArrayDepth != actual.Source.ArrayDepth || definition.LevelNum != actual.Source.LevelNum ||
                definition.MinMipLevel != actual.Source.MinMipLevel || definition.MultiSample != actual.Source.MultiSample)
                throw new InvalidDataException("Shared attachment declaration differs from its loaded physical owner.");
            textures.Add(definition.Object.ObjectId, actual with { Source = definition });
        }
        if (textures.Values.Select(t => t.ExplicitExtent).Distinct().Count() != 1)
            throw new InvalidDataException("Shared attachment extents differ.");
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var resources = new RenderDeviceResources("Device rejected a shared attachment view or framebuffer.");
            Rid Own(Rid rid) => resources.Own(rid);
            try
            {
                var colors = new List<Rid>();
                Rid Surface(RenderTextureDefinition definition, uint surfaceId)
                {
                    var actual = textures[definition.Object.ObjectId];
                    if (!rd.TextureIsValid(actual.StorageRid))
                        throw new ObjectDisposedException(nameof(storageOwner));
                    var surface = RenderSurfaceMath.Resolve(actual.EffectiveSource.Declaration, surfaceId);
                    if (actual.EffectiveSource.Kind == RenderTextureKind.Texture2D &&
                        actual.TargetFormat == actual.StorageFormat) return actual.StorageRid;
                    using var view = new RDTextureView { FormatOverride = actual.TargetFormat };
                    return Own(actual.EffectiveSource.Kind == RenderTextureKind.Texture2D
                        ? rd.TextureCreateShared(view, actual.StorageRid)
                        : rd.TextureCreateSharedFromSlice(view, actual.StorageRid, surface.FirstArraySlice,
                            surface.MipLevel, 1, RenderingDevice.TextureSliceType.Slice2D));
                }
                foreach (var target in source.ColorTargets) colors.Add(Surface(target.Texture, target.SurfaceId));
                Rid depth = source.DepthTarget is null ? default : Surface(source.DepthTarget, source.DepthSurfaceId);
                var attachments = new Godot.Collections.Array<Rid>(colors);
                if (depth.IsValid) attachments.Add(depth);
                Rid framebuffer = Own(rd.FramebufferCreate(attachments));
                return new RenderTargetFrame(source, textures, colors, depth, framebuffer, resources, storageOwner.Resolutions);
            }
            catch (Exception failure)
            {
                resources.ReleaseAfterConstructionFailure(rd,failure,"Render-target-attachment-frame-partial");
                throw;
            }
        });
    }

    private RenderTargetFrame(LoadedRenderGraphTargets source,
        Dictionary<uint, RenderTargetDeviceTexture> textures, List<Rid> colors,
        Rid depth, Rid framebuffer, RenderDeviceResources resources, RenderTextureResolutionFrame? resolutions)
        => (Source, Textures, ColorViews, DepthView, Framebuffer, _resources, Resolutions) =
            (source, textures, colors.AsReadOnly(), depth, framebuffer, resources, resolutions);

    /// <summary>Retained device framebuffer over the same six actual color
    /// views and depth. Active output positions come from this actual program,
    /// and every unwritten color view is unreferenced. Variants are
    /// owned by this frame, so borrowing draws retire before their frame.</summary>
    internal async Task<ModelDrawTarget> PrepareArmyDeviceDrawTargetAsync(Army48TargetBinding actualArmy,
        IReadOnlyList<uint> actualShaderOutputLocations)
    {
        ObjectDisposedException.ThrowIf(_retiring,this);actualArmy.RequireCurrent();
        if(!ReferenceEquals(actualArmy.Frame,this))throw new InvalidDataException("Army device subpass belongs to another attachment frame.");
        var recipe=Army48DeviceFramebufferPass.Prepare(actualArmy.Source,actualShaderOutputLocations);
        recipe.RequireSource(Source);
        await DrainPendingArmyVariantAsync();
        ModelDrawTarget Target(Rid framebuffer)=>new(framebuffer,ColorViews,DepthView,
            Textures.Values.Select(t=>t.StorageRid).Distinct().ToArray(),Source.Graph.Source.ResourceUri){Army48DevicePass=recipe};
        lock(_armyDevicePassGate)
        {
            ObjectDisposedException.ThrowIf(_retiring,this);
            if(_pendingArmyVariant is not null)throw new InvalidOperationException("An unfinished Army framebuffer variant must retire before another request.");
            if(_armyDevicePasses.TryGetValue(recipe.ShaderOutputMask,out var prior))return Target(prior.Framebuffer);
        }
        return await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            ObjectDisposedException.ThrowIf(_retiring,this);actualArmy.RequireCurrent();
            lock(_armyDevicePassGate)
            {
                ObjectDisposedException.ThrowIf(_retiring,this);
                if(_pendingArmyVariant is not null)throw new InvalidOperationException("An unfinished Army framebuffer variant must retire before another request.");
                if(_armyDevicePasses.TryGetValue(recipe.ShaderOutputMask,out var prior))return Target(prior.Framebuffer);
                if(!rd.FramebufferIsValid(Framebuffer))throw new ObjectDisposedException(nameof(RenderTargetFrame));
                var resources=new RenderDeviceResources("Device rejected the Army actual-output subpass over six attachments.");
                try
                {
                    Rid selected;ModelDrawTarget result;
                    var attachments=new Godot.Collections.Array<Rid>(ColorViews){DepthView};
                    using(var pass=recipe.CreateDevicePass())
                    {
                        selected=resources.Own(rd.FramebufferCreateMultipass(attachments,new Godot.Collections.Array<RDFramebufferPass>{pass}));
                        if(!rd.FramebufferIsValid(selected))
                            throw new InvalidDataException("Device rejected the Army actual-output subpass over six attachments.");
                        result=Target(selected);
                    }
                    _armyDevicePasses.Add(recipe.ShaderOutputMask,(selected,recipe,resources));
                    return result;
                }
                catch(Exception failure)
                {
                    try { resources.ReleaseAfterConstructionFailure(rd,failure,"Render-target-Army-variant-partial"); }
                    catch(NativeMapBorrowerConstructionException partial)
                    {
                        _pendingArmyVariant=partial.RetainedPackage;
                        throw;
                    }
                    throw;
                }
            }
        });
    }

    internal void RequireArmyDeviceDrawTarget(ModelDrawTarget actualTarget,IReadOnlyList<uint> actualProgramOutputs)
    {
        ObjectDisposedException.ThrowIf(_retiring,this);
        var pass=actualTarget.Army48DevicePass??throw new InvalidDataException("Army draw has no actual shader-output device subpass.");
        pass.RequireSource(Source);
        uint mask=actualProgramOutputs.Aggregate(0u,(current,slot)=>current|(1u<<checked((int)slot)));
        lock(_armyDevicePassGate)
            if(mask!=pass.ShaderOutputMask||!_armyDevicePasses.TryGetValue(mask,out var retained)||
                retained.Framebuffer!=actualTarget.Framebuffer||!actualTarget.ColorTextures.SequenceEqual(ColorViews)||
                actualTarget.DepthTexture!=DepthView)
                throw new InvalidDataException("Army draw must use this actual frame's retained shader-output subpass and six views.");
    }

    public static Task<RenderTargetFrame> BuildAsync(LoadedRenderGraphTargets source,
        IReadOnlyDictionary<uint, Vector2I> explicitExtents, bool allowReadback = false, bool allowGpuCopy = false,
        RenderTextureResolutionFrame? resolutions = null)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(explicitExtents);
        RenderTextureDefinition[] definitions = source.ColorTargets.Select(t => t.Texture)
            .Concat(source.DepthTarget is null ? [] : new[] { source.DepthTarget })
            .DistinctBy(t => t.Object.ObjectId).ToArray();
        if (definitions.Length == 0 || definitions.Length != explicitExtents.Count)
            throw new InvalidDataException("Attachment allocation requires one explicit extent per declared texture.");
        resolutions?.ValidateAgainstLoadedTargets(definitions);
        var effective = definitions.ToDictionary(t => t.Object.ObjectId, t => resolutions?.Resolve(t).Effective ??
            RenderTextureRuntimeDescriptor.ConstructDeclared(t,
                "frame-declared/" + t.Source.Source.ResourceUri + "/" + t.Object.ObjectId.ToString("X8")));
        // Snapshot caller input before crossing the render-thread boundary.
        var sizes = new Dictionary<uint, Vector2I>();
        foreach (var texture in definitions)
        {
            var terminal = effective[texture.Object.ObjectId];
            if (!explicitExtents.TryGetValue(texture.Object.ObjectId, out var size) || size.X <= 0 || size.Y <= 0 ||
                terminal.Width < 0 || terminal.Height < 0 ||
                (terminal.Width > 0 && terminal.Width != size.X) || (terminal.Height > 0 && terminal.Height != size.Y))
                throw new InvalidDataException("Explicit attachment extent is absent, invalid or differs from a nonzero source extent.");
            if (terminal.LevelNum != 1 || terminal.MinMipLevel != 0 || terminal.MultiSample != 0)
                throw new InvalidDataException("Device attachment adapter requires the verified single mip/sample source contract.");
            var native = RenderTextureFormatMath.Resolve(terminal.PixelFormat,terminal.ColorSpace,terminal.OutputColorSpace);
            bool declaredDepth = source.DepthTarget?.Object.ObjectId == texture.Object.ObjectId;
            if(declaredDepth != (native.TargetViewFormat is 20 or 40))
                throw new InvalidDataException("Resolved terminal format cannot change the graph attachment's color/depth role.");
            sizes.Add(texture.Object.ObjectId, size);
        }
        if (sizes.Values.Distinct().Count() != 1)
            throw new InvalidDataException("Framebuffer attachment extents differ.");
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var resources = new RenderDeviceResources("RenderingDevice rejected an original attachment allocation/view.");
            Rid Own(Rid rid) => resources.Own(rid);
            try
            {
                var textures = new Dictionary<uint, RenderTargetDeviceTexture>();
                var colors = new List<Rid>(); Rid depth = default;
                var physical = new Dictionary<string, RenderTargetDeviceTexture>(StringComparer.Ordinal);
                foreach (var texture in definitions)
                {
                    var terminal = effective[texture.Object.ObjectId];
                    Vector2I size = sizes[texture.Object.ObjectId];
                    if (physical.TryGetValue(terminal.OwnerIdentity, out var alias))
                    {
                        if (!ReferenceEquals(alias.EffectiveSource, terminal) || alias.ExplicitExtent != size)
                            throw new InvalidDataException("A shared terminal RT owner requires one descriptor and one explicit extent.");
                        textures.Add(texture.Object.ObjectId, alias with { Source = texture });
                        continue;
                    }
                    RenderTextureFormats native = RenderTextureFormatMath.Resolve(
                        terminal.PixelFormat, terminal.ColorSpace, terminal.OutputColorSpace);
                    RenderingDevice.DataFormat target = ViewFormat(native.TargetViewFormat);
                    // Native R32 typeless depth storage has an R32_FLOAT SRV
                    // and D32_FLOAT DSV. Vulkan samples the same depth aspect.
                    RenderingDevice.DataFormat shader = native.TargetViewFormat==40&&native.ShaderViewFormat==41
                        ? target : ViewFormat(native.ShaderViewFormat);
                    // DXGI backing storage is typeless. Godot's framebuffer uses
                    // Texture::format, while a shared view override does not change
                    // that metadata. Keep the target format as the primary Vulkan
                    // image format so framebuffer and actual target view agree.
                    RenderingDevice.DataFormat storage = target;
                    bool isDepth = native.TargetViewFormat is 20 or 40;
                    var usage = RenderingDevice.TextureUsageBits.SamplingBit |
                        (isDepth ? RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit : RenderingDevice.TextureUsageBits.ColorAttachmentBit);
                    if ((allowReadback || allowGpuCopy) && !isDepth) usage |= RenderingDevice.TextureUsageBits.CanCopyFromBit;
                    if (allowGpuCopy) usage |= RenderingDevice.TextureUsageBits.CanCopyFromBit | RenderingDevice.TextureUsageBits.CanCopyToBit;
                    if (!rd.TextureIsFormatSupportedForUsage(storage, usage))
                        throw new NotSupportedException("Device cannot allocate this exact attachment format and usage.");
                    uint layers = terminal.Kind == RenderTextureKind.Texture2D ? 1u :
                        terminal.ArrayDepth is > 0 ? checked((uint)terminal.ArrayDepth.Value) :
                        throw new InvalidDataException("Original array attachment has no positive source depth.");
                    using var format = new RDTextureFormat {
                        Format = storage, Width = (uint)size.X, Height = (uint)size.Y,
                        Depth = 1, ArrayLayers = layers, Mipmaps = 1,
                        TextureType = terminal.Kind == RenderTextureKind.Texture2D
                            ? RenderingDevice.TextureType.Type2D : RenderingDevice.TextureType.Type2DArray,
                        Samples = RenderingDevice.TextureSamples.Samples1, UsageBits = usage };
                    foreach (var f in new[] { storage, shader, target }.Distinct()) format.AddShareableFormat(f);
                    using var baseView = new RDTextureView();
                    Rid storageRid = Own(rd.TextureCreate(format, baseView));
                    Rid shaderRid = storageRid;
                    if (shader != storage)
                    {
                        using var shaderView = new RDTextureView { FormatOverride = shader };
                        shaderRid = Own(rd.TextureCreateShared(shaderView, storageRid));
                    }
                    var allocated = new RenderTargetDeviceTexture(texture, size, storageRid, shaderRid, storage, shader, target)
                        { EffectiveSource = terminal };
                    textures.Add(texture.Object.ObjectId, allocated);
                    physical.Add(terminal.OwnerIdentity, allocated);
                }
                Rid Surface(RenderTextureDefinition texture, uint surfaceId)
                {
                    var allocated = textures[texture.Object.ObjectId];
                    RenderSurface surface = RenderSurfaceMath.Resolve(allocated.EffectiveSource.Declaration, surfaceId);
                    if (allocated.EffectiveSource.Kind == RenderTextureKind.Texture2D && allocated.TargetFormat == allocated.StorageFormat)
                        return allocated.StorageRid;
                    using var view = new RDTextureView { FormatOverride = allocated.TargetFormat };
                    return Own(allocated.EffectiveSource.Kind == RenderTextureKind.Texture2D
                        ? rd.TextureCreateShared(view, allocated.StorageRid)
                        : rd.TextureCreateSharedFromSlice(view, allocated.StorageRid, surface.FirstArraySlice,
                            surface.MipLevel, 1, RenderingDevice.TextureSliceType.Slice2D));
                }
                foreach (var reference in source.ColorTargets) colors.Add(Surface(reference.Texture, reference.SurfaceId));
                if (source.DepthTarget is not null) depth = Surface(source.DepthTarget, source.DepthSurfaceId);
                var attachments = new Godot.Collections.Array<Rid>(colors);
                if (depth.IsValid) attachments.Add(depth);
                Rid framebuffer = Own(rd.FramebufferCreate(attachments));
                return new RenderTargetFrame(source, textures, colors, depth, framebuffer, resources, resolutions);
            }
            catch (Exception failure)
            {
                resources.ReleaseAfterConstructionFailure(rd,failure,"Render-target-frame-partial");
                throw;
            }
        });
    }

    internal static RenderingDevice.DataFormat ViewFormat(uint dxgi) => dxgi switch
    {
        28 => RenderingDevice.DataFormat.R8G8B8A8Unorm, 29 => RenderingDevice.DataFormat.R8G8B8A8Srgb,
        87 => RenderingDevice.DataFormat.B8G8R8A8Unorm, 91 => RenderingDevice.DataFormat.B8G8R8A8Srgb,
        11 => RenderingDevice.DataFormat.R16G16B16A16Unorm, 34 => RenderingDevice.DataFormat.R16G16Sfloat,
        61 => RenderingDevice.DataFormat.R8Unorm,
        40 => RenderingDevice.DataFormat.D32Sfloat, 41 => RenderingDevice.DataFormat.R32Sfloat,
        26 => RenderingDevice.DataFormat.B10G11R11UfloatPack32,
        // Native depth SRV21 and DSV20 share a depth/stencil Vulkan image here.
        // Sampling aspect/returned components require a separate consumer check.
        20 or 21 => RenderingDevice.DataFormat.D32SfloatS8Uint,
        _ => throw new InvalidDataException("Native attachment view format has no explicit device adapter.")
    };

    private async Task DrainPendingArmyVariantAsync()
    {
        NativeMapBorrowerRetirementPackage? pending;
        lock(_armyDevicePassGate)pending=_pendingArmyVariant;
        if(pending is null)return;
        await pending.DisposeAsync();
        lock(_armyDevicePassGate)
        {
            if(ReferenceEquals(_pendingArmyVariant,pending))_pendingArmyVariant=null;
        }
    }

    public ValueTask DisposeAsync()
    {
        lock(_armyDevicePassGate)
        {
            _retiring=true;
            if(_disposal is null||_disposal.IsFaulted||_disposal.IsCanceled)_disposal=DisposeCoreAsync();
            return new(_disposal);
        }
    }

    private async Task DisposeCoreAsync()
    {
        await DrainPendingArmyVariantAsync();
        while(true)
        {
            uint mask;RenderDeviceResources resources;
            lock(_armyDevicePassGate)
            {
                if(_armyDevicePasses.Count==0)break;
                var variant=_armyDevicePasses.Last();
                mask=variant.Key;resources=variant.Value.Resources;
            }
            await resources.DisposeAsync();
            lock(_armyDevicePassGate)_armyDevicePasses.Remove(mask);
        }
        await _resources.DisposeAsync();
    }
}
