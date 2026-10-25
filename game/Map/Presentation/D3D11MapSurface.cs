using Godot;
using San14.Data;

/// <summary>Displays the explicit staged native Main source through one retained
/// BGRA output. Camera, display and actual World publications come from
/// the caller; this surface retains GPU and Canvas lifetimes.</summary>
public partial class D3D11MapSurface : Control
{
    public const int OutputWidth = 1920, OutputHeight = 1080;
    public event Action<D3D11MapInput, D3D11MapReceipt>? Accepted;
    public event Action<D3D11MapInput, D3D11MapReceipt>? FramePrepared;
    public event Action<Exception>? Failed;
    public D3D11MapInput? LastAcceptedInput { get; private set; }
    public D3D11MapReceipt? LastReceipt { get; private set; }
    public Exception? LastError { get; private set; }
    public bool IsRendering => _rendering;
    public bool IsClosed => _closed;
    private bool _configured, _rendering, _closed;
    private string _sourceDirectory = "", _nativeDLLPath = "";
    private uint _populationMask;
    private Rid _texture, _framebuffer;
    private D3D11RenderSession? _session;
    internal IG1EffectDirectComputeDevice? EffectComputeDevice => _session;
    private Texture2Drd? _presentation;
    private TextureRect? _image;
    private D3D11MapInput? _pending;
    private D3D11MapInput? _openingInput;
    private readonly List<(D3D11MapInput Input,uint FpsMode)> _animationPending=[];
    private Func<D3D11MapInput, D3D11MapInput>? _prepareCurrent;
    private double _pendingElapsed;
    private Task? _pump, _close;
    private TaskCompletionSource<bool>? _ready;
    private TaskCompletionSource<bool>? _frameWait;
    private Action? _frameHandler, _framePreHandler, _firstDrawHandler;

    public async Task ConfigureAsync(string sourceDirectory, string nativeDLLPath,
        D3D11MapInput initialInput, uint populationMask,
        Func<D3D11MapInput, D3D11MapInput>? prepareCurrent = null)
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        if (_configured) throw new InvalidOperationException("Native map surface is already configured.");
        if (!IsInsideTree()) throw new InvalidOperationException("Attach the native surface before opening it.");
        if (RenderingServer.GetCurrentRenderingDriverName() != "d3d12" || GetViewport().UseHdr2D)
            throw new NotSupportedException("Native Main presentation requires D3D12 and a non-HDR Canvas.");
        ArgumentNullException.ThrowIfNull(initialInput);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeDLLPath);
        _sourceDirectory = sourceDirectory; _nativeDLLPath = nativeDLLPath; _populationMask = populationMask;
        _prepareCurrent = prepareCurrent;
        _configured = true;
        MouseFilter = MouseFilterEnum.Ignore;
        _image = new TextureRect { MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = TextureFilterEnum.Nearest };
        AddChild(_image); _image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // The first normal Map callback has its own immutable input. Camera
        // publications received while the GPU opens cannot replace it.
        _openingInput=initialInput;_pending=null;_pendingElapsed=0;
        _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _pump = PumpAsync();
        if (!await _ready.Task) throw new OperationCanceledException("Native map surface closed while opening.");
    }

    public void Record(MainMapCameraFrame frame, double elapsedSeconds,
        bool paused = false, bool eligibleAnimationUpdate = false) =>
        Record(D3D11MapInput.FromCamera(frame, elapsedSeconds, paused, eligibleAnimationUpdate));

    /// <summary>Main-thread publication. An in-flight request keeps its input;
    /// pending requests retain the newest camera and their accumulated elapsed time.</summary>
    public void Record(D3D11MapInput input)
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        if (!_configured || LastError is not null) throw new InvalidOperationException("Native surface is not available.");
        ArgumentNullException.ThrowIfNull(input);
        var latest = input.WithElapsed(_pendingElapsed + input.ElapsedSeconds);
        _pending = latest; _pendingElapsed = latest.ElapsedSeconds;
        if (!_rendering) _pump = PumpAsync();
    }

    /// <summary>One actual reached manager callback. Preserve its World/camera
    /// even if multiple drawing requests are later merged.</summary>
    public void RecordAnimationUpdate(D3D11MapInput input,uint fpsMode)
    {
        ObjectDisposedException.ThrowIf(_closed,this);
        _animationPending.Add((input,fpsMode));
    }

    private D3D11MapInput TakePending()
    {
        var input = _pending!.WithElapsed(_pendingElapsed);
        _pending = null; _pendingElapsed = 0;
        return _prepareCurrent?.Invoke(input) ?? input;
    }

    private async Task PumpAsync()
    {
        _rendering = true;
        try
        {
            if (_session is null)
            {
                await OnRenderThread(rd =>
                {
                    if (_closed) return false;
                    // ed1daf0bf's BGRA sRGB mapping has a cross-family view bug.
                    // BGRA UNORM preserves the original final sRGB-encoded bytes;
                    // Godot's non-HDR Canvas consumes those encoded values directly.
                    using var format = new RDTextureFormat { Format = RenderingDevice.DataFormat.B8G8R8A8Unorm,
                        Width = OutputWidth, Height = OutputHeight, Depth = 1, ArrayLayers = 1, Mipmaps = 1,
                        TextureType = RenderingDevice.TextureType.Type2D,
                        UsageBits = RenderingDevice.TextureUsageBits.ColorAttachmentBit |
                            RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanCopyFromBit |
                            RenderingDevice.TextureUsageBits.CanCopyToBit };
                    using var view = new RDTextureView(); _texture = rd.TextureCreate(format, view);
                    if (!_texture.IsValid) throw new InvalidOperationException("Native BGRA output allocation failed.");
                    _framebuffer = rd.FramebufferCreate(new Godot.Collections.Array<Rid> { _texture });
                    if (!_framebuffer.IsValid) throw new InvalidOperationException("Native output framebuffer creation failed.");
                    rd.DrawListBegin(_framebuffer, RenderingDevice.DrawFlags.ClearColor0, [Colors.Black]); rd.DrawListEnd();
                    return true;
                });
                if (_closed || !await WaitForFrameAsync()) return;
                var initial=_prepareCurrent?.Invoke(_openingInput!)??_openingInput!;
                _openingInput=null;
                var receipt = await OnRenderThread(rd =>
                {
                    if (_closed) return default(D3D11MapReceipt);
                    _session = new(_sourceDirectory, _nativeDLLPath, _populationMask);
                    return _session.Create(rd.GetDriverResource(RenderingDevice.DriverResource.LogicalDevice, default, 0),
                        rd.GetDriverResource(RenderingDevice.DriverResource.CommandQueue, default, 0),
                        rd.GetDriverResource(RenderingDevice.DriverResource.Texture, _texture, 0), initial);
                });
                if (_closed) return;
                _presentation = new Texture2Drd { TextureRdRid = _texture }; _image!.Texture = _presentation;
                FramePrepared?.Invoke(initial, receipt);
                // Its first real Canvas submission establishes the SRV layout.
                if (_closed || !await WaitForFrameAsync(initialCanvas: true)) return;
                Publish(initial, receipt);
                _ready!.TrySetResult(true);
            }
            while (!_closed && _pending is not null)
            {
                var input = TakePending();
                var updates=_animationPending.ToArray();_animationPending.Clear();
                var receipt = await OnRenderThread(rd =>
                {
                    if(_closed)return default;
                    foreach(var update in updates)_session!.UpdateAnimations(update.Input,update.FpsMode);
                    return _session!.Next(rd.GetDriverResource(RenderingDevice.DriverResource.Texture,_texture,0),input);
                });
                if (_closed) return;
                FramePrepared?.Invoke(input, receipt);
                if (_closed || !await WaitForFrameAsync()) return;
                Publish(input, receipt);
            }
        }
        catch (Exception error)
        {
            LastError = error; _pending = null; _openingInput=null;_pendingElapsed = 0;_animationPending.Clear();
            _ready?.TrySetException(error);
            Failed?.Invoke(error);
            GD.PushError($"Native D3D11 map surface failed: {error}");
        }
        finally
        {
            _rendering = false;
            if (LastError is not null && !_closed) _ = ObserveCloseAsync();
        }
    }

    private void Publish(D3D11MapInput input, D3D11MapReceipt receipt)
    {
        LastAcceptedInput = input; LastReceipt = receipt; Accepted?.Invoke(input, receipt);
    }

    private Task<bool> WaitForFrameAsync(bool initialCanvas = false)
    {
        if (_closed) return Task.FromResult(false);
        var wait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _frameWait = wait;
        bool submitted = !initialCanvas;
        if (initialCanvas)
        {
            // Draw builds the TextureRect command; it is not a GPU callback.
            // A hidden/zero-size map cannot establish the first SRV use just
            // because a different viewport emitted FramePostDraw.
            bool commandsReady = false;
            _firstDrawHandler = () => commandsReady = true;
            _image!.Draw += _firstDrawHandler;
            _framePreHandler = () => submitted = commandsReady && _image.IsVisibleInTree() &&
                _image.Size.X > 0 && _image.Size.Y > 0;
            RenderingServer.FramePreDraw += _framePreHandler;
        }
        _frameHandler = () => { if (submitted) CompleteFrameWait(true); };
        RenderingServer.FramePostDraw += _frameHandler;
        return wait.Task;
    }
    private void CompleteFrameWait(bool submitted)
    {
        if (_frameHandler is not null) RenderingServer.FramePostDraw -= _frameHandler;
        if (_framePreHandler is not null) RenderingServer.FramePreDraw -= _framePreHandler;
        if (_firstDrawHandler is not null && GodotObject.IsInstanceValid(_image)) _image!.Draw -= _firstDrawHandler;
        _frameHandler = null; _framePreHandler = null; _firstDrawHandler = null;
        var wait = _frameWait; _frameWait = null; wait?.TrySetResult(submitted);
    }

    public Task CloseAsync()
    {
        if (_close is not null) return _close;
        _closed = true; _pending = null; _openingInput=null;_pendingElapsed = 0;_animationPending.Clear();
        _ready?.TrySetResult(false);
        CompleteFrameWait(false);
        _close = CloseCoreAsync(); return _close;
    }
    private async Task CloseCoreAsync()
    {
        // The pump includes the queued render-thread call, so creation/close
        // cannot retire a session or RID while an actual GPU request uses it.
        if (_pump is not null)
        {
            try { await _pump; }
            catch (Exception error) { GD.PushError($"Native map callback failed while retiring: {error}"); }
        }
        if (_image is not null && GodotObject.IsInstanceValid(_image)) _image.Texture = null;
        if (_presentation is not null) _presentation.TextureRdRid = default;
        await OnRenderThread(rd =>
        {
            _session?.Dispose(); _session = null;
            if (_framebuffer.IsValid) { rd.FreeRid(_framebuffer); _framebuffer = default; }
            if (_texture.IsValid) { rd.FreeRid(_texture); _texture = default; }
            return true;
        });
        _presentation?.Dispose(); _presentation = null;
    }
    public override void _ExitTree() => _ = ObserveCloseAsync();
    private async Task ObserveCloseAsync()
    {
        try { await CloseAsync(); }
        catch (Exception error) { GD.PushError($"Native D3D11 map retirement failed: {error}"); }
    }
    private static Task<T> OnRenderThread<T>(Func<RenderingDevice, T> action)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        RenderingServer.CallOnRenderThread(Callable.From(() =>
        {
            try { done.SetResult(action(RenderingServer.GetRenderingDevice() ??
                throw new NotSupportedException("Godot has no global RenderingDevice."))); }
            catch (Exception error) { done.SetException(error); }
        }));
        return done.Task;
    }
}
