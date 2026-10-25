namespace San14.Data;

/// <summary>The original four-byte effect manager declaration. The last two
/// bytes are retained without assigning them an unproven meaning.</summary>
public readonly record struct G1EffectInfoRow(byte Flags, byte QualityThreshold,
    byte Reserved2, byte Reserved3)
{
    public bool UsesFe4 => (Flags & 8) != 0;
    public bool UsesDedicatedPool => (Flags & 1) != 0;

    public bool AcceptsQuality(float sourceQuality) =>
        !UsesDedicatedPool || (Flags & 4) != 0 || !(QualityThreshold < sourceQuality);
}

public sealed record LoadedMapEffectInfo(ReadOnlyMemory<byte> Bytes, SourceProvenance Source)
{
    public const int RowCount = 137;

    public G1EffectInfoRow ReadRow(int kind)
    {
        if ((uint)kind >= RowCount) throw new ArgumentOutOfRangeException(nameof(kind));
        if (Bytes.Length != RowCount * 4)
            throw new InvalidDataException("Incomplete effect manager declaration table.");
        var row = Bytes.Span.Slice(kind * 4, 4);
        return new(row[0], row[1], row[2], row[3]);
    }
}

public sealed partial class GameResourceRepository
{
    /// <summary>Loads the normal installed or overridden declaration once for
    /// the map resource owner. Callers retain it with their loaded effects.</summary>
    public LoadedMapEffectInfo LoadEffectInfo()
    {
        var (bytes, source) = ReadSource("0000/effect_info.bin");
        if (bytes.Length != LoadedMapEffectInfo.RowCount * 4)
            throw new InvalidDataException("Effect manager requires 137 complete four-byte rows.");
        return new(bytes, source);
    }
}
