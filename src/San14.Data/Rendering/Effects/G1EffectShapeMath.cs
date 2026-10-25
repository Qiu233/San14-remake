namespace San14.Data;

/// <summary>Scalar approximation algorithms used by the source shape samplers.</summary>
internal static class G1EffectShapeMath
{
    internal static (float Sine, float Cosine) SinCos(float angle)
    {
        if (angle == 0) return (0, 1);
        bool positiveSine = angle >= 0;
        if (!positiveSine) angle = -angle;
        while (angle >= 6.2831855f) angle -= 6.2831855f;
        bool positiveCosine = angle < 3.1415927f;
        if (!positiveCosine)
        {
            angle -= 3.1415927f;
            positiveSine = !positiveSine;
        }
        if (angle >= 1.5707964f)
        {
            angle = 3.1415927f - angle;
            positiveCosine = !positiveCosine;
        }
        if (angle < 0.6683185f)
        {
            float square = angle * angle;
            float sinePolynomial = positiveSine
                ? (6f - square) * 20f + square * square
                : (square - 6f) * 20f - square * square;
            float sine = sinePolynomial * angle * 0.008333334f;
            float cosine = CosinePolynomial(square, positiveCosine);
            return (sine, cosine);
        }
        if (angle < 0.9083185f)
        {
            float cosine = CosinePolynomial(angle * angle, positiveCosine);
            float complement = 1.5707964f - angle;
            float sine = CosinePolynomial(complement * complement, positiveSine);
            return (sine, cosine);
        }
        angle = 1.5707964f - angle;
        float squared = angle * angle;
        float cosinePolynomial = positiveCosine
            ? (6f - squared) * 20f + squared * squared
            : (squared - 6f) * 20f - squared * squared;
        return (CosinePolynomial(squared, positiveSine), cosinePolynomial * angle * 0.008333334f);
    }

    private static float CosinePolynomial(float square, bool positive) => positive
        ? ((square * square + 360f) * (30f - square) - 10080f) * 0.0013888889f
        : ((square * square + 360f) * (square - 30f) + 10080f) * 0.0013888889f;

    internal static float CubeRoot(float value)
    {
        float sign = value >= 0 ? 1f : -1f;
        value = MathF.Abs(value);
        if (value == 0) return value;
        // frexp decomposes the exact float input into [0.5, 1) and a binary
        // exponent. The polynomial and remainder multipliers stay float32.
        int exponent = Math.ILogB(value) + 1;
        float mantissa = (float)Math.ScaleB(value, -exponent);
        float x = mantissa - 0.75f;
        float square = x * x;
        float polynomial = (((x * 0.40380457f + 0.9085603f) - square * 0.1794687f) +
            square * x * 0.13293977f) - square * square * 0.11816869f;
        int quotient = (exponent < 0 ? exponent - 2 : exponent) / 3;
        float remainderMultiplier = (exponent - quotient * 3) switch
        {
            0 => 1f,
            1 => BitConverter.Int32BitsToSingle(0x3FA14518),
            _ => BitConverter.Int32BitsToSingle(0x3FCB2FF5)
        };
        return (float)Math.ScaleB(polynomial, quotient) * remainderMultiplier * sign;
    }
}
