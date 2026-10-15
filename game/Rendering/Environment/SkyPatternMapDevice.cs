using Godot;
using San14.Data;

/// <summary>Own exact source R8 SkyPatternMap target. Allocation supplies no
/// source contents, sky constants or implicit sampler.</summary>
public sealed class SkyPatternMapDevice : IAsyncDisposable
{
    public RenderTextureDefinition Source { get; }
    public Rid TextureRid { get; }
    public Rid Framebuffer { get; }
    private readonly RenderDeviceResources resources;

    private SkyPatternMapDevice(RenderTextureDefinition source, Rid texture, Rid framebuffer, RenderDeviceResources resources)
        => (Source, TextureRid, Framebuffer, this.resources) = (source, texture, framebuffer, resources);

    public static Task<SkyPatternMapDevice> BuildAsync(RenderTextureDefinition source, bool allowReadback = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Kind != RenderTextureKind.Texture2D || source.Width <= 0 || source.Height <= 0 ||
            source.LevelNum != 1 || source.MinMipLevel != 0 || source.MultiSample != 0 ||
            RenderTextureFormatMath.Resolve(source) != new RenderTextureFormats(61,61,61))
            throw new InvalidDataException("SkyPatternMap adapter requires the exact finite single-mip/sample R8 source recipe.");
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var resources = new RenderDeviceResources("Device rejected original SkyPatternMap allocation.");
            try
            {
                var usage = RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.ColorAttachmentBit;
                if (allowReadback) usage |= RenderingDevice.TextureUsageBits.CanCopyFromBit;
                if (!rd.TextureIsFormatSupportedForUsage(RenderingDevice.DataFormat.R8Unorm, usage))
                    throw new NotSupportedException("Device does not support the exact original R8 target and sampling usage.");
                using var format = new RDTextureFormat { Format = RenderingDevice.DataFormat.R8Unorm,
                    Width = (uint)source.Width, Height = (uint)source.Height, Depth = 1, ArrayLayers = 1, Mipmaps = 1,
                    TextureType = RenderingDevice.TextureType.Type2D, Samples = RenderingDevice.TextureSamples.Samples1,
                    UsageBits = usage };
                using var view = new RDTextureView();
                Rid texture = rd.TextureCreate(format,view);
                if (!texture.IsValid) throw new InvalidDataException("Device rejected original SkyPatternMap texture.");
                resources.Own(texture);
                Rid framebuffer = rd.FramebufferCreate([texture]);
                if (!framebuffer.IsValid)
                    throw new InvalidDataException("Device rejected original SkyPatternMap framebuffer.");
                resources.Own(framebuffer);
                if (!rd.FramebufferIsValid(framebuffer))
                    throw new InvalidDataException("Device rejected original SkyPatternMap framebuffer.");
                return new SkyPatternMapDevice(source,texture,framebuffer,resources);
            }
            catch (Exception failure)
            {
                resources.ReleaseAfterConstructionFailure(rd, failure, "Sky-pattern-map-partial");
                throw;
            }
        });
    }

    public ValueTask DisposeAsync() => resources.DisposeAsync();
}
