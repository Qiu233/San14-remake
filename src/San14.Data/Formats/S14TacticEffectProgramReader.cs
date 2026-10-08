using System.Security.Cryptography;

namespace San14.Data.Formats;

/// <summary>One of the two fixed CE tactic instruction tuples. Raw64 and Raw68
/// deliberately retain their native getter identities until their consumer is known.</summary>
public sealed record S14TacticEffectInstruction(int Index, byte Opcode, byte Raw64,
    byte EffectValue, byte Raw68);

public sealed record S14TacticEffectProgram(int TacticSlot, string Sha256,
    IReadOnlyList<S14TacticEffectInstruction> Instructions);

/// <summary>The inspected loader maps CE section12 bytes80..87 to native
/// +62..69, and the four current getter families select matching pair indices.
/// Section13 supplies no effect-program references.</summary>
public static class S14TacticEffectProgramReader
{
    public static S14TacticEffectProgram Read(S14TacticDefinition tactic)
    {
        ArgumentNullException.ThrowIfNull(tactic);
        if (tactic.Slot is < 0 or > 200 || tactic.RawBytes is not { Length: 94 } ||
            tactic.RawParameterBytes is not { Length: 46 } ||
            !tactic.RawBytes.AsSpan(48).SequenceEqual(tactic.RawParameterBytes))
            throw new InvalidDataException("Tactic effect input is not one complete CE section12 record.");
        return Read(tactic.Slot, tactic.RawBytes.AsSpan(80, 8));
    }

    internal static S14TacticEffectProgram Read(int tacticSlot, ReadOnlySpan<byte> pairs)
    {
        if (tacticSlot is < 0 or > 200 || pairs.Length != 8)
            throw new InvalidDataException("Tactic effect instruction pairs differ from the inspected CE profile.");
        S14TacticEffectInstruction[] instructions =
        [
            new(0, pairs[0], pairs[2], pairs[4], pairs[6]),
            new(1, pairs[1], pairs[3], pairs[5], pairs[7])
        ];
        return new(tacticSlot, Convert.ToHexString(SHA256.HashData(pairs)),
            Array.AsReadOnly(instructions));
    }
}
