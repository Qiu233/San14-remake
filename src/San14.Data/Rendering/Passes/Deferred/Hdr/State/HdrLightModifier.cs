using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Current color adjustment supplied by a light's optional modifier.
/// Directional, ambient and hemisphere factors are independent; alpha is retained.</summary>
public readonly record struct HdrLightModifier(bool Present, bool AdjustsLighting,
    Vector4 DirectionalFactors, float AmbientFactor, float HemisphereFactor)
{
    public Vector2 OcclusionRates { get; init; } = Vector2.One;
    public bool RequiresAdjustment(bool suppressAmbient) => suppressAmbient || Present && AdjustsLighting;

    public float DirectionalFactor(int index) => index switch
    {
        0 => DirectionalFactors.X, 1 => DirectionalFactors.Y,
        2 => DirectionalFactors.Z, 3 => DirectionalFactors.W,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    internal static Vector4 ScaleRgb(Vector4 color, float factor)
    {
        if (!float.IsFinite(factor)) throw new InvalidDataException("Nonfinite HDR color multiplier.");
        if (factor == 1) return color;
        if (!float.IsFinite(color.X) || !float.IsFinite(color.Y) || !float.IsFinite(color.Z))
            throw new InvalidDataException("Nonfinite HDR color to scale.");
        return new(Multiply(color.X, factor), Multiply(color.Y, factor), Multiply(color.Z, factor), color.W);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Multiply(float first, float second) => first * second;
}
