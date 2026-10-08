using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

namespace San14.Data;

public sealed record OriginalMapTextPreset(OriginalAreaMarkerColor LT, OriginalAreaMarkerColor RT,
    OriginalAreaMarkerColor LB, OriginalAreaMarkerColor RB, OriginalAreaMarkerColor StrokeColor,
    int StrokeWidth, OriginalAreaMarkerColor OffsetLT, OriginalAreaMarkerColor OffsetRT,
    OriginalAreaMarkerColor OffsetLB, OriginalAreaMarkerColor OffsetRB, int OffsetX, int OffsetY)
{
    public bool HasStroke => StrokeColor.AuthoredPacked != 0;
    public bool HasOffset => OffsetLT.AuthoredPacked != 0;
}

/// <summary>32C770's78 original48B styles, including the startup byte-order
/// and color conversion. Load once with source resources; lookup does no IO.</summary>
public sealed class OriginalMapTextPresets
{
    private readonly OriginalMapTextPreset[] _rows;
    private OriginalMapTextPresets(OriginalMapTextPreset[] rows) => _rows = rows;

    public static OriginalMapTextPresets LoadCurrentTc(OriginalGameRepository repository)
    {
        _ = OriginalCeHexWorldCoordinates.VerifyInstalledExecutable(repository.SourceDirectory, "TC");
        using var stream = File.OpenRead(Path.Combine(repository.SourceDirectory, "SAN14CE_TC.exe"));
        using var pe = new PEReader(stream);
        var bytes = pe.GetSectionData(0x1A27E40).GetContent(0, 78 * 48).AsSpan();
        var rows = new OriginalMapTextPreset[78];
        for (int i = 0; i < rows.Length; i++) rows[i] = Read(bytes.Slice(i * 48, 48));
        return new(rows);
    }

    // The source falls back to row0 for any index outside0..77.
    public OriginalMapTextPreset this[int index] => _rows[(uint)index < 78 ? index : 0];

    private static OriginalMapTextPreset Read(ReadOnlySpan<byte> row)
        => new(Color(row, 0), Color(row, 4), Color(row, 8), Color(row, 12), Color(row, 16),
            Int(row, 20), Color(row, 24), Color(row, 28), Color(row, 32), Color(row, 36), Int(row, 40), Int(row, 44));
    private static int Int(ReadOnlySpan<byte> row, int at) => BinaryPrimitives.ReadInt32LittleEndian(row.Slice(at, 4));
    private static OriginalAreaMarkerColor Color(ReadOnlySpan<byte> row, int at)
    {
        uint packed = BinaryPrimitives.ReverseEndianness(BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(at, 4)));
        uint cpu = (packed & 0xFF000000) | Gamma((byte)packed) | (uint)Gamma((byte)(packed >> 8)) << 8 |
            (uint)Gamma((byte)(packed >> 16)) << 16;
        return new(packed, cpu);
    }
    private static byte Gamma(byte value) => (byte)Math.Clamp((int)(MathF.Pow(value / 255f, 2.2f) * 255f), 0, 255);
}
