using System.Numerics;

namespace San14.Data;

/// <summary>One current field cell. Its grid position is axis aligned: the
/// field declaration's quaternion is not read by this source geometry path.</summary>
public readonly record struct ReflectionCaptureFieldCellGeometry(
    uint SourceOrdinal, Vector4 Position, Vector4 Scale, float ViewRange,
    float ViewRangeSquared, Vector4 BoundsMinimum, Vector4 BoundsMaximum);

/// <summary>The actual capture camera inputs after copying the selected
/// current View camera and applying this cell's capture writes. This is not
/// a camera declaration or a replacement for that current View owner.</summary>
public readonly record struct ReflectionCaptureCameraInputs(
    Vector4 Eye, Vector4 Target, Vector4 Up, CameraLens Lens,
    float ViewDistance9F0, float ViewDistance9EC)
{
    public MapCameraConstructionMatrices BuildMatrices(uint actualClipProfile,
        NativeCameraTangentDispatch actualTangentDispatch) =>
        MapCameraConstructionMath.CompleteAB6C60(Eye, Target, Up, Lens,
            actualClipProfile, actualTangentDispatch);
}

/// <summary>Normal probe grid and capture geometry. The caller supplies the
/// actual manager's current position and the registered View's current camera;
/// neither is synthesized from an unrelated map camera or a unit matrix.</summary>
public static class ReflectionCaptureFieldGeometry
{
    /// <summary>Reads one source ordinal using the native unsigned X modulus,
    /// signed ordinal-to-float division, double remainder for Y, and floor for Z.
    /// World position is the current manager's four-component position, which
    /// can differ from the original declaration after the field position setter.</summary>
    public static ReflectionCaptureFieldCellGeometry ReadCell(
        ReflectionCaptureFieldDefinition field, uint sourceOrdinal,
        Vector4 currentManagerPosition70)
    {
        ArgumentNullException.ThrowIfNull(field);
        var dimensions = field.FieldDimension;
        uint plane = unchecked(dimensions.X * dimensions.Y);
        // The actual source performs an integer modulus and two divisions.
        // A malformed zero divisor cannot supply usable capture geometry.
        if (dimensions.X == 0 || dimensions.Y == 0 || plane == 0)
            throw new InvalidDataException("reflection.capture.field.grid_divisor");

        float ordinal = (float)unchecked((int)sourceOrdinal);
        float rowQuotient = ordinal / (float)dimensions.X;
        double rowRemainder = (double)rowQuotient % (double)dimensions.Y;
        float planeQuotient = ordinal / (float)plane;
        int x = unchecked((int)((ulong)sourceOrdinal % dimensions.X));
        int y = unchecked((int)rowRemainder);
        int z = unchecked((int)MathF.Floor(planeQuotient));
        float size = field.CellSize;
        Vector4 position = new(
            NativeFloat.Add(NativeFloat.Multiply(size, (float)x), currentManagerPosition70.X),
            NativeFloat.Add(NativeFloat.Multiply(size, (float)y), currentManagerPosition70.Y),
            NativeFloat.Add(NativeFloat.Multiply(size, (float)z), currentManagerPosition70.Z),
            NativeFloat.Add(NativeFloat.Multiply(size, 0f), currentManagerPosition70.W));

        float range = field.ViewRange;
        float half = NativeFloat.Multiply(range, .5f);
        Vector4 minimum = new(NativeFloat.Subtract(position.X, half),
            NativeFloat.Subtract(position.Y, half), NativeFloat.Subtract(position.Z, half), 1f);
        Vector4 maximum = new(NativeFloat.Add(position.X, half),
            NativeFloat.Add(position.Y, half), NativeFloat.Add(position.Z, half), 1f);
        return new(sourceOrdinal, position, new(1f, 1f, 1f, 1f), range,
            NativeFloat.Multiply(range, range), minimum, maximum);
    }

    /// <summary>Copies the source View's Up and lens, then applies only the
    /// capture camera writes. In particular Near, ViewOffset, orthographic
    /// scale and flags remain the actual View values. Extra view distances
    /// correspond to the separate extended-camera writes, not lens Near.</summary>
    public static ReflectionCaptureCameraInputs PrepareCamera(
        ReflectionCaptureFieldCellGeometry cell, Vector4 actualCurrentViewUp30,
        CameraLens actualCurrentViewLens)
    {
        Vector4 position = cell.Position;
        float eyeY = NativeFloat.Add(NativeFloat.Add(cell.ViewRange, cell.ViewRange), position.Y);
        Vector4 eye = new(position.X, eyeY, position.Z, position.W);
        float far = NativeFloat.Multiply(cell.ViewRange, 3.5f);
        CameraLens lens = actualCurrentViewLens with
        {
            Far = far,
            VerticalFovRadians = BitConverter.UInt32BitsToSingle(0x3FC90FDB),
            Aspect = 1f
        };
        return new(eye, position, actualCurrentViewUp30, lens, far,
            NativeFloat.Add(far, 100f));
    }
}
