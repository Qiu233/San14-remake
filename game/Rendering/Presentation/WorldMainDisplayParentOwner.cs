using Godot;
using San14.Data;

/// <summary>Borrowed actual device surface. SurfaceOwner and WrapperOwner are
/// the real allocation/wrapper objects, independent of any RGK source name.
/// SourceDxgiFormat describes this port display selection; it does not claim
/// that the original Windows device.v2C8 was invoked.</summary>
public sealed record WorldMainDisplaySurface(object SurfaceOwner, object WrapperOwner,
    Rid StorageRid, Rid TargetView, Vector2I Extent, uint SourceDxgiFormat,
    Action RequireCurrent, string SourceEvidence);

public sealed record WorldMainDisplaySurfaceSet(WorldMainDisplaySurface Color,
    WorldMainDisplaySurface? Depth);

/// <summary>Actual row selected after this owner successfully created/selected
/// its real framebuffer. It is not a named graph target or geometry draw.</summary>
public sealed class WorldMainDisplayParentEntry : IWorldGeometryTargetDeviceRow
{
    internal WorldMainDisplayParentEntry(WorldMainDisplayParentOwner owner,
        WorldMainDisplaySurfaceSet surfaces, Rid framebuffer,
        WorldMainDisplayEntryRecipe recipe)
    {
        Owner = owner; Surfaces = surfaces; Framebuffer = framebuffer; Recipe = recipe;
        ColorViews = Array.AsReadOnly(new[] { surfaces.Color.TargetView });
        DepthView = surfaces.Depth?.TargetView ?? default; Extent = surfaces.Color.Extent;
        var v = recipe.Viewport;
        Domain = new(v.X, v.Y, v.Width, v.Height, BitConverter.UInt32BitsToSingle(v.MinimumDepthBits),
            BitConverter.UInt32BitsToSingle(v.MaximumDepthBits), recipe.ScissorEnabled,
            new(recipe.ScissorX, recipe.ScissorY, recipe.ScissorWidth, recipe.ScissorHeight),
            recipe.SourceEvidence + ";" + surfaces.Color.SourceEvidence + ";actual-display-framebuffer=" + framebuffer.Id);
    }
    public WorldMainDisplayParentOwner Owner { get; }
    public WorldMainDisplaySurfaceSet Surfaces { get; }
    public Rid Framebuffer { get; }
    public IReadOnlyList<Rid> ColorViews { get; }
    public Rid DepthView { get; }
    public Vector2I Extent { get; }
    public WorldMainDisplayEntryRecipe Recipe { get; }
    public WorldGeometryDrawDomain Domain { get; }
    public int TargetStackIndex => Recipe.TargetStackIndex;
    public string SourceEvidence => Domain.SourceEvidence;
    public void RequireCurrent() => Owner.RequireCurrent(this);
}

/// <summary>The normal Main physical-display parent. Owns only its framebuffer;
/// actual color/depth surfaces and target views remain with their real owner.
/// Source is imported once. Begin/End are normal invocation lifetime boundaries,
/// and each Begin selects one real row rather than a frame-number branch.</summary>
public sealed class WorldMainDisplayParentOwner : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Func<WorldMainDisplaySurfaceSet> readActualCurrentSurfaces;
    private WorldMainDisplaySurfaceSet? cachedSurfaces;
    private Rid framebuffer;
    private WorldMainDisplayParentEntry? current;
    private int targetStackIndex = -1;
    private bool disposed;

    public WorldMainDisplayParentOwner(LoadedWorldMainDisplayParentSource source,
        Func<WorldMainDisplaySurfaceSet> readActualCurrentSurfaces)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        this.readActualCurrentSurfaces = readActualCurrentSurfaces ?? throw new ArgumentNullException(nameof(readActualCurrentSurfaces));
    }
    public LoadedWorldMainDisplayParentSource Source { get; }

    public async Task<WorldMainDisplayParentEntry> BeginCurrentAsync(
        WorldMainDisplayViewport retainedViewport, bool retainedScissorEnabled)
    {
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (current is not null) throw new InvalidOperationException("Previous Main display row remains selected.");
            var surfaces = readActualCurrentSurfaces();
            ArgumentNullException.ThrowIfNull(surfaces); ArgumentNullException.ThrowIfNull(surfaces.Color);
            surfaces.Color.RequireCurrent(); surfaces.Depth?.RequireCurrent();
            var recipe = Source.PrepareEntry(surfaces.Color.Extent.X, surfaces.Color.Extent.Y,
                retainedViewport, retainedScissorEnabled, targetStackIndex);
            Rid selected = await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
            {
                ValidateDeviceSurfaces(rd, surfaces);
                if (framebuffer.IsValid && SameSurface(cachedSurfaces?.Color, surfaces.Color) &&
                    SameSurface(cachedSurfaces?.Depth, surfaces.Depth)) return framebuffer;
                var attachments = new Godot.Collections.Array<Rid> { surfaces.Color.TargetView };
                if (surfaces.Depth is { } depth) attachments.Add(depth.TargetView);
                Rid created = rd.FramebufferCreate(attachments);
                if (!created.IsValid) throw new InvalidDataException("Device rejected the real Main display framebuffer.");
                if (framebuffer.IsValid) rd.FreeRid(framebuffer);
                framebuffer = created; cachedSurfaces = surfaces;
                return created;
            });
            // RenderingDevice selects a framebuffer per actual draw list;
            // no empty draw list is needed to declare this current target row.
            targetStackIndex = recipe.TargetStackIndex;
            return current = new(this, surfaces, selected, recipe);
        }
        finally { gate.Release(); }
    }

    /// <summary>Called after the whole actual graph returns. Pops only this
    /// owner's outer row. Host reset fields are returned for the actual Host
    /// writer; no shader/generic/context completion is invented here.</summary>
    public WorldMainDisplayExitRecipe EndCurrent(WorldMainDisplayParentEntry entry)
    {
        gate.Wait();
        try
        {
            RequireCurrent(entry);
            var result = Source.PrepareExit(targetStackIndex);
            targetStackIndex = result.TargetStackIndex; current = null;
            return result;
        }
        finally { gate.Release(); }
    }

    /// <summary>Graph failure retirement only. Revoke this port's managed row
    /// authority so the caller can drain/retire its real resources. There is
    /// no source End/Host reset or whole-graph-success receipt on this path.
    /// RenderingDevice has no global target binding to restore here.</summary>
    public void AbandonCurrentForRetirement()
    {
        gate.Wait();
        try { current = null; targetStackIndex = -1; }
        finally { gate.Release(); }
    }

    internal void RequireCurrent(WorldMainDisplayParentEntry entry)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!ReferenceEquals(current, entry)) throw new InvalidOperationException("Main display entry is no longer the current row.");
        entry.Surfaces.Color.RequireCurrent(); entry.Surfaces.Depth?.RequireCurrent();
    }

    private static bool SameSurface(WorldMainDisplaySurface? a, WorldMainDisplaySurface? b)
        => a is null ? b is null : b is not null && ReferenceEquals(a.SurfaceOwner, b.SurfaceOwner) &&
            ReferenceEquals(a.WrapperOwner, b.WrapperOwner) && a.StorageRid == b.StorageRid &&
            a.TargetView == b.TargetView && a.Extent == b.Extent && a.SourceDxgiFormat == b.SourceDxgiFormat;

    private void ValidateDeviceSurfaces(RenderingDevice rd, WorldMainDisplaySurfaceSet surfaces)
    {
        var color = surfaces.Color;
        if (!color.StorageRid.IsValid || !color.TargetView.IsValid || !rd.TextureIsValid(color.StorageRid) ||
            !rd.TextureIsValid(color.TargetView) || color.Extent.X <= 0 || color.Extent.Y <= 0)
            throw new InvalidDataException("Main display color requires its actual live texture and view.");
        var source = Source.ResolveColorFormat(color.SourceDxgiFormat);
        var expected = (source.NativeFormat, source.Srgb) switch
        {
            (0, false) => RenderingDevice.DataFormat.R8G8B8A8Unorm,
            (0, true) => RenderingDevice.DataFormat.R8G8B8A8Srgb,
            (1, false) => RenderingDevice.DataFormat.B8G8R8A8Unorm,
            (1, true) => RenderingDevice.DataFormat.B8G8R8A8Srgb,
            _ => throw new NotSupportedException("Unsupported display color translation.")
        };
        using var format = rd.TextureGetFormat(color.TargetView);
        if (format.Width != color.Extent.X || format.Height != color.Extent.Y || format.Format != expected ||
            format.Samples != RenderingDevice.TextureSamples.Samples1)
            throw new InvalidDataException("Main display extent/format differs from its actual physical surface selection.");
        if (surfaces.Depth is not { } depth) return;
        if (depth.Extent != color.Extent || !depth.TargetView.IsValid || !depth.StorageRid.IsValid ||
            !rd.TextureIsValid(depth.TargetView) || !rd.TextureIsValid(depth.StorageRid))
            throw new InvalidDataException("Main display depth must be its actual same-extent live optional surface.");
        using var depthFormat = rd.TextureGetFormat(depth.TargetView);
        // Native108/78/5/20 map to the real D3D query's four depth formats.
        var expectedDepth = Source.ResolveDepthFormat(depth.SourceDxgiFormat) switch
        {
            108 => RenderingDevice.DataFormat.D32SfloatS8Uint,
            78 => RenderingDevice.DataFormat.D32Sfloat,
            5 => RenderingDevice.DataFormat.D24UnormS8Uint,
            20 => RenderingDevice.DataFormat.D16Unorm,
            _ => throw new NotSupportedException("Unsupported display depth translation.")
        };
        if (depthFormat.Width != depth.Extent.X || depthFormat.Height != depth.Extent.Y || depthFormat.Format != expectedDepth ||
            depthFormat.Samples != RenderingDevice.TextureSamples.Samples1)
            throw new InvalidDataException("Main display depth format differs from its actual physical surface selection.");
    }

    /// <summary>Retire the borrowed attachment framebuffer before its actual
    /// surface owner resizes or disposes. Keep the loaded source/outer-row owner.
    /// The next ordinary Begin borrows the new real display surface generation.</summary>
    public async Task RetireFramebufferAsync()
    {
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (current is not null) throw new InvalidOperationException("Return the Main display row before retiring its borrowed framebuffer.");
            await ReleaseFramebufferAsync();
        }
        finally { gate.Release(); }
    }

    private async Task ReleaseFramebufferAsync()
    {
        if (framebuffer.IsValid)
            await TerrainDeviceTexture.OnRenderThreadAsync(rd => { rd.FreeRid(framebuffer); return true; });
        framebuffer = default; cachedSurfaces = null;
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (disposed) return;
            if (current is not null) throw new InvalidOperationException("Return the actual Main display row before retiring its framebuffer.");
            await ReleaseFramebufferAsync();
            disposed = true;
            // Borrowed color/depth textures and views are never freed here.
        }
        finally { gate.Release(); }
    }
}
