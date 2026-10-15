using Godot;
using San14.Data;

/// <summary>RenderingDevice transport copy of the current logical E13 depth.
/// The source E13 remains the stencil attachment. This is not a graph output
/// or source writer receipt and cannot stand in for RefractionDepth.</summary>
public sealed record DeferredEarlyShaderDepthPublication(
    RenderTargetDeviceTexture LogicalSource, Rid ShaderViewRid, Rid TransportParent,
    long FrameNumber, string FrameProducerProvenance, string CurrentSourceContentProvenance,
    string BackendTransportProvenance);

/// <summary>Godot forbids sampling an attached depth/stencil parent even when
/// the original draw only reads stencil. CopySubresource transport preserves
/// the same depth bits while keeping actual source E13 stencil attached.</summary>
public sealed class DeferredEarlyShaderDepthMirror : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private Rid texture;
    private Vector2I extent;
    private volatile bool retiring;
    private readonly object lifetime = new();
    private Task? disposal;
    public DeferredEarlyShaderDepthPublication? Current { get; private set; }

    /// <summary>Called after this accepted frame's geometry writers and before
    /// early draws. The caller's ordered Scene owns the source-content boundary.
    /// A failed copy invalidates the previous publication.</summary>
    public async Task<DeferredEarlyShaderDepthPublication> CopyAsync(
        RenderTargetDeviceTexture currentMainDepth, long frameNumber,
        string frameProducerProvenance, string currentSourceContentProvenance)
    {
        ArgumentNullException.ThrowIfNull(currentMainDepth);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameProducerProvenance);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentSourceContentProvenance);
        if (frameNumber <= 0) throw new InvalidDataException("Depth transport requires an explicit accepted frame.");
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring, this);
            Current = null;
            ValidateSource(currentMainDepth);
            if (texture.IsValid && extent != currentMainDepth.ExplicitExtent)
                throw new InvalidOperationException("Retire dependent early draws before resizing the depth transport owner.");
            var publication = await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
            {
                if (!rd.TextureIsValid(currentMainDepth.StorageRid))
                    throw new InvalidDataException("Current logical E13 owner retired before depth transport.");
                using var actual = rd.TextureGetFormat(currentMainDepth.StorageRid);
                if (actual.Format != RenderingDevice.DataFormat.D32SfloatS8Uint ||
                    actual.Width != currentMainDepth.ExplicitExtent.X || actual.Height != currentMainDepth.ExplicitExtent.Y ||
                    actual.Mipmaps != 1 || actual.ArrayLayers != 1 || actual.Samples != RenderingDevice.TextureSamples.Samples1 ||
                    !actual.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.CanCopyFromBit))
                    throw new InvalidDataException("Logical E13 has no exact single-sample GPU-copy depth owner.");
                if (!texture.IsValid)
                {
                    using var format = new RDTextureFormat
                    {
                        Format = RenderingDevice.DataFormat.D32SfloatS8Uint,
                        Width = actual.Width, Height = actual.Height, Depth = 1, ArrayLayers = 1, Mipmaps = 1,
                        TextureType = RenderingDevice.TextureType.Type2D, Samples = RenderingDevice.TextureSamples.Samples1,
                        // Godot classifies depth TextureCopy by this usage bit,
                        // independently of the exact image format. The mirror
                        // remains unattached during early draws.
                        UsageBits = RenderingDevice.TextureUsageBits.SamplingBit |
                            RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit |
                            RenderingDevice.TextureUsageBits.CanCopyToBit | RenderingDevice.TextureUsageBits.CanCopyFromBit
                    };
                    using var view = new RDTextureView();
                    texture = rd.TextureCreate(format, view);
                    if (!texture.IsValid) throw new InvalidDataException("Device rejected the exact E13 depth transport format.");
                    extent = currentMainDepth.ExplicitExtent;
                }
                if (texture == currentMainDepth.StorageRid)
                    throw new InvalidDataException("Depth transport must have a distinct physical parent.");
                var error = rd.TextureCopy(currentMainDepth.StorageRid, texture, Vector3.Zero, Vector3.Zero,
                    new Vector3(extent.X, extent.Y, 1), 0, 0, 0, 0);
                if (error != Error.Ok) throw new InvalidDataException("Exact current E13 depth transport failed: " + error);
                return new DeferredEarlyShaderDepthPublication(currentMainDepth, texture, texture, frameNumber,
                    frameProducerProvenance, currentSourceContentProvenance,
                    "RenderingDevice-only exact D32SfloatS8Uint copy; logical E13DDB4F; original stencil attachment retained; no original graph writer");
            });
            return Current = publication;
        }
        finally { gate.Release(); }
    }

    public DeferredEarlyShaderDepthPublication RequireCurrent(RenderTargetDeviceTexture source,
        long frameNumber, string frameProducerProvenance)
    {
        var current = Current;
        if (current is null || current.LogicalSource != source || current.FrameNumber != frameNumber ||
            current.FrameProducerProvenance != frameProducerProvenance || !current.TransportParent.IsValid)
            throw new InvalidDataException("Depth transport belongs to another logical E13 owner or accepted frame.");
        return current;
    }

    private static void ValidateSource(RenderTargetDeviceTexture source)
    {
        // The parent frame owns resource import. Check only the depth shape
        // and formats consumed by this transport.
        if (source.Source.Object.ObjectId != 0xE13DDB4F ||
            source.Source.Kind != RenderTextureKind.Texture2D || source.Source.LevelNum != 1 ||
            source.Source.MinMipLevel != 0 || source.Source.MultiSample != 0 ||
            source.ExplicitExtent.X <= 0 || source.ExplicitExtent.Y <= 0 ||
            source.StorageFormat != RenderingDevice.DataFormat.D32SfloatS8Uint ||
            source.ShaderFormat != RenderingDevice.DataFormat.D32SfloatS8Uint ||
            source.TargetFormat != RenderingDevice.DataFormat.D32SfloatS8Uint)
            throw new InvalidDataException("Depth transport requires the actual current original E13 single-mip depth declaration.");
    }

    /// <summary>Every cached early draw that references this transport must
    /// retire first. Reverse destruction belongs to the containing factory.</summary>
    public async Task RetireAsync()
    {
        await gate.WaitAsync();
        try
        {
            Current = null;
            await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
            { if (texture.IsValid) rd.FreeRid(texture); texture = default; extent = default; return true; });
        }
        finally { gate.Release(); }
    }
    public ValueTask DisposeAsync()
    {
        lock (lifetime)
        {
            retiring = true;
            if (disposal is null || disposal.IsFaulted || disposal.IsCanceled)
                disposal = RetireAsync();
            return new(disposal);
        }
    }
}
