using System.Buffers.Binary;

namespace San14.Data;

public sealed partial class G1EffectDirectComputePackage
{
    public const uint LinkedParticleBuffer = 9;
    public G1EffectParticleDeclaration? LinkedDeclaration { get; private set; }
    public int LinkedCapacity { get; private set; }

    private bool PrepareLinked(G1EffectParticleDeclaration linked, List<G1EffectDirectBuffer> buffers)
    {
        if (linked.Record.FileOffset != LinkedGenericFileOffset || linked.Record.Type != 10 || !linked.UsesDirectBirth)
            throw new InvalidDataException("Direct linked backing does not match its actual generic reference.");
        var expected = new G1EffectParticleBirthOperation[] { new(0, 9), new(6, 1), new(15, 51), new(15, 52), new(15, 53) };
        if (!linked.BirthOperations.SequenceEqual(expected)) return false;
        ReadOnlyMemory<byte> bytes = linked.Record.Bytes;
        int count = BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[0x204..]);
        int table = checked(0x208 + BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[0x208..]));
        if (count < 0) throw new InvalidDataException("Negative linked direct controller count.");
        for (int index = 0; index < count; index++)
        {
            int entry = checked(table + index * 4);
            int at = checked(entry + BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[entry..]));
            if (BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[(at + 4)..]) != 3) return false;
            int curve = BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[(at + 0x24)..]);
            if (curve is not (0 or 3)) return false;
        }
        int capacity = BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[0xC..]);
        if (capacity <= 0) throw new InvalidDataException("Linked direct generic has no capacity.");
        LinkedDeclaration = linked; LinkedCapacity = capacity;
        buffers.Add(new(LinkedParticleBuffer, checked(capacity * linked.Stride + 24),
            G1EffectDirectBufferUsage.ShaderResource | G1EffectDirectBufferUsage.UnorderedAccess, default));
        string common = ReadOwnedSource("G1EffectDirect.Common.hlsl");
        programs.Add(G1EffectDirectKernel.LinkedBirth, new(G1EffectDirectKernel.LinkedBirth,
            common + ReadOwnedSource("G1EffectDirect.LinkedBirth.hlsl"), "LinkedBirth"));
        programs.Add(G1EffectDirectKernel.LinkedUpdate, new(G1EffectDirectKernel.LinkedUpdate,
            common + ReadOwnedSource("G1EffectDirect.LinkedUpdate.hlsl"), "LinkedUpdate"));
        return true;
    }

    public IReadOnlyList<G1EffectDirectDispatch> InitializeAll()
    {
        var commands = new List<G1EffectDirectDispatch> { Initialize() };
        if (LinkedDeclaration is { } linked)
        {
            byte[] constants = new byte[48];
            Put(constants, 0, LinkedCapacity); Put(constants, 4, checked(LinkedCapacity * linked.Stride + 20));
            linked.Layout.Span[..32].CopyTo(constants.AsSpan(16)); constants[22] = 0; constants[23] = 0;
            commands.Add(new(G1EffectDirectKernel.Initialize, constants, [],
                [new(0, LinkedParticleBuffer)], Groups(LinkedCapacity, 64)));
        }
        return commands.AsReadOnly();
    }

    public G1EffectDirectDispatch LinkedBirth(int count, int mainStartIndex, int sourceByteOffset,
        int definitionByteOffset, int genericByteOffset, int linkedGenericByteOffset, uint randomNumber)
    {
        var linked = LinkedDeclaration ?? throw new InvalidOperationException("No prepared linked generic exists.");
        byte[] constants = new byte[48];
        Put(constants, 0, LinkedCapacity); Put(constants, 4, count); Put(constants, 8, mainStartIndex); Put(constants, 12, sourceByteOffset);
        Put(constants, 16, definitionByteOffset); Put(constants, 20, linkedGenericByteOffset);
        Put(constants, 24, unchecked((int)randomNumber)); Put(constants, 28, Capacity);
        Put(constants, 32, genericByteOffset); Put(constants, 36, checked(LinkedCapacity * linked.Stride + 20));
        linked.Record.Bytes.Span.Slice(0x218, 4).CopyTo(constants.AsSpan(40));
        return new(G1EffectDirectKernel.LinkedBirth, constants,
            [new(0, ObjectBuffer), new(1, ParticleBuffer)], [new(0, LinkedParticleBuffer)], Groups(count, 64));
    }

    public G1EffectDirectDispatch LinkedUpdate(float seconds, int genericByteOffset, int linkedGenericByteOffset)
    {
        if (LinkedDeclaration is null) throw new InvalidOperationException("No prepared linked generic exists.");
        byte[] constants = new byte[32]; Put(constants, 0, LinkedCapacity);
        BinaryPrimitives.WriteSingleLittleEndian(constants.AsSpan(4), seconds);
        Put(constants, 8, linkedGenericByteOffset); Put(constants, 12, Capacity); Put(constants, 16, genericByteOffset);
        return new(G1EffectDirectKernel.LinkedUpdate, constants,
            [new(0, ObjectBuffer), new(1, ParticleBuffer)], [new(0, LinkedParticleBuffer)], Groups(LinkedCapacity, 64));
    }
}
