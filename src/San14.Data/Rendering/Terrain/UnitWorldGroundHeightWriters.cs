using System.Numerics;

namespace San14.Data;

/// <summary>The observed branch in Field resource preparation. The caller must
/// supply the proven manager/allocation outcome; unknown context is not zero.</summary>
public enum UnitWorldGroundHeightFieldPreparationBranch
{
    TerrainManagerAbsent,
    WrapperAllocationAbsent,
    RetainedWrapperCreated
}

public static class UnitWorldGroundHeightWriters
{
    /// <summary>SC A0D650 / TC A0C650; input is a center position. Returns
    /// updated grid fields; native wrapper caches must subsequently be copied.</summary>
    public static UnitWorldGroundHeightGrid SetPositionA0D650(
        UnitWorldGroundHeightGrid grid, Vector3 position)
    {
        ArgumentNullException.ThrowIfNull(grid);
        Check(position);
        float midpoint = Add(Known(grid.MaximumHeight54, "+54 maximum"), Known(grid.MinimumHeight50, "+50 minimum"));
        float x = Multiply(Known(grid.ExtentX18, "+18 X extent"), .5f);
        float z = Multiply(Known(grid.ExtentZ1C, "+1C Z extent"), .5f);
        midpoint = Multiply(midpoint, .5f);
        x = Subtract(x, position.X);
        float y = Subtract(midpoint, position.Y);
        z = Subtract(z, position.Z);
        return grid with { OriginX20 = x, OriginY28 = y, OriginZ24 = z };
    }

    /// <summary>SC A0D5C0 / TC A0C5C0. Source spacing uses X, height uses Y,
    /// existing horizontal origin offsets use X and Z respectively.</summary>
    public static UnitWorldGroundHeightGrid SetScaleA0D5C0(
        UnitWorldGroundHeightGrid grid, Vector3 scale)
    {
        ArgumentNullException.ThrowIfNull(grid);
        Check(scale);
        float spacing = Multiply(Known(grid.SampleSpacing4C, "+4C spacing"), scale.X);
        float minimum = Multiply(scale.Y, Known(grid.MinimumHeight50, "+50 minimum"));
        float maximum = Multiply(scale.Y, Known(grid.MaximumHeight54, "+54 maximum"));
        float range = Subtract(maximum, minimum);
        float heightScale = Multiply(range, BitConverter.Int32BitsToSingle(0x33800001));
        int width = grid.CellWidth48 ?? throw Unknown("+48 width");
        int height = grid.CellHeight4A ?? throw Unknown("+4A height");
        float extentX = Multiply(width, spacing), extentZ = Multiply(height, spacing);
        float originX = Multiply(scale.X, Known(grid.OriginX20, "+20 X origin"));
        float originZ = Multiply(scale.Z, Known(grid.OriginZ24, "+24 Z origin"));
        return grid with { SampleSpacing4C = spacing, MinimumHeight50 = minimum, MaximumHeight54 = maximum,
            HeightScale14 = heightScale, ExtentX18 = extentX, ExtentZ1C = extentZ,
            OriginX20 = originX, OriginZ24 = originZ };
    }

    /// <summary>SC A0D740 / TC A0C740. Rewrites unsigned packed heights while
    /// retaining every low-byte flag. wrapperMinimum20 is the wrapper's stored
    /// current minimum, which this function reads independently of grid+50.</summary>
    public static UnitWorldGroundHeightGrid RequantizeRangeA0D740(
        UnitWorldGroundHeightGrid grid, float? wrapperMinimum20, float maximum, float minimum)
    {
        ArgumentNullException.ThrowIfNull(grid);
        float priorMinimum = Known(wrapperMinimum20, "wrapper+20 minimum");
        float priorScale = Known(grid.HeightScale14, "+14 scale");
        if (!float.IsFinite(maximum) || !float.IsFinite(minimum) || maximum <= minimum)
            throw new InvalidDataException("Finite native requantization requires a positive target height range.");
        float range = Subtract(maximum, minimum);
        float scale = Multiply(range, BitConverter.Int32BitsToSingle(0x33800001));
        if (!float.IsFinite(scale) || scale <= 0)
            throw new InvalidDataException("Finite native requantization target scale is invalid.");
        int width = grid.CellWidth48 ?? throw Unknown("+48 width");
        int height = grid.CellHeight4A ?? throw Unknown("+4A height");
        uint[] prior = grid.PackedSamples58 ?? throw Unknown("+58 samples");
        if (width is < 1 or > ushort.MaxValue || height is < 1 or > ushort.MaxValue ||
            prior.LongLength != (width + 1L) * (height + 1L))
            throw new InvalidDataException("Native requantization requires a complete grid.");
        var output = new uint[prior.Length];
        float inverse = Divide(1f, scale);
        for (int i = 0; i < output.Length; i++)
        {
            float value = Multiply(prior[i] >> 8, priorScale);
            value = Add(value, priorMinimum);
            value = Subtract(value, minimum);
            value = Multiply(value, inverse);
            // Native CVTTSS2SI uses a 64-bit destination before shifting ECX.
            long integer = !float.IsFinite(value) || value >= 9223372036854775808f || value < -9223372036854775808f
                ? long.MinValue : (long)value;
            output[i] = (unchecked((uint)integer) << 8) | (prior[i] & 0xFFu);
        }
        return grid with { PackedSamples58 = output, MaximumHeight54 = maximum,
            MinimumHeight50 = minimum, HeightScale14 = scale };
    }

    /// <summary>Known fresh Field+B0 prefix in SC BC49C0 / TC BC32D0.
    /// Does not infer that a manager/wrapper was created or that later edits
    /// did not occur. Kind7 initial A297B0 dispatch does not change the grid.</summary>
    public static UnitWorldGroundHeightGrid PrepareFreshFieldBc49C0(
        LoadedHeightField source, UnitWorldGroundHeightFieldPreparationBranch? branch)
    {
        if (branch is null || !Enum.IsDefined(branch.Value))
            throw new InvalidDataException("Current native Field preparation manager/wrapper branch is unknown.");
        var grid = UnitWorldGroundHeightGrid.ConstructMhkA0CD50(source);
        if (branch != UnitWorldGroundHeightFieldPreparationBranch.RetainedWrapperCreated) return grid;
        float minimum = Subtract(Known(grid.MinimumHeight50, "+50 minimum"), 1000f);
        float maximum = Add(Known(grid.MaximumHeight54, "+54 maximum"), 1000f);
        return RequantizeRangeA0D740(grid, grid.MinimumHeight50, maximum, minimum);
    }

    private static void Check(Vector3 value)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
            throw new InvalidDataException("Native ground grid writer requires finite vector inputs.");
    }
    private static float Known(float? value, string field)
    {
        float known = value ?? throw Unknown(field);
        if (!float.IsFinite(known)) throw new InvalidDataException($"Current ground grid writer {field} is not finite.");
        return known;
    }
    private static InvalidDataException Unknown(string field) => new($"Current ground grid writer {field} is unknown.");
    private static float Add(float a, float b) => UnitWorldGroundHeightFloat.Add(a, b);
    private static float Subtract(float a, float b) => UnitWorldGroundHeightFloat.Subtract(a, b);
    private static float Multiply(float a, float b) => UnitWorldGroundHeightFloat.Multiply(a, b);
    private static float Divide(float a, float b) => UnitWorldGroundHeightFloat.Divide(a, b);
}
