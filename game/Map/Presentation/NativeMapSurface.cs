using Godot;
using San14.Data;

/// <summary>One accepted native map frame. Source producers supply the complete
/// camera, terrain state and deferred packet; the surface adds no lighting,
/// texture or raster defaults.</summary>
public sealed record NativeMapRenderRequest(
    IReadOnlyList<NativeTerrainBatch> Batches,
    DeferredCameraInputs Camera,
    TerrainGbufferDrawInputs TerrainParameters,
    TerrainGbufferRasterState TerrainRaster,
    Color[] ClearColors,
    float ClearDepth,
    uint ClearStencil,
    Func<NativeMapFrameContext, Task<NativeDeferredPacket>>? PrepareDeferred)
{
    public RenderingDevice.DrawFlags GeometryClearActions { get; init; } = RenderingDevice.DrawFlags.ClearAll;
    /// <summary>The actual ordered World submissions share one geometry and
    /// presentation transaction. Supply exactly one deferred producer.</summary>
    public Func<NativeMapFrameContext, Task<IReadOnlyList<NativeDeferredPacket>>>? PrepareOrderedDeferred { get; init; }
}

/// <summary>Displays the original device output in the client through the
/// existing GPU presentation bridge. Takes ownership of a constructed native
/// scene after Configure succeeds. Callers still own borrowed stage assets and
/// prepare coherent source frames. This control does not turn on Main lighting
/// or convert the current preview camera to a source camera.</summary>
public partial class NativeMapSurface : Control
{
    private NativeMapScene? _scene;
    private TextureRect? _image;
    private RenderOutputPresentation? _acceptedOutput;
    private RenderOutputPresentation? _spareOutput;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _lifetimeGate = new();
    private Task? _disposal;
    private bool _retiring;
    private bool _hasAcceptedOutput;
    private bool _extentConnected;

    public long PresentedFrameNumber { get; private set; }
    public bool HasAcceptedOutput => _hasAcceptedOutput;
    public Task Retirement => _disposal ?? Task.CompletedTask;
    public event Action<Vector2I>? DisplayExtentChanged;
    /// <summary>Synchronous metadata publication immediately before the canvas
    /// swaps to a successfully copied, independent GPU snapshot.</summary>
    public event Action<NativeMapFrameContext>? BeforeAcceptedOutputPublished;

    /// <summary>Ownership transfers only after successful control setup. An
    /// already rendered scene requires ConfigureAsync to copy its pixels.</summary>
    public void Configure(NativeMapScene ownedScene)
    {
        ArgumentNullException.ThrowIfNull(ownedScene);
        if (ownedScene.FrameNumber != 0)
            throw new InvalidOperationException("Use ConfigureAsync to attach a completed scene snapshot.");
        ConfigureControl(ownedScene);
    }

    public async Task ConfigureAsync(NativeMapScene ownedScene)
    {
        ConfigureControl(ownedScene);
        if (ownedScene.FrameNumber == 0) return;
        await _gate.WaitAsync();
        try { await PublishCompletedOutputAsync(ownedScene); }
        finally { _gate.Release(); }
    }

    private void ConfigureControl(NativeMapScene ownedScene)
    {
        ArgumentNullException.ThrowIfNull(ownedScene);
        if (_scene is not null || _image is not null || _retiring)
            throw new InvalidOperationException("Native map surface was already configured or retired.");
        bool awaitingFinal=ownedScene.RequiresExternalPresentation && ownedScene.FrameNumber==0 && !ownedScene.HasDisplayPresentation;
        if (!awaitingFinal && (!GodotObject.IsInstanceValid(ownedScene.DisplayPresentation.Texture) ||
            ownedScene.DisplayPresentation.Texture.GetWidth() <= 0 ||
            ownedScene.DisplayPresentation.Texture.GetHeight() <= 0))
            throw new InvalidDataException("Native map surface requires a live device presentation.");

        var image = new TextureRect
        {
            Name = "OriginalNativeMapOutput",
            MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = TextureFilterEnum.Nearest
        };
        try
        {
            AddChild(image);
            image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
        catch
        {
            if (image.GetParent() == this) RemoveChild(image);
            image.Free();
            throw;
        }
        _scene = ownedScene;
        _image = image;
        MouseFilter = MouseFilterEnum.Ignore;
        _hasAcceptedOutput = false;
        PresentedFrameNumber = 0;
        Resized += NotifyDisplayExtent;
        _extentConnected = true;
    }

    private void NotifyDisplayExtent()
    {
        // The caller creates the new source camera and target extent together.
        // Control resizing alone never changes an original source matrix.
        if (!_retiring && Size.X > 0 && Size.Y > 0 && Size.IsFinite())
            DisplayExtentChanged?.Invoke(new((int)Math.Ceiling(Size.X), (int)Math.Ceiling(Size.Y)));
    }

    public async Task RenderFrameAsync(NativeMapRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var single = request.PrepareDeferred;
        var ordered = request.PrepareOrderedDeferred;
        if ((single is null) == (ordered is null))
            throw new ArgumentException("A map frame requires exactly one single or ordered deferred producer.", nameof(request));
        NativeMapScene scene = RequireScene();
        // Retain the request before an asynchronous queue wait. Providers keep
        // their borrowed resources alive through the accepted native frame.
        var batches = request.Batches.Select(batch => new NativeTerrainBatch(batch.Coarse,
            Array.AsReadOnly(batch.Patches.ToArray()))).ToArray();
        Color[] colors = request.ClearColors.ToArray();
        TerrainGbufferDrawInputs terrain = request.TerrainParameters with
        {
            WorldToProjectionRows = request.TerrainParameters.WorldToProjectionRows.ToArray(),
            WorldToViewRows = request.TerrainParameters.WorldToViewRows.ToArray()
        };
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_retiring, this);
            if (ordered is not null)
                await scene.RenderOrderedAsync(batches, request.Camera, terrain,
                    request.TerrainRaster, colors, request.ClearDepth, request.ClearStencil, ordered,
                    request.GeometryClearActions);
            else
                await scene.RenderAsync(batches, request.Camera, terrain,
                    request.TerrainRaster, colors, request.ClearDepth, request.ClearStencil, single!,
                    request.GeometryClearActions);
            await PublishCompletedOutputAsync(scene);
        }
        catch
        {
            // Source targets and their wrappers may already be retired. The
            // accepted independent image remains usable; detach only if that
            // image itself is no longer live.
            if (_image is not null && !IsLiveOutput(_image.Texture))
            {
                _image.Texture = null;
                _hasAcceptedOutput = false;
                PresentedFrameNumber = 0;
            }
            throw;
        }
        finally { _gate.Release(); }
    }

    private async Task PublishCompletedOutputAsync(NativeMapScene scene)
    {
        var context = scene.LastCompletedContext
            ?? throw new InvalidDataException("Presentation has no completed scene context.");
        var source = scene.DisplayPresentation.Source;
        if (_spareOutput is { } spare && (!ReferenceEquals(spare.Source, source) || !IsLiveOutput(spare.Texture)))
        {
            await spare.DisposeAsync();
            _spareOutput = null;
        }
        _spareOutput ??= await RenderOutputPresentation.BuildAsync(source, forceGpuCopy: true);
        // Draw targets may be overwritten before a later stage fails. Copy
        // only a completed frame into the buffer that the canvas does not use.
        await _spareOutput.RefreshAsync();
        if (_retiring) return;
        BeforeAcceptedOutputPublished?.Invoke(context);
        var previous = _acceptedOutput;
        _acceptedOutput = _spareOutput;
        _spareOutput = previous;
        _image!.Texture = _acceptedOutput.Texture;
        PresentedFrameNumber = scene.FrameNumber;
        _hasAcceptedOutput = true;
    }

    private static bool IsLiveOutput(Texture2D? texture) =>
        texture is Texture2Drd native && GodotObject.IsInstanceValid(native) &&
        native.TextureRdRid.IsValid && native.GetWidth() > 0 && native.GetHeight() > 0;

    /// <summary>Explicit diagnostic readback of the independent accepted
    /// canvas image, including when a later attempt failed. No raw unfinished
    /// draw target or resized replacement is exported as a completed frame.</summary>
    public async Task<NativeMapPixelSnapshot> CaptureAcceptedPixelsAsync()
    {
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_retiring, this);
            var accepted = _acceptedOutput;
            if (!_hasAcceptedOutput || PresentedFrameNumber <= 0 || accepted is null || !IsLiveOutput(accepted.Texture))
                throw new InvalidOperationException("Native map has no accepted presentation to capture.");
            var extent = accepted.Source.ExplicitExtent;
            var bytes = await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
            {
                if (!rd.TextureIsValid(accepted.PresentedRid))
                    throw new InvalidDataException("Accepted presentation retired before readback.");
                using var format = rd.TextureGetFormat(accepted.PresentedRid);
                if (format.TextureType != RenderingDevice.TextureType.Type2D || format.ArrayLayers != 1 ||
                    format.Mipmaps != 1 || format.Width != extent.X || format.Height != extent.Y ||
                    !format.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.CanCopyFromBit))
                    throw new InvalidDataException("Accepted presentation has no exact ordinary readback image.");
                bool rgba = format.Format is RenderingDevice.DataFormat.R8G8B8A8Unorm or RenderingDevice.DataFormat.R8G8B8A8Srgb;
                bool bgra = format.Format is RenderingDevice.DataFormat.B8G8R8A8Unorm or RenderingDevice.DataFormat.B8G8R8A8Srgb;
                if (!rgba && !bgra)
                    throw new NotSupportedException("Lossless BMP capture requires an actual 8-bit RGBA/BGRA accepted image.");
                byte[] pixels = rd.TextureGetData(accepted.PresentedRid, 0);
                if (pixels.LongLength != checked((long)extent.X * extent.Y * 4))
                    throw new InvalidDataException("Accepted image readback has an unexpected byte count.");
                if (rgba)
                    for (int i = 0; i < pixels.Length; i += 4) (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
                return pixels;
            });
            return new(PresentedFrameNumber, extent, bytes, "accepted-independent-Surface-image;" +
                accepted.Source.Source.Source.Source.ResourceUri + ";frame=" + PresentedFrameNumber);
        }
        finally { _gate.Release(); }
    }

    /// <summary>The independent accepted image survives target replacement.
    /// A new extent is published only after a complete new frame is copied.</summary>
    public async Task ResizeTargetsAsync(IReadOnlyDictionary<uint, Vector2I> terrainExtents,
        IReadOnlyDictionary<uint, Vector2I> outputExtents, bool allowReadback = false)
    {
        ArgumentNullException.ThrowIfNull(terrainExtents);
        ArgumentNullException.ThrowIfNull(outputExtents);
        NativeMapScene scene = RequireScene();
        var terrain = terrainExtents.ToDictionary(pair => pair.Key, pair => pair.Value);
        var output = outputExtents.ToDictionary(pair => pair.Key, pair => pair.Value);
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_retiring, this);
            await scene.ResizeAsync(terrain, output, allowReadback);
        }
        finally { _gate.Release(); }
    }

    private NativeMapScene RequireScene()
    {
        ObjectDisposedException.ThrowIf(_retiring, this);
        return _scene ?? throw new InvalidOperationException("Native map surface is not configured.");
    }

    public Task RetireAsync()
    {
        lock (_lifetimeGate)
        {
            if (_disposal is not null) return _disposal;
            _retiring = true;
            if (_image is not null && GodotObject.IsInstanceValid(_image)) _image.Texture = null;
            _hasAcceptedOutput = false;
            PresentedFrameNumber = 0;
            if (_extentConnected)
            {
                Resized -= NotifyDisplayExtent;
                _extentConnected = false;
            }
            return _disposal = RetireCoreAsync();
        }
    }

    private async Task RetireCoreAsync()
    {
        // Publish the cached task before a failing external retirement stage
        // could reset it; a caller can then retry after fixing that stage.
        await Task.Yield();
        await _gate.WaitAsync();
        try
        {
            if (_acceptedOutput is { } accepted) { await accepted.DisposeAsync(); _acceptedOutput = null; }
            if (_spareOutput is { } spare) { await spare.DisposeAsync(); _spareOutput = null; }
            if (_scene is not null) await _scene.DisposeAsync();
            _scene = null;
        }
        catch
        {
            lock (_lifetimeGate) _disposal = null;
            throw;
        }
        finally { _gate.Release(); }
    }

    public override void _ExitTree()
    {
        _ = ObserveRetirementAsync(RetireAsync());
    }

    private static async Task ObserveRetirementAsync(Task retirement)
    {
        try { await retirement; }
        catch (Exception error) { GD.PushError($"Native map surface retirement failed: {error}"); }
    }
}
