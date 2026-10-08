using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

namespace San14.Data;

public readonly record struct OriginalMapCornerColors(OriginalAreaMarkerColor LT, OriginalAreaMarkerColor RT,
    OriginalAreaMarkerColor LB, OriginalAreaMarkerColor RB);

/// <summary>Source sprite palettes and the force-letter contrast selector.
/// Sprite palettes get the startup conversion; contrast colors use their own
/// source table rules. Prepare once, retaining both authored and CPU colors.</summary>
public sealed class OriginalMapSpritePalettes
{
    private readonly OriginalMapCornerColors[] _rows;
    private readonly OriginalAreaMarkerColor[] _contrast;
    private readonly byte[] _contrastMap;
    private readonly byte _contrastFallback;
    public int MoraleStyleThreshold { get; }
    public int DurabilityStylePercent { get; }

    private OriginalMapSpritePalettes(OriginalMapCornerColors[] rows, OriginalAreaMarkerColor[] contrast,
        byte[] map, byte fallback, int moraleThreshold, int durabilityPercent)
        => (_rows, _contrast, _contrastMap, _contrastFallback, MoraleStyleThreshold, DurabilityStylePercent) =
            (rows, contrast, map, fallback, moraleThreshold, durabilityPercent);

    public static OriginalMapSpritePalettes LoadCurrentTc(OriginalGameRepository repository)
    {
        _ = OriginalCeHexWorldCoordinates.VerifyInstalledExecutable(repository.SourceDirectory, "TC");
        using var stream = File.OpenRead(Path.Combine(repository.SourceDirectory, "SAN14CE_TC.exe"));
        using var pe = new PEReader(stream);
        var bytes = pe.GetSectionData(0x1A279D0).GetContent(0, 71 * 16).AsSpan();
        var rows = new OriginalMapCornerColors[71];
        for (int i = 0; i < rows.Length; i++)
        {
            var row = bytes.Slice(i * 16, 16);
            rows[i] = new(Read(row, 0, true), Read(row, 4, true), Read(row, 8, true), Read(row, 12, true));
        }
        var pool = pe.GetSectionData(0x1A27668).GetContent(0, 63 * 4).AsSpan();
        var contrast = new OriginalAreaMarkerColor[63];
        for (int i = 0; i < contrast.Length; i++) contrast[i] = Read(pool, i * 4, i >= 2);
        return new(rows, contrast, pe.GetSectionData(0x1A27768).GetContent(0, 62).ToArray(),
            pe.GetSectionData(0x1A277A4).GetContent(0, 1)[0],
            BinaryPrimitives.ReadInt32LittleEndian(pe.GetSectionData(0x1A49EC0).GetContent(0, 4).AsSpan()),
            BinaryPrimitives.ReadInt32LittleEndian(pe.GetSectionData(0x1A4AC20).GetContent(0, 4).AsSpan()));
    }

    public OriginalMapCornerColors this[int index] => _rows[(uint)index < 71 ? index : 0];

    // 2AD610 ->32C950: this is neither Force fill nor a text preset number.
    public OriginalAreaMarkerColor ForceLetter(int actualForceColor49)
        => _contrast[(uint)actualForceColor49 < 62 ? _contrastMap[actualForceColor49] : _contrastFallback];

    private static OriginalAreaMarkerColor Read(ReadOnlySpan<byte> bytes, int at, bool reverse)
    {
        uint authored = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(at, 4));
        if (reverse) authored = BinaryPrimitives.ReverseEndianness(authored);
        uint cpu = (authored & 0xFF000000) | Gamma((byte)authored) | (uint)Gamma((byte)(authored >> 8)) << 8 |
            (uint)Gamma((byte)(authored >> 16)) << 16;
        return new(authored, cpu);
    }
    private static byte Gamma(byte value) => (byte)Math.Clamp((int)(MathF.Pow(value / 255f, 2.2f) * 255f), 0, 255);
}
