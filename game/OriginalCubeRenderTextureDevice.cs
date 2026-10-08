using Godot;
using San14.Data;

public sealed record OriginalCubeRenderDeviceSurface(uint SurfaceId, OriginalCubeRenderSurface Source,
    Rid TargetView, Rid Framebuffer);

/// <summary>Allocates the original one-cube/six-face/mip target layout.
/// No contents, sky producer, light selection or sampler are invented.</summary>
public sealed class OriginalCubeRenderTextureDevice : IAsyncDisposable
{
    public OriginalCubeRenderTextureDefinition Source { get; }
    public Rid TextureRid { get; }
    public IReadOnlyList<OriginalCubeRenderDeviceSurface> Surfaces { get; }
    private readonly IReadOnlyList<Rid> _owned;
    private Task? _disposal;

    private OriginalCubeRenderTextureDevice(OriginalCubeRenderTextureDefinition source, Rid texture,
        List<OriginalCubeRenderDeviceSurface> surfaces, List<Rid> owned)
        => (Source, TextureRid, Surfaces, _owned) = (source, texture, surfaces.AsReadOnly(), owned.AsReadOnly());

    public static Task<OriginalCubeRenderTextureDevice> BuildAsync(OriginalCubeRenderTextureDefinition source, bool allowReadback = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        _ = OriginalCubeRenderTextureResources.ResolveSurface(source, 0);
        OriginalRenderTextureFormats formats = OriginalRenderTextureFormatMath.Resolve(source.PixelFormat, source.ColorSpace, source.OutputColorSpace);
        // This finite adapter matches the five original SkyAmbient CubeRTs.
        // Other native formats require explicit shader/target view adaptation.
        if (formats != new OriginalRenderTextureFormats(26, 26, 26) || source.MinMipLevel != 0)
            throw new InvalidDataException("Cube device adapter requires the verified linear R11G11B10 single-cube recipe.");
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var owned = new List<Rid>();
            var surfaces = new List<OriginalCubeRenderDeviceSurface>();
            Rid Own(Rid rid)
            {
                if (!rid.IsValid) throw new InvalidDataException("Device rejected an original cube target or view.");
                owned.Add(rid); return rid;
            }
            try
            {
                var usage = RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.ColorAttachmentBit;
                if (allowReadback) usage |= RenderingDevice.TextureUsageBits.CanCopyFromBit;
                const RenderingDevice.DataFormat pixelFormat = RenderingDevice.DataFormat.B10G11R11UfloatPack32;
                if (!rd.TextureIsFormatSupportedForUsage(pixelFormat, usage))
                    throw new NotSupportedException("Device does not support the exact original cube target format/usage.");
                using var format = new RDTextureFormat { Format = pixelFormat, Width = (uint)source.Width,
                    Height = (uint)source.Width, Depth = 1, ArrayLayers = 6, Mipmaps = source.LevelNum,
                    TextureType = RenderingDevice.TextureType.Cube, Samples = RenderingDevice.TextureSamples.Samples1,
                    UsageBits = usage };
                using var view = new RDTextureView();
                Rid texture = Own(rd.TextureCreate(format, view));
                for (uint id = 0; id < 6u * source.LevelNum; id++)
                {
                    OriginalCubeRenderSurface surface = OriginalCubeRenderTextureResources.ResolveSurface(source, id);
                    Rid target = Own(rd.TextureCreateSharedFromSlice(view, texture, surface.Face,
                        surface.MipLevel, 1, RenderingDevice.TextureSliceType.Slice2D));
                    Rid framebuffer = Own(rd.FramebufferCreate([target]));
                    surfaces.Add(new(id, surface, target, framebuffer));
                }
                return new OriginalCubeRenderTextureDevice(source, texture, surfaces, owned);
            }
            catch
            {
                for (int i = owned.Count - 1; i >= 0; i--) rd.FreeRid(owned[i]);
                throw;
            }
        });
    }

    public ValueTask DisposeAsync() => new(_disposal ??= OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
    {
        for (int i = _owned.Count - 1; i >= 0; i--) rd.FreeRid(_owned[i]);
        return true;
    }));
}
