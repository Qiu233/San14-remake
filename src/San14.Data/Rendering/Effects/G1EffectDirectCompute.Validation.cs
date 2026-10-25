using System.Buffers.Binary;

namespace San14.Data;

internal static class G1EffectDirectValidation
{
    internal static string? BirthProgram(G1EffectParticleDeclaration declaration)
    {
        foreach (var operation in declaration.BirthOperations)
        {
            bool supported = operation.Opcode switch
            {
                0 => operation.Subcode == 0,
                1 or 2 => operation.Subcode == 0,
                4 => operation.Subcode == 27, // Original GPU dispatch has no parent-pointer writer.
                5 => operation.Subcode is 0 or 1 or 2 or 4,
                6 => operation.Subcode == 2,
                10 => operation.Subcode == 3,
                15 => operation.Subcode is 48 or 49 or 54,
                18 => operation.Subcode == 1,
                _ => false
            };
            if (!supported) return $"Direct birth operation {operation.Opcode}/{operation.Subcode} is not implemented.";
        }
        return null;
    }

    internal static void BirthCommand(G1EffectDirectBirthCommand command)
    {
        var source = command.SourceContext;
        int shapeAt = checked(0x80 + I(source, 0x80));
        Shape(source, shapeAt);
        // Current direct sources are outer emitters with an actual prepared transform context.
        _ = I(source, 0x16C);
        var definition = command.Definition.Record.Bytes;
        if (I(definition, 0x28) != 0)
            throw new NotSupportedException("Direct definition matrix distribution is not implemented.");
        Shape(definition, checked(0x2C + I(definition, 0x2C)));
        foreach (int field in new[] { 0x40, 0x44, 0x48, 0x4C, 0x5C, 0x60, 0x64, 0x68 })
        {
            int cursor = checked(field + I(definition, field) + 12);
            Expression(definition, ref cursor, source.Length, 0);
        }
    }

    private static void Shape(ReadOnlyMemory<byte> bytes, int at)
    {
        int size = I(bytes, at), type = I(bytes, at + 4);
        if (type is not (0 or 3 or 6 or 10)) throw new NotSupportedException($"Direct birth shape {type} is not implemented.");
        Require(bytes, at, size);
        int needed = type switch { 0 => 0x80, 3 => 0x70, 6 => 0x60, _ => 0x50 };
        if (size < needed) throw new InvalidDataException("Incomplete direct birth shape.");
    }

    private static void Expression(ReadOnlyMemory<byte> bytes, ref int cursor, int sourceSize, int depth)
    {
        if (depth >= 32) throw new NotSupportedException("Direct scalar expression exceeds its supported nesting depth.");
        int op = I(bytes, cursor); cursor = checked(cursor + 4);
        if (op is 0 or 1)
        {
            int operand = I(bytes, cursor); cursor = checked(cursor + 4);
            if (op == 1 && (operand < 0 || operand > sourceSize - 4))
                throw new InvalidDataException("Direct scalar source field escapes its live owner.");
        }
        else if (op == 2) { _ = I(bytes, cursor + 4); cursor = checked(cursor + 8); }
        else if (op is 12 or 13 or 15)
        { Expression(bytes, ref cursor, sourceSize, depth + 1); Expression(bytes, ref cursor, sourceSize, depth + 1); }
        else if (op == 14) { Expression(bytes, ref cursor, sourceSize, depth + 1); _ = I(bytes, cursor); cursor = checked(cursor + 4); }
        else if (op == 32)
        {
            for (int i = 0; i < 3; i++)
            {
                int field = I(bytes, cursor); cursor = checked(cursor + 4);
                if (field < 0 || field > sourceSize - 4) throw new InvalidDataException("Direct scalar field escapes its live owner.");
            }
        }
        else throw new NotSupportedException($"Direct scalar expression {op} is not implemented.");
    }

    internal static string? Motion(G1EffectParticleDeclaration declaration, ReadOnlyMemory<byte> image,
        int origin, out bool needsNoise)
    {
        needsNoise = false;
        var bytes = declaration.Record.Bytes;
        int offset = I(bytes, 0x204);
        if (offset == 0) return null;
        int motion = checked(0x204 + offset);
        if (I(bytes, motion + 4) != 0) return "Direct motion wrapper requires its separate GPU implementation.";
        int count = I(bytes, motion + 0x10);
        if (count < 0) throw new InvalidDataException("Negative direct motion force count.");
        for (int index = 0; index < count; index++)
        {
            int slot = checked(motion + 0x14 + index * 4), at = checked(slot + I(bytes, slot)), kind = I(bytes, at + 4);
            if (kind is 0 or 2 or 15) continue;
            if (kind != 3) return $"Direct GPU force {kind} is not implemented.";
            int sampler = checked(at + 0x10);
            if (I(bytes, sampler + 4) != 0) return "Direct force sampler requires its separate GPU consumer.";
            int field = checked(sampler + 0x10), target = checked(declaration.Record.FileOffset - origin + field + I(bytes, field));
            string? missing = Auxiliary(image, target, 0, ref needsNoise);
            if (missing is not null) return missing;
        }
        return null;
    }

    private static string? Auxiliary(ReadOnlyMemory<byte> image, int at, int depth, ref bool noise)
    {
        if (depth >= 8) return "Direct auxiliary hierarchy exceeds its supported depth.";
        int size = I(image, at), type = I(image, at + 4); Require(image, at, size);
        if (I(image, at + 0x14) != 0 || I(image, at + 0x1C) != 0)
            return "Direct animated auxiliary parameters require their controller consumer.";
        if (type == 0)
        {
            if (I(image, at + 0x20) != 0) return "Direct transformed auxiliary requires its prepared frame consumer.";
            int count = I(image, at + 0x2C);
            if (count < 0) throw new InvalidDataException("Negative direct auxiliary child count.");
            for (int i = 0; i < count; i++)
            {
                int field = checked(at + 0x30 + i * 4);
                string? missing = Auxiliary(image, checked(field + I(image, field)), depth + 1, ref noise);
                if (missing is not null) return missing;
            }
            return null;
        }
        if (type == 6) { Require(image, at, 0x68); return null; }
        if (type != 7) return $"Direct auxiliary field {type} is not implemented.";
        Require(image, at, 0x70);
        int mask = I(image, at + 0x6C);
        if ((mask & 15) == 0 || (mask & ~15) != 0) return "Direct vector noise requires one through four coordinates.";
        noise = true; return null;
    }
    private static int I(ReadOnlyMemory<byte> bytes, int at)
    { Require(bytes, at, 4); return BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[at..]); }
    private static void Require(ReadOnlyMemory<byte> bytes, int at, int count)
    { if (at < 0 || count < 0 || at > bytes.Length - count) throw new InvalidDataException("Direct GPU declaration escapes its normal resource."); }
}
