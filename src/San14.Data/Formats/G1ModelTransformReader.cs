using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data.Formats;

public sealed record G1JointTransform(int Index, Vector3 Scale, int ParentIndex,
    Quaternion Rotation, Vector4 Position, byte[] RawBytes);
public sealed record G1ReferenceMatrix(int Index, Matrix4x4 Matrix, byte[] RawBytes);
public sealed record G1JointPaletteEntry(int Index, int MatrixIndex, uint RawValue4,
    int JointIndex, byte[] RawBytes);
public sealed record G1JointPalette(int Index, IReadOnlyList<G1JointPaletteEntry> Entries, byte[] RawCount);
public sealed record G1ModelTransforms(int JointDataOffset, uint UnknownValue16,
    ushort RawValue24, ushort RawValue26, IReadOnlyList<ushort> RawJointMap,
    byte[] UnknownBeforeJoints, IReadOnlyList<G1JointTransform> Joints,
    IReadOnlyList<Matrix4x4> JointModelMatrices, IReadOnlyList<G1ReferenceMatrix> ReferenceMatrices,
    IReadOnlyList<G1JointPalette> JointPalettes, byte[] RawSkeletonHeader, byte[] RawMatrixHeader);
public sealed record G1RestPoseDrawPositions(int DrawIndex, int PaletteIndex, int VertexStart,
    IReadOnlyList<Vector3> Positions, IReadOnlyList<int> PaletteEntryIndices);

/// <summary>
/// Reads SM1G2300, MM1G0200 and GM1G4400 joint palettes, and applies the verified
/// single-influence model rest-pose position chain. This does not define world/Hex
/// coordinates, animation, normal transforms, shader deformation or general skin weights.
/// </summary>
public static class G1ModelTransformReader
{
    public static G1ModelTransforms Read(ReadOnlySpan<byte> model, G1ModelGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        G1ModelHeaders headers = G1ModelHeaderReader.Read(model, model.Length);
        VerifyGeometry(model, headers, geometry);
        G1ModelChunkHeader skeleton = headers.Chunks[1], matrices = headers.Chunks[2];
        ReadOnlySpan<byte> sm = model.Slice(skeleton.Offset, skeleton.Length);
        ReadOnlySpan<byte> mm = model.Slice(matrices.Offset, matrices.Length);
        if (sm.Length < 28 || mm.Length < 16) throw Error("truncated skeleton or matrix header");
        int jointOffset = Number(sm, 12, "joint offset");
        int jointCount = Short(sm, 20), mapCount = Short(sm, 22);
        if (jointCount == 0 || jointOffset < 28L + mapCount * 2L ||
            (long)jointOffset + jointCount * 48L != sm.Length)
            throw Error("joint/map boundaries do not exactly consume the skeleton");
        var map = new ushort[mapCount];
        for (int i = 0; i < mapCount; i++) map[i] = Short(sm, 28 + i * 2);
        var joints = new List<G1JointTransform>(jointCount);
        for (int i = 0; i < jointCount; i++)
        {
            ReadOnlySpan<byte> bytes = sm.Slice(jointOffset + i * 48, 48);
            Vector3 scale = new(Float(bytes, 0), Float(bytes, 4), Float(bytes, 8));
            int parent = BinaryPrimitives.ReadInt32LittleEndian(bytes[12..]);
            Quaternion rotation = new(Float(bytes, 16), Float(bytes, 20), Float(bytes, 24), Float(bytes, 28));
            Vector4 position = new(Float(bytes, 32), Float(bytes, 36), Float(bytes, 40), Float(bytes, 44));
            float norm = rotation.LengthSquared();
            if (parent < -1 || parent >= jointCount || !float.IsFinite(norm) || Math.Abs(norm - 1) > 0.0001f || position.W != 1)
                throw Error($"joint {i} has an unsupported parent, quaternion or position W");
            joints.Add(new(i, scale, parent, rotation, position, bytes.ToArray()));
        }
        Matrix4x4[] jointMatrices = BuildJointMatrices(joints);
        int matrixCount = Number(mm, 12, "reference matrix count");
        if (matrixCount > (mm.Length - 16) / 64 || 16L + matrixCount * 64L != mm.Length)
            throw Error("matrix count does not exactly consume the matrix chunk");
        var referenceMatrices = new List<G1ReferenceMatrix>(matrixCount);
        for (int i = 0; i < matrixCount; i++)
        {
            ReadOnlySpan<byte> bytes = mm.Slice(16 + i * 64, 64);
            Matrix4x4 value = ReadMatrix(bytes);
            if (value.M14 != 0 || value.M24 != 0 || value.M34 != 0 || value.M44 != 1)
                throw Error($"reference matrix {i} is not in the verified affine row layout");
            referenceMatrices.Add(new(i, value, bytes.ToArray()));
        }
        IReadOnlyList<G1JointPalette> palettes = ReadPalettes(geometry, matrixCount, jointCount);
        return new(jointOffset, UInt(sm, 16), Short(sm, 24), Short(sm, 26), map,
            sm.Slice(28 + mapCount * 2, jointOffset - 28 - mapCount * 2).ToArray(), joints,
            jointMatrices, referenceMatrices, palettes, sm[..28].ToArray(), mm[..16].ToArray());
    }

    public static G1RestPoseDrawPositions TransformPositions(G1ModelTransforms transforms,
        G1DrawRecord draw, G1DecodedVertexLayout layout)
    {
        ArgumentNullException.ThrowIfNull(transforms);
        ArgumentNullException.ThrowIfNull(draw);
        ArgumentNullException.ThrowIfNull(layout);
        string context = $"draw {draw.Index}";
        if (layout.Index != draw.LayoutIndex || draw.UnknownValue8 >= transforms.JointPalettes.Count)
            throw Error(context + " references a missing or mismatched layout/palette at +8");
        int paletteIndex = (int)draw.UnknownValue8;
        G1JointPalette palette = transforms.JointPalettes[paletteIndex];
        G1DecodedAttribute[] positions = layout.Attributes.Where(a => a.UsageCode == 0).ToArray();
        G1DecodedAttribute[] selectors = layout.Attributes.Where(a => a.UsageCode == 2).ToArray();
        if (positions.Length != 1 || positions[0].UsageIndex != 0 || positions[0].Encoding != G1AttributeEncoding.Float32x3 ||
            positions[0].ComponentCount != 3 || positions[0].BufferIndex != draw.PrimaryVertexBufferIndex ||
            draw.VertexStart < 0 || draw.VertexCount < 0 || (long)draw.VertexStart + draw.VertexCount > positions[0].ElementCount ||
            (long)positions[0].ElementCount * 3 != positions[0].Values.Length)
            throw Error(context + " has an unsupported or truncated primary position stream");
        G1DecodedAttribute position = positions[0];
        G1DecodedAttribute? selector = selectors.Length == 1 ? selectors[0] : null;
        if (selectors.Length > 1 || (selector is null && palette.Entries.Count != 1))
            throw Error(context + " has an ambiguous palette without a verified single-influence selector");
        bool hasUsage1 = layout.Attributes.Any(a => a.UsageCode == 1);
        if (selector is null && hasUsage1 && !IsVerifiedOpaqueShaderProfile(layout, position))
            throw Error(context + " has unsupported weights or an unverified opaque shader layout");
        if (selector is not null && (selector.UsageIndex != 0 || selector.Encoding != G1AttributeEncoding.UInt8x4 ||
            selector.ComponentCount != 4 || selector.BufferIndex != position.BufferIndex || selector.ElementCount != position.ElementCount ||
            (long)selector.ElementCount * 4 != selector.Values.Length || hasUsage1))
            throw Error(context + " has unsupported weights or index storage");

        var result = new Vector3[draw.VertexCount];
        var entries = new int[draw.VertexCount];
        var matrixCache = new Dictionary<int, Matrix4x4>();
        for (int i = 0; i < draw.VertexCount; i++)
        {
            int vertex = draw.VertexStart + i, entryIndex = 0;
            if (selector is not null)
            {
                ReadOnlySpan<float> values = selector.GetElement(vertex);
                if (!float.IsFinite(values[0]) || values[0] < 0 || values[0] > 255 || values[0] % 3 != 0 ||
                    values[1] != 0 || values[2] != 0 || values[3] != 0)
                    throw Error(context + $" vertex {vertex} has an unsupported single-influence register offset");
                entryIndex = (int)values[0] / 3;
            }
            if ((uint)entryIndex >= palette.Entries.Count)
                throw Error(context + $" vertex {vertex} references a missing palette entry");
            if (!matrixCache.TryGetValue(entryIndex, out Matrix4x4 matrix))
            {
                G1JointPaletteEntry entry = palette.Entries[entryIndex];
                if ((uint)entry.MatrixIndex >= transforms.ReferenceMatrices.Count ||
                    (uint)entry.JointIndex >= transforms.JointModelMatrices.Count)
                    throw Error(context + " palette entry references a missing matrix or joint");
                // System.Numerics uses row vectors: originalReference * jointModel.
                matrix = transforms.ReferenceMatrices[entry.MatrixIndex].Matrix * transforms.JointModelMatrices[entry.JointIndex];
                if (!Finite(matrix)) throw Error(context + " has a non-finite composed matrix");
                matrixCache.Add(entryIndex, matrix);
            }
            ReadOnlySpan<float> raw = position.GetElement(vertex);
            Vector3 transformed = Vector3.Transform(new(raw[0], raw[1], raw[2]), matrix);
            if (!float.IsFinite(transformed.X) || !float.IsFinite(transformed.Y) || !float.IsFinite(transformed.Z))
                throw Error(context + $" vertex {vertex} has a non-finite transformed position");
            result[i] = transformed; entries[i] = entryIndex;
        }
        return new(draw.Index, paletteIndex, draw.VertexStart, result, entries);
    }

    private static bool IsVerifiedOpaqueShaderProfile(G1DecodedVertexLayout layout, G1DecodedAttribute position)
    {
        // These 172 original stride-72 layouts contain usage 1 values outside a
        // weight range. Permit their exact raw layout for static positions only.
        // The usage-1 channel and any additional constant stream remain unused.
        (ushort Type, ushort Usage, int Offset, G1AttributeEncoding Encoding, int Components)[] profile =
        [
            (2, 0, 0, G1AttributeEncoding.Float32x3, 3),
            (11, 3, 12, G1AttributeEncoding.Float16x4, 4),
            (13, 10, 20, G1AttributeEncoding.UNorm8x4, 4),
            (11, 266, 24, G1AttributeEncoding.Float16x4, 4),
            (11, 522, 32, G1AttributeEncoding.Float16x4, 4),
            (11, 778, 40, G1AttributeEncoding.Float16x4, 4),
            (11, 1, 48, G1AttributeEncoding.Float16x4, 4),
            (1, 5, 56, G1AttributeEncoding.Float32x2, 2),
            (11, 6, 64, G1AttributeEncoding.Float16x4, 4)
        ];
        G1DecodedAttribute[] primary = layout.Attributes.Where(a => a.Source.BufferReferenceSlot == 0).ToArray();
        if (primary.Length != profile.Length) return false;
        for (int i = 0; i < profile.Length; i++)
        {
            G1DecodedAttribute attribute = primary[i]; var expected = profile[i];
            if (attribute.Source.TypeCode != expected.Type || attribute.Source.SemanticCode != expected.Usage ||
                attribute.Source.ByteOffset != expected.Offset || attribute.Encoding != expected.Encoding ||
                attribute.ComponentCount != expected.Components || attribute.BufferIndex != position.BufferIndex ||
                attribute.ElementCount != position.ElementCount || (long)attribute.ElementCount * attribute.ComponentCount != attribute.Values.Length)
                return false;
        }
        G1DecodedAttribute[] extra = layout.Attributes.Where(a => a.Source.BufferReferenceSlot != 0).ToArray();
        if (extra.Length == 0) return true;
        if (extra.Length != 3) return false;
        ushort[] usage = [11, 4, 12];
        for (int i = 0; i < extra.Length; i++)
            if (extra[i].Source.BufferReferenceSlot != 1 || extra[i].Source.TypeCode != 3 ||
                extra[i].Source.SemanticCode != usage[i] || extra[i].Source.ByteOffset != i * 16 ||
                extra[i].Encoding != G1AttributeEncoding.Float32x4 || extra[i].ComponentCount != 4 ||
                extra[i].ElementCount != 1 || extra[i].Values.Length != 4 || extra[i].BufferIndex == position.BufferIndex ||
                extra[i].BufferIndex != extra[0].BufferIndex)
                return false;
        return true;
    }

    private static Matrix4x4[] BuildJointMatrices(IReadOnlyList<G1JointTransform> joints)
    {
        var matrices = new Matrix4x4[joints.Count];
        var state = new byte[joints.Count];
        for (int start = 0; start < joints.Count; start++)
        {
            if (state[start] == 2) continue;
            var chain = new List<int>();
            int index = start;
            while (index != -1 && state[index] != 2)
            {
                if (state[index] == 1) throw Error($"joint {index} participates in a parent cycle");
                state[index] = 1; chain.Add(index); index = joints[index].ParentIndex;
            }
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                int jointIndex = chain[i]; G1JointTransform joint = joints[jointIndex];
                Matrix4x4 local = Matrix4x4.CreateScale(joint.Scale) *
                    Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(joint.Rotation)) *
                    Matrix4x4.CreateTranslation(joint.Position.X, joint.Position.Y, joint.Position.Z);
                Matrix4x4 value = joint.ParentIndex == -1 ? local : local * matrices[joint.ParentIndex];
                if (!Finite(value)) throw Error($"joint {jointIndex} has a non-finite composed matrix");
                matrices[jointIndex] = value; state[jointIndex] = 2;
            }
        }
        return matrices;
    }

    private static IReadOnlyList<G1JointPalette> ReadPalettes(G1ModelGeometry geometry, int matrixCount, int jointCount)
    {
        G1GeometrySection[] matches = geometry.Sections.Where(s => s.Id == 0x10006).ToArray();
        if (matches.Length != 1) throw Error("missing or repeated joint palette section");
        G1GeometrySection section = matches[0]; ReadOnlySpan<byte> bytes = section.RawBytes;
        if (bytes.Length < 12 || bytes.Length != section.Length || section.ItemCount < 0 ||
            UInt(bytes, 0) != section.Id || UInt(bytes, 4) != section.Length || UInt(bytes, 8) != section.ItemCount ||
            section.ItemCount > (bytes.Length - 12) / 4)
            throw Error("invalid joint palette section header");
        var palettes = new List<G1JointPalette>(section.ItemCount);
        int position = 12;
        for (int i = 0; i < section.ItemCount; i++)
        {
            if (position > bytes.Length - 4) throw Error($"palette {i} count is truncated");
            byte[] rawCount = bytes.Slice(position, 4).ToArray();
            int count = Number(bytes, position, $"palette {i} entry count"); position += 4;
            if (count > (bytes.Length - position) / 12) throw Error($"palette {i} entries exceed the section");
            var entries = new List<G1JointPaletteEntry>(count);
            for (int j = 0; j < count; j++)
            {
                ReadOnlySpan<byte> entry = bytes.Slice(position, 12);
                int matrix = Number(entry, 0, $"palette {i} entry {j} matrix");
                int joint = Number(entry, 8, $"palette {i} entry {j} joint");
                if (matrix >= matrixCount || joint >= jointCount)
                    throw Error($"palette {i} entry {j} references a missing matrix or joint");
                entries.Add(new(j, matrix, UInt(entry, 4), joint, entry.ToArray())); position += 12;
            }
            palettes.Add(new(i, entries, rawCount));
        }
        if (position != bytes.Length) throw Error("joint palette section has unindexed trailing bytes");
        return palettes;
    }

    private static void VerifyGeometry(ReadOnlySpan<byte> model, G1ModelHeaders headers, G1ModelGeometry geometry)
    {
        if (!headers.RawHeader.AsSpan().SequenceEqual(geometry.Headers.RawHeader) || headers.Chunks.Count != geometry.Headers.Chunks.Count)
            throw Error("model and geometry headers do not match");
        for (int i = 0; i < headers.Chunks.Count; i++)
            if (headers.Chunks[i].Offset != geometry.Headers.Chunks[i].Offset || headers.Chunks[i].Length != geometry.Headers.Chunks[i].Length ||
                !headers.Chunks[i].RawHeader.AsSpan().SequenceEqual(geometry.Headers.Chunks[i].RawHeader))
                throw Error("model and geometry chunk headers do not match");
        G1ModelChunkHeader gm = headers.Chunks[3];
        if (gm.Length < 48 || geometry.RawGeometryHeader.Length != 48 || !model.Slice(gm.Offset, 48).SequenceEqual(geometry.RawGeometryHeader))
            throw Error("model and geometry body header do not match");
        foreach (G1GeometrySection section in geometry.Sections)
            if (section.Offset < 48 || section.Length < 12 || section.Offset > gm.Length - section.Length ||
                section.RawBytes.Length != section.Length || !model.Slice(gm.Offset + section.Offset, section.Length).SequenceEqual(section.RawBytes))
                throw Error("model and geometry source sections do not match");
    }
    private static Matrix4x4 ReadMatrix(ReadOnlySpan<byte> bytes) => new(
        Float(bytes, 0), Float(bytes, 4), Float(bytes, 8), Float(bytes, 12),
        Float(bytes, 16), Float(bytes, 20), Float(bytes, 24), Float(bytes, 28),
        Float(bytes, 32), Float(bytes, 36), Float(bytes, 40), Float(bytes, 44),
        Float(bytes, 48), Float(bytes, 52), Float(bytes, 56), Float(bytes, 60));
    private static bool Finite(Matrix4x4 m) => float.IsFinite(m.M11) && float.IsFinite(m.M12) && float.IsFinite(m.M13) && float.IsFinite(m.M14) &&
        float.IsFinite(m.M21) && float.IsFinite(m.M22) && float.IsFinite(m.M23) && float.IsFinite(m.M24) &&
        float.IsFinite(m.M31) && float.IsFinite(m.M32) && float.IsFinite(m.M33) && float.IsFinite(m.M34) &&
        float.IsFinite(m.M41) && float.IsFinite(m.M42) && float.IsFinite(m.M43) && float.IsFinite(m.M44);
    private static float Float(ReadOnlySpan<byte> bytes, int offset)
    {
        float value = BinaryPrimitives.ReadSingleLittleEndian(bytes[offset..]);
        if (!float.IsFinite(value)) throw Error("joint/matrix source contains a non-finite value");
        return value;
    }
    private static int Number(ReadOnlySpan<byte> bytes, int offset, string context)
    {
        uint value = UInt(bytes, offset);
        if (value > int.MaxValue) throw Error(context + " exceeds the supported integer range");
        return (int)value;
    }
    private static uint UInt(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static ushort Short(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
    private static InvalidDataException Error(string message) => new($"G1M model transforms: {message}.");
}
