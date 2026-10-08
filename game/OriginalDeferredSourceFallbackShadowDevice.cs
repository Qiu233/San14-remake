using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Owns the actual source-initialized white shadow images. Point is a
/// one-cube CubeArray and spot a one-layer Texture2DArray. Consumers borrow
/// images until their draws retire. Existing sampler owners consume the source
/// SamplerInput recipes; this image owner does not duplicate sampler bindings.</summary>
public sealed class OriginalDeferredSourceFallbackShadowDevice : IAsyncDisposable
{
    public LoadedOriginalDeferredSourceFallbackShadowImages Source { get; }
    public OriginalDeferredFallbackShadowConstruction Construction { get; }
    private readonly IReadOnlyDictionary<OriginalDeferredFallbackShadowKind, Rid> images;
    private readonly bool readbackAllowed;
    private readonly object lifetime = new();
    private bool retiring;
    private Task? disposal;
    private OriginalDeferredSourceFallbackShadowDevice(LoadedOriginalDeferredSourceFallbackShadowImages source,
        OriginalDeferredFallbackShadowConstruction construction,
        IReadOnlyDictionary<OriginalDeferredFallbackShadowKind, Rid> textures, bool allowReadback)
        => (Source, Construction, images, readbackAllowed) = (source, construction, textures, allowReadback);

    public static Task<OriginalDeferredSourceFallbackShadowDevice> BuildAsync(
        LoadedOriginalDeferredSourceFallbackShadowImages source,
        OriginalDeferredFallbackShadowConstruction explicitConstruction, bool allowReadback = false)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(explicitConstruction);
        explicitConstruction.Validate();
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var created = new Dictionary<OriginalDeferredFallbackShadowKind, Rid>();
            try
            {
                foreach (var kind in new[] { OriginalDeferredFallbackShadowKind.Point, OriginalDeferredFallbackShadowKind.Spot })
                {
                    var usage = RenderingDevice.TextureUsageBits.SamplingBit;
                    if (allowReadback) usage |= RenderingDevice.TextureUsageBits.CanCopyFromBit;
                    const RenderingDevice.DataFormat pixel = RenderingDevice.DataFormat.B8G8R8A8Unorm;
                    if (!rd.TextureIsFormatSupportedForUsage(pixel, usage))
                        throw new NotSupportedException("Device cannot represent the source BGRA8 fallback image.");
                    using var format = new RDTextureFormat
                    {
                        Width = 1, Height = 1, Depth = 1, ArrayLayers = source.ArrayLayers(kind), Mipmaps = 1,
                        Samples = RenderingDevice.TextureSamples.Samples1, Format = pixel,
                        TextureType = kind == OriginalDeferredFallbackShadowKind.Point
                            ? RenderingDevice.TextureType.CubeArray : RenderingDevice.TextureType.Type2DArray,
                        UsageBits = usage
                    };
                    using var view = new RDTextureView();
                    var layers = new Godot.Collections.Array<byte[]>();
                    for (uint layer = 0; layer < source.ArrayLayers(kind); layer++) layers.Add(source.LayerPixels(kind));
                    Rid texture = rd.TextureCreate(format, view, layers);
                    if (!texture.IsValid || !rd.TextureIsValid(texture))
                        throw new InvalidDataException("Device rejected the original white fallback shadow array.");
                    created.Add(kind, texture);
                }
                return new OriginalDeferredSourceFallbackShadowDevice(source, explicitConstruction, created, allowReadback);
            }
            catch { foreach (Rid texture in created.Values.Reverse()) rd.FreeRid(texture); throw; }
        });
    }

    public Task<OriginalDeferredDeviceBinding> BorrowAsync(OriginalDeferredFallbackShadowKind kind,
        DxbcRdefBinding selectedConsumer, OriginalDeferredFallbackShadowCurrentSelection explicitCurrent)
    {
        ArgumentNullException.ThrowIfNull(explicitCurrent);
        Source.ValidateConsumer(kind, selectedConsumer); explicitCurrent.Validate(Construction);
        lock (lifetime) ObjectDisposedException.ThrowIf(retiring, this);
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            lock (lifetime) ObjectDisposedException.ThrowIf(retiring, this);
            Rid texture = images[kind]; ValidateImage(rd, kind, texture);
            return new OriginalDeferredDeviceBinding("Texture", selectedConsumer.BindPoint, texture, texture,
                Source.ImageUri(kind, Construction));
        });
    }

    /// <summary>One explicit numeric readback for source byte verification.
    /// Ordinary production frames never request or read image pixels.</summary>
    public Task<IReadOnlyList<byte[]>> ReadLayersAsync(OriginalDeferredFallbackShadowKind kind)
    {
        if (!readbackAllowed) throw new InvalidOperationException("Numeric fallback readback requires an explicit diagnostic build.");
        lock (lifetime) ObjectDisposedException.ThrowIf(retiring, this);
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync<IReadOnlyList<byte[]>>(rd =>
        {
            lock (lifetime) ObjectDisposedException.ThrowIf(retiring, this);
            Rid texture = images[kind]; ValidateImage(rd, kind, texture);
            return Enumerable.Range(0, checked((int)Source.ArrayLayers(kind))).Select(layer => rd.TextureGetData(texture, (uint)layer)).ToArray();
        });
    }
    private void ValidateImage(RenderingDevice rd, OriginalDeferredFallbackShadowKind kind, Rid image)
    {
        if (!rd.TextureIsValid(image)) throw new InvalidDataException("Fallback shadow image retired before its borrowing draw.");
        using var actual = rd.TextureGetFormat(image);
        if (actual.Width != 1 || actual.Height != 1 || actual.Depth != 1 || actual.ArrayLayers != Source.ArrayLayers(kind) ||
            actual.Mipmaps != 1 || actual.Format != RenderingDevice.DataFormat.B8G8R8A8Unorm ||
            actual.TextureType != (kind == OriginalDeferredFallbackShadowKind.Point ? RenderingDevice.TextureType.CubeArray : RenderingDevice.TextureType.Type2DArray) ||
            !actual.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit))
            throw new InvalidDataException("Fallback shadow image differs from its original array SRV format/extent.");
    }
    public ValueTask DisposeAsync()
    {
        lock (lifetime)
        {
            retiring = true;
            if (disposal?.IsFaulted == true || disposal?.IsCanceled == true) disposal = null;
            return new(disposal ??= OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
            { foreach (Rid texture in images.Values.Reverse()) if (rd.TextureIsValid(texture)) rd.FreeRid(texture); return true; }));
        }
    }
}
