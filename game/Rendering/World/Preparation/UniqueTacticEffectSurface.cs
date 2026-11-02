using Godot;
using San14.Data;

/// <summary>A dedicated selector-2/3 output. Native acceptance follows actual
/// copy/fence and Canvas submission; allocating a texture is not acceptance.</summary>
internal partial class UniqueTacticEffectSurface : Control
{
    private readonly SemaphoreSlim submissions = new(1, 1);
    private D3D11UiEffectSession? session;
    private Rid texture, framebuffer;
    private Texture2Drd? presentation;
    private TextureRect? image;
    private Shader? compositionShader;
    private ShaderMaterial? compositionMaterial;
    private SubViewport? canvasPrefix;
    private uint imageVisibilityLayer;
    private bool compositionAttached;
    private Task? close;
    private bool closed;
    private TaskCompletionSource<bool>? frameWait;
    private Action? postDraw, trackPreDraw, trackPostDraw, trackImageDraw;
    private bool commandsReady, samplesThisFrame;
    private byte sourceAlpha;
    private D3D11UiEffectTextureUse outputUse = D3D11UiEffectTextureUse.RenderTarget;
    private ulong pendingSequence;
    private long canvasFrame, pendingAfterFrame;
    internal event Action<ulong>? Presented;
    internal ulong LastPresentedSequence { get; private set; }
    internal event Action? CanvasCutChanged;
    internal bool CanvasCutEnabled => !closed && presentation is not null && sourceAlpha != 0;
    internal Exception? LastError { get; private set; }

    // Alpha is the primitive's source byte, including its authored inheritance
    // gates and animation. It is not Godot's continuously multiplied COLOR.a.
    internal void ApplySourceAlpha(byte value)
    {
        if (sourceAlpha == value) return;
        bool enabled = CanvasCutEnabled;
        sourceAlpha = value;
        compositionMaterial?.SetShaderParameter("pane_alpha", value / 255f);
        compositionMaterial?.SetShaderParameter("pane_opaque", value == byte.MaxValue);
        if (CanvasCutEnabled != enabled) CanvasCutChanged?.Invoke();
    }

    internal async Task OpenAsync(string nativeDll, int selector, int width, int height)
    {
        if (session is not null) throw new InvalidOperationException("UI effect surface is already open.");
        ObjectDisposedException.ThrowIf(closed, this);
        if (!IsInsideTree() || RenderingServer.GetCurrentRenderingDriverName() != "d3d12" || GetViewport().UseHdr2D)
            throw new NotSupportedException("UI effects require an attached non-HDR D3D12 Canvas.");
        MouseFilter = MouseFilterEnum.Ignore; Size = new(width, height);
        image = new TextureRect { MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale, TextureFilter = TextureFilterEnum.Nearest };
        AddChild(image); image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        await submissions.WaitAsync();
        try
        {
            await Render(rd =>
            {
                using var format = new RDTextureFormat { Format = RenderingDevice.DataFormat.B8G8R8A8Unorm,
                    Width = (uint)width, Height = (uint)height, Depth = 1, ArrayLayers = 1, Mipmaps = 1,
                    TextureType = RenderingDevice.TextureType.Type2D,
                    UsageBits = RenderingDevice.TextureUsageBits.ColorAttachmentBit | RenderingDevice.TextureUsageBits.SamplingBit |
                        RenderingDevice.TextureUsageBits.CanCopyFromBit | RenderingDevice.TextureUsageBits.CanCopyToBit };
                using var view = new RDTextureView(); texture = rd.TextureCreate(format, view);
                if (!texture.IsValid) throw new InvalidOperationException("UI effect output allocation failed.");
                framebuffer = rd.FramebufferCreate(new Godot.Collections.Array<Rid> { texture });
                if (!framebuffer.IsValid) throw new InvalidOperationException("UI effect framebuffer allocation failed.");
                rd.DrawListBegin(framebuffer, RenderingDevice.DrawFlags.ClearColor0, [Colors.Transparent]); rd.DrawListEnd();
                return true;
            });
            if (!await Frame()) throw new OperationCanceledException("UI effect surface closed while opening.");
            await Render(rd =>
            {
                if (closed) throw new OperationCanceledException();
                session = new(nativeDll);
                return session.Create(rd.GetDriverResource(RenderingDevice.DriverResource.LogicalDevice, default, 0),
                    rd.GetDriverResource(RenderingDevice.DriverResource.CommandQueue, default, 0),
                    rd.GetDriverResource(RenderingDevice.DriverResource.Texture, texture, 0), width, height, selector);
            });
            // Native Create has already written the real empty FF image:
            // Cfx=0, transmittance=1. Allocation's temporary clear is not shown.
            presentation = new Texture2Drd { TextureRdRid = texture }; image.Texture = presentation;
            image.Hide(); CanvasCutChanged?.Invoke();
            TrackCanvas();
        }
        catch (Exception error) { LastError = error; throw; }
        finally { submissions.Release(); }
    }

    internal void AttachCanvasPrefix(SubViewport predecessor, SubViewport successor, uint outputLayer)
    {
        ObjectDisposedException.ThrowIf(closed, this);
        if (!CanvasCutEnabled || predecessor.Size != successor.Size || predecessor.Size.X <= 0 || predecessor.Size.Y <= 0)
            throw new InvalidOperationException("UI effect composition requires its actual ordered Canvas prefix.");
        compositionShader ??= new Shader { ResourceName = "UniqueEffectTransmittanceComposition", Code = """
            shader_type canvas_item;
            render_mode unshaded, blend_disabled;
            uniform sampler2D destination_image : filter_nearest, repeat_disable;
            uniform float pane_alpha = 1.0;
            uniform bool pane_opaque = true;
            vec3 decode_srgb(vec3 v) {
                return mix(pow((v + 0.055) / 1.055, vec3(2.4)), v / 12.92,
                    lessThanEqual(v, vec3(0.04045)));
            }
            vec3 encode_srgb(vec3 v) {
                return mix(1.055 * pow(max(v, vec3(0.0)), vec3(1.0 / 2.4)) - 0.055,
                    v * 12.92, lessThanEqual(v, vec3(0.0031308)));
            }
            void fragment() {
                vec4 fx = texture(TEXTURE, UV);
                vec4 prior = texture(destination_image, SCREEN_UV);
                // E88's owned null-ToneScale resolve encodes with gamma2.2.
                // The non-HDR Canvas prefix uses the existing sRGB encoding.
                vec3 fx_linear = pow(max(fx.rgb, vec3(0.0)), vec3(2.2));
                vec3 destination = decode_srgb(prior.rgb);
                vec3 result;
                float alpha;
                if (pane_opaque) {
                    result = fx_linear + destination * fx.a;
                    alpha = prior.a;
                } else {
                    // Source PS19 multiplies sampled alpha by the actual
                    // primitive alpha. Packed00554400 blends alpha by itself.
                    float a = fx.a * pane_alpha;
                    result = fx_linear * a + destination * (1.0 - a);
                    alpha = a * a + prior.a * (1.0 - a);
                }
                COLOR = vec4(encode_srgb(result), alpha);
            }
            """ };
        compositionMaterial ??= new ShaderMaterial { Shader = compositionShader };
        compositionMaterial.SetShaderParameter("destination_image", predecessor.GetTexture());
        compositionMaterial.SetShaderParameter("pane_alpha", sourceAlpha / 255f);
        compositionMaterial.SetShaderParameter("pane_opaque", sourceAlpha == byte.MaxValue);
        if (!compositionAttached) imageVisibilityLayer = image!.VisibilityLayer;
        canvasPrefix = predecessor; compositionAttached = true;
        image!.Material = compositionMaterial; image.VisibilityLayer = outputLayer; image.Show();
    }

    internal void DetachCanvasPrefix()
    {
        canvasPrefix = null;
        if (GodotObject.IsInstanceValid(image))
        {
            image!.Hide(); image.Material = null;
            if (compositionAttached) image.VisibilityLayer = imageVisibilityLayer;
        }
        compositionAttached = false;
        compositionMaterial?.SetShaderParameter("destination_image", default(Variant));
    }

    internal async Task<D3D11UiEffectReceipt> SubmitAsync(G1EffectGpuPublication publication, bool reverseDepth,
        int expectedDrawCount, byte paneAlpha)
    {
        // Do not coalesce packages: a publication may install newly selected
        // shader and texture resources used by the next publication.
        await submissions.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(closed, this);
            // Reach a real frame boundary before selecting the output's prior
            // D3D12 use. Hidden UI is allowed: it has not sampled this texture.
            if (!await Frame()) throw new OperationCanceledException("UI effect surface closed before submission.");
            if (expectedDrawCount > 0)
            {
                // An alpha-zero source emits no UI command. Enabling its next
                // actual output can attach a prefix only after this submit.
                // Hidden source panes can update their native output
                // while issuing no Canvas draw. A visible pane requires its
                // actual ordered predecessor before it can be sampled.
                if (CanvasCutEnabled && (!compositionAttached || !GodotObject.IsInstanceValid(canvasPrefix)))
                {
                    bool visible = IsVisibleInTree();
                    if (visible) throw new NotSupportedException("unique_effect.actual_ordered_canvas_prefix");
                }
            }
            var priorUse = outputUse;
            var receipt = await Render(rd => (session ?? throw new InvalidOperationException("UI effect session is not open."))
                .Next(rd.GetDriverResource(RenderingDevice.DriverResource.Texture, texture, 0), publication, reverseDepth, priorUse, paneAlpha));
            // D3D11InteropOwner wraps with identical input/output states and
            // restores priorUse after the fenced copy. A native write therefore
            // does not turn an already sampled output back into an attachment.
            // This is the real GPU receipt, not Canvas acceptance. Only a later
            // reached visible Canvas submission can present this sequence.
            pendingSequence = publication.Sequence; pendingAfterFrame = canvasFrame;
            ApplySourceAlpha(paneAlpha);
            return receipt;
        }
        catch (Exception error) { LastError = error; throw; }
        finally { submissions.Release(); }
    }

    private void TrackCanvas()
    {
        trackImageDraw = () => { if (presentation is not null && image!.Texture == presentation) commandsReady = true; };
        trackPreDraw = () =>
        {
            canvasFrame++;
            samplesThisFrame = CanvasCutEnabled && commandsReady && image!.IsVisibleInTree() && image.Size.X > 0 && image.Size.Y > 0;
        };
        trackPostDraw = () =>
        {
            if (!samplesThisFrame) return;
            outputUse = D3D11UiEffectTextureUse.ShaderRead;
            if (pendingSequence == 0 || canvasFrame <= pendingAfterFrame) return;
            ulong sequence = pendingSequence; pendingSequence = 0;
            LastPresentedSequence = sequence; Presented?.Invoke(sequence);
        };
        image!.Draw += trackImageDraw;
        RenderingServer.FramePreDraw += trackPreDraw;
        RenderingServer.FramePostDraw += trackPostDraw;
        image.QueueRedraw();
    }

    private Task<bool> Frame()
    {
        if (closed) return Task.FromResult(false);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        frameWait = completion;
        postDraw = () => CompleteFrame(true);
        RenderingServer.FramePostDraw += postDraw; return completion.Task;
    }

    private void CompleteFrame(bool accepted)
    {
        if (postDraw is not null) RenderingServer.FramePostDraw -= postDraw;
        postDraw = null;
        var completion = frameWait; frameWait = null; completion?.TrySetResult(accepted);
    }

    // Must run on the owning UI thread before its Story output parent is
    // released. No native resource is disposed until CloseAsync drains work.
    internal void DetachForRetirement()
    {
        _ = CloseAsync();
        DetachCanvasPrefix();
        if (GodotObject.IsInstanceValid(image)) image!.Texture = null;
        if (GetParent() is { } parent) parent.RemoveChild(this);
    }

    internal Task CloseAsync()
    {
        if (close is not null) return close;
        closed = true; CanvasCutChanged?.Invoke(); CompleteFrame(false);
        if (trackPreDraw is not null) RenderingServer.FramePreDraw -= trackPreDraw;
        if (trackPostDraw is not null) RenderingServer.FramePostDraw -= trackPostDraw;
        if (trackImageDraw is not null && GodotObject.IsInstanceValid(image)) image!.Draw -= trackImageDraw;
        trackPreDraw = trackPostDraw = trackImageDraw = null; pendingSequence = 0;
        return close = CloseCoreAsync();
    }

    private async Task CloseCoreAsync()
    {
        await submissions.WaitAsync();
        try
        {
            if (GodotObject.IsInstanceValid(image)) image!.Texture = null;
            DetachCanvasPrefix();
            if (presentation is not null) presentation.TextureRdRid = default;
            // A failed pre-device Open owns no native work. Its actual empty
            // owner can close under headless/Dummy without inventing a device.
            if (session is not null || framebuffer.IsValid || texture.IsValid)
                await Render(rd =>
                {
                    session?.Dispose(); session = null;
                    if (framebuffer.IsValid) { rd.FreeRid(framebuffer); framebuffer = default; }
                    if (texture.IsValid) { rd.FreeRid(texture); texture = default; }
                    return true;
                });
            presentation?.Dispose(); presentation = null;
            compositionMaterial?.Dispose(); compositionMaterial = null;
            compositionShader?.Dispose(); compositionShader = null;
        }
        finally { submissions.Release(); }
    }

    public override void _EnterTree() => VisibilityChanged += NotifyCanvasCutChanged;
    private void NotifyCanvasCutChanged() => CanvasCutChanged?.Invoke();
    public override void _ExitTree()
    {
        VisibilityChanged -= NotifyCanvasCutChanged;
        _ = ObserveCloseAsync();
    }
    private async Task ObserveCloseAsync()
    {
        try { await CloseAsync(); }
        catch (Exception error) { LastError = error; GD.PushError($"UI effect retirement failed: {error}"); }
    }
    private static Task<T> Render<T>(Func<RenderingDevice, T> action)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        RenderingServer.CallOnRenderThread(Callable.From(() =>
        {
            try { completion.SetResult(action(RenderingServer.GetRenderingDevice() ?? throw new NotSupportedException("No global RenderingDevice."))); }
            catch (Exception error) { completion.SetException(error); }
        }));
        return completion.Task;
    }
}
