using System.Numerics;

namespace San14.Data;

public sealed record OriginalNativeSinCos(float Sin, float Cos);

/// <summary>Native187A40 and1C58E0 scalar arithmetic, independent of the
/// existing fitted preview projection. Current coordinate origins and the
/// original custom sine/cosine function has a built-in source implementation.
/// SinCos and Quaternion use project arithmetic in both locales.</summary>
public sealed class OriginalUnitGeometry
{
    public float? OriginX1A22578 { get; private set; }
    public float? OriginZ2143CA0 { get; private set; }
    public Func<float?>? OriginXReader { get; set; }
    public Func<float?>? OriginZReader { get; set; }
    /// <summary>Optional override of the original SinCos. An explicit unknown
    /// override result is not replaced by the built-in implementation.</summary>
    public Func<float, OriginalNativeSinCos?>? SinCos014560 { get; set; }

    public void WriteOriginX(float? value) => OriginX1A22578 = value;
    public void WriteOriginZ(float? value) => OriginZ2143CA0 = value;
    /// <summary>Explicit image seed, not a perpetual current-value default.</summary>
    public void MaterializeOriginXImageSeed() => OriginX1A22578 = 37594f;
    /// <summary>Complete0011D0 initializer. Keep its float32 operation order;
    /// it writes Z once and does not reset X or later origin changes.</summary>
    public void InitializeOriginZ()
    {
        float value = MathF.Sqrt(3f);
        value = value * 0.5f;
        value = value * 468f;
        value = value + 12930f;
        OriginZ2143CA0 = value;
    }

    public OriginalModelHexPosition NativeHexPosition187A40(int hex)
    {
        float x = 0, z = 0;
        WriteNativeHexPosition187A40(hex, value => x = value, value => z = value);
        return new(x, z);
    }
    public void WriteNativeHexPosition187A40(int hex, Action<float> writeX, Action<float> writeZ)
    {
        ArgumentNullException.ThrowIfNull(writeX);
        ArgumentNullException.ThrowIfNull(writeZ);
        int parity = hex % 2;
        int row = hex / 220;
        int column = unchecked(hex - row * 220);
        float x = column;
        x = x * 702f;
        x = x + ((OriginXReader is null ? OriginX1A22578 : OriginXReader())
            ?? throw new InvalidDataException("Current native model origin X is unknown."));
        writeX(x);
        float offset;
        if (parity < 1)
        {
            offset = MathF.Sqrt(3f);
            offset = offset * 468f;
            offset = offset * 0.5f;
        }
        else offset = 0;
        float z = MathF.Sqrt(3f);
        z = z * 468f;
        z = z * row;
        z = z + offset;
        z = z + ((OriginZReader is null ? OriginZ2143CA0 : OriginZReader())
            ?? throw new InvalidDataException("Current native model origin Z is unknown."));
        writeZ(z);
    }

    public Vector4 NativeQuaternion1C58E0(Vector3 euler)
    {
        var x = ReadSinCos(euler.X * 0.5f);
        var y = ReadSinCos(euler.Y * 0.5f);
        var z = ReadSinCos(euler.Z * 0.5f);
        float a = y.Sin * x.Sin;
        float b = y.Sin * x.Cos;
        float c = y.Cos * x.Cos;
        float d = y.Cos * x.Sin;
        float x0 = b * z.Sin;
        float x1 = d * z.Cos;
        b = b * z.Cos;
        d = d * z.Sin;
        float qx = x1 - x0;
        float z0 = a * z.Cos;
        a = a * z.Sin;
        float qy = b + d;
        float z1 = c * z.Sin;
        c = c * z.Cos;
        float qw = a + c;
        float qz = z1 - z0;
        return new(qx, qy, qz, qw);
    }
    private OriginalNativeSinCos ReadSinCos(float angle)
    {
        if (SinCos014560 is not null) return SinCos014560(angle)
            ?? throw new InvalidDataException("Current original sine/cosine result is unknown.");
        return OriginalNativeTrigonometry.SinCos014590(angle);
    }
}
