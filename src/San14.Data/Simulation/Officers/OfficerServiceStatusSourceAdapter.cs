using San14.Core;

namespace San14.Data;

/// <summary>Only the source profile has a byte-sized service identity. Domain
/// identities outside that representation are unresolved, never truncated.</summary>
public static class OfficerServiceStatusSourceAdapter
{
    public static byte? Encode(OfficerServiceStatus? status) => status?.KindId is >= 0 and <= byte.MaxValue
        ? (byte)status.KindId : null;
    public static OfficerServiceStatus? Decode(byte? status) => status is byte known ? new(known) : null;
}
