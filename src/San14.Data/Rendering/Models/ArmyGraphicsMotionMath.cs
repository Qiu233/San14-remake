using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>CE 3F76A0 and 2AED40 scalar heading arithmetic in source X/Z coordinates.</summary>
public static class ArmyGraphicsMotionMath
{
    private const float Tau = 6.2831855f;

    /// <summary>Heading of a horizontal target delta. The source caller supplies
    /// a nonzero delta; zero has the source floating-point NaN result.</summary>
    public static float Heading(float deltaX, float deltaZ)
    {
        float length = MathF.Sqrt(Add(Add(Product(deltaX, deltaX), Product(0f, 0f)),
            Product(deltaZ, deltaZ)));
        float y = Divide(Subtract(Product(deltaZ, 0f), deltaX), length);
        float x = Divide(Add(Product(deltaX, 0f), deltaZ), length);
        float angle = Product(MathF.Atan2(y, x), -1f);
        while (angle >= Tau) angle = Add(angle, -Tau);
        while (angle < 0f) angle = Add(angle, Tau);
        return angle;
    }

    /// <summary>Original Unit direction byte to heading in radians; unmapped bytes yield zero.</summary>
    public static float Direction(byte sourceDirection)
    {
        float degrees = sourceDirection switch
        {
            1 => 240f,
            2 => 180f,
            3 => 120f,
            4 => 300f,
            6 => 60f,
            _ => 0f
        };
        return Product(degrees, 0.017453292f);
    }

    [MethodImpl(MethodImplOptions.NoInlining)] private static float Product(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Subtract(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Divide(float a, float b) => a / b;
}
