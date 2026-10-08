using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

public readonly record struct OriginalDeferredEarlyShadowAtlasRectangle(int X, int Y, int Width, int Height);
public readonly record struct OriginalDeferredEarlyShadowAtlasUnitSize(int Width4C, int Height50);
public readonly record struct OriginalDeferredEarlyShadowAtlasTargetDimensions(ushort Width40, ushort Height42);

/// <summary>Current numeric fields at1059500. Target198 is an actual held
/// descriptor domain; null is supplied by that owner, never inferred from a
/// graph name. Retained entries can be unknown when the original has not
/// written them. The source skip branch does not read those unknown entries.</summary>
public sealed record OriginalDeferredEarlyShadowAtlasInputs(int PassCount18, int UnitCountBC0,
    uint Mask22C, uint TouchedMask230, bool SpecialAtlasD24, bool RectangleOverrideD25,
    bool HalfTexelDisabledD21, OriginalDeferredEarlyShadowAtlasTargetDimensions? ActualTarget198,
    int FallbackWidth5B4, int FallbackHeight5B8,
    IReadOnlyList<OriginalDeferredEarlyShadowAtlasRectangle?> RetainedRectanglesCE0,
    IReadOnlyList<OriginalDeferredEarlyShadowAtlasUnitSize?> RetainedUnitSizes,
    IReadOnlyList<Vector4?> RetainedScaleOffsets6F0);

public sealed class OriginalDeferredEarlyShadowAtlasNumericFields
{
    internal OriginalDeferredEarlyShadowAtlasNumericFields(bool skipped,
        OriginalDeferredEarlyShadowAtlasRectangle?[] rectangles,
        OriginalDeferredEarlyShadowAtlasUnitSize?[] sizes, Vector4?[] scales,
        bool rectanglesWritten, bool sizesWritten, bool scalesWritten, bool specialAtlasD24, string evidence)
        => (SkippedUnchangedMask, RectanglesCE0, UnitSizes, ScaleOffsets6F0,
            RectanglesWritten, UnitSizesWritten, ScaleOffsetsWritten, SpecialAtlasD24, SourceEvidence) =
            (skipped, Array.AsReadOnly(rectangles), Array.AsReadOnly(sizes), Array.AsReadOnly(scales),
            rectanglesWritten, sizesWritten, scalesWritten, specialAtlasD24, evidence);
    public bool SkippedUnchangedMask { get; }
    public IReadOnlyList<OriginalDeferredEarlyShadowAtlasRectangle?> RectanglesCE0 { get; }
    public IReadOnlyList<OriginalDeferredEarlyShadowAtlasUnitSize?> UnitSizes { get; }
    public IReadOnlyList<Vector4?> ScaleOffsets6F0 { get; }
    public bool RectanglesWritten { get; }
    public bool UnitSizesWritten { get; }
    public bool ScaleOffsetsWritten { get; }
    public bool SpecialAtlasD24 { get; }
    public string SourceEvidence { get; }

    public OriginalDeferredEarlyShadowTargetRectangles PreparePassDrawDomain(int pass,
        OriginalWorldMainDisplayViewport actualFrontendDepthRange)
    {
        if (SpecialAtlasD24) throw new NotSupportedException("Special atlasD24 has a different per-pass rectangle adjustment.");
        var r = RectanglesCE0[pass] ?? throw new InvalidDataException("This pass has no written or retained actual CE0 rectangle.");
        return new(actualFrontendDepthRange with { X = r.X - 1, Y = r.Y - 1,
            Width = r.Width + 2, Height = r.Height + 2 }, r.X, r.Y, r.Width, r.Height, true,
            SourceEvidence + ";10575E0-current-pass;viewport-padded1/scissor-interior;1D9-enabled;retained13C/17C");
    }

    /// <summary>Only actually written CE0,unit4C/50 and6F0 fields. A skip
    /// never rewrites a retained domain or initializes unknown padding.</summary>
    public void WriteOwnedNumericFields(Span<byte> actualOwnedCpu)
    {
        OriginalDeferredEarlyShadowCompactConstructor.RequireStorage(actualOwnedCpu.Length);
        for (int i = 0; i < RectanglesCE0.Count; i++)
        {
            if (RectanglesWritten && RectanglesCE0[i] is { } r)
            {
                int at = 0xCE0 + i * 16;
                BinaryPrimitives.WriteInt32LittleEndian(actualOwnedCpu[at..], r.X);
                BinaryPrimitives.WriteInt32LittleEndian(actualOwnedCpu[(at + 4)..], r.Y);
                BinaryPrimitives.WriteInt32LittleEndian(actualOwnedCpu[(at + 8)..], r.Width);
                BinaryPrimitives.WriteInt32LittleEndian(actualOwnedCpu[(at + 12)..], r.Height);
            }
            if (ScaleOffsetsWritten && ScaleOffsets6F0[i] is { } s)
            {
                int at = 0x6F0 + i * 16;
                BinaryPrimitives.WriteSingleLittleEndian(actualOwnedCpu[at..], s.X);
                BinaryPrimitives.WriteSingleLittleEndian(actualOwnedCpu[(at + 4)..], s.Y);
                BinaryPrimitives.WriteSingleLittleEndian(actualOwnedCpu[(at + 8)..], s.Z);
                BinaryPrimitives.WriteSingleLittleEndian(actualOwnedCpu[(at + 12)..], s.W);
            }
        }
        if (UnitSizesWritten)
            for (int i = 0; i < UnitSizes.Count; i++)
                if (UnitSizes[i] is { } s)
                {
                    int unit = OriginalDeferredEarlyShadowCompactConstructor.PassBoundsOffset(i);
                    BinaryPrimitives.WriteInt32LittleEndian(actualOwnedCpu[(unit + 0x4C)..], s.Width4C);
                    BinaryPrimitives.WriteInt32LittleEndian(actualOwnedCpu[(unit + 0x50)..], s.Height50);
                }
    }
}

public sealed class LoadedOriginalDeferredEarlyShadowAtlasSource
{
    internal LoadedOriginalDeferredEarlyShadowAtlasSource(string locale, string evidence)
        => (Locale, SourceEvidence) = (locale, evidence);
    public string Locale { get; }
    public string SourceEvidence { get; }

    public OriginalDeferredEarlyShadowAtlasNumericFields PrepareCurrent(
        OriginalDeferredEarlyShadowCurrentCasterFields actualCaster, OriginalDeferredEarlyShadowAtlasInputs actualInputs)
    {
        ArgumentNullException.ThrowIfNull(actualCaster);
        ArgumentNullException.ThrowIfNull(actualInputs);
        if (actualCaster.Volume.Compact.Numeric.PassCount != actualInputs.PassCount18 ||
            actualCaster.Numeric.TouchedMaskBC8 != actualInputs.TouchedMask230)
            throw new InvalidDataException("Current atlas must consume the same compact count and actual v50 BC8->230 store.");
        return PrepareNumericCurrentFields(actualInputs);
    }

    /// <summary>Complete ordinaryD24=0 numeric1059500/1057E70/1059670
    /// scope. Resource/setter publication and current22C are caller inputs.</summary>
    public OriginalDeferredEarlyShadowAtlasNumericFields PrepareNumericCurrentFields(OriginalDeferredEarlyShadowAtlasInputs f)
    {
        ArgumentNullException.ThrowIfNull(f);
        if (f.PassCount18 is < 1 or > 4 || f.UnitCountBC0 is < 0 or > 4 || f.UnitCountBC0 > f.PassCount18 ||
            f.RetainedRectanglesCE0.Count != f.PassCount18 || f.RetainedUnitSizes.Count != f.UnitCountBC0 ||
            f.RetainedScaleOffsets6F0.Count != f.PassCount18)
            throw new ArgumentException("Compact atlas needs actual current pass/unit counts and corresponding retained fields.");
        var rectangles = f.RetainedRectanglesCE0.ToArray();
        var sizes = f.RetainedUnitSizes.ToArray();
        var scales = f.RetainedScaleOffsets6F0.ToArray();
        if (!f.RectangleOverrideD25 && f.Mask22C == f.TouchedMask230)
            return new(true, rectangles, sizes, scales, false, false, false, f.SpecialAtlasD24, SourceEvidence);
        if (f.SpecialAtlasD24)
            throw new NotSupportedException("D24 uses the separate1058030/10597C0 atlas family.");
        bool dimensionsPresent = f.ActualTarget198 is not null || f.FallbackWidth5B4 > 0;
        int width = f.ActualTarget198?.Width40 ?? f.FallbackWidth5B4;
        int height = f.ActualTarget198?.Height42 ?? f.FallbackHeight5B8;
        if (dimensionsPresent && (width <= 0 || height <= 0))
            throw new InvalidDataException("The consumed current atlas target/fallback dimensions must be positive.");
        bool rectanglesWritten = !f.RectangleOverrideD25;
        if (rectanglesWritten)
        {
            int activeCount = 0;
            for (int i = 0; i < f.PassCount18; i++) if ((f.TouchedMask230 & (1u << i)) != 0) activeCount++;
            OriginalDeferredEarlyShadowAtlasRectangle[]? packed = dimensionsPresent ? GenerateNormalGrid(width, height, activeCount) : null;
            int selected = 0;
            for (int i = 0; i < f.PassCount18; i++)
                rectangles[i] = (f.TouchedMask230 & (1u << i)) == 0 ? new(0, 0, 1, 1) :
                    packed is not null ? packed[selected++] : throw new InvalidDataException(
                        "Original1057E70 returns without initializing the scratch rectangle; active passes require its real preceding dimension writer.");
        }
        for (int i = 0; i < f.UnitCountBC0; i++)
        {
            var r = rectangles[i] ?? throw new InvalidDataException("Actual unit extent update reads an unknown retained CE0 rectangle.");
            sizes[i] = new(unchecked(r.Width + 2), unchecked(r.Height + 2));
        }
        if (dimensionsPresent)
        {
            float halfX = f.HalfTexelDisabledD21 ? 0f : 0.5f / width;
            float halfY = f.HalfTexelDisabledD21 ? 0f : 0.5f / height;
            for (int i = 0; i < f.PassCount18; i++)
            {
                var r = rectangles[i] ?? throw new InvalidDataException("Actual scale update reads an unknown retained CE0 rectangle.");
                scales[i] = new((float)unchecked(r.Width + 2) / width, (float)unchecked(r.Height + 2) / height,
                    (float)unchecked(r.X - 1) / width + halfX, (float)unchecked(r.Y - 1) / height + halfY);
            }
        }
        return new(false, rectangles, sizes, scales, rectanglesWritten, f.UnitCountBC0 != 0,
            dimensionsPresent, f.SpecialAtlasD24, SourceEvidence);
    }

    private static OriginalDeferredEarlyShadowAtlasRectangle[] GenerateNormalGrid(int width, int height, int count)
    {
        int columns = 1, rows = 1;
        if (count is 2 or 3) { if (width > height) columns = count; else rows = count; }
        else if (count == 4)
        {
            uint verticalMinimum = (uint)Math.Min(height / 4, width);
            uint horizontalMinimum = (uint)Math.Min(width / 4, height);
            uint squareMinimum = (uint)Math.Min(width / 2, height / 2);
            if (horizontalMinimum < verticalMinimum && verticalMinimum > squareMinimum) rows = 4;
            else if (horizontalMinimum > verticalMinimum && horizontalMinimum > squareMinimum) columns = 4;
            else columns = rows = 2;
        }
        int pitchWidth = width / columns, pitchHeight = height / rows;
        var result = new OriginalDeferredEarlyShadowAtlasRectangle[columns * rows];
        for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
            result[y * columns + x] = new(1 + x * pitchWidth, 1 + y * pitchHeight, pitchWidth - 2, pitchHeight - 2);
        return result;
    }
}

public static class OriginalDeferredEarlyShadowAtlasSource
{
    public static LoadedOriginalDeferredEarlyShadowAtlasSource Load(OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.LoadEarlyShadowAtlasSource(repository.SourceDirectory, locale);
    }
}
