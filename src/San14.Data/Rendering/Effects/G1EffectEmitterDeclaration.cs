using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public readonly record struct G1EffectEmissionKey(float Time, float Value);

public sealed record G1EffectEmitterController(uint Type, int TargetOffset, int TimeOffset,
    int ScaleOffset, ReadOnlyMemory<byte> Bytes, IReadOnlyList<G1EffectEmissionKey> Keys,
    G1EffectScalarLinear? LinearScalar = null);

public sealed record G1EffectEmitterDefinition(G1EffectRecord Declaration, G1EffectRecord Particle);

/// <summary>Declaration of an outer G1FX emitter. Nested type 3 controllers use a
/// different layout and are not outer emitters.</summary>
public sealed record G1EffectEmitterDeclaration(G1EffectRecord Record, uint Flags, float InitialRate,
    uint ShapeType, ReadOnlyMemory<byte> Shape, ReadOnlyMemory<byte> EmissionModifier,
    IReadOnlyList<G1EffectEmitterController> Controllers,
    IReadOnlyList<G1EffectEmitterDefinition> Definitions)
{
    public static G1EffectEmitterDeclaration Read(G1EffectModule module, G1EffectRecord emitter)
    {
        if (emitter.Table != G1EffectTable.Emitter)
            throw new ArgumentException("An outer emitter declaration is required.", nameof(emitter));
        var data = module.Data;
        int root = checked(emitter.FileOffset - module.FileOffset);
        ReadOnlySpan<byte> Span(int offset, int length)
        {
            if (offset < 0 || length < 0 || offset > data.Length - length)
                throw new InvalidDataException("G1FX emitter reference escapes its module.");
            return data.Span.Slice(offset, length);
        }
        int I(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Span(offset, 4));
        uint U(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(Span(offset, 4));
        float F(int offset) => BinaryPrimitives.ReadSingleLittleEndian(Span(offset, 4));
        int Relative(int slot) => checked(slot + I(slot));
        ReadOnlyMemory<byte> RecordAt(int offset)
        {
            int length = checked((int)U(offset));
            if (length < 8) throw new InvalidDataException("Incomplete G1FX emitter child.");
            _ = Span(offset, length);
            return data.Slice(offset, length);
        }
        int[] Table(int countOffset, int pointerOffset)
        {
            int count = checked((int)U(countOffset));
            if (count == 0) return [];
            int table = Relative(pointerOffset);
            _ = Span(table, checked(count * 4));
            var targets = new int[count];
            for (int index = 0; index < count; index++)
                targets[index] = Relative(checked(table + index * 4));
            return targets;
        }
        _ = Span(root, 0x98);
        int end = checked(root + emitter.ByteLength);
        _ = Span(root, emitter.ByteLength);
        ReadOnlyMemory<byte> OwnedChild(int offset)
        {
            var bytes = RecordAt(offset);
            if (offset < root || offset > end - bytes.Length)
                throw new InvalidDataException("G1FX emitter child escapes its declaration.");
            return bytes;
        }

        var controllers = new List<G1EffectEmitterController>();
        foreach (int at in Table(root + 0x7C, root + 0x90))
        {
            var bytes = OwnedChild(at);
            if (bytes.Length < 0x20) throw new InvalidDataException("Incomplete emitter controller.");
            uint type = U(at + 4);
            var keys = new List<G1EffectEmissionKey>();
            G1EffectScalarLinear? scalar = null;
            if (type == 14)
            {
                int count = checked((int)U(at + 0x1C));
                if (count == 0 || count > (bytes.Length - 0x20) / 8)
                    throw new InvalidDataException("Incomplete emitter event keys.");
                for (int index = 0; index < count; index++)
                    keys.Add(new(F(at + 0x20 + index * 8), F(at + 0x24 + index * 8)));
            }
            else if (type == 2)
            {
                scalar = G1EffectScalarLinear.Read(bytes[0x20..]);
            }
            controllers.Add(new(type, I(at + 8), I(at + 0x10),
                I(at + (type == 2 ? 0x18 : 0x14)), bytes, keys, scalar));
        }

        var definitions = new List<G1EffectEmitterDefinition>();
        foreach (int at in Table(root + 0x78, root + 0x88))
        {
            // Definition and generic records are module siblings, not bytes
            // owned by the emitter. Resolve their actual relative references.
            var definition = module.Records.FirstOrDefault(record =>
                record.Table == G1EffectTable.ParticleDefinition &&
                record.FileOffset == module.FileOffset + at)
                ?? throw new InvalidDataException("Emitter references an absent particle definition.");
            if (definition.ByteLength < 0x24)
                throw new InvalidDataException("Incomplete emitter particle definition.");
            int particleAt = Relative(at + 0x20);
            var particle = module.Records.FirstOrDefault(record =>
                record.Table == G1EffectTable.Generic &&
                record.FileOffset == module.FileOffset + particleAt)
                ?? throw new InvalidDataException("Emitter definition references an absent particle.");
            definitions.Add(new(definition, particle));
        }
        int shapeAt = Relative(root + 0x80);
        var shape = OwnedChild(shapeAt);
        ReadOnlyMemory<byte> modifier = I(root + 0x8C) == 0
            ? ReadOnlyMemory<byte>.Empty : OwnedChild(Relative(root + 0x8C));
        return new(emitter, U(root + 4), F(root + 0x70), U(shapeAt + 4), shape,
            modifier, controllers, definitions);
    }
}
