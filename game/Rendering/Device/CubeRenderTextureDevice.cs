using Godot;
using San14.Data;

public sealed record CubeRenderDeviceSurface(uint SurfaceId, CubeRenderSurface Source,
    Rid TargetView, Rid Framebuffer);

/// <summary>Allocates the original one-cube/six-face/mip target layout.
/// No contents, sky producer, light selection or sampler are invented.</summary>
public sealed class CubeRenderTextureDevice : IAsyncDisposable
{
    public CubeRenderTextureDefinition Source { get; }
    public Rid TextureRid { get; }
    public IReadOnlyList<CubeRenderDeviceSurface> Surfaces { get; }
    private readonly RenderDeviceResources resources;

    private CubeRenderTextureDevice(CubeRenderTextureDefinition source, Rid texture,
        List<CubeRenderDeviceSurface> surfaces, RenderDeviceResources resources)
        => (Source, TextureRid, Surfaces, this.resources) = (source, texture, surfaces.AsReadOnly(), resources);

    public static Task<CubeRenderTextureDevice> BuildAsync(CubeRenderTextureDefinition source, bool allowReadback = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        _ = CubeRenderTextureResources.ResolveSurface(source, 0);
        RenderTextureFormats formats = RenderTextureFormatMath.Resolve(source.PixelFormat, source.ColorSpace, source.OutputColorSpace);
        // This finite adapter matches the five original SkyAmbient CubeRTs.
        // Other native formats require explicit shader/target view adaptation.
        if (formats != new RenderTextureFormats(26, 26, 26) || source.MinMipLevel != 0)
            throw new InvalidDataException("Cube device adapter requires the verified linear R11G11B10 single-cube recipe.");
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var resources = new RenderDeviceResources("Device rejected an original cube target or view.");
            var surfaces = new List<CubeRenderDeviceSurface>();
            Rid Own(Rid rid) => resources.Own(rid);
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
                    CubeRenderSurface surface = CubeRenderTextureResources.ResolveSurface(source, id);
                    Rid target = Own(rd.TextureCreateSharedFromSlice(view, texture, surface.Face,
                        surface.MipLevel, 1, RenderingDevice.TextureSliceType.Slice2D));
                    Rid framebuffer = Own(rd.FramebufferCreate([target]));
                    surfaces.Add(new(id, surface, target, framebuffer));
                }
                return new CubeRenderTextureDevice(source, texture, surfaces, resources);
            }
            catch (Exception failure)
            {
                resources.ReleaseAfterConstructionFailure(rd, failure, "Cube-render-texture-partial");
                throw;
            }
        });
    }

    public ValueTask DisposeAsync() => resources.DisposeAsync();
}
