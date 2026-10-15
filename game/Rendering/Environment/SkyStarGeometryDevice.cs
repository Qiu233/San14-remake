using Godot;
using San14.Data;

/// <summary>One uploaded controller point stream shared by a producer's six
/// draws. Retire all borrowing draws before releasing its array and buffer.</summary>
public sealed class SkyStarGeometryDevice : IAsyncDisposable
{
    public SkyStarGeometry Source { get; }
    internal long VertexFormat { get; private set; }
    internal Rid VertexArray => vertexArray;
    private Rid vertexBuffer, vertexArray;
    private readonly object lifetimeGate = new();
    private volatile bool retiring;
    private Task? disposal;

    private SkyStarGeometryDevice(SkyStarGeometry source) => Source = source;

    public static Task<SkyStarGeometryDevice> BuildAsync(SkyStarGeometry source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.PointCount == 0)
            throw new InvalidDataException("Star device geometry requires a nonempty controller point stream.");
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var geometry = new SkyStarGeometryDevice(source);
            try
            {
                geometry.vertexBuffer = rd.VertexBufferCreate((uint)source.ByteLength, source.CopyVertexBytes());
                if (!geometry.vertexBuffer.IsValid) throw new InvalidDataException("Star vertex buffer allocation failed.");
                using var position = new RDVertexAttribute { Location = 0, Binding = 0, Offset = 0, Stride = 20,
                    Format = RenderingDevice.DataFormat.R32G32B32Sfloat, Frequency = RenderingDevice.VertexFrequency.Instance };
                using var size = new RDVertexAttribute { Location = 1, Binding = 0, Offset = 12, Stride = 20,
                    Format = RenderingDevice.DataFormat.R32Sfloat, Frequency = RenderingDevice.VertexFrequency.Instance };
                using var color = new RDVertexAttribute { Location = 2, Binding = 0, Offset = 16, Stride = 20,
                    Format = RenderingDevice.DataFormat.R8G8B8A8Unorm, Frequency = RenderingDevice.VertexFrequency.Instance };
                long format = rd.VertexFormatCreate([position, size, color]);
                geometry.VertexFormat = format;
                geometry.vertexArray = rd.VertexArrayCreate(4, format, [geometry.vertexBuffer]);
                if (!geometry.vertexArray.IsValid) throw new InvalidDataException("Star vertex array allocation failed.");
                return geometry;
            }
            catch (Exception constructionFailure)
            {
                try { geometry.Release(rd); }
                catch (Exception retirementFailure)
                {
                    var partial = new NativeMapBorrowerRetirementPackage("Star-geometry-partial/" + Guid.NewGuid().ToString("N"), geometry);
                    partial.RetainFailure();
                    throw new NativeMapBorrowerConstructionException(constructionFailure, retirementFailure, partial);
                }
                throw;
            }
        });
    }

    internal void RequireAvailable() => ObjectDisposedException.ThrowIf(retiring, this);

    private void Release(RenderingDevice rd)
    {
        if (vertexArray.IsValid) { rd.FreeRid(vertexArray); vertexArray = default; }
        if (vertexBuffer.IsValid) { rd.FreeRid(vertexBuffer); vertexBuffer = default; }
    }

    public ValueTask DisposeAsync()
    {
        lock (lifetimeGate)
        {
            retiring = true;
            if (disposal is null || disposal.IsFaulted)
                disposal = TerrainDeviceTexture.OnRenderThreadAsync(rd =>
                {
                    Release(rd);
                    return true;
                });
            return new(disposal);
        }
    }
}
