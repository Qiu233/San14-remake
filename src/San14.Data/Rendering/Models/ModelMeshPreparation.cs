using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ModelMeshUvStream(int AttributeIndex, int BufferIndex,
    int BufferReferenceSlot, int ByteOffset, ushort TypeCode, ushort SemanticCode,
    byte UsageIndex, G1AttributeEncoding Encoding, int VertexStart, int VertexCount,
    IReadOnlyList<Vector2> Values,
    byte[] RawDescriptor, byte[] RawSourceBytes);

public sealed record ModelMeshSurface(int SurfaceIndex, int DrawIndex, int LayoutIndex,
    int PrimaryVertexBufferIndex, int IndexBufferIndex, int PaletteIndex,
    int VertexStart, int VertexCount, int IndexStart, int IndexCount,
    IReadOnlyList<int> SourceIndices, IReadOnlyList<int> PaletteEntryIndices, byte[] RawDrawRecord)
{
    public IReadOnlyList<Vector3> Positions { get; init; } = [];
    public IReadOnlyList<int> Indices { get; init; } = [];
    public IReadOnlyList<ModelMeshUvStream> SourceUvStreams { get; init; } = [];
    /// <summary>Complete weighted bind-pose inputs. Ordinary meshes retain an empty list.</summary>
    public IReadOnlyList<IReadOnlyList<G1ArmyBindPoseInfluence>> SourceArmyBindPoseInfluences { get; init; } = [];
}

public sealed record PreparedModelMesh(LoadedRdbAsset Source, IReadOnlyList<ModelMeshSurface> Surfaces);

/// <summary>Prepares static or explicit weighted bind-pose mesh data from imported model resources.
/// Positions retain their original axes and scale. No device, material or current animation is selected.</summary>
public static class ModelMeshPreparation
{
    public static PreparedModelMesh Prepare(LoadedModelTransforms loaded, bool allowArmyBindPose = false)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        string uri = loaded.Source.ResourceUri;
        var pending = new List<ModelMeshSurface>();
        try
        {
            G1ModelVertexData vertices = G1ModelVertexReader.Read(loaded.Geometry);
            G1ModelDrawBindings bindings = G1ModelDrawReader.Read(loaded.Geometry);
            if (bindings.Draws.Count == 0) throw new InvalidDataException("No source draws exist.");
            foreach (G1DrawRecord draw in bindings.Draws)
            {
                try
                {
                    G1TriangleList triangles = G1ModelTriangleReader.Read(loaded.Geometry, draw);
                    IReadOnlyList<ModelMeshUvStream> uvStreams = ReadUvStreams(
                        loaded.Geometry, draw, vertices.Layouts[draw.LayoutIndex]);
                    G1DecodedVertexLayout layout = vertices.Layouts[draw.LayoutIndex];
                    G1ArmyBindPoseDrawPositions? weighted = allowArmyBindPose &&
                        layout.Attributes.Any(a => a.UsageCode == 1 && a.Encoding is
                            G1AttributeEncoding.Float32x1 or G1AttributeEncoding.Float32x2 or G1AttributeEncoding.Float32x3)
                        ? G1ArmyBindPoseReader.TransformPositions(loaded.Transforms, draw, layout) : null;
                    G1RestPoseDrawPositions pose = weighted?.Positions ?? G1ModelTransformReader.TransformPositions(
                        loaded.Transforms, draw, layout);
                    if (pose.Positions.Count != draw.VertexCount || pose.VertexStart != draw.VertexStart ||
                        pose.DrawIndex != draw.Index || pose.PaletteEntryIndices.Count != draw.VertexCount)
                        throw new InvalidDataException("Static position result does not match the source draw window.");
                    if (draw.VertexCount == 0 || draw.IndexCount == 0)
                        throw new InvalidDataException("An empty draw cannot be represented by one indexed Godot triangle surface.");
                    Vector3[] positions = pose.Positions.ToArray();
                    var sourceIndices = new int[draw.IndexCount];
                    var indices = new int[draw.IndexCount];
                    int next = 0;
                    foreach (G1IndexedTriangle triangle in triangles.Triangles)
                    {
                        sourceIndices[next++] = triangle.VertexIndex0;
                        sourceIndices[next++] = triangle.VertexIndex1;
                        sourceIndices[next++] = triangle.VertexIndex2;
                    }
                    if (next != draw.IndexCount) throw new InvalidDataException("Triangle indices do not match the source index count.");
                    for (int i = 0; i < indices.Length; i++)
                    {
                        indices[i] = sourceIndices[i] - draw.VertexStart;
                        if ((uint)indices[i] >= draw.VertexCount)
                            throw new InvalidDataException("Compact index falls outside the static position array.");
                    }
                    pending.Add(new ModelMeshSurface(pending.Count, draw.Index,
                        draw.LayoutIndex, draw.PrimaryVertexBufferIndex, draw.IndexBufferIndex, pose.PaletteIndex,
                        draw.VertexStart, draw.VertexCount, draw.IndexStart, draw.IndexCount,
                        Array.AsReadOnly(sourceIndices), Array.AsReadOnly(pose.PaletteEntryIndices.ToArray()),
                        draw.RawBytes.ToArray()) { Positions = Array.AsReadOnly(positions), Indices = Array.AsReadOnly(indices), SourceUvStreams = uvStreams,
                            SourceArmyBindPoseInfluences = weighted?.Influences ?? [] });
                }
                catch (Exception error) when (error is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException)
                { throw new InvalidDataException($"Original model mesh {uri}, draw {draw.Index}: {error.Message}", error); }
            }
        }
        catch (Exception error) when (error is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException)
        { throw new InvalidDataException($"Original model mesh source {uri}: {error.Message}", error); }

        return new(loaded.Source, pending.AsReadOnly());
    }

    private static IReadOnlyList<ModelMeshUvStream> ReadUvStreams(G1ModelGeometry geometry,
        G1DrawRecord draw, G1DecodedVertexLayout layout)
    {
        G1DecodedAttribute[] attributes = layout.Attributes.Where(attribute => attribute.UsageCode == 5).ToArray();
        if (attributes.GroupBy(attribute => attribute.UsageIndex).Any(group => group.Count() != 1))
            throw new InvalidDataException("UV usage/index is not unique in the source layout.");
        var result = new List<ModelMeshUvStream>(attributes.Length);
        foreach (G1DecodedAttribute attribute in attributes)
        {
            if (attribute.ComponentCount != 2 || attribute.Encoding is not (G1AttributeEncoding.Float32x2 or G1AttributeEncoding.Float16x2))
                throw new InvalidDataException($"UV layer {attribute.UsageIndex} does not use verified two-component storage.");
            // Equal buffer counts alone do not establish a common index space.
            // All supported source UVs explicitly refer to the primary position buffer.
            if (attribute.BufferIndex != draw.PrimaryVertexBufferIndex)
                throw new InvalidDataException($"UV layer {attribute.UsageIndex} refers to a different vertex buffer; its index-space correspondence is unverified.");
            if (draw.VertexStart < 0 || draw.VertexCount < 0 ||
                (long)draw.VertexStart + draw.VertexCount > attribute.ElementCount)
                throw new InvalidDataException($"UV layer {attribute.UsageIndex} does not cover the source draw vertex window.");
            G1VertexBuffer buffer = geometry.VertexBuffers[attribute.BufferIndex];
            int encodedWidth = attribute.Encoding == G1AttributeEncoding.Float32x2 ? 8 : 4;
            var raw = new byte[checked(draw.VertexCount * encodedWidth)];
            var values = new Vector2[draw.VertexCount];
            for (int i = 0; i < draw.VertexCount; i++)
            {
                int sourceIndex = checked(draw.VertexStart + i);
                ReadOnlySpan<float> value = attribute.GetElement(sourceIndex);
                values[i] = new Vector2(value[0], value[1]);
                buffer.Data.AsSpan(checked(sourceIndex * buffer.Stride + attribute.Source.ByteOffset), encodedWidth)
                    .CopyTo(raw.AsSpan(i * encodedWidth, encodedWidth));
            }
            result.Add(new(attribute.Index, attribute.BufferIndex, attribute.Source.BufferReferenceSlot,
                attribute.Source.ByteOffset, attribute.Source.TypeCode, attribute.Source.SemanticCode,
                attribute.UsageIndex, attribute.Encoding, draw.VertexStart, draw.VertexCount,
                Array.AsReadOnly(values), attribute.Source.RawBytes.ToArray(), raw));
        }
        return result.AsReadOnly();
    }

}
