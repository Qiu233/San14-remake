using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>One real definition-to-generic link. Count expressions and position
/// distributions are distinct expression families, not emitter scalar curves.</summary>
public sealed record G1EffectParticleDefinition(G1EffectRecord Record,
    G1EffectParticleDeclaration Particle, uint Flags, ReadOnlyMemory<byte> CountExpression,
    ReadOnlyMemory<byte> Distribution)
{
    public uint CountOpcode => BinaryPrimitives.ReadUInt32LittleEndian(CountExpression.Span);
    public uint? DistributionOpcode => Distribution.IsEmpty ? null :
        BinaryPrimitives.ReadUInt32LittleEndian(Distribution.Span);

    public static G1EffectParticleDefinition Read(G1EffectModule module, G1EffectRecord definition)
    {
        ArgumentNullException.ThrowIfNull(module); ArgumentNullException.ThrowIfNull(definition);
        if (definition.Table is not (G1EffectTable.ParticleDefinition or G1EffectTable.NestedParticleDefinition))
            throw new ArgumentException("A particle definition is required.", nameof(definition));
        var bytes = definition.Bytes;
        int I(int at)
        {
            if (at < 0 || at > bytes.Length - 4)
                throw new InvalidDataException("Particle definition reference escapes its record.");
            return BinaryPrimitives.ReadInt32LittleEndian(bytes.Span.Slice(at, 4));
        }
        int target = checked(definition.FileOffset + 0x20 + I(0x20));
        var particle = Find(module.Records, target) ??
            throw new InvalidDataException("Particle definition references an absent generic.");
        // Expression containers carry a twelve-byte declaration prefix.
        int countAt = checked(0x30 + I(0x24));
        if (countAt < 0 || countAt > bytes.Length - 8)
            throw new InvalidDataException("Incomplete particle count expression.");
        ReadOnlyMemory<byte> distribution = default;
        if (I(0x28) != 0)
        {
            int at = checked(0x34 + I(0x28));
            if (at < 0 || at > bytes.Length - 4)
                throw new InvalidDataException("Incomplete particle distribution expression.");
            distribution = bytes[at..];
        }
        return new(definition, G1EffectParticleDeclaration.Read(particle), definition.Flags,
            bytes[countAt..], distribution);
    }

    /// <summary>All four supported installed effects use literal count ASTs.
    /// Rounding still consumes one shared random draw for an integral literal.
    /// This returns attempts, not allocated or initialized live particles.</summary>
    public int SampleCount(G1EffectRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (CountOpcode != 0)
            throw new NotSupportedException($"Particle count expression opcode {CountOpcode} is not implemented.");
        float value = BinaryPrimitives.ReadSingleLittleEndian(CountExpression.Span[4..]);
        return random.RoundCount(value);
    }

    public float EvaluateScalar(int referenceField, ReadOnlyMemory<byte> sourceContext, G1EffectRandom random)
    {
        var bytes = Record.Bytes;
        if (referenceField < 0 || referenceField > bytes.Length - 4)
            throw new InvalidDataException("Particle expression field is outside its definition.");
        int cursor = checked(referenceField + 12 + BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[referenceField..]));
        return G1EffectBirthExpression.Evaluate(bytes, ref cursor, sourceContext, random);
    }

    private static G1EffectRecord? Find(IReadOnlyList<G1EffectRecord> records, int offset)
    {
        foreach (var record in records)
        {
            if (record.FileOffset == offset && record.Table == G1EffectTable.Generic) return record;
            if (Find(record.Children, offset) is { } nested) return nested;
        }
        return null;
    }
}
