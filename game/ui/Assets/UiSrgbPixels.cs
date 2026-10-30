using San14.Data.Formats;

/// <summary>Byte-exact sRGB decoding for the UI sprite's linear sampling image.</summary>
internal static class UiSrgbPixels
{
    private static readonly float[] linear = CreateLinearTable();

    private static float[] CreateLinearTable()
    {
        var values = new float[256];
        for (int index = 0; index < values.Length; index++)
        {
            float value = index / 255f;
            values[index] = value <= .04045f ? value / 12.92f :
                MathF.Pow((value + .055f) / 1.055f, 2.4f);
        }
        return values;
    }

    // Keep the original transfer expression, one-texel border and addressing.
    // Input channels are bytes, so its 256 results cover every source value;
    // recomputing the same power for each texel adds no image information.
    internal static float[] Bordered(RgbaTexture source, int left, int top, int width, int height,
        bool wrapU, bool wrapV)
    {
        var pixels = new float[(width + 2) * (height + 2) * 4];
        for (int y = 0; y < height + 2; y++)
        {
            int row = Coordinate(top + y - 1, source.Height, wrapV) * source.Width;
            for (int x = 0; x < width + 2; x++)
            {
                int input = (row + Coordinate(left + x - 1, source.Width, wrapU)) * 4;
                int output = (y * (width + 2) + x) * 4;
                pixels[output] = linear[source.Pixels[input]];
                pixels[output + 1] = linear[source.Pixels[input + 1]];
                pixels[output + 2] = linear[source.Pixels[input + 2]];
                pixels[output + 3] = source.Pixels[input + 3] / 255f;
            }
        }
        return pixels;
    }

    private static int Coordinate(int value, int extent, bool wrap) => wrap
        ? ((value % extent) + extent) % extent : Math.Clamp(value, 0, extent - 1);
}
