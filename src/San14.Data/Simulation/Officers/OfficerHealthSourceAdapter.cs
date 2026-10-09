using San14.Core;

namespace San14.Data;

/// <summary>The historical current-person profile encodes health as one byte.
/// Domain health has no such width. Unrepresentable conditions stay unresolved
/// for consumers of that profile rather than being truncated.</summary>
public static class OfficerHealthSourceAdapter
{
    public static byte? Encode(OfficerHealth? health) => health?.StateId is >= 0 and <= byte.MaxValue
        ? (byte)health.StateId : null;

    public static OfficerHealth? Decode(byte? identity) => identity is byte known ? new(known) : null;

    public static OfficerHealth? Read(SourceWorldContext context, int physical) => context.ReadBoundOfficerHealth(physical);

    public static byte? ReadByte(SourceWorldContext context, int physical) => Encode(Read(context, physical));
}
