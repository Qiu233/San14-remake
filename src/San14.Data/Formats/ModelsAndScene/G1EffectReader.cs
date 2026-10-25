using System.Buffers.Binary;

namespace San14.Data.Formats;

public enum G1EffectTable { Emitter, ParticleDefinition, Generic, Auxiliary, Controller,
    NestedParticleDefinition, NestedAuxiliary }

public sealed record G1EffectRecord(int FileOffset, int ByteLength, G1EffectTable Table, int Index,
    uint? Type, uint Flags, int? ConfigurationOffset, ReadOnlyMemory<byte> Bytes,
    IReadOnlyList<G1EffectRecord> Children);

public sealed record G1EffectModule(int Index, uint Type, int FileOffset, ReadOnlyMemory<byte> Prefix,
    ReadOnlyMemory<byte> Data, uint SerializedVersion, int PrewarmSteps, float PrewarmStepSeconds,
    ushort StopMode, ushort StopKind, float StopSeconds, IReadOnlyList<G1EffectRecord> Records);

public sealed record G1EffectSystem(int Index, int FileOffset, ReadOnlyMemory<byte> Matrix,
    ReadOnlyMemory<byte> ClockControl, IReadOnlyList<G1EffectModule> Modules);

public sealed record G1EffectAsset(uint Flags, uint Role, uint Bank, ReadOnlyMemory<byte> Bytes,
    IReadOnlyList<G1EffectSystem> Systems);

/// <summary>Reads installed XF1G5200 declarations. Records retain their bounded
/// original bytes for runtime initialization; parsing neither emits particles nor
/// substitutes a captured draw stream for an effect instance.</summary>
public static class G1EffectReader
{
    public static G1EffectAsset Read(ReadOnlyMemory<byte> bytes)
    {
        var reader = new Reader(bytes);
        return reader.Read();
    }

    private sealed class Reader(ReadOnlyMemory<byte> bytes)
    {
        internal G1EffectAsset Read()
        {
            if (bytes.Length < 24 || !bytes.Span[..8].SequenceEqual("XF1G5200"u8) || N(8) != bytes.Length)
                throw new InvalidDataException("Unsupported or incomplete G1FX asset.");
            int count = N(0x10);
            Range(0x14, checked(count * 4), 0, bytes.Length);
            var systems = new List<G1EffectSystem>(count);
            for (int index = 0; index < count; index++)
            {
                int at = N(0x14 + index * 4);
                Range(at, 84, 0, bytes.Length);
                int moduleCount = N(at + 80), cursor = at + 84;
                var modules = new List<G1EffectModule>();
                for (int module = 0; module < moduleCount; module++)
                {
                    Range(cursor, 84, at, bytes.Length);
                    uint type = U(cursor);
                    if (type != 1) throw new InvalidDataException($"Unsupported G1FX module type {type}.");
                    int prefix = cursor + 4, blob = prefix + 80;
                    Range(blob, 0x48, at, bytes.Length);
                    int length = N(blob);
                    if (length < 0x48) throw new InvalidDataException("Incomplete G1FX module header.");
                    int end = checked(blob + length);
                    Range(blob, length, at, bytes.Length);
                    var records = new List<G1EffectRecord>();
                    var seen = new HashSet<int>();
                    G1EffectTable[] tables = [G1EffectTable.Emitter, G1EffectTable.ParticleDefinition,
                        G1EffectTable.Generic, G1EffectTable.Auxiliary];
                    for (int table = 0; table < tables.Length; table++)
                    {
                        int size = N(blob + 8 + table * 4), slot = blob + 0x18 + table * 4;
                        foreach (var (row, offset) in Table(slot, size, blob, end))
                            records.Add(Record(offset, tables[table], row, blob, end, seen));
                    }
                    modules.Add(new(module, type, blob, bytes.Slice(prefix, 80), bytes.Slice(blob, length),
                        U(blob + 4), N(blob + 0x40), F(blob + 0x44), H(blob + 0x28), H(blob + 0x2A),
                        F(blob + 0x2C), records));
                    cursor = end;
                }
                systems.Add(new(index, at, bytes.Slice(at, 64), bytes.Slice(at + 64, 16), modules));
            }
            uint flags = U(0xC), roleBank = (flags >> 16) & 0x3F;
            return new(flags, roleBank & 15, (roleBank >> 4) & 3, bytes, systems);
        }

        private G1EffectRecord Record(int at, G1EffectTable table, int index, int lower, int end,
            HashSet<int> seen)
        {
            if (!seen.Add(at)) throw new InvalidDataException("Duplicate or cyclic G1FX record target.");
            Range(at, 12, lower, end);
            int length = N(at);
            if (length < 12) throw new InvalidDataException("Incomplete G1FX record header.");
            Range(at, length, lower, end);
            int recordEnd = checked(at + length);
            uint? type = table == G1EffectTable.Generic ? U(at + 4) : null;
            uint flags = U(at + (table is G1EffectTable.Emitter or G1EffectTable.ParticleDefinition ? 4 : 8));
            int? configuration = null;
            if (type.HasValue)
            {
                Range(at + 0x14, 4, at, recordEnd);
                configuration = checked(at + 0x14 + I(at + 0x14));
                Range(configuration.Value, 4, at, recordEnd);
            }
            var children = new List<G1EffectRecord>();
            if (type == 3)
            {
                Range(at, 0x238, at, recordEnd);
                G1EffectTable[] tables = [G1EffectTable.Controller, G1EffectTable.NestedParticleDefinition,
                    G1EffectTable.Generic, G1EffectTable.NestedAuxiliary];
                for (int childTable = 0; childTable < tables.Length; childTable++)
                    foreach (var (row, child) in Table(at + 0x228 + childTable * 4,
                        N(at + 0x218 + childTable * 4), at, recordEnd))
                        children.Add(Record(child, tables[childTable], row, lower, recordEnd, seen));
            }
            return new(at, length, table, index, type, flags, configuration, bytes.Slice(at, length), children);
        }

        private IEnumerable<(int Index, int Offset)> Table(int slot, int count, int lower, int end)
        {
            Range(slot, 4, lower, end);
            int at = checked(slot + I(slot));
            Range(at, checked(count * 4), lower, end);
            for (int index = 0; index < count; index++)
            {
                int element = at + index * 4, record = checked(element + I(element));
                Range(record, 4, lower, end);
                yield return (index, record);
            }
        }

        private uint U(int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Span.Slice(at, 4));
        private int I(int at) => BinaryPrimitives.ReadInt32LittleEndian(bytes.Span.Slice(at, 4));
        private ushort H(int at) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.Span.Slice(at, 2));
        private float F(int at) => BinaryPrimitives.ReadSingleLittleEndian(bytes.Span.Slice(at, 4));
        private int N(int at) => checked((int)U(at));
        private void Range(int at, int length, int lower, int end)
        {
            if (lower < 0 || end > bytes.Length || at < lower || length < 0 || at > end - length)
                throw new InvalidDataException("G1FX data escapes its owning record.");
        }
    }
}
