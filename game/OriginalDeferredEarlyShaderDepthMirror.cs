using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>RenderingDevice transport copy of the current logical E13 depth.
/// The source E13 remains the stencil attachment. This is not a graph output
/// or source writer receipt and cannot stand in for RefractionDepth.</summary>
public sealed record OriginalDeferredEarlyShaderDepthPublication(
    OriginalRenderTargetDeviceTexture LogicalSource, Rid ShaderViewRid, Rid TransportParent,
    long FrameNumber, string FrameProducerProvenance, string CurrentSourceContentProvenance,
    string BackendTransportProvenance);

/// <summary>Godot forbids sampling an attached depth/stencil parent even when
/// the original draw only reads stencil. CopySubresource transport preserves
/// the same depth bits while keeping actual source E13 stencil attached.</summary>
public sealed class OriginalDeferredEarlyShaderDepthMirror : IAsyncDisposable
{
    // The DOK is an imported source object. Retain its declaration for that
    // loaded lifetime; a reload creates another key and imports again.
    private static readonly ConditionalWeakTable<LoadedObjects, OriginalRenderTextureDefinition> loadedDepthSources = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private Rid texture;
    private Vector2I extent;
    private bool retiring;
    private readonly object lifetime = new();
    private Task? disposal;
    public OriginalDeferredEarlyShaderDepthPublication? Current { get; private set; }

    /// <summary>Called after this accepted frame's geometry writers and before
    /// early draws. The caller's ordered Scene owns the source-content boundary.
    /// A failed copy invalidates the previous publication.</summary>
    public async Task<OriginalDeferredEarlyShaderDepthPublication> CopyAsync(
        OriginalRenderTargetDeviceTexture currentMainDepth, long frameNumber,
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
            var publication = await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
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
                return new OriginalDeferredEarlyShaderDepthPublication(currentMainDepth, texture, texture, frameNumber,
                    frameProducerProvenance, currentSourceContentProvenance,
                    "RenderingDevice-only exact D32SfloatS8Uint copy; logical E13DDB4F; original stencil attachment retained; no original graph writer");
            });
            return Current = publication;
        }
        finally { gate.Release(); }
    }

    public OriginalDeferredEarlyShaderDepthPublication RequireCurrent(OriginalRenderTargetDeviceTexture source,
        long frameNumber, string frameProducerProvenance)
    {
        var current = Current;
        if (current is null || current.LogicalSource != source || current.FrameNumber != frameNumber ||
            current.FrameProducerProvenance != frameProducerProvenance || !current.TransportParent.IsValid)
            throw new InvalidDataException("Depth transport belongs to another logical E13 owner or accepted frame.");
        return current;
    }

    private static void ValidateSource(OriginalRenderTargetDeviceTexture source)
    {
        if (source.Source.Object.ObjectId != 0xE13DDB4F ||
            source.Source.Kind != OriginalRenderTextureKind.Texture2D || source.Source.LevelNum != 1 ||
            source.Source.MinMipLevel != 0 || source.Source.MultiSample != 0 ||
            source.ExplicitExtent.X <= 0 || source.ExplicitExtent.Y <= 0 ||
            source.StorageFormat != RenderingDevice.DataFormat.D32SfloatS8Uint ||
            source.ShaderFormat != RenderingDevice.DataFormat.D32SfloatS8Uint ||
            source.TargetFormat != RenderingDevice.DataFormat.D32SfloatS8Uint)
            throw new InvalidDataException("Depth transport requires the actual current original E13 single-mip depth declaration.");
        var canonical = loadedDepthSources.GetValue(source.Source.Source, ImportDepthSource);
        if (canonical with { Source = source.Source.Source, Object = source.Source.Object } != source.Source)
            throw new InvalidDataException("Logical E13 declaration differs from its imported source.");
    }

    private static OriginalRenderTextureDefinition ImportDepthSource(LoadedObjects source)
    {
        var bytes = source.Source;
        if (bytes.OverrideSource is not null || bytes.ResourceUri != OriginalDeferredAuxiliaryTargetSources.ActualObjectsResourceUri ||
            bytes.DecodedSha256 != OriginalDeferredAuxiliaryTargetSources.ActualObjectsSha256 ||
            Convert.ToHexString(SHA256.HashData(bytes.Bytes)) != bytes.DecodedSha256)
            throw new InvalidDataException("Depth transport requires the original E13 objects at source import.");
        var canonical = OriginalRenderTextureResources.Read(new LoadedObjects(DokReader.Read(bytes.Bytes), bytes), 0xE13DDB4F);
        // Do not retain a second complete parsed DOK alongside the actual
        // loaded objects. Only the verified E13 declaration is needed here.
        return canonical with { Source = source };
    }

    /// <summary>Every cached early draw that references this transport must
    /// retire first. Reverse destruction belongs to the containing factory.</summary>
    public async Task RetireAsync()
    {
        await gate.WaitAsync();
        try
        {
            Current = null;
            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
            { if (texture.IsValid) rd.FreeRid(texture); texture = default; extent = default; return true; });
        }
        finally { gate.Release(); }
    }
    public ValueTask DisposeAsync()
    {
        lock (lifetime) return new(disposal ??= DisposeCoreAsync());
    }
    private async Task DisposeCoreAsync()
    { retiring = true; await RetireAsync(); }
}
