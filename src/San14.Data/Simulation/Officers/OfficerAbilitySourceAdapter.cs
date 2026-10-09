using San14.Core;

namespace San14.Data;

/// <summary>Decodes the resource format's slot-dependent upper-limit encoding
/// once. Simulation state owns the resulting limits, not serialized bytes.</summary>
internal static class OfficerAbilitySourceAdapter
{
    public static OfficerAbilityState Decode(int record, byte? ordinary, byte? charm, byte? doctrine) =>
        new(ordinary is byte a ? DecodeLimit(a, record) : null,
            charm is byte b ? DecodeLimit(b, record) : null, doctrine);

    public static int DecodeLimit(byte encoded, int record) =>
        (byte)((byte)((encoded << 3) | (encoded >> 5)) ^ (byte)record);
}
