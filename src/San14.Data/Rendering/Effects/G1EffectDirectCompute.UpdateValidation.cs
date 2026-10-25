using System.Buffers.Binary;

namespace San14.Data;

/// <summary>One-time resource capability check for the owned direct Update0/1
/// controller kernel. It does not certify motion, postprocess or GPU execution.</summary>
public static class G1EffectDirectUpdateValidation
{
    public static string? GetMissing(G1EffectParticleDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        if (declaration.Record.Type is not (0 or 1))
            return $"Direct controller update for generic {declaration.Record.Type} is not implemented.";
        if (declaration.Stride < 112)
            throw new InvalidDataException("Direct Update0/1 particle state is incomplete.");
        ReadOnlyMemory<byte> bytes = declaration.Record.Bytes;
        if (I(bytes, 0x214) != 0)
            return "Direct secondary controller update requires its separate controller consumer.";
        int count = I(bytes, 0x208), table = checked(0x20C + I(bytes, 0x20C));
        if (count < 0) throw new InvalidDataException("Negative direct controller count.");
        for (int index = 0; index < count; index++)
        {
            int slot = checked(table + index * 4), at = checked(slot + I(bytes, slot)), size = I(bytes, at);
            Require(bytes.Length, at, size);
            string? missing = Controller(bytes.Slice(at, size), declaration.Stride, true);
            if (missing is not null) return missing;
        }
        return null;
    }

    private static string? Controller(ReadOnlyMemory<byte> bytes, int stride, bool allowWrapper)
    {
        int kind = I(bytes, 4);
        if (kind is >= 0 and <= 3)
        {
            Field(bytes, 8, stride); Field(bytes, 0x10, stride);
            if (kind is 1 or 3) Field(bytes, 0x14, stride);
            if (kind is 2 or 3) Field(bytes, 0x18, stride);
            int curve = I(bytes, 0x24);
            if (curve is not (0 or 1 or 2 or 3 or 5 or 6 or 8 or 9 or 11))
                return $"Direct scalar controller curve {curve} is not implemented.";
            int size = I(bytes, 0x20);
            Require(bytes.Length, 0x20, size);
            int required = curve is 0 or 1 or 2 ? 0x20 : 0x2C;
            if (size < required) throw new InvalidDataException("Incomplete direct scalar curve declaration.");
            return null;
        }
        if (!allowWrapper || kind is not (7 or 8 or 13))
            return $"Direct controller {kind} is not implemented.";
        if (kind == 13) Field(bytes, 8, stride);
        else if (I(bytes, 8) is < 48 or > 51)
            throw new InvalidDataException("Direct packed-color controller is outside its source color word.");
        int nestedSize = I(bytes, 0x10);
        Require(bytes.Length, 0x10, nestedSize);
        return Controller(bytes.Slice(0x10, nestedSize), stride, false);
    }

    private static void Field(ReadOnlyMemory<byte> bytes, int at, int stride)
    {
        int field = I(bytes, at);
        if (field < 0 || field > stride - 4 || (field & 3) != 0)
            throw new InvalidDataException("Direct scalar controller references an absent or unaligned particle field.");
    }
    private static int I(ReadOnlyMemory<byte> bytes, int at)
    { Require(bytes.Length, at, 4); return BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[at..]); }
    private static void Require(int size, int at, int length)
    {
        if (at < 0 || length < 4 || at > size - length)
            throw new InvalidDataException("Direct controller escapes its normal resource declaration.");
    }
}
