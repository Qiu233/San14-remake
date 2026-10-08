using Godot;
using San14.Data;

public sealed record OriginalModelDeviceDrawArrays(int DrawIndex, int LayoutIndex, long VertexFormat,
    Rid Vertices, Rid Indices, int IndexCount);

/// <summary>Reusable original G1M vertex/index uploads. Full source buffers
/// remain unchanged, indices keep their absolute vertex values, and each
/// original draw window gets its own index array. Caller owns shader selection.</summary>
public sealed class OriginalModelGeometryDevice : IAsyncDisposable
{
    public string SourceResourceUri { get; }
    public OriginalModelDeviceGeometryPacket Packet { get; }
    public IReadOnlyDictionary<int, OriginalModelDeviceDrawArrays> Draws { get; }
    private readonly IReadOnlyDictionary<int, Rid> _vertexBuffers, _indexBuffers;
    private readonly IReadOnlyList<Rid> _owned;
    private Task? _disposal;

    private OriginalModelGeometryDevice(string uri, OriginalModelDeviceGeometryPacket packet,
        Dictionary<int, OriginalModelDeviceDrawArrays> draws, Dictionary<int, Rid> vertices,
        Dictionary<int, Rid> indices, List<Rid> owned)
        => (SourceResourceUri, Packet, Draws, _vertexBuffers, _indexBuffers, _owned) =
            (uri, packet, new System.Collections.ObjectModel.ReadOnlyDictionary<int, OriginalModelDeviceDrawArrays>(draws),
                vertices, indices, owned.AsReadOnly());

    public static Task<OriginalModelGeometryDevice> BuildAsync(string sourceResourceUri,
        OriginalModelDeviceGeometryPacket packet)
        => BuildAsync(sourceResourceUri, packet, false);

    // Early19 can select fewer attributes than the original layout declares.
    // Retain every source buffer in the upload/packet; only the vertex-array
    // bindings omit trailing streams with no selected device attribute.
    internal static Task<OriginalModelGeometryDevice> BuildSelectedInputStreamsAsync(string sourceResourceUri,
        OriginalModelDeviceGeometryPacket packet) => BuildAsync(sourceResourceUri, packet, true);

    private static Task<OriginalModelGeometryDevice> BuildAsync(string sourceResourceUri,
        OriginalModelDeviceGeometryPacket packet, bool selectedInputStreams)
    {
        ArgumentNullException.ThrowIfNull(packet);
        _ = GameResourceId.Parse(sourceResourceUri);
        if (packet.Draws.Count == 0 || packet.Draws.Any(d => d.VertexCount == 0 || d.IndexCount == 0))
            throw new InvalidDataException("Original model device requires nonempty indexed draws.");
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
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
                        var references = selectedInputStreams
                            ? layout.BufferReferences.Take(checked((int)layout.Attributes.Max(a=>a.StreamBinding)+1)).ToArray()
                            : FlagActiveBufferPrefix(sourceResourceUri,packet,layout);
                        var buffers = new Godot.Collections.Array<Rid>(references.Select(i => vertices[i]));
                        Rid array = Own(rd.VertexArrayCreate((uint)layout.VertexCount, format, buffers));
                        layouts.Add(layout.Index, (format, array));
                    }
                    finally { foreach (var item in attributes) item.Dispose(); }
                }
                var draws = new Dictionary<int, OriginalModelDeviceDrawArrays>();
                foreach (var draw in packet.Draws)
                {
                    Rid array = Own(rd.IndexArrayCreate(indices[draw.IndexBufferIndex], (uint)draw.PackedFirstIndex, (uint)draw.IndexCount));
                    var layout = layouts[draw.LayoutIndex];
                    draws.Add(draw.Index, new(draw.Index, draw.LayoutIndex, layout.Format, layout.Vertices, array, draw.IndexCount));
                }
                return new OriginalModelGeometryDevice(sourceResourceUri, packet, draws, vertices, indices, owned);
            }
            catch { for (int i = owned.Count - 1; i >= 0; i--) rd.FreeRid(owned[i]); throw; }
        });
    }

    /// <summary>The exact A72 flag input inventory has six primary-stream
    /// attributes. Its original one-record affine stream is still uploaded and
    /// retained in packet metadata, but has no device attribute. Passing that
    /// inactive trailing RID to RD VertexArrayCreate is unsafe on this backend.</summary>
    private static IReadOnlyList<int> FlagActiveBufferPrefix(string uri,OriginalModelDeviceGeometryPacket packet,
        OriginalModelDeviceLayout layout)
    {
        bool banner=uri==GameResourceId.Rdb("CharacterEditor",0x9EEA5B67).ToString();
        bool flag=uri==GameResourceId.Rdb("CharacterEditor",0x80C82506).ToString();
        if(!banner&&!flag||layout.Attributes.Count!=6)return layout.BufferReferences;
        string expected=banner?"8A7DB206664F168366DEFA91B184F32383AA6FE1D9DE8BD7BF3BFC94B9E354DF":
            "7714F0AACB8C5433AAB1333E285F9C43233418E8E4169A9DED0C55CC99E42874";
        int layouts=banner?3:2,affine=banner?3:2;
        ushort[] semantics=[0,1,2,3,5,6];uint[] locations=[0,1,3,5,6,7];int[] offsets=[0,12,24,28,40,48];
        OriginalModelDeviceVertexFormat[] formats=[OriginalModelDeviceVertexFormat.Float32x3,OriginalModelDeviceVertexFormat.Float32x3,
            OriginalModelDeviceVertexFormat.UInt8x4,OriginalModelDeviceVertexFormat.Float32x3,
            OriginalModelDeviceVertexFormat.Float32x2,OriginalModelDeviceVertexFormat.Float32x4];
        var attributes=layout.Attributes.OrderBy(a=>a.Location).ToArray();
        if(packet.SourceSha256!=expected||packet.Layouts.Count!=layouts||packet.VertexBuffers.Count!=layouts+1||
            layout.Index<0||layout.Index>=layouts||!layout.BufferReferences.SequenceEqual(new[]{layout.Index,affine})||
            attributes.Where((a,i)=>a.SourceSemanticCode!=semantics[i]||a.Location!=locations[i]||a.ByteOffset!=offsets[i]||
                a.Format!=formats[i]||a.StreamBinding!=0||a.SourceBufferIndex!=layout.Index||a.Stride!=64||a.ExplicitSingleInstanceFrequency).Any())
            throw new InvalidDataException("Source A72 flag inactive-stream transport differs from its exact URI/hash/layout/attribute profile.");
        var primary=packet.VertexBuffers.Single(b=>b.Index==layout.Index);
        var trailing=packet.VertexBuffers.Single(b=>b.Index==affine);
        if(primary.Stride!=64||primary.VertexCount!=layout.VertexCount||primary.ByteLength!=primary.VertexCount*64||
            trailing.Stride!=48||trailing.VertexCount!=1||trailing.ByteLength!=48||
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(trailing.CopyBytes()))!=
                "31CFE40CBF2CFF090F2776A574A157FF4DC1119954F6BE87CF98C285B067EE98")
            throw new InvalidDataException("Source A72 flag trailing unused reference is not its complete original one-record affine buffer.");
        return new[]{layout.BufferReferences[0]};
    }

    public static RenderingDevice.DataFormat DeviceFormat(OriginalModelDeviceVertexFormat format) => format switch
    {
        OriginalModelDeviceVertexFormat.Float32x1 => RenderingDevice.DataFormat.R32Sfloat,
        OriginalModelDeviceVertexFormat.Float32x2 => RenderingDevice.DataFormat.R32G32Sfloat,
        OriginalModelDeviceVertexFormat.Float32x3 => RenderingDevice.DataFormat.R32G32B32Sfloat,
        OriginalModelDeviceVertexFormat.Float32x4 => RenderingDevice.DataFormat.R32G32B32A32Sfloat,
        OriginalModelDeviceVertexFormat.UInt8x4 => RenderingDevice.DataFormat.R8G8B8A8Uint,
        OriginalModelDeviceVertexFormat.SInt16x4 => RenderingDevice.DataFormat.R16G16B16A16Sint,
        OriginalModelDeviceVertexFormat.Float16x2 => RenderingDevice.DataFormat.R16G16Sfloat,
        OriginalModelDeviceVertexFormat.Float16x4 => RenderingDevice.DataFormat.R16G16B16A16Sfloat,
        OriginalModelDeviceVertexFormat.UNorm8x4 => RenderingDevice.DataFormat.R8G8B8A8Unorm,
        _ => throw new InvalidDataException("Unknown original model device numeric format.")
    };

    /// <summary>Explicit own-device diagnostics; no normal-frame readback.</summary>
    public Task<byte[]> ReadVertexBufferAsync(int sourceBufferIndex) => ReadAsync(_vertexBuffers, sourceBufferIndex);
    public Task<byte[]> ReadIndexBufferAsync(int sourceBufferIndex) => ReadAsync(_indexBuffers, sourceBufferIndex);
    private Task<byte[]> ReadAsync(IReadOnlyDictionary<int, Rid> buffers, int index)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null, this);
        if (!buffers.TryGetValue(index, out Rid buffer)) throw new InvalidDataException("Original model buffer index is absent.");
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
        { ObjectDisposedException.ThrowIf(_disposal is not null, this); return rd.BufferGetData(buffer); });
    }

    public ValueTask DisposeAsync() => new(_disposal ??= OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
    { for (int i = _owned.Count - 1; i >= 0; i--) rd.FreeRid(_owned[i]); return true; }));

    internal void RequireLive() => ObjectDisposedException.ThrowIf(_disposal is not null, this);
}
