using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;

namespace San14.Data;

public sealed record LoadedArmyFlagFreshPaneLayout(ArmyFlagPaneGlyph SourceText,
    ArmyFlagPaneColorSelection SourceColors, int ExplicitSourcePaneKind,
    LoadedArmyFlagGlyphAtlasPrefix Atlas, byte[] SourceRecord48,
    ArmyFlagGlyphQuad Quad, string CallerRecordSource)
{
    public int LogicalPaneWidth => 200;
    public int LogicalPaneHeight => 200;
    public int NativeLayoutWidth => 128;
    public int NativeLayoutHeight => 64;
    public int NativeBaseline => 32;
    public int NativeLineWidth => 32;
    public int NativeLineHeight => 32;
    public int NativeLineOffsetX => 48;
    public int NativeLineOffsetY => 16;
    public byte SourceStyleAlignment => 0x11;
    public byte SourceFaceSelector => 0;
    public byte SourcePaletteSelector => 0;
    public float SourceScaleX => 1;
    public float SourceScaleY => 1;
    public float SourceDepth => 0;
    public bool PaneStateFidelityVerified => false;
    public bool SourceRecordPointerIsCallerOwned => true;
    public string NativeSourceProvenance => "SC17E9D0/17EA0F logical200x200; " +
        "AEE9D0/AEEB20 fresh scale1/face0/palette0/align11; B059B1/B05F88/B05F9F " +
        "primaryImage128x64 -> AC3E40/ADC150/AD7D90; AD68C0 line centering; " +
        "AD81A0..8240 cursor entry; B080CD..80DF/B08497/1064060 glyph record; " +
        "1063390/1062D20/95B7F0/95B570 opcode4B";
}

/// <summary>
/// Bounded positive fresh-pane layout for the source font0 metric cohort. The native job
/// passes the selected image dimensions to the layout engine. Complete caller
/// record bytes retain the opaque owned handle at+10 and unwritten tail at+2C;
/// they are not original pointers or evidence about a current original frame.
/// </summary>
public static class ArmyFlagFreshPaneLayout
{
    public const string SourceFontSha256 =
        "1527A29A84080CECBB5B2A636D9E8D412F61421C23763C7B3376E310A9153C84";
    public const string SourceCaoMetricHex = "2020002020F0002000211200";
    public const string SourceMetricCohortHex = "2020002020F00020";
    public const string SourceCaoEncodedSha256 =
        "1E5EC9F5858A5D73F0CCFFB2810BCC8BBDF583AFAECF111EF7C488D430B38DA8";
    public const string SourceCaoPixelsSha256 =
        "E59D173B1383B9FD1C73646B62FB34F5B9CA4DB6882279361DFEE82E03214C9E";

    public static LoadedArmyFlagFreshPaneLayout BuildCao(
        LoadedArmyFlagFontSource font, LoadedArmyFlagGlyphAtlasPrefix atlas,
        ArmyFlagPaneGlyph sourceText, ArmyFlagPaneColorSelection sourceColors,
        int explicitSourcePaneKind, ReadOnlySpan<byte> completeCallerRecord48,
        string callerRecordSource, bool explicitOwnedFreshPaneState)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        if (sourceText.TwoUtf16Units != "曹\0")
            throw Error("BuildCao requires the proved source one-unit U+66F9 text");
        return BuildSameMetricGlyph(font, atlas, sourceText, sourceColors, explicitSourcePaneKind,
            completeCallerRecord48, callerRecordSource, explicitOwnedFreshPaneState);
    }

    /// <summary>
    /// Publishes one glyph only when its actual source metric has the complete
    /// same first8 bytes as U+66F9. Width/height, bearings and advances therefore
    /// retain the proved fresh horizontal layout. Its pixel reference and image
    /// are independently read from the pinned font; no other metric is inferred.
    /// </summary>
    public static LoadedArmyFlagFreshPaneLayout BuildSameMetricGlyph(
        LoadedArmyFlagFontSource font, LoadedArmyFlagGlyphAtlasPrefix atlas,
        ArmyFlagPaneGlyph sourceText, ArmyFlagPaneColorSelection sourceColors,
        int explicitSourcePaneKind, ReadOnlySpan<byte> completeCallerRecord48,
        string callerRecordSource, bool explicitOwnedFreshPaneState)
    {
        ArgumentNullException.ThrowIfNull(font); ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(sourceText); ArgumentNullException.ThrowIfNull(sourceColors);
        if (!explicitOwnedFreshPaneState || string.IsNullOrWhiteSpace(callerRecordSource) ||
            completeCallerRecord48.Length != 48)
            throw Error("requires explicit fresh pane state and a labelled complete48-byte owned record");
        if ((uint)explicitSourcePaneKind > 2 || sourceText.NativeCopyUnitCount != 2 ||
            sourceText.TwoUtf16Units.Length != 2 || sourceText.TwoUtf16Units[1] != '\0' ||
            sourceText.TwoUtf16Units[0] is '\0' or '\r' or '\n' ||
            string.IsNullOrWhiteSpace(sourceText.CallerSource) ||
            string.IsNullOrWhiteSpace(sourceColors.CallerSource))
            throw Error("requires one source glyph then zero, without a native line break, and explicit pane kinds0..2");
        if (!font.FreshHostDefaultFontFidelityVerified || font.ExplicitCommonArrayIndex != 0 ||
            font.Parameter.ObjectId != 0x97D72A63 || font.Source.FileId != 0xE876B7F0 ||
            font.Source.DecodedSha256 != SourceFontSha256 || Hash(font.Source.Bytes) != SourceFontSha256 ||
            atlas.Font.Source.DecodedSha256 != SourceFontSha256)
            throw Error("requires the source-owned fresh Host default font collection");
        ushort codeUnit = sourceText.TwoUtf16Units[0];
        var glyph = ArmyFlagFontSources.ReadGlyph(font, codeUnit, 0, 0);
        // AC2940 reads signed+4; AD6AE0 reads+1 and signed+3;1064060
        // reads+0..4. Requiring all first8 bytes also retains the source
        // vertical metric fields while this contract uses the horizontal path.
        if (glyph.Record.RawBytes.Length != 12 ||
            Convert.ToHexString(glyph.Record.RawBytes.AsSpan(0, 8)) != SourceMetricCohortHex)
            throw Error("source glyph is outside the proved first8-byte metric cohort");
        if (codeUnit == 0x66F9 && (glyph.Record.Index != 2402 ||
            Convert.ToHexString(glyph.Record.RawBytes) != SourceCaoMetricHex ||
            Hash(glyph.EncodedBytes) != SourceCaoEncodedSha256 || Hash(glyph.Pixels.Pixels) != SourceCaoPixelsSha256))
            throw Error("source U+66F9 glyph differs from the bounded layout proof");
        // AC31B0 and ADDEB0 use the source face's U+3042 metric as baseline.
        // B07CC0 repeats the same call before producing records.
        var japaneseMetric = ArmyFlagFontSources.ReadGlyph(font, 0x3042, 0, 0).Record;
        if (japaneseMetric.Height != 32)
            throw Error("source fresh face baseline differs from the proved32");
        if (atlas.Glyph.Utf16CodeUnit != codeUnit || atlas.Glyph.FaceIndex != 0 ||
            atlas.Glyph.PaletteIndex != 0 || !atlas.Glyph.Record.RawBytes.AsSpan().SequenceEqual(glyph.Record.RawBytes) ||
            atlas.Glyph.Record.Index != glyph.Record.Index ||
            !atlas.Glyph.EncodedBytes.AsSpan().SequenceEqual(glyph.EncodedBytes) ||
            !atlas.Glyph.Pixels.Pixels.AsSpan().SequenceEqual(glyph.Pixels.Pixels) ||
            atlas.LogicalRectangle != new ArmyFlagGlyphAtlasRectangle(1, 1, 32, 32) ||
            atlas.SourceGlyphCount != 1 || atlas.NextCursorX != 34 || atlas.NextCursorY != 1 ||
            atlas.NextRowBottom != 34 || atlas.UvRectangle != new Vector4(1f / 1024, 1f / 1024, 33f / 1024, 33f / 1024) ||
            atlas.Bytes.Length != ArmyFlagGlyphAtlasPrefix.AtlasByteLength ||
            Hash(atlas.Bytes) != atlas.WrittenAtlasSha256)
            throw Error("requires the verified owned first insertion into the source fresh font atlas");

        // Source helpers use float32 +/-half and truncate. The finite source
        // metric and bounds are exact integers, yielding offsets48 and16.
        int lineX = (int)((128 - 32) * 0.5f);
        int lineY = 0 - (int)((0 - 32 + 64) * -0.5f);
        int x = glyph.Record.HorizontalOffsetX + lineX;
        int y = lineY - glyph.Record.HorizontalOffsetY + 32;
        int right = x + glyph.Record.Width, bottom = y + glyph.Record.Height;
        uint color = explicitSourcePaneKind < 2
            ? sourceColors.BannerGlyphColor.PanePacked : sourceColors.FreshFlagGlyphPacked;
        if (explicitSourcePaneKind == 2 && color != 0xFFFFFFFF)
            throw Error("fresh kind2 primary corner colors must retain the source constructor value");

        byte[] record = completeCallerRecord48.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(0), x);
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(4), y);
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(8), right);
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(12), bottom);
        BinaryPrimitives.WriteSingleLittleEndian(record.AsSpan(24), 0);
        for (int i = 0; i < 4; i++) BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(28 + i * 4), color);

        // Native1063390 builds TL,TR,BL,BR. Source962CC0 separately publishes
        // vertices in0,2,3,1 order. This bounded rectangle is wholly in its
        // source128x64 clip, so1062D20 leaves coordinates and UVs unchanged.
        int[] xy = [x, y, right, y, x, bottom, right, bottom];
        float[] uv = [atlas.UvRectangle.X, atlas.UvRectangle.Y,
            atlas.UvRectangle.Z, atlas.UvRectangle.Y, atlas.UvRectangle.X,
            atlas.UvRectangle.W, atlas.UvRectangle.Z, atlas.UvRectangle.W];
        byte[] command = new byte[88];
        BinaryPrimitives.WriteUInt32LittleEndian(command, 0x4B);
        for (int i = 0; i < 8; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(command.AsSpan(4 + i * 4), xy[i]);
            BinaryPrimitives.WriteSingleLittleEndian(command.AsSpan(36 + i * 4), uv[i]);
        }
        for (int i = 0; i < 4; i++) BinaryPrimitives.WriteUInt32LittleEndian(command.AsSpan(68 + i * 4), color);
        BinaryPrimitives.WriteSingleLittleEndian(command.AsSpan(84), 0);
        var quad = ArmyFlagGlyphQuad.ReadComplete(command,
            $"source bounded fresh font0 U+{codeUnit:X4} same-metric layout; {callerRecordSource}");
        return new(sourceText, sourceColors, explicitSourcePaneKind, atlas, record, quad, callerRecordSource);
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static InvalidDataException Error(string message) => new($"Army flag fresh pane layout {message}.");
}
