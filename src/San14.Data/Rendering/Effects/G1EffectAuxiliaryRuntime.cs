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
    }
    private readonly G1EffectContext context;
    private readonly List<Node> roots = [];
    private readonly Dictionary<int, Node> nodes = [];

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

    /// <summary>Initial prepared auxiliary declarations, in parent-before-child order.
    /// The GPU object owner applies each fragment at its existing normal-resource coordinate.
    /// Views retain this owner's storage; the consumer must upload before its next update.</summary>
    public IEnumerable<G1EffectObjectDataPatch> EnumeratePreparedData()
    {
        foreach (var node in nodes.Values) yield return new(node.FileOffset, node.Bytes);
    }

    /// <summary>The only animated field of the currently supported auxiliary dispatches is
    /// elapsed time. Upload four bytes per node, without cloning the module each frame.</summary>
    public IEnumerable<G1EffectObjectDataPatch> EnumerateLiveData()
    {
        foreach (var node in nodes.Values) yield return new(checked(node.FileOffset + 0x10), node.Bytes.AsMemory(0x10, 4));
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
                    throw new NotSupportedException("A transformed auxiliary requires its prepared frame binding.");
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

    private static void Advance(Node node, float seconds)
    {
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
                Vector4 sum = default;
                foreach (Node child in node.Children)
                {
                    Vector4 sample = Sample(child, position);
                    sum = new(sample.X + sum.X, sample.Y + sum.Y,
                        sample.Z + sum.Z, sample.W + sum.W);
                }
                return sum;
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
}
