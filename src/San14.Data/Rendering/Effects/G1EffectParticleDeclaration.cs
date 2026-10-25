using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public readonly record struct G1EffectParticleBirthOperation(short Opcode, short Subcode);

/// <summary>The generic's actual slot layout and ordered birth program. Neither
/// the layout template nor this declaration is an initialized live particle.</summary>
public sealed record G1EffectParticleDeclaration(G1EffectRecord Record, uint Flags,
    int Stride, int InitialCapacity, ReadOnlyMemory<byte> Layout,
    IReadOnlyList<G1EffectParticleBirthOperation> BirthOperations)
{
    public bool UsesDirectBirth => (Flags & 0x200) != 0;
    public bool UsesExpandableBlocks => (Flags & 0x180) != 0;
    public bool UsesSharedArenaGrowth => (Flags & 0x100) != 0;

    public static G1EffectParticleDeclaration Read(G1EffectRecord generic)
    {
        ArgumentNullException.ThrowIfNull(generic);
        if (generic.Table != G1EffectTable.Generic || generic.Type is null)
            throw new ArgumentException("A generic particle declaration is required.", nameof(generic));
        var data = generic.Bytes;
        int I(int at) => BinaryPrimitives.ReadInt32LittleEndian(Slice(at, 4));
        ReadOnlySpan<byte> Slice(int at, int length)
        {
            if (at < 0 || length < 0 || at > data.Length - length)
                throw new InvalidDataException("Particle declaration reference escapes its record.");
            return data.Span.Slice(at, length);
        }
        _ = Slice(0, 0x20);
        int layout = checked(0x14 + I(0x14));
        int stride = I(layout);
        if (stride < 32) throw new InvalidDataException("Particle slot cannot contain its source header.");
        _ = Slice(layout, stride);
        int capacity = checked(I(0xC) * I(0x10));
        if (capacity < 0) throw new InvalidDataException("Particle capacity is negative.");
        int program = checked(0x1C + I(0x1C)), count = I(program);
        _ = Slice(program + 4, checked(count * 4));
        var operations = new G1EffectParticleBirthOperation[count];
        for (int index = 0; index < count; index++)
        {
            var op = Slice(program + 4 + index * 4, 4);
            operations[index] = new(BinaryPrimitives.ReadInt16LittleEndian(op),
                BinaryPrimitives.ReadInt16LittleEndian(op[2..]));
        }
        return new(generic, generic.Flags, stride, capacity, data.Slice(layout, stride),
            Array.AsReadOnly(operations));
    }

    /// <summary>Block size requested by the source shared allocator, including
    /// its header. This computes size; it does not prove available arena storage.</summary>
    public int RequiredExpandableBlockBytes()
    {
        int needed = checked(Stride * InitialCapacity + 48), size = 1024;
        while (size < needed) size = checked(size * 2);
        return size;
    }
}
