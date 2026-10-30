using System.Buffers.Binary;
using Godot;
using San14.Data.Formats;

/// <summary>Normal Type6 resource data. Binary offsets and packed values stay
/// at this layout boundary; runtime alpha and dimensions belong to the caller.</summary>
internal sealed record UiSourceType6Parameters(float WeightInput, float PixelScale,
    byte StoredByte, uint CommonFlags, float AuthoredAlpha, byte Compare,
    UiSourceType6Blend Blend)
{
    internal static UiSourceType6Parameters Read(LcskDocument layout,LcskNodeRecord node)
    {
        if (node.Type != 6 || node.RawBytes.Length != 144)
            throw new InvalidDataException($"Expected a complete Type6 node: {node.Name}.");
        var bytes = node.RawBytes.AsSpan();
        float weight = BinaryPrimitives.ReadSingleLittleEndian(bytes[64..]);
        // B11E60 introduced these two fields after LCSK6500. Older panes
        // retain the Type6 constructor's sampling scale and stored byte.
        bool samplingFields=layout.Signature is "LCSK7500" or "LCSK8500" or "LCSK9500";
        float pixel = samplingFields?BinaryPrimitives.ReadSingleLittleEndian(bytes[68..]):1f;
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(bytes[104..]);
        uint packed = BinaryPrimitives.ReadUInt32LittleEndian(bytes[108..]);
        float alpha = BinaryPrimitives.ReadSingleLittleEndian(bytes[112..]);
        byte compare = bytes[116];
        // Original panes also author zero sample spacing. It keeps all taps
        // at the same texel; PerPixel and the kernel require no division by it.
        if (!float.IsFinite(weight) || weight <= 0 || !float.IsFinite(pixel) || pixel < 0 ||
            !float.IsFinite(alpha) || alpha < 0 || alpha > 1 || compare != 7)
            throw new InvalidDataException($"Unsupported Type6 parameters: {node.Name}.");
        UiSourceType6Blend blend = packed switch
        {
            0x00001100 => UiSourceType6Blend.Replace,
            0x00918000 => UiSourceType6Blend.PreserveRgbPrepareAlpha,
            0x00098800 => UiSourceType6Blend.DestinationAlpha,
            // Alpha factor selectors 3 and 5 both resolve to D3D11
            // INV_SRC_ALPHA in the source table. Tutorial reward panels use
            // selector 5; their RGB and alpha equations are otherwise equal.
            0x00394800 or 0x00594800 => UiSourceType6Blend.DestinationAlphaSourceAlpha,
            0x00554400 => UiSourceType6Blend.SourceAlpha,
            _ => throw new InvalidDataException($"Unsupported Type6 blend {packed:X8}: {node.Name}.")
        };
        return new(weight, pixel, samplingFields?bytes[72]:(byte)0, flags, alpha, compare, blend);
    }

    // B21930 skips its draw before the kernel for weight<1 and the
    // authored unit-weight tolerance. Zero is a real animation endpoint.
    internal bool DrawEnabled => WeightInput >= 1f &&
        (WeightInput < 0.9999881f || WeightInput > 1.0000119f);

    // The source CRT exp call has not been proved bit-identical to .NET exp.
    // Keep its float input/output and accumulation boundaries explicit.
    internal float[] Weights()
    {
        var weights = new float[10];
        float variance = WeightInput * WeightInput / 100f;
        float total = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            float coordinate = (float)i + i + 1f;
            float value = MathF.Exp((coordinate * coordinate * -.5f) / variance);
            weights[i] = value;
            total += i == 0 ? value : value + value;
        }
        if (!(total > 0) || !float.IsFinite(total))
            throw new InvalidDataException("Type6 weights cannot be normalized.");
        for (int i = 0; i < weights.Length; i++) weights[i] /= total;
        return weights;
    }

    internal Vector2 PerPixel(Vector2I contextSize)
    {
        if (contextSize.X <= 0 || contextSize.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(contextSize));
        return new((1f / contextSize.X) * PixelScale, (1f / contextSize.Y) * PixelScale);
    }

    internal static Vector4 BlurRegion(Rect2I rectangle, Vector2I contextSize, Vector2I captureSize)
    {
        if (contextSize.X <= 0 || contextSize.Y <= 0 || captureSize.X <= 0 || captureSize.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(contextSize));
        float ratio = (float)contextSize.X / captureSize.X;
        float insetX = (1f / contextSize.X) * ratio * 1.5f;
        float insetY = (1f / contextSize.Y) * ratio * 1.5f;
        return new(Math.Clamp((float)rectangle.Position.X / contextSize.X + insetX, 0, 1),
            Math.Clamp((float)rectangle.Position.Y / contextSize.Y + insetY, 0, 1),
            Math.Clamp((float)rectangle.End.X / contextSize.X - insetX, 0, 1),
            Math.Clamp((float)rectangle.End.Y / contextSize.Y - insetY, 0, 1));
    }

    internal Vector4 Composite(Vector4 source, Vector4 destination)
    {
        float a = destination.W;
        Vector3 rgb = new(source.X, source.Y, source.Z);
        Vector3 dst = new(destination.X, destination.Y, destination.Z);
        return Blend switch
        {
            UiSourceType6Blend.Replace => source,
            UiSourceType6Blend.PreserveRgbPrepareAlpha => new(dst.X, dst.Y, dst.Z,
                source.W * a + a * (1 - a)),
            UiSourceType6Blend.DestinationAlpha => Rgba(rgb * a + dst * (1 - a), source.W * a),
            UiSourceType6Blend.DestinationAlphaSourceAlpha => Rgba(rgb * a + dst * (1 - a),
                source.W * source.W + a * (1 - source.W)),
            UiSourceType6Blend.SourceAlpha => Rgba(rgb * source.W + dst * (1 - source.W),
                source.W * source.W + a * (1 - source.W)),
            _ => throw new InvalidOperationException("Unsupported normal Type6 blend.")
        };
        static Vector4 Rgba(Vector3 rgb, float alpha) => new(rgb.X, rgb.Y, rgb.Z, alpha);
    }
}

internal enum UiSourceType6Blend { Replace, PreserveRgbPrepareAlpha, DestinationAlpha, DestinationAlphaSourceAlpha, SourceAlpha }
