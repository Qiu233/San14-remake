using San14.Core;

namespace San14.Data;

/// <summary>The source word/byte boundary. Unknown bit meanings remain here;
/// the domain knows neither a 16-bit capacity nor physical field offsets.</summary>
public static class OfficerBehaviorSourceAdapter
{
    private const ushort SpecialPerson = 0x4000, MaximumLoyalty = 0x2000, AiRelease = 0x0200;
    private const ushort KnownBehaviorMask = SpecialPerson | MaximumLoyalty | AiRelease;

    public static OfficerBehavior Decode(ushort? flags, byte? marker) => new(
        flags is ushort word ? (word & SpecialPerson) != 0 : null,
        flags is ushort loyal ? (loyal & MaximumLoyalty) != 0 : null,
        flags is ushort release ? (release & AiRelease) != 0 : null,
        flags is ushort residual ? residual & ~KnownBehaviorMask : null, marker);

    public static ushort? EncodeFlags(OfficerBehavior? behavior)
    {
        if (behavior is not { UsesSpecialPersonRules: bool special, PreservesMaximumLoyalty: bool loyal,
            ReleaseAfterAiCapture: bool release, UninterpretedFlags: int residual } ||
            residual > ushort.MaxValue || (residual & KnownBehaviorMask) != 0) return null;
        return (ushort)(residual | (special ? SpecialPerson : 0) |
            (loyal ? MaximumLoyalty : 0) | (release ? AiRelease : 0));
    }

    public static byte? EncodeMarker(OfficerBehavior? behavior) => behavior?.SpecialValidityMarker is int marker && marker <= byte.MaxValue
        ? (byte)marker : null;

    public static OfficerBehavior WithSourceFlags(OfficerBehavior? behavior, ushort? flags) =>
        Decode(flags, null).WithSpecialValidityMarker(behavior?.SpecialValidityMarker);

    // These three source operations are proved writes, but the meanings of
    // their retained bits are not yet established. Do not invent domain flags.
    public static ushort MarkReturn(ushort flags) => (ushort)(flags | 1);
    public static ushort FinishCaptiveProcessing(ushort flags) => (ushort)(flags & 0xFFEF);
    public static ushort ClearFreedomState(ushort flags) => (ushort)(flags & 0xFFDF);
}
