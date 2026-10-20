using System.Buffers.Binary;
using San14.Data;
using San14.Data.Formats;

/// <summary>The normal CE UI locale selector and original G1N glyphs. Font
/// resources stay in memory; nothing is installed or exported as a font.</summary>
internal sealed class MapFontSource
{
    private readonly Dictionary<(int Face, int Palette, int Glyph), G1FontGlyphPixels> _pixels = [];

    internal string Locale { get; }
    internal uint ParameterKey { get; }
    internal uint ParameterObjectId { get; }
    internal LoadedRdbAsset Resource { get; }
    internal G1FontCollection Collection { get; }

    private MapFontSource(string locale, uint key, uint parameter,
        LoadedRdbAsset resource, G1FontCollection collection)
    {
        Locale = locale; ParameterKey = key; ParameterObjectId = parameter;
        Resource = resource; Collection = collection;
    }

    internal static MapFontSource Load(GameResourceRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        locale = locale.ToUpperInvariant();
        if (locale is not ("SC" or "TC"))
            throw new InvalidDataException($"Original map font locale is unsupported: {locale}.");
        // CE TC 1526F0 uses the literal font_TC. BCE510 finds that key in
        // the complete Common key/name arrays; BFE990 publishes Host+460.
        uint key = NameHash("font_" + locale);
        LoadedObjects common = repository.LoadObjects("KIDSSystemResource", 0x208BE13A);
        DokObject settings = common.Document.Objects.Single(o => o.ObjectId == 0xE0D0BB40);
        uint[] keys = Values(settings, 0x8985E35B), names = Values(settings, 0xF504C347);
        if (keys.Length != names.Length)
            throw new InvalidDataException("Original UI font key/name arrays have different lengths.");
        int index = Array.IndexOf(keys, key);
        if (index < 0)
            throw new InvalidDataException($"Original UI font key font_{locale} is missing.");
        DokObject parameter = common.Document.Objects.Single(o => o.ObjectId == names[index]);
        LoadedRdbAsset resource = repository.LoadRdbAsset("RRPreview", Values(parameter, 0x5348D820).Single());
        return new(locale, key, parameter.ObjectId, resource, G1FontCollectionReader.Read(resource.Bytes));
    }

    internal G1FontGlyphRecord Record(char codeUnit, int face)
    {
        G1FontFace sourceFace = Collection.Faces[face];
        return sourceFace.Glyphs[sourceFace.UnicodeGlyphMap[codeUnit]];
    }

    internal G1FontGlyphPixels Glyph(char codeUnit, int face, int palette)
    {
        int glyph = Record(codeUnit, face).Index;
        var key = (face, palette, glyph);
        if (_pixels.TryGetValue(key, out G1FontGlyphPixels? existing)) return existing;
        G1FontGlyphPixels pixels = G1FontCollectionReader.DecodeGlyph(Collection,
            Resource.Bytes, codeUnit, face, palette);
        _pixels.Add(key, pixels);
        return pixels;
    }

    internal MapFontMeasure Measure(string text, MapFontProfile profile)
    {
        int width = 0, above = 0, below = 0;
        foreach (char code in text)
        {
            if (char.IsControl(code)) throw new InvalidDataException("Map text requires a single source line.");
            G1FontGlyphRecord glyph = Record(code, profile.Face);
            width += (int)(glyph.HorizontalAdvance * profile.ScaleX) + profile.HorizontalSpace;
            int top = -(int)(glyph.HorizontalOffsetY * profile.ScaleY);
            above = Math.Max(above, -top);
            below = Math.Max(below, top + (int)(glyph.Height * profile.ScaleY));
        }
        // AC2951/AC2A00 measure the spaces between characters only.
        if (text.Length != 0) width -= profile.HorizontalSpace;
        return new(width, above, below);
    }

    // AC2D60 -> ADBFD0 -> ADB880: fit the source line only when it exceeds
    // the cell minus its reserved right-hand space. Modes2/3 preserve height.
    internal MapFontProfile FitToWidth(string text, MapFontProfile profile, float cellWidth)
    {
        if (profile.WidthFitMode == 0 || text.Length == 0) return profile;
        if (profile.WidthFitMode is not (1 or 2 or 3))
            throw new InvalidDataException($"Unsupported single-line width fitting: {profile.Source}.");
        int available = (int)cellWidth - (int)profile.WidthReserve;
        int measured = Measure(text, profile).Width;
        if (available >= measured) return profile;
        float factor = available / (float)measured * 0.98f;
        return profile with
        {
            ScaleX = profile.ScaleX * factor,
            // AC2D60 mode3 uses ADBE40's literal UTF-16 path, which calls the
            // same ADB880 fitting calculation as mode2 with height preserved.
            ScaleY = profile.WidthFitMode == 1 ? profile.ScaleY * factor : profile.ScaleY,
            HorizontalSpace = unchecked((short)(int)(profile.HorizontalSpace * factor))
        };
    }

    internal float CellBaselineY(MapFontProfile profile, float cellHeight)
    {
        // AC1FC0/AC20D0 use U+3042 for the nominal line height, independently
        // of the current string. B07260 and 141063170 add that height before
        // subtracting each glyph's original signed Y bearing.
        // AD6140 selects nominalHeight + VerticalSpace for flag0x4, then
        // B07260 subtracts VerticalSpace from the first glyph baseline.
        // The top-aligned single-line path therefore keeps this baseline.
        int lineHeight = (int)(Record('\u3042', profile.Face).Height * profile.ScaleY);
        return (profile.RawAlign & 15) switch
        {
            0 => lineHeight,
            1 => (cellHeight - lineHeight) * 0.5f + lineHeight,
            2 => cellHeight,
            _ => throw new InvalidDataException($"Original text vertical alignment is unsupported: {profile.Source}.")
        };
    }

    private static uint[] Values(DokObject obj, uint name)
    {
        DokProperty property = obj.Properties.Single(p => p.NameHash == name);
        if (property.ElementSize != 4 || property.RawValue.Length % 4 != 0)
            throw new InvalidDataException("Original UI font declaration is not UInt32.");
        var values = new uint[property.RawValue.Length / 4];
        for (int i = 0; i < values.Length; i++)
            values[i] = BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue.AsSpan(i * 4, 4));
        return values;
    }

    private static uint NameHash(string text)
    {
        uint hash = 0, factor = 1;
        foreach (char value in text)
        {
            factor = unchecked(factor * 31);
            hash = unchecked(hash + value * factor);
        }
        return hash;
    }
}

internal readonly record struct MapFontMeasure(int Width, int AboveBaseline, int BelowBaseline)
{
    internal int Height => AboveBaseline + BelowBaseline;
}

/// <summary>Type-2 source fields consumed by CE B13570/B12950. RawByte151
/// is the G1N face, 152 the palette; 150 and 153 remain separate align fields.</summary>
internal sealed record MapFontProfile(string Source, int Face, int Palette,
    float ScaleX, float ScaleY, short HorizontalSpace, short VerticalSpace,
    uint ColourLT, uint ColourRT, uint ColourLB, uint ColourRB,
    uint RawFlags, byte RawAlign, byte RawLineArrange,
    short ShadowX, short ShadowY,
    uint ShadowColourLT, uint ShadowColourRT, uint ShadowColourLB, uint ShadowColourRB)
{
    internal bool HasStroke { get; init; }
    internal uint StrokeColour { get; init; }
    internal float StrokeWidth { get; init; }
    internal float? OffsetXOverride { get; init; }
    internal float? OffsetYOverride { get; init; }
    internal sbyte WidthFitMode { get; init; }
    // Raw155 controls character processing independently of raw153 layout.
    // The supported plain, single-line labels contain no control bytecode.
    internal byte RawCharacterMode { get; init; }
    internal float WidthReserve { get; init; }

    internal MapFontProfile WithPreset(MapTextPreset preset) => this with
    {
        ColourLT = preset.LT.CpuPacked, ColourRT = preset.RT.CpuPacked,
        ColourLB = preset.LB.CpuPacked, ColourRB = preset.RB.CpuPacked,
        RawFlags = (RawFlags & ~1u) | (preset.HasOffset ? 1u : 0u),
        ShadowColourLT = preset.OffsetLT.CpuPacked, ShadowColourRT = preset.OffsetRT.CpuPacked,
        ShadowColourLB = preset.OffsetLB.CpuPacked, ShadowColourRB = preset.OffsetRB.CpuPacked,
        OffsetXOverride = preset.OffsetX, OffsetYOverride = preset.OffsetY,
        HasStroke = preset.HasStroke, StrokeColour = preset.StrokeColor.CpuPacked, StrokeWidth = preset.StrokeWidth
    };
    // AEE3D0 publishes the first effect enable and AC1F50 rounds the
    // scaled source offset. Original text uses Y-up; Canvas uses Y-down.
    internal bool HasShadow => (RawFlags & 1) != 0;
    // B12950: raw bit2 -> font+5F -> ACC3E0 compact+2C -> AD6140.
    // It selects the nominal line height, not a glyph rendering effect.
    internal bool UsesFixedLineHeight => (RawFlags & 4) != 0;
    internal void ValidateSingleLine()
    {
        if ((RawFlags & ~5u) != 0)
            throw new InvalidDataException($"Original text options require their source consumer: {Source}.");
        if (UsesFixedLineHeight && VerticalSpace != 0 &&
            ((RawAlign & 15) != 0 || RawLineArrange != 1))
            throw new InvalidDataException($"Fixed-height text with vertical spacing requires its source line layout: {Source}.");
    }
    internal (int X, int Y) ShadowOffset => (Round((OffsetXOverride ?? ShadowX) * ScaleX), Round((OffsetYOverride ?? ShadowY) * ScaleY));
    internal IEnumerable<(int X, int Y)> StrokeOffsets()
    {
        (int X, int Y)[] directions = [(-1,0),(1,0),(0,1),(0,-1),(-1,1),(-1,-1),(1,1),(1,-1)];
        for (int i = 0; i < directions.Length; i++)
        {
            float f = (i < 4 ? 1f : 0.7171f) * StrokeWidth;
            yield return (Round(f * ScaleX * directions[i].X), Round(f * ScaleY * directions[i].Y));
        }
    }
    private static int Round(float value) => (int)(value < 0 ? value - 0.5f : value + 0.5f);

    internal static MapFontProfile FromNode(LcskNodeRecord node, string source)
    {
        if (node.Type != 2 || node.RawBytes.Length != 224)
            throw new InvalidDataException("Original map font profile requires a complete source text node.");
        byte[] bytes = node.RawBytes;
        float scaleX = F(136), scaleY = F(140);
        if (!float.IsFinite(scaleX) || scaleX <= 0 || !float.IsFinite(scaleY) || scaleY <= 0)
            throw new InvalidDataException("Original map font scale is invalid.");
        return new(source + "/" + node.Name, bytes[151], bytes[152], scaleX, scaleY,
            I16(144), I16(146), U(120), U(124), U(128), U(132), U(116), bytes[150], bytes[153],
            I16(156), I16(158), U(160), U(164), U(168), U(172))
        {
            WidthFitMode = unchecked((sbyte)bytes[154]),
            RawCharacterMode = bytes[155],
            WidthReserve = F(204)
        };
        uint U(int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at, 4));
        short I16(int at) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(at, 2));
        float F(int at) => BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(at, 4));
    }
}
