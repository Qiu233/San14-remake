using System.Buffers.Binary;
using System.Collections.Immutable;

namespace San14.Data.AI.Original;

/// <summary>Immutable definitions from the normal installed JBTP resource. These
/// describe the data format; native node support belongs to the policy compiler.</summary>
public sealed record OriginalAiBehaviorDocument(uint Version, uint Reserved,
    ImmutableArray<OriginalAiBehaviorBundle> Bundles);

public sealed record OriginalAiContainerHeader(uint DeclaredSize, uint HeaderSize,
    uint BlockCount, ImmutableArray<uint> Reserved);

public sealed record OriginalAiBehaviorBundle(int Index, uint ResourceKey,
    OriginalAiContainerHeader Header, ImmutableArray<uint> Settings,
    uint TreeSectionReserved, OriginalAiBehaviorTree Tree,
    uint BlackboardSectionReserved, OriginalAiBlackboard Blackboard);

public sealed record OriginalAiBehaviorTree(uint Version, uint Key, uint Reserved,
    ImmutableArray<OriginalAiBehaviorNode> Nodes)
{
    public OriginalAiBehaviorNode Root => Nodes[0];
}

public sealed record OriginalAiBehaviorNode(int Index, uint Flags, uint TypeId,
    uint? OptionalId, int ParentIndex, uint Metadata,
    ImmutableArray<uint> Arguments, ImmutableArray<byte> NameBytes,
    ImmutableArray<uint> ExtraArguments, ImmutableArray<int> ChildIndices)
{
    public bool IsNative => (Flags & 1) != 0;
    public uint DeclaredChildCount => Metadata;
}

/// <summary>The header's three configuration bytes and all serialized 32-byte
/// initial values are retained, including encodings not consumed by a policy.</summary>
public sealed record OriginalAiBlackboard(OriginalAiContainerHeader Header,
    ImmutableArray<uint> Settings, uint ValuesSectionReserved,
    byte Configuration0, byte ValueEncoding, byte Configuration2, byte Reserved,
    ImmutableArray<OriginalAiBlackboardValue> Values);

public sealed record OriginalAiBlackboardValue(ImmutableArray<byte> Bytes);

/// <summary>Strict bounded reader for JBTP version 2, TB1G0000, KTBT, and BB1G0000.
/// Reads resource bytes only. It neither resolves native constructors nor runs AI.</summary>
public static class OriginalAiBehaviorReader
{
    public static OriginalAiBehaviorDocument Read(ReadOnlySpan<byte> bytes)
    {
        var file = new Cursor(bytes, "JBTP");
        file.Magic("JBTP"u8);
        uint version = file.UInt32();
        if (version != 2) throw file.Error($"Unsupported JBTP version {version}.");
        uint count = file.UInt32();
        uint reserved = file.UInt32();
        if (count != 4) throw file.Error($"Expected four AI bundles, found {count}.");
        var directory = new (int Offset, int Length, uint Key)[4];
        for (int index = 0; index < directory.Length; index++)
        {
            int offset = file.Int32();
            int length = file.Int32();
            uint key = file.UInt32();
            if (offset < 64 || length < 32 || offset > bytes.Length - length)
                throw file.Error($"Bundle {index} has an out-of-range extent.");
            directory[index] = (offset, length, key);
        }
        // The installed directory covers the file completely. Reject overlaps,
        // gaps and trailing bytes rather than silently discarding unknown data.
        int expectedOffset = 64;
        foreach (var item in directory.OrderBy(item => item.Offset))
        {
            if (item.Offset != expectedOffset)
                throw file.Error("Bundle directory overlaps or leaves unparsed bytes.");
            expectedOffset = checked(item.Offset + item.Length);
        }
        if (expectedOffset != bytes.Length) throw file.Error("Unparsed file trailer.");
        var bundles = ImmutableArray.CreateBuilder<OriginalAiBehaviorBundle>(4);
        for (int index = 0; index < directory.Length; index++)
        {
            var item = directory[index];
            bundles.Add(ReadBundle(bytes.Slice(item.Offset, item.Length), index, item.Key));
        }
        return new(version, reserved, bundles.MoveToImmutable());
    }

    private static OriginalAiBehaviorBundle ReadBundle(ReadOnlySpan<byte> bytes, int index, uint key)
    {
        var reader = new Cursor(bytes, $"bundle {index}");
        var header = ReadHeader(ref reader, "TB1G0000"u8);
        if (header.BlockCount != 3) throw reader.Error("Unsupported TB1G block count.");
        var settings = ReadSettings(ref reader);
        var treeSection = reader.Section("ITBG0000"u8, out uint treeReserved);
        var tree = ReadTree(treeSection, index);
        var blackboardSection = reader.Section("IBBG0000"u8, out uint blackboardReserved);
        var blackboard = ReadBlackboard(blackboardSection, index);
        reader.End();
        return new(index, key, header, settings, treeReserved, tree, blackboardReserved, blackboard);
    }

    private static OriginalAiContainerHeader ReadHeader(ref Cursor reader, scoped ReadOnlySpan<byte> magic)
    {
        reader.Magic(magic);
        uint size = reader.UInt32();
        uint headerSize = reader.UInt32();
        uint blocks = reader.UInt32();
        var reserved = reader.Words(3);
        if (size != reader.Length || headerSize != 32)
            throw reader.Error("Invalid container size or header extent.");
        return new(size, headerSize, blocks, reserved);
    }

    private static ImmutableArray<uint> ReadSettings(ref Cursor reader)
    {
        var span = reader.Section("IZSG0000"u8, out uint reserved);
        if (reserved != 0) throw reader.Error("Unsupported IZSG section flags.");
        var settings = new Cursor(span, "IZSG");
        var words = settings.Words(settings.Length / 4);
        settings.End();
        return words;
    }

    private static OriginalAiBehaviorTree ReadTree(ReadOnlySpan<byte> bytes, int bundle)
    {
        var reader = new Cursor(bytes, $"bundle {bundle} KTBT");
        reader.Magic("KTBT"u8);
        uint version = reader.UInt32();
        uint key = reader.UInt32();
        uint reserved = reader.UInt32();
        if (version != 1) throw reader.Error($"Unsupported KTBT version {version}.");
        int count = reader.Count(20);
        if (count == 0) throw reader.Error("A behavior tree must have a root.");
        var nodes = new OriginalAiBehaviorNode[count];
        var children = new List<int>[count];
        for (int index = 0; index < count; index++) children[index] = [];
        for (int index = 0; index < count; index++)
        {
            uint flags = reader.UInt32();
            if ((flags & ~15u) != 0) throw reader.Error($"Unknown node flags 0x{flags:X}.");
            uint type = reader.UInt32();
            uint? id = (flags & 4) != 0 ? reader.UInt32() : null;
            int parent = reader.Int32();
            uint metadata = reader.UInt32();
            int argumentCount = reader.Count(4);
            var arguments = reader.Words(argumentCount);
            var name = ImmutableArray<byte>.Empty;
            if ((flags & 2) != 0)
            {
                int length = reader.Count(1);
                if (length > 255) throw reader.Error("Node name exceeds the source format bound.");
                name = reader.Bytes(length);
            }
            var extra = (flags & 8) != 0 ? reader.Words(reader.Count(4)) : ImmutableArray<uint>.Empty;
            if (index == 0 ? parent != -1 : parent < 0 || parent >= index)
                throw reader.Error($"Invalid parent {parent} for node {index}.");
            if (parent >= 0) children[parent].Add(index);
            nodes[index] = new(index, flags, type, id, parent, metadata, arguments, name, extra, []);
        }
        reader.End();
        var result = ImmutableArray.CreateBuilder<OriginalAiBehaviorNode>(count);
        for (int index = 0; index < count; index++)
        {
            // This serialized field is the source's declared child count.
            if (nodes[index].Metadata != children[index].Count)
                throw reader.Error($"Child count mismatch at node {index}.");
            result.Add(nodes[index] with { ChildIndices = children[index].ToImmutableArray() });
        }
        return new(version, key, reserved, result.MoveToImmutable());
    }

    private static OriginalAiBlackboard ReadBlackboard(ReadOnlySpan<byte> bytes, int bundle)
    {
        var reader = new Cursor(bytes, $"bundle {bundle} blackboard");
        var header = ReadHeader(ref reader, "BB1G0000"u8);
        if (header.BlockCount != 1) throw reader.Error("Unsupported BB1G block count.");
        var settings = ReadSettings(ref reader);
        if (settings.Length != 1 || settings[0] != 1)
            throw reader.Error("Unsupported blackboard settings.");
        var valuesSpan = reader.Section("IBBG0000"u8, out uint valuesReserved);
        var valuesReader = new Cursor(valuesSpan, $"bundle {bundle} blackboard values");
        byte configuration0 = valuesReader.Byte();
        byte encoding = valuesReader.Byte();
        byte configuration2 = valuesReader.Byte();
        byte reserved = valuesReader.Byte();
        int count = valuesReader.Count(32);
        var values = ImmutableArray.CreateBuilder<OriginalAiBlackboardValue>(count);
        for (int index = 0; index < count; index++) values.Add(new(valuesReader.Bytes(32)));
        valuesReader.End();
        reader.End();
        return new(header, settings, valuesReserved, configuration0, encoding,
            configuration2, reserved, values.MoveToImmutable());
    }

    private ref struct Cursor(ReadOnlySpan<byte> bytes, string context)
    {
        private readonly ReadOnlySpan<byte> _bytes = bytes;
        private readonly string _context = context;
        private int _offset = 0;
        public int Length => _bytes.Length;
        public InvalidDataException Error(string message) =>
            new($"{_context} at byte {_offset}: {message}");
        private ReadOnlySpan<byte> Take(int count)
        {
            if (count < 0 || count > _bytes.Length - _offset)
                throw Error($"Truncated field ({count} bytes requested).");
            var result = _bytes.Slice(_offset, count);
            _offset += count;
            return result;
        }
        public void Magic(scoped ReadOnlySpan<byte> magic)
        {
            if (!Take(magic.Length).SequenceEqual(magic)) throw Error("Unexpected format signature.");
        }
        public byte Byte() => Take(1)[0];
        public uint UInt32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));
        public int Int32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));
        public int Count(int minimumElementSize)
        {
            int count = Int32();
            if (count < 0 || count > (_bytes.Length - _offset) / minimumElementSize)
                throw Error($"Invalid element count {count}.");
            return count;
        }
        public ImmutableArray<uint> Words(int count)
        {
            if (count < 0 || count > (_bytes.Length - _offset) / 4)
                throw Error($"Invalid word count {count}.");
            var result = ImmutableArray.CreateBuilder<uint>(count);
            for (int index = 0; index < count; index++) result.Add(UInt32());
            return result.MoveToImmutable();
        }
        public ImmutableArray<byte> Bytes(int count) => ImmutableArray.CreateRange(Take(count).ToArray());
        public ReadOnlySpan<byte> Section(scoped ReadOnlySpan<byte> magic, out uint reserved)
        {
            Magic(magic);
            int length = Int32();
            reserved = UInt32();
            if (length < 16) throw Error("Invalid section size.");
            return Take(length - 16);
        }
        public void End()
        {
            if (_offset != _bytes.Length) throw Error($"Unparsed bytes: {_bytes.Length - _offset}.");
        }
    }
}

