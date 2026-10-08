using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record G1IndexedTriangle(int Index, int SourceIndexStart,
    ushort VertexIndex0, ushort VertexIndex1, ushort VertexIndex2);
public sealed record G1TriangleList(int DrawIndex, int LayoutIndex, int PrimaryVertexBufferIndex,
    int IndexBufferIndex, int VertexStart, int VertexCount, int IndexStart, int IndexCount,
    uint PrimitiveCode, IReadOnlyList<G1IndexedTriangle> Triangles);

/// <summary>
/// Reads the verified code-3 triangle-list topology with original consecutive
/// u16 index order. Validates the explicit layout/buffer windows independently
/// of DrawReader. Does not remove degenerate faces, rewind indices or infer
/// front-face, culling, axes, shader or material settings.
/// </summary>
public static class G1ModelTriangleReader
{
    public static G1TriangleList Read(G1ModelGeometry geometry, G1DrawRecord draw)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(draw);
        if (draw.PrimitiveCode != 3)
            throw Error(draw, $"unsupported primitive code {draw.PrimitiveCode}; only verified code 3 triangle lists are supported");
        if (draw.Index < 0 || draw.IndexCount < 0 || draw.IndexCount % 3 != 0)
            throw Error(draw, "index count must be nonnegative and a multiple of three");
        if ((uint)draw.LayoutIndex >= geometry.Layouts.Count ||
            (uint)draw.PrimaryVertexBufferIndex >= geometry.VertexBuffers.Count ||
            (uint)draw.IndexBufferIndex >= geometry.IndexBuffers.Count)
            throw Error(draw, "references a missing layout, vertex buffer or index buffer");
        G1VertexLayout layout = geometry.Layouts[draw.LayoutIndex];
        G1VertexAttribute[] positions = layout.Attributes.Where(a => a.SemanticCode == 0).ToArray();
        if (layout.Index != draw.LayoutIndex || positions.Length != 1 ||
            (uint)positions[0].BufferReferenceSlot >= layout.BufferReferences.Count ||
            layout.BufferReferences[positions[0].BufferReferenceSlot] != draw.PrimaryVertexBufferIndex)
            throw Error(draw, "primary position stream does not reference the declared vertex buffer");
        G1VertexBuffer vertices = geometry.VertexBuffers[draw.PrimaryVertexBufferIndex];
        G1IndexBuffer indices = geometry.IndexBuffers[draw.IndexBufferIndex];
        if (vertices.Index != draw.PrimaryVertexBufferIndex || vertices.Stride <= 0 || vertices.VertexCount < 0 ||
            (long)vertices.Stride * vertices.VertexCount != vertices.Data.Length)
            throw Error(draw, "vertex buffer metadata disagrees with its raw byte length");
        if (indices.Index != draw.IndexBufferIndex || indices.BitsPerIndex != 16 || indices.IndexCount < 0 ||
            (long)indices.IndexCount * 2 != indices.Data.Length)
            throw Error(draw, "unsupported index width or index buffer metadata disagrees with its raw byte length");
        if (draw.VertexStart < 0 || draw.VertexCount < 0 || draw.IndexStart < 0 ||
            (long)draw.VertexStart + draw.VertexCount > vertices.VertexCount ||
            (long)draw.IndexStart + draw.IndexCount > indices.IndexCount)
            throw Error(draw, "vertex or index window exceeds its explicitly referenced buffer");

        var triangles = new List<G1IndexedTriangle>(draw.IndexCount / 3);
        long vertexEnd = (long)draw.VertexStart + draw.VertexCount;
        for (int index = draw.IndexStart; index < (long)draw.IndexStart + draw.IndexCount; index += 3)
        {
            ushort a = BinaryPrimitives.ReadUInt16LittleEndian(indices.Data.AsSpan(index * 2, 2));
            ushort b = BinaryPrimitives.ReadUInt16LittleEndian(indices.Data.AsSpan((index + 1) * 2, 2));
            ushort c = BinaryPrimitives.ReadUInt16LittleEndian(indices.Data.AsSpan((index + 2) * 2, 2));
            if (a < draw.VertexStart || a >= vertexEnd || b < draw.VertexStart || b >= vertexEnd || c < draw.VertexStart || c >= vertexEnd)
                throw Error(draw, $"triangle {triangles.Count} at source index {index} references a vertex outside the declared window");
            triangles.Add(new(triangles.Count, index, a, b, c));
        }
        return new(draw.Index, draw.LayoutIndex, draw.PrimaryVertexBufferIndex, draw.IndexBufferIndex,
            draw.VertexStart, draw.VertexCount, draw.IndexStart, draw.IndexCount, draw.PrimitiveCode, triangles);
    }

    private static InvalidDataException Error(G1DrawRecord draw, string message) =>
        new($"G1M triangle draw {draw.Index}: {message}.");
}
