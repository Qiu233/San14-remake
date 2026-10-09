using Godot;
using San14.Data;

/// <summary>Uploads the explicitly supplied original half4 and float typed views.
/// No sample distribution, active sample count or weight normalization is inferred.</summary>
public sealed class SkyFilterSampleBuffers : IAsyncDisposable
{
    public SkyFilterSamplePacket Source { get; }
    public Rid Directions { get; }
    public Rid Weights { get; }
    private Task? _disposal;

    private SkyFilterSampleBuffers(SkyFilterSamplePacket source, Rid directions, Rid weights)
        => (Source,Directions,Weights)=(source,directions,weights);

    public static Task<SkyFilterSampleBuffers> BuildAsync(SkyFilterSamplePacket source)
    {
        ArgumentNullException.ThrowIfNull(source);
        // Snapshot again before dispatch; a caller must not change device bytes
        // through retained memory while the render-thread upload is pending.
        var packet=new SkyFilterSamplePacket(source.DestinationMip,source.DirectionBytes.Span,source.WeightBytes.Span,source.WeightSum);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            Rid directions=default,weights=default;
            try
            {
                directions=rd.TextureBufferCreate((uint)packet.SampleCount,RenderingDevice.DataFormat.R16G16B16A16Sfloat,packet.DirectionBytes.ToArray());
                if (!directions.IsValid)throw new InvalidDataException("Device rejected original half4 sky direction buffer.");
                weights=rd.TextureBufferCreate((uint)packet.SampleCount,RenderingDevice.DataFormat.R32Sfloat,packet.WeightBytes.ToArray());
                if (!weights.IsValid)throw new InvalidDataException("Device rejected original float sky weight buffer.");
                return new SkyFilterSampleBuffers(packet,directions,weights);
            }
            catch
            {
                if(weights.IsValid)rd.FreeRid(weights);
                if(directions.IsValid)rd.FreeRid(directions);
                throw;
            }
        });
    }

    public ValueTask DisposeAsync() => new(_disposal ??= TerrainDeviceTexture.OnRenderThreadAsync(rd =>
    { rd.FreeRid(Weights);rd.FreeRid(Directions);return true; }));
}
