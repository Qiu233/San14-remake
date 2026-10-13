using Godot;
using San14.Data;

public sealed record ModelDeviceDrawArrays(int DrawIndex, int LayoutIndex, long VertexFormat,
    Rid Vertices, Rid Indices, int IndexCount);

/// <summary>Reusable original G1M vertex/index uploads. Full source buffers
/// remain unchanged, indices keep their absolute vertex values, and each
/// original draw window gets its own index array. Caller owns shader selection.</summary>
public sealed class ModelGeometryDevice : IAsyncDisposable
{
    public string SourceResourceUri { get; }
    public ModelDeviceGeometryPacket Packet { get; }
    public IReadOnlyDictionary<int, ModelDeviceDrawArrays> Draws { get; }
    private readonly IReadOnlyDictionary<int, Rid> _vertexBuffers, _indexBuffers;
    private readonly IReadOnlyList<Rid> _owned;
    private Task? _disposal;

    private ModelGeometryDevice(string uri, ModelDeviceGeometryPacket packet,
        Dictionary<int, ModelDeviceDrawArrays> draws, Dictionary<int, Rid> vertices,
        Dictionary<int, Rid> indices, List<Rid> owned)
        => (SourceResourceUri, Packet, Draws, _vertexBuffers, _indexBuffers, _owned) =
            (uri, packet, new System.Collections.ObjectModel.ReadOnlyDictionary<int, ModelDeviceDrawArrays>(draws),
                vertices, indices, owned.AsReadOnly());

    public static Task<ModelGeometryDevice> BuildAsync(string sourceResourceUri,
        ModelDeviceGeometryPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        _ = GameResourceId.Parse(sourceResourceUri);
        if (packet.Draws.Count == 0 || packet.Draws.Any(d => d.VertexCount == 0 || d.IndexCount == 0))
            throw new InvalidDataException("Original model device requires nonempty indexed draws.");
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var owned = new List<Rid>(); var vertices = new Dictionary<int, Rid>(); var indices = new Dictionary<int, Rid>();
            Rid Own(Rid value)
            {
                if (!value.IsValid) throw new InvalidDataException("Original model geometry device allocation failed.");
                owned.Add(value); return value;
            }
            try
            {
                foreach (var buffer in packet.VertexBuffers)
                    vertices.Add(buffer.Index, Own(rd.VertexBufferCreate((uint)buffer.ByteLength, buffer.CopyBytes())));
                foreach (var buffer in packet.IndexBuffers)
                    indices.Add(buffer.Index, Own(rd.IndexBufferCreate((uint)buffer.IndexCount,
                        buffer.BitsPerIndex == 16 ? RenderingDevice.IndexBufferFormat.Uint16 :
                            throw new InvalidDataException("Unsupported original model index width."), buffer.CopyBytes())));
                var layouts = new Dictionary<int, (long Format, Rid Vertices)>();
                foreach (var layout in packet.Layouts.Where(l => packet.Draws.Any(d => d.LayoutIndex == l.Index)))
                {
                    var attributes = new Godot.Collections.Array<RDVertexAttribute>();
                    try
                    {
                        foreach (var item in layout.Attributes)
                            attributes.Add(new()
                            {
                                Location = item.Location, Binding = item.StreamBinding, Offset = (uint)item.ByteOffset,
                                Stride = (uint)item.Stride, Format = DeviceFormat(item.Format),
                                Frequency = item.ExplicitSingleInstanceFrequency ? RenderingDevice.VertexFrequency.Instance : RenderingDevice.VertexFrequency.Vertex
                            });
                        long format = rd.VertexFormatCreate(attributes);
                        if (format < 0) throw new InvalidDataException("Device rejected the original model vertex format.");
                        var buffers = new Godot.Collections.Array<Rid>(layout.VertexArrayBufferReferences.Select(i => vertices[i]));
                        Rid array = Own(rd.VertexArrayCreate((uint)layout.VertexCount, format, buffers));
                        layouts.Add(layout.Index, (format, array));
                    }
                    finally { foreach (var item in attributes) item.Dispose(); }
                }
                var draws = new Dictionary<int, ModelDeviceDrawArrays>();
                foreach (var draw in packet.Draws)
                {
                    Rid array = Own(rd.IndexArrayCreate(indices[draw.IndexBufferIndex], (uint)draw.PackedFirstIndex, (uint)draw.IndexCount));
                    var layout = layouts[draw.LayoutIndex];
                    draws.Add(draw.Index, new(draw.Index, draw.LayoutIndex, layout.Format, layout.Vertices, array, draw.IndexCount));
                }
                return new ModelGeometryDevice(sourceResourceUri, packet, draws, vertices, indices, owned);
            }
            catch { for (int i = owned.Count - 1; i >= 0; i--) rd.FreeRid(owned[i]); throw; }
        });
    }

    public static RenderingDevice.DataFormat DeviceFormat(ModelDeviceVertexFormat format) => format switch
    {
        ModelDeviceVertexFormat.Float32x1 => RenderingDevice.DataFormat.R32Sfloat,
        ModelDeviceVertexFormat.Float32x2 => RenderingDevice.DataFormat.R32G32Sfloat,
        ModelDeviceVertexFormat.Float32x3 => RenderingDevice.DataFormat.R32G32B32Sfloat,
        ModelDeviceVertexFormat.Float32x4 => RenderingDevice.DataFormat.R32G32B32A32Sfloat,
        ModelDeviceVertexFormat.UInt8x4 => RenderingDevice.DataFormat.R8G8B8A8Uint,
        ModelDeviceVertexFormat.SInt16x4 => RenderingDevice.DataFormat.R16G16B16A16Sint,
        ModelDeviceVertexFormat.Float16x2 => RenderingDevice.DataFormat.R16G16Sfloat,
        ModelDeviceVertexFormat.Float16x4 => RenderingDevice.DataFormat.R16G16B16A16Sfloat,
        ModelDeviceVertexFormat.UNorm8x4 => RenderingDevice.DataFormat.R8G8B8A8Unorm,
        _ => throw new InvalidDataException("Unknown original model device numeric format.")
    };

    /// <summary>Explicit own-device diagnostics; no normal-frame readback.</summary>
    public Task<byte[]> ReadVertexBufferAsync(int sourceBufferIndex) => ReadAsync(_vertexBuffers, sourceBufferIndex);
    public Task<byte[]> ReadIndexBufferAsync(int sourceBufferIndex) => ReadAsync(_indexBuffers, sourceBufferIndex);
    private Task<byte[]> ReadAsync(IReadOnlyDictionary<int, Rid> buffers, int index)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null, this);
        if (!buffers.TryGetValue(index, out Rid buffer)) throw new InvalidDataException("Original model buffer index is absent.");
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        { ObjectDisposedException.ThrowIf(_disposal is not null, this); return rd.BufferGetData(buffer); });
    }

    public ValueTask DisposeAsync() => new(_disposal ??= TerrainDeviceTexture.OnRenderThreadAsync(rd =>
    { for (int i = _owned.Count - 1; i >= 0; i--) rd.FreeRid(_owned[i]); return true; }));

    internal void RequireLive() => ObjectDisposedException.ThrowIf(_disposal is not null, this);
}
