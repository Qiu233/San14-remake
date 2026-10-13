using System.Collections.Frozen;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The caller supplies the source semantic code and the translated
/// vertex location. Numeric source semantic codes are not assigned names here.</summary>
public sealed record ModelVertexSemanticBinding(int LayoutIndex, ushort SourceSemanticCode, uint Location);

/// <summary>Device input layout value shared by geometry caches. Stream frequency
/// is part of the layout; equivalent bindings share an upload across programs.</summary>
public sealed class ModelGeometryInputLayout : IEquatable<ModelGeometryInputLayout>
{
    private readonly ModelVertexSemanticBinding[] bindings;
    private readonly int[] streams;
    public IReadOnlyList<ModelVertexSemanticBinding> Bindings { get; }
    public IReadOnlySet<int> InstanceStreams { get; }

    public ModelGeometryInputLayout(IEnumerable<ModelVertexSemanticBinding> bindings, IReadOnlySet<int>? instanceStreams)
    {
        this.bindings = bindings.OrderBy(binding => binding.LayoutIndex).ThenBy(binding => binding.Location).ToArray();
        streams = instanceStreams?.Order().ToArray() ?? [];
        Bindings = Array.AsReadOnly(this.bindings);
        InstanceStreams = streams.ToFrozenSet();
    }

    public bool Equals(ModelGeometryInputLayout? other) => other is not null &&
        bindings.SequenceEqual(other.bindings) && streams.SequenceEqual(other.streams);
    public override bool Equals(object? other) => other is ModelGeometryInputLayout layout && Equals(layout);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var binding in bindings) hash.Add(binding);
        foreach (int stream in streams) hash.Add(stream);
        return hash.ToHashCode();
    }
}

public enum ModelDeviceVertexFormat
{
    Float32x2, Float32x3, Float32x4, UInt8x4, Float16x2, Float16x4, UNorm8x4,
    Float32x1, SInt16x4
}

public sealed record ModelDeviceAttribute(int SourceAttributeIndex, ushort SourceSemanticCode,
    uint Location, int SourceBufferIndex, uint StreamBinding, int ByteOffset, int Stride,
    ModelDeviceVertexFormat Format, int ComponentCount, int ByteWidth, uint RequiredShaderComponentType)
{
    public bool ExplicitSingleInstanceFrequency { get; init; }
}

public sealed class ModelDeviceVertexBuffer
{
    private readonly byte[] _bytes;
    public int Index { get; }
    public int Stride { get; }
    public int VertexCount { get; }
    public int ByteLength => _bytes.Length;
    public byte[] CopyBytes() => _bytes.ToArray();
    internal ModelDeviceVertexBuffer(G1VertexBuffer source)
    {
        (Index, Stride, VertexCount) = (source.Index, source.Stride, source.VertexCount);
        _bytes = source.Data.ToArray();
    }
}

public sealed class ModelDeviceIndexBuffer
{
    private readonly byte[] _bytes;
    public int Index { get; }
    public int IndexCount { get; }
    public int BitsPerIndex { get; }
    public int ByteLength => _bytes.Length;
    public byte[] CopyBytes() => _bytes.ToArray();
    internal ModelDeviceIndexBuffer(G1IndexBuffer source)
    {
        (Index, IndexCount, BitsPerIndex) = (source.Index, source.IndexCount, source.BitsPerIndex);
        _bytes = source.Data.ToArray();
    }
}

public sealed class ModelDeviceLayout
{
    public int Index { get; }
    public int VertexCount { get; }
    /// <summary>Dense stream binding to original vertex-buffer index. Every
    /// declared stream is retained, including streams not selected by a shader.</summary>
    public IReadOnlyList<int> BufferReferences { get; }
    /// <summary>Positional stream bindings through the highest selected
    /// attribute. Gaps keep their source slots; inactive trailing streams
    /// remain in the upload packet but are not vertex-array inputs.</summary>
    public IReadOnlyList<int> VertexArrayBufferReferences { get; }
    public IReadOnlyList<ModelDeviceAttribute> Attributes { get; }
    internal ModelDeviceLayout(int index, int vertexCount, IEnumerable<int> buffers,
        IEnumerable<ModelDeviceAttribute> attributes)
    {
        (Index, VertexCount) = (index, vertexCount);
        BufferReferences = Array.AsReadOnly(buffers.ToArray());
        Attributes = Array.AsReadOnly(attributes.ToArray());
        int count = Attributes.Count == 0 ? 0 : checked((int)Attributes.Max(a => a.StreamBinding) + 1);
        if (count > BufferReferences.Count)
            throw new InvalidDataException("Selected vertex attribute exceeds its source stream bindings.");
        VertexArrayBufferReferences = Array.AsReadOnly(BufferReferences.Take(count).ToArray());
    }
}

public sealed class ModelDeviceDraw
{
    private readonly byte[] _rawBytes;
    public int Index { get; }
    public int LayoutIndex { get; }
    public int IndexBufferIndex { get; }
    public int VertexStart { get; }
    public int VertexCount { get; }
    public int SourceIndexStart { get; }
    public int IndexCount { get; }
    /// <summary>IndexArrayCreate offset when uploading the complete original
    /// index buffer. Original indices remain absolute; no vertex rebasing occurs.</summary>
    public int PackedFirstIndex => SourceIndexStart;
    public uint PrimitiveCode { get; }
    public uint Flags { get; }
    public uint RawIndex12 { get; }
    public uint RawIndex20 { get; }
    public uint RawIndex24 { get; }
    public byte[] CopyRawBytes() => _rawBytes.ToArray();
    internal ModelDeviceDraw(G1DrawRecord source)
    {
        (Index, LayoutIndex, IndexBufferIndex, VertexStart, VertexCount, SourceIndexStart,
            IndexCount, PrimitiveCode, Flags, RawIndex12, RawIndex20, RawIndex24) =
            (source.Index, source.LayoutIndex, source.IndexBufferIndex, source.VertexStart,
                source.VertexCount, source.IndexStart, source.IndexCount, source.PrimitiveCode,
                source.Flags, source.RawIndex12, source.RawIndex20, source.RawIndex24);
        _rawBytes = source.RawBytes.ToArray();
    }
}

/// <summary>Immutable CPU upload packet. Copies retain complete source vertex
/// and index bytes, and the complete decoded model retains all uninterpreted
/// headers, padding, groups and sections. This packet does not select a shader,
/// material, coordinate transform, culling, depth or blend state.</summary>
public sealed class ModelDeviceGeometryPacket
{
    private readonly byte[] _sourceBytes;
    public string SourceSha256 { get; }
    public int SourceByteLength => _sourceBytes.Length;
    public IReadOnlyList<ModelDeviceVertexBuffer> VertexBuffers { get; }
    public IReadOnlyList<ModelDeviceIndexBuffer> IndexBuffers { get; }
    public IReadOnlyList<ModelDeviceLayout> Layouts { get; }
    public IReadOnlyList<ModelDeviceDraw> Draws { get; }
    public byte[] CopySourceBytes() => _sourceBytes.ToArray();
    internal ModelDeviceGeometryPacket(byte[] source, string sha256, G1ModelGeometry geometry,
        IEnumerable<ModelDeviceLayout> layouts, G1ModelDrawBindings bindings)
    {
        _sourceBytes = source;
        SourceSha256 = sha256;
        VertexBuffers = Array.AsReadOnly(geometry.VertexBuffers.Select(x => new ModelDeviceVertexBuffer(x)).ToArray());
        IndexBuffers = Array.AsReadOnly(geometry.IndexBuffers.Select(x => new ModelDeviceIndexBuffer(x)).ToArray());
        Layouts = Array.AsReadOnly(layouts.ToArray());
        Draws = Array.AsReadOnly(bindings.Draws.Select(x => new ModelDeviceDraw(x)).ToArray());
    }
}

public static class ModelDeviceGeometry
{
    public static ModelDeviceGeometryPacket Prepare(LoadedModelGeometry source,
        IReadOnlyList<ModelVertexSemanticBinding> explicitBindings,
        IReadOnlySet<int>? explicitSingleInstanceSourceStreams=null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(explicitBindings);
        G1ModelGeometry geometry = source.Geometry;
        G1ModelDrawBindings draws = G1ModelDrawReader.Read(geometry);
        if (draws.Draws.Any(draw => draw.PrimitiveCode != 3 || draw.IndexCount % 3 != 0))
            throw new InvalidDataException("Model device geometry requires triangle-list draws.");
        var plan = explicitBindings.ToArray();
        if (plan.Any(x => x is null || (uint)x.LayoutIndex >= geometry.Layouts.Count))
            throw new InvalidDataException("Model semantic binding references a missing layout.");
        var usedLayouts = draws.Draws.Select(x => x.LayoutIndex).ToHashSet();
        var layouts = new List<ModelDeviceLayout>(geometry.Layouts.Count);
        foreach (G1VertexLayout layout in geometry.Layouts)
        {
            ModelVertexSemanticBinding[] selected = plan.Where(x => x.LayoutIndex == layout.Index).ToArray();
            if (usedLayouts.Contains(layout.Index) && selected.Length == 0)
                throw new InvalidDataException($"Model layout {layout.Index} has no explicit shader input bindings.");
            if (selected.Select(x => x.SourceSemanticCode).Distinct().Count() != selected.Length ||
                selected.Select(x => x.Location).Distinct().Count() != selected.Length)
                throw new InvalidDataException($"Model layout {layout.Index} has repeated source semantics or device locations.");
            int vertexCount = geometry.VertexBuffers[layout.BufferReferences[0]].VertexCount;
            var attributes = new List<ModelDeviceAttribute>(selected.Length);
            foreach (ModelVertexSemanticBinding binding in selected)
            {
                var matches = layout.Attributes.Select((x, i) => (Attribute: x, Index: i))
                    .Where(x => x.Attribute.SemanticCode == binding.SourceSemanticCode).ToArray();
                if (matches.Length != 1)
                    throw new InvalidDataException($"Model layout {layout.Index} source semantic {binding.SourceSemanticCode:X4} is absent or ambiguous.");
                (G1VertexAttribute attribute, int attributeIndex) = matches[0];
                int sourceBuffer = layout.BufferReferences[attribute.BufferReferenceSlot];
                G1VertexBuffer buffer = geometry.VertexBuffers[sourceBuffer];
                (ModelDeviceVertexFormat format, int components, int width, uint shaderType) = Format(attribute.TypeCode);
                if (attribute.ByteOffset < 0 || attribute.ByteOffset > buffer.Stride - width)
                    throw new InvalidDataException($"Model layout {layout.Index} attribute width exceeds its stream stride.");
                bool singleInstance=explicitSingleInstanceSourceStreams?.Contains(sourceBuffer)==true;
                if(singleInstance&&buffer.VertexCount!=1)
                    throw new InvalidDataException("Explicit single-instance source stream requires one complete source record.");
                if (!singleInstance&&buffer.VertexCount < vertexCount)
                    throw new InvalidDataException($"Model layout {layout.Index} bound stream {attribute.BufferReferenceSlot} is shorter than its vertex array.");
                attributes.Add(new(attributeIndex, attribute.SemanticCode, binding.Location, sourceBuffer,
                    checked((uint)attribute.BufferReferenceSlot), attribute.ByteOffset, buffer.Stride,
                    format, components, width, shaderType){ExplicitSingleInstanceFrequency=singleInstance});
            }
            layouts.Add(new(layout.Index, vertexCount, layout.BufferReferences, attributes));
        }
        return new(source.Source.Bytes.ToArray(), source.Source.DecodedSha256, geometry, layouts, draws);
    }

    internal static (ModelDeviceVertexFormat Format, int Components, int ByteWidth, uint ShaderType) Format(ushort code) => code switch
    {
        0 => (ModelDeviceVertexFormat.Float32x1, 1, 4, 3),
        1 => (ModelDeviceVertexFormat.Float32x2, 2, 8, 3),
        2 => (ModelDeviceVertexFormat.Float32x3, 3, 12, 3),
        3 => (ModelDeviceVertexFormat.Float32x4, 4, 16, 3),
        5 => (ModelDeviceVertexFormat.UInt8x4, 4, 4, 1),
        7 => (ModelDeviceVertexFormat.SInt16x4, 4, 8, 2),
        10 => (ModelDeviceVertexFormat.Float16x2, 2, 4, 3),
        11 => (ModelDeviceVertexFormat.Float16x4, 4, 8, 3),
        13 => (ModelDeviceVertexFormat.UNorm8x4, 4, 4, 3),
        _ => throw new InvalidDataException($"Unsupported model device numeric format {code:X4}.")
    };
}
