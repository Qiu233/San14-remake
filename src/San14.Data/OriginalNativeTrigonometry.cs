using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Original 014590 scalar sine/cosine polynomial. No framework trigonometry substitution.</summary>
public static class OriginalNativeTrigonometry
{
    private const float Pi = 3.1415927410125732f;
    private const float HalfPi = 1.5707963705062866f;
    private const float Tau = 6.2831854820251465f;

    public static OriginalNativeSinCos SinCos014590(float angle)
    {
        if (!float.IsFinite(angle))
            throw new InvalidDataException("Original sine/cosine requires a finite angle.");
        if (angle == 0) return new(0, 1); // Original -0 input also takes this branch.
        bool positiveSin = angle >= 0, positiveCos = true;
        float a = MathF.Abs(angle);
        int reductions = 0;
        while (a >= Tau)
        {
            // The original can loop indefinitely on huge floats. Reject
            // excessive work instead of changing its range-reduction math.
            if (++reductions > 65536)
                throw new InvalidDataException("Original sine/cosine exceeds the bounded range-reduction contract.");
            a = OriginalNativeFloat.Add(a, -Tau);
        }
        if (a >= Pi)
        {
            a = OriginalNativeFloat.Add(a, -Pi);
            positiveSin = !positiveSin;
            positiveCos = false;
        }
        if (a >= HalfPi)
        {
            a = OriginalNativeFloat.Subtract(Pi, a);
            positiveCos = !positiveCos;
        }
        if (a < 0.668318510055542f)
            return new(Sin5(a, positiveSin), Cos6(a, positiveCos));
        float complement = OriginalNativeFloat.Subtract(HalfPi, a);
        if (a < 0.9083185195922852f)
            return new(Cos6(complement, positiveSin), Cos6(a, positiveCos));
        return new(Cos6(complement, positiveSin), Sin5(complement, positiveCos));
    }

    private static float Sin5(float a, bool positive)
    {
        float q = OriginalNativeFloat.Multiply(a, a);
        float q2 = OriginalNativeFloat.Multiply(q, q);
        float b = positive
            ? OriginalNativeFloat.Add(OriginalNativeFloat.Multiply(OriginalNativeFloat.Subtract(6, q), 20), q2)
            : OriginalNativeFloat.Subtract(OriginalNativeFloat.Multiply(OriginalNativeFloat.Subtract(q, 6), 20), q2);
        b = OriginalNativeFloat.Multiply(b, a);
        return OriginalNativeFloat.Multiply(b, 0.008333333767950535f);
    }

    private static float Cos6(float a, bool positive)
    {
        float q = OriginalNativeFloat.Multiply(a, a);
        float q2plus = OriginalNativeFloat.Add(OriginalNativeFloat.Multiply(q, q), 360);
        float b = positive
            ? OriginalNativeFloat.Subtract(OriginalNativeFloat.Multiply(q2plus, OriginalNativeFloat.Subtract(30, q)), 10080)
            : OriginalNativeFloat.Add(OriginalNativeFloat.Multiply(q2plus, OriginalNativeFloat.Subtract(q, 30)), 10080);
        return OriginalNativeFloat.Multiply(b, 0.0013888889225199819f);
    }
}

// Separate call boundaries preserve the native float operations and forbid
// collapsing multiply/add steps into a fused operation.
internal static class OriginalNativeFloat
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float Add(float left, float right) => left + right;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float Subtract(float left, float right) => left - right;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float Multiply(float left, float right) => left * right;
}
