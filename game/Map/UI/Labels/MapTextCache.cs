using San14.Data.Formats;

/// <summary>Three independent text caches in the source linear colour domain.
/// Final node opacity is applied by the Canvas consumer, never per glyph.</summary>
internal sealed record MapTextCache(int Left, int Top, int Width, int Height,
    float[] Offset, float[] Stroke, float[] Body)
{
    internal static MapTextCache Build(MapFontSource source, string value,
        MapFontProfile profile)
    {
        var glyphs = new List<(G1FontGlyphPixels Glyph, int X, int Y, int Width, int Height)>();
        int cursor = 0;
        foreach (char code in value)
        {
            var glyph = source.Glyph(code, profile.Face, profile.Palette);
            var record = glyph.Record;
            glyphs.Add((glyph, cursor + (int)(record.HorizontalOffsetX * profile.ScaleX),
                -(int)(record.HorizontalOffsetY * profile.ScaleY),
                (int)(record.Width * profile.ScaleX), (int)(record.Height * profile.ScaleY)));
            cursor += (int)(record.HorizontalAdvance * profile.ScaleX) + profile.HorizontalSpace;
        }
        var strokeOffsets = profile.HasStroke ? profile.StrokeOffsets().ToArray() : [];
        var shadow = profile.ShadowOffset;
        var offsets = new List<(int X, int Y)> { (0, 0) };
        if (profile.HasShadow) offsets.Add(shadow);
        offsets.AddRange(strokeOffsets);
        // Transparent padding keeps the final linear sampler inside this cache.
        int left = glyphs.Min(g => g.X) + offsets.Min(p => p.X) - 1;
        int top = glyphs.Min(g => g.Y) + offsets.Min(p => p.Y) - 1;
        int right = glyphs.Max(g => g.X + g.Width) + offsets.Max(p => p.X) + 1;
        int bottom = glyphs.Max(g => g.Y + g.Height) + offsets.Max(p => p.Y) + 1;
        int width = right - left, height = bottom - top;
        var cache = new MapTextCache(left, top, width, height,
            new float[width * height * 4], new float[width * height * 4], new float[width * height * 4]);
        if (profile.HasShadow)
            Run(cache.Offset, shadow.X, shadow.Y,
                [profile.ShadowColourLT, profile.ShadowColourRT, profile.ShadowColourRB, profile.ShadowColourLB]);
        if (profile.HasStroke)
            foreach (var offset in strokeOffsets)
                Run(cache.Stroke, offset.X, offset.Y,
                    [profile.StrokeColour, profile.StrokeColour, profile.StrokeColour, profile.StrokeColour]);
        Run(cache.Body, 0, 0, [profile.ColourLT, profile.ColourRT, profile.ColourRB, profile.ColourLB]);
        return cache;

        void Run(float[] destination, int dx, int dy, uint[] colours)
        {
            foreach (var g in glyphs)
                for (int y = 0; y < g.Height; y++)
                    for (int x = 0; x < g.Width; x++)
                    {
                        float fx = (x + .5f) / g.Width, fy = (y + .5f) / g.Height;
                        var texel = Sample(g.Glyph, fx * g.Glyph.Record.Width - .5f,
                            fy * g.Glyph.Record.Height - .5f);
                        var tint = Corner(colours, fx, fy);
                        float alpha = texel[3] * tint[3];
                        // Stock PS3 / 95E970 rejects zero coverage before the
                        // ONE blend, so transparent body texels keep clear RGB.
                        if (alpha < .1f * .003921569f) continue;
                        int at = ((g.Y + dy + y - top) * width + g.X + dx + x - left) * 4;
                        // Ordinary cache glyphs use stock PS3 (straight RGBA)
                        // and packed551100: RGB/A ONE, INV_SRC_ALPHA, ADD.
                        for (int c = 0; c < 3; c++)
                            destination[at + c] = Math.Clamp(texel[c] * tint[c] + destination[at + c] * (1 - alpha), 0, 1);
                        destination[at + 3] = alpha + destination[at + 3] * (1 - alpha);
                    }
        }
    }

    private static float[] Sample(G1FontGlyphPixels glyph, float x, float y)
    {
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float fx = x - x0, fy = y - y0;
        var result = new float[4];
        for (int c = 0; c < 4; c++)
        {
            float a = Pixel(x0, y0, c), b = Pixel(x0 + 1, y0, c);
            float d = Pixel(x0, y0 + 1, c), e = Pixel(x0 + 1, y0 + 1, c);
            result[c] = (a + (b - a) * fx) * (1 - fy) + (d + (e - d) * fx) * fy;
        }
        return result;

        float Pixel(int px, int py, int c) => glyph.Pixels.Pixels[
            (Math.Clamp(py, 0, glyph.Pixels.Height - 1) * glyph.Pixels.Width +
             Math.Clamp(px, 0, glyph.Pixels.Width - 1)) * 4 + c] / 255f;
    }

    private static float[] Corner(uint[] colours, float x, float y)
    {
        // The original quad consists of the LT/RT/RB and LT/RB/LB triangles.
        var result = new float[4];
        for (int c = 0; c < 4; c++)
        {
            float a = Channel(colours[0]), b = Channel(colours[1]);
            float d = Channel(colours[2]), e = Channel(colours[3]);
            result[c] = x >= y ? a * (1 - x) + b * (x - y) + d * y
                : a * (1 - y) + d * x + e * (y - x);
            float Channel(uint colour) => ((colour >> (c * 8)) & 255) / 255f;
        }
        return result;
    }
}
