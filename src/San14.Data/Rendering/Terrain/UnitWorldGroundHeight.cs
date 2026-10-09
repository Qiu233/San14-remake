using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Current native grid fields read by A0B830. Unknown fields stay unknown.
/// Origins are additive local offsets; constructor and later writes are distinct.</summary>
public sealed record UnitWorldGroundHeightGrid(
    float? HeightScale14, float? OriginX20, float? OriginZ24,
    int? CellWidth48, int? CellHeight4A, float? SampleSpacing4C,
    float? MinimumHeight50, uint[]? PackedSamples58)
{
    public LoadedHeightField? Source { get; init; }
    public float? ExtentX18 { get; init; }
    public float? ExtentZ1C { get; init; }
    public float? OriginY28 { get; init; }
    public float? MaximumHeight54 { get; init; }

    /// <summary>SC A0CD50 / TC A0BD50: copy the MHK header and sample bytes,
    /// explicitly reset origins, then derive the stored float32 height scale.</summary>
    public static UnitWorldGroundHeightGrid ConstructMhkA0CD50(MhkHeightField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (field.CellWidth is < 1 or > ushort.MaxValue || field.CellHeight is < 1 or > ushort.MaxValue ||
            field.PackedSamples.LongLength != (field.CellWidth + 1L) * (field.CellHeight + 1L) ||
            !float.IsFinite(field.SampleSpacing) || field.SampleSpacing <= 0 ||
            !float.IsFinite(field.MinimumHeight) || !float.IsFinite(field.MaximumHeight) ||
            field.MaximumHeight < field.MinimumHeight)
            throw new InvalidDataException("Native GroundHeight MHK constructor inputs are invalid.");
        float range = UnitWorldGroundHeightFloat.Subtract(field.MaximumHeight, field.MinimumHeight);
        // Native literal is 0x33800001, rounded 1/0xFFFFFF, not 1/0x1000000.
        float scale = UnitWorldGroundHeightFloat.Multiply(range,
            BitConverter.Int32BitsToSingle(0x33800001));
        return new(scale, 0f, 0f, field.CellWidth, field.CellHeight,
            field.SampleSpacing, field.MinimumHeight, field.PackedSamples.ToArray())
        {
            MaximumHeight54 = field.MaximumHeight, OriginY28 = 0f,
            ExtentX18 = UnitWorldGroundHeightFloat.Multiply(field.CellWidth, field.SampleSpacing),
            ExtentZ1C = UnitWorldGroundHeightFloat.Multiply(field.CellHeight, field.SampleSpacing)
        };
    }

    public static UnitWorldGroundHeightGrid ConstructMhkA0CD50(LoadedHeightField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        return ConstructMhkA0CD50(field.HeightField) with { Source = field };
    }
}

/// <summary>Current CE SC A0B830 / TC A0A830 adjacent-sample CPU terrain query.
/// This is independent of renderer LOD and of the per-Hex 445 height-cache clamp.</summary>
public static class UnitWorldGroundHeight
{
    public static float QueryA0B830(UnitWorldGroundHeightGrid grid, float x, float z)
    {
        ArgumentNullException.ThrowIfNull(grid);
        float spacing = Known(grid.SampleSpacing4C, "+4C spacing");
        float scale = Known(grid.HeightScale14, "+14 quantization scale");
        float minimum = Known(grid.MinimumHeight50, "+50 minimum height");
        float localX = UnitWorldGroundHeightFloat.Add(x, Known(grid.OriginX20, "+20 X origin"));
        float localZ = UnitWorldGroundHeightFloat.Add(z, Known(grid.OriginZ24, "+24 Z origin"));
        int width = grid.CellWidth48 ?? throw Unknown("+48 width");
        int height = grid.CellHeight4A ?? throw Unknown("+4A height");
        uint[] samples = grid.PackedSamples58 ?? throw Unknown("+58 packed samples");
        if (!float.IsFinite(localX) || !float.IsFinite(localZ) || spacing <= 0 ||
            width is < 1 or > ushort.MaxValue || height is < 1 or > ushort.MaxValue ||
            samples.LongLength != (width + 1L) * (height + 1L))
            throw new InvalidDataException("Native GroundHeight requires finite local coordinates and a complete valid grid.");

        int column = CellIndex(localX, spacing, width);
        int row = CellIndex(localZ, spacing, height);
        int stride = width + 1;
        long a = row * (long)stride + column;
        uint packedA = samples[a];
        bool opposite = (byte)packedA != 0;
        // Original 10340F8 uses the signed remainder of the local coordinates,
        // even when the containing-cell indices were clamped or overflowed.
        float rx = UnitWorldGroundHeightFloat.Remainder(localX, spacing);
        float rz = UnitWorldGroundHeightFloat.Remainder(localZ, spacing);
        uint baseQ, firstQ, secondQ;
        if (!opposite)
        {
            if (UnitWorldGroundHeightFloat.Add(rz, rx) < spacing)
            {
                baseQ = packedA >> 8;
                firstQ = samples[a + stride] >> 8;
                secondQ = samples[a + 1] >> 8;
            }
            else
            {
                baseQ = samples[a + stride + 1] >> 8;
                firstQ = samples[a + 1] >> 8;
                secondQ = samples[a + stride] >> 8;
                rx = UnitWorldGroundHeightFloat.Subtract(spacing, rx);
                rz = UnitWorldGroundHeightFloat.Subtract(spacing, rz);
            }
        }
        else
        {
            float complementX = UnitWorldGroundHeightFloat.Subtract(spacing, rx);
            if (UnitWorldGroundHeightFloat.Add(rz, complementX) < spacing)
            {
                baseQ = samples[a + 1] >> 8;
                firstQ = packedA >> 8;
                secondQ = samples[a + stride + 1] >> 8;
                rx = complementX;
            }
            else
            {
                baseQ = samples[a + stride] >> 8;
                firstQ = samples[a + stride + 1] >> 8;
                secondQ = packedA >> 8;
                rz = UnitWorldGroundHeightFloat.Subtract(spacing, rz);
            }
        }

        float first = Decode(firstQ, scale, minimum);
        float baseline = Decode(baseQ, scale, minimum);
        float inverse = UnitWorldGroundHeightFloat.Divide(1f, spacing);
        float second = Decode(secondQ, scale, minimum);
        float weightZ = UnitWorldGroundHeightFloat.Multiply(inverse, rz);
        float weightX = UnitWorldGroundHeightFloat.Multiply(inverse, rx);
        float firstDelta = UnitWorldGroundHeightFloat.Subtract(first, baseline);
        float secondDelta = UnitWorldGroundHeightFloat.Subtract(second, baseline);
        float termX = UnitWorldGroundHeightFloat.Multiply(weightX, opposite ? firstDelta : secondDelta);
        float termZ = UnitWorldGroundHeightFloat.Multiply(weightZ, opposite ? secondDelta : firstDelta);
        return UnitWorldGroundHeightFloat.Add(baseline,
            UnitWorldGroundHeightFloat.Add(termX, termZ));
    }

    private static int CellIndex(float local, float spacing, int extent)
    {
        if (local < 0) return 0;
        float quotient = UnitWorldGroundHeightFloat.Divide(local, spacing);
        // CVTTSS2SI returns the indefinite integer on overflow. The native
        // signed clamp then selects the first cell, even for huge positive X/Z.
        int index = quotient >= 2147483648f || !float.IsFinite(quotient)
            ? int.MinValue : (int)quotient;
        return Math.Clamp(index, 0, extent - 1);
    }

    private static float Decode(uint q, float scale, float minimum) =>
        UnitWorldGroundHeightFloat.Add(
            UnitWorldGroundHeightFloat.Multiply(q, scale), minimum);

    private static float Known(float? value, string field)
    {
        float known = value ?? throw Unknown(field);
        if (!float.IsFinite(known)) throw new InvalidDataException($"Current GroundHeight grid {field} is not finite.");
        return known;
    }
    private static InvalidDataException Unknown(string field) =>
        new($"Current GroundHeight grid {field} is unknown.");
}

internal static class UnitWorldGroundHeightFloat
{
    // Separate boundaries forbid multiply/add fusion and preserve scalar SSE order.
    [MethodImpl(MethodImplOptions.NoInlining)] public static float Add(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] public static float Subtract(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] public static float Multiply(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] public static float Divide(float a, float b) => a / b;
    [MethodImpl(MethodImplOptions.NoInlining)] public static float Remainder(float a, float b) => a % b;
}
