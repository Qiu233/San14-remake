using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public readonly record struct G1EffectObjectDataPatch(int FileOffset, ReadOnlyMemory<byte> Bytes);

/// <summary>Live module force fields. Relative resource references bind to these
/// managed nodes; no executable address or process pointer enters effect state.</summary>
public sealed class G1EffectAuxiliaryRuntime
{
    private sealed class Node(int fileOffset, byte[] bytes)
    {
        internal readonly int FileOffset = fileOffset;
        internal readonly byte[] Bytes = bytes;
        internal readonly List<Node> Children = [];
        internal int FrameOffset = -1;
        internal bool FramePrepared;
        internal Matrix4x4 Transform;
        internal Matrix4x4 InverseTransform;
    }
    private readonly G1EffectContext context;
    private readonly List<Node> roots = [];
    private readonly Dictionary<int, Node> nodes = [];
    private readonly Dictionary<int, List<Node>> ownedRoots = [];
    private Matrix4x4? worldTransform;

    public G1EffectAuxiliaryRuntime(IReadOnlyList<G1EffectRecord> auxiliaries, G1EffectContext context)
    {
        ArgumentNullException.ThrowIfNull(auxiliaries); ArgumentNullException.ThrowIfNull(context);
        this.context = context;
        foreach (var record in auxiliaries)
        {
            if (record.Table is not (G1EffectTable.Auxiliary or G1EffectTable.NestedAuxiliary))
                throw new ArgumentException("A module auxiliary declaration is required.", nameof(auxiliaries));
            roots.Add(ReadNode(record.FileOffset, record.Bytes));
        }
    }

    public void Advance(float seconds)
    {
        foreach (Node root in roots) Advance(root, seconds);
    }

    internal void SetWorldTransform(Matrix4x4 world) => worldTransform = world;

    internal void RegisterOwned(int genericFileOffset, IReadOnlyList<G1EffectRecord> declarations)
    {
        if (declarations.Count == 0) return;
        var owned = new List<Node>(declarations.Count);
        foreach (var declaration in declarations)
            owned.Add(ReadNode(declaration.FileOffset, declaration.Bytes));
        ownedRoots.Add(genericFileOffset, owned);
    }

    internal void AdvanceOwned(int genericFileOffset, float seconds)
    {
        if (ownedRoots.TryGetValue(genericFileOffset, out var owned))
            foreach (Node root in owned) Advance(root, seconds);
    }

    /// <summary>Initial prepared auxiliary declarations, in parent-before-child order.
    /// The GPU object owner applies each fragment at its existing normal-resource coordinate.
    /// Views retain this owner's storage; the consumer must upload before its next update.</summary>
    public IEnumerable<G1EffectObjectDataPatch> EnumeratePreparedData()
    {
        foreach (var node in nodes.Values) yield return new(node.FileOffset, node.Bytes);
    }

    /// <summary>Upload elapsed time and the actual bound transform matrices where present,
    /// without cloning the module or the complete field tree each frame.</summary>
    public IEnumerable<G1EffectObjectDataPatch> EnumerateLiveData()
    {
        foreach (var node in nodes.Values)
        {
            yield return new(checked(node.FileOffset + 0x10), node.Bytes.AsMemory(0x10, 4));
            if (node.FrameOffset >= 0 && node.FramePrepared)
                yield return new(checked(node.FileOffset + node.FrameOffset + 0x10),
                    node.Bytes.AsMemory(node.FrameOffset + 0x10, 0xC0));
        }
    }

    public Vector4 Sample(int fileOffset, Vector4 position)
    {
        if (!nodes.TryGetValue(fileOffset, out Node? node))
            throw new InvalidDataException("Particle force references an absent module auxiliary.");
        return Sample(node, position);
    }

    private Node ReadNode(int fileOffset, ReadOnlyMemory<byte> source)
    {
        int size = I(source.Span, 0);
        if (size < 0x20 || size > source.Length)
            throw new InvalidDataException("Incomplete force-field declaration.");
        var node = new Node(fileOffset, source.Span[..size].ToArray());
        if (!nodes.TryAdd(fileOffset, node))
            throw new InvalidDataException("Duplicate or cyclic force-field declaration.");
        Span<byte> bytes = node.Bytes;
        if (I(bytes, 0x14) != 0 || I(bytes, 0x1C) != 0)
            throw new NotSupportedException("Force-field animation controllers are not implemented.");
        int type = I(bytes, 4);
        switch (type)
        {
            case 0:
                if (I(bytes, 0x20) != 0)
                {
                    int at = checked(0x20 + I(bytes, 0x20));
                    Require(bytes.Length, at, 0xF0);
                    if (I(bytes, at) != 0xF0 || I(bytes, at + 4) != 0x01110000 ||
                        I(bytes, at + 0xE0) != 0 || I(bytes, at + 0xE4) != 0)
                        throw new NotSupportedException("Auxiliary requires an unsupported transform provider.");
                    node.FrameOffset = at;
                    BinaryPrimitives.WriteUInt64LittleEndian(bytes[(at + 8)..], 1);
                    bytes.Slice(at + 0xD0, 16).Clear();
                }
                int count = I(bytes, 0x2C);
                if (count < 0) throw new InvalidDataException("Negative force-field child count.");
                for (int index = 0; index < count; index++)
                {
                    int slot = checked(0x30 + index * 4), at = checked(slot + I(bytes, slot));
                    Require(bytes.Length, at, 4);
                    node.Children.Add(ReadNode(checked(fileOffset + at), source.Slice(at, size - at)));
                }
                break;
            case 6:
                Copy(bytes, 0x40, 0x20, 32); Copy(bytes, 0x68, 0x60, 8);
                break;
            case 7:
                Copy(bytes, 0x40, 0x20, 32); Copy(bytes, 0x70, 0x60, 8);
                break;
            default: throw new NotSupportedException($"Auxiliary force field {type} is not implemented.");
        }
        PutF(bytes, 0x10, 0); BinaryPrimitives.WriteInt32LittleEndian(bytes[8..], 1);
        return node;
    }

    private void Advance(Node node, float seconds)
    {
        if (node.FrameOffset >= 0)
        {
            Matrix4x4 next = worldTransform ??
                throw new InvalidOperationException("Auxiliary has no current system transform owner.");
            if (!Matrix4x4.Invert(next, out var inverse))
                throw new InvalidDataException("Auxiliary transform owner is singular.");
            Matrix4x4 velocity = default;
            if (node.FramePrepared && seconds > 0)
            {
                velocity = G1EffectDistribution.Multiply(node.Transform, inverse);
                velocity.M11 -= 1; velocity.M22 -= 1; velocity.M33 -= 1; velocity.M44 -= 1;
                velocity *= -1f / seconds;
            }
            node.Transform = next; node.InverseTransform = inverse; node.FramePrepared = true;
            WriteMatrix(node.Bytes, node.FrameOffset + 0x10, next);
            WriteMatrix(node.Bytes, node.FrameOffset + 0x50, inverse);
            WriteMatrix(node.Bytes, node.FrameOffset + 0x90, velocity);
        }
        foreach (Node child in node.Children) Advance(child, seconds);
        // Types six and seven have genuinely empty value-update dispatches;
        // sampling still uses the live time advanced by the auxiliary owner.
        PutF(node.Bytes, 0x10, F(node.Bytes, 0x10) + seconds);
    }

    private Vector4 Sample(Node node, Vector4 position)
    {
        ReadOnlySpan<byte> bytes = node.Bytes;
        switch (I(bytes, 4))
        {
            case 0:
            {
                if (node.FrameOffset >= 0)
                {
                    if (!node.FramePrepared)
                        throw new InvalidOperationException("Auxiliary frame has not received its current owner transform.");
                    position = Transform(position, node.InverseTransform);
                }
                Vector4 sum = default;
                foreach (Node child in node.Children)
                {
                    Vector4 sample = Sample(child, position);
                    sum = new(sample.X + sum.X, sample.Y + sum.Y,
                        sample.Z + sum.Z, sample.W + sum.W);
                }
                if (node.FrameOffset < 0) return sum;
                var transformed = Transform(new(sum.X, sum.Y, sum.Z, 0), node.Transform);
                return new(transformed.X, transformed.Y, transformed.Z, sum.W);
            }
            case 6:
            {
                float dx = position.X - F(bytes, 0x20), dy = position.Y - F(bytes, 0x24),
                    dz = position.Z - F(bytes, 0x28);
                float y = F(bytes, 0x38) * dx - F(bytes, 0x30) * dz,
                    x = F(bytes, 0x34) * dz - F(bytes, 0x38) * dy,
                    z = F(bytes, 0x30) * dy - F(bytes, 0x34) * dx;
                float denominator = F(bytes, 0x60), squared = y * y + x * x + z * z;
                if (denominator < squared) denominator = squared;
                float gain = F(bytes, 0x64) / denominator;
                return new(x * gain, y * gain, z * gain, gain * 0f);
            }
            case 7:
            {
                Span<float> coordinates = stackalloc float[4];
                int count = 0, mask = I(bytes, 0x6C);
                if ((mask & 1) != 0) coordinates[count++] = (position.X - F(bytes, 0x30)) * F(bytes, 0x20);
                if ((mask & 2) != 0) coordinates[count++] = (position.Y - F(bytes, 0x34)) * F(bytes, 0x24);
                if ((mask & 4) != 0) coordinates[count++] = (position.Z - F(bytes, 0x38)) * F(bytes, 0x28);
                if ((mask & 8) != 0) coordinates[count++] = (F(bytes, 0x10) - F(bytes, 0x3C)) * F(bytes, 0x64);
                return G1EffectVectorNoise.Sample(context, coordinates[..count], F(bytes, 0x60),
                    unchecked((uint)I(bytes, 0x68)));
            }
            default: throw new NotSupportedException("Unsupported initialized force field.");
        }
    }

    private static void Copy(Span<byte> bytes, int from, int to, int length)
    { Require(bytes.Length, from, length); Require(bytes.Length, to, length); bytes.Slice(from, length).CopyTo(bytes[to..]); }
    private static void Require(int size, int at, int length)
    {
        if (at < 0 || length < 0 || at > size - length)
            throw new InvalidDataException("Force field escapes its declaration.");
    }
    private static int I(ReadOnlySpan<byte> bytes, int at)
    { Require(bytes.Length, at, 4); return BinaryPrimitives.ReadInt32LittleEndian(bytes[at..]); }
    private static float F(ReadOnlySpan<byte> bytes, int at)
    { Require(bytes.Length, at, 4); return BinaryPrimitives.ReadSingleLittleEndian(bytes[at..]); }
    private static void PutF(Span<byte> bytes, int at, float value) =>
        BinaryPrimitives.WriteSingleLittleEndian(bytes[at..], value);
    private static Vector4 Transform(Vector4 value, Matrix4x4 matrix) => new(
        value.Y*matrix.M21 + value.X*matrix.M11 + value.Z*matrix.M31 + value.W*matrix.M41,
        value.X*matrix.M12 + value.Y*matrix.M22 + value.Z*matrix.M32 + value.W*matrix.M42,
        value.X*matrix.M13 + value.Y*matrix.M23 + value.Z*matrix.M33 + value.W*matrix.M43,
        value.X*matrix.M14 + value.Y*matrix.M24 + value.Z*matrix.M34 + value.W*matrix.M44);
    private static void WriteMatrix(Span<byte> bytes, int at, Matrix4x4 value)
    {
        PutF(bytes, at, value.M11); PutF(bytes, at+4, value.M12); PutF(bytes, at+8, value.M13); PutF(bytes, at+12, value.M14);
        PutF(bytes, at+16, value.M21); PutF(bytes, at+20, value.M22); PutF(bytes, at+24, value.M23); PutF(bytes, at+28, value.M24);
        PutF(bytes, at+32, value.M31); PutF(bytes, at+36, value.M32); PutF(bytes, at+40, value.M33); PutF(bytes, at+44, value.M34);
        PutF(bytes, at+48, value.M41); PutF(bytes, at+52, value.M42); PutF(bytes, at+56, value.M43); PutF(bytes, at+60, value.M44);
    }
}
