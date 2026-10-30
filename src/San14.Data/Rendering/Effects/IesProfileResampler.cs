namespace San14.Data;

/// <summary>Generates a float IES map layer from a prepared candela lattice.</summary>
public static class IesProfileResampler
{
    // The original pixel program's literal bits retain its acos approximation.
    private static readonly float Polynomial0 = BitConverter.Int32BitsToSingle(unchecked((int)0xbc996e30));
    private static readonly float Polynomial1 = BitConverter.Int32BitsToSingle(0x3d981627);
    private static readonly float Polynomial2 = BitConverter.Int32BitsToSingle(unchecked((int)0xbe593484));
    private static readonly float Polynomial3 = BitConverter.Int32BitsToSingle(0x3fc90da4);
    private static readonly float Pi = BitConverter.Int32BitsToSingle(0x40490fdb);
    private static readonly float InverseTwoPi = BitConverter.Int32BitsToSingle(0x3e22f983);
    private static readonly float NegativeInverseTwoPi = BitConverter.Int32BitsToSingle(unchecked((int)0xbe22f983));
    private static readonly float InversePi = BitConverter.Int32BitsToSingle(0x3ea2f983);

    public static float[] Resample(ReadOnlySpan<float> preparedCandela,
        int sourceWidth, int sourceHeight, float scale, int outputWidth, int outputHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputHeight);
        if (preparedCandela.Length != checked(sourceWidth * sourceHeight))
            throw new ArgumentException("Candela lattice dimensions do not match its length.", nameof(preparedCandela));

        var result = new float[checked(outputWidth * outputHeight)];
        var sampleXs = new float[outputWidth];
        for (int x = 0; x < outputWidth; x++)
        {
            float u = (x + 0.5f) / outputWidth;
            float vertical = ApproximateAcos((-u * 2f) + 1f) * InversePi;
            sampleXs[x] = ((vertical * (sourceWidth - 1f)) + 0.5f) / sourceWidth;
        }

        for (int y = 0; y < outputHeight; y++)
        {
            float v = (y + 0.5f) / outputHeight;
            // Only the selected half is needed. The unused original shader
            // candidate can contain a square root outside its valid domain.
            float horizontal = v < 0.5f
                ? ApproximateAcos((0.25f - v) * 4f) * InverseTwoPi
                : ApproximateAcos((v - 0.75f) * 4f) * NegativeInverseTwoPi;
            float sampleY = (0.5f / sourceHeight) + horizontal;
            int destination = y * outputWidth;
            for (int x = 0; x < outputWidth; x++)
                result[destination + x] = SampleLinear(preparedCandela, sourceWidth, sourceHeight,
                    sampleXs[x], sampleY) * scale;
        }
        return result;
    }

    private static float ApproximateAcos(float value)
    {
        float absolute = MathF.Abs(value);
        float polynomial = (absolute * Polynomial0) + Polynomial1;
        polynomial = (polynomial * absolute) + Polynomial2;
        polynomial = (polynomial * absolute) + Polynomial3;
        float root = MathF.Sqrt(1f - absolute);
        float product = root * polynomial;
        float correction = value < -value ? ((product * -2f) + Pi) : 0f;
        return (polynomial * root) + correction;
    }

    private static float SampleLinear(ReadOnlySpan<float> source, int width, int height, float u, float v)
    {
        // A normalized texture coordinate addresses texel centers at (i+.5)/N.
        // The source has only mip 0; its sampler clamps U and wraps V.
        float texelX = (u * width) - 0.5f;
        float texelY = (v * height) - 0.5f;
        int left = (int)MathF.Floor(texelX), top = (int)MathF.Floor(texelY);
        float fractionX = texelX - left, fractionY = texelY - top;
        int x0 = Math.Clamp(left, 0, width - 1), x1 = Math.Clamp(left + 1, 0, width - 1);
        int y0 = top % height;
        if (y0 < 0) y0 += height;
        int y1 = y0 + 1 == height ? 0 : y0 + 1;
        int row0 = y0 * width, row1 = y1 * width;
        float upper = (source[row0 + x0] * (1f - fractionX)) + (source[row0 + x1] * fractionX);
        float lower = (source[row1 + x0] * (1f - fractionX)) + (source[row1 + x1] * fractionX);
        return (upper * (1f - fractionY)) + (lower * fractionY);
    }
}
