namespace San14.Data;

public readonly record struct OriginalModelTextureFormats(uint ResourceFormat, uint ShaderViewFormat);

/// <summary>Exact original texture resource/SRV rows. ColorSpace is an explicit
/// source input:0 selects the ordinary view,1 the alternate view. A zero
/// alternate format is unavailable and never synthesized.</summary>
public static class OriginalModelTextureFormatMath
{
    public static OriginalModelTextureFormats Resolve(byte pixelFormat, byte explicitColorSpace)
    {
        if (explicitColorSpace > 1)
            throw new InvalidDataException("Original model texture color-space mode is outside the source loader range.");
        var row = pixelFormat switch
        {
            0x00 => (Resource: 27u, Base: 28u, Alternate: 29u),
            0x01 => (Resource: 90u, Base: 87u, Alternate: 91u),
            0x02 => (Resource: 39u, Base: 41u, Alternate: 0u),
            0x03 => (Resource: 9u, Base: 10u, Alternate: 0u),
            0x04 => (Resource: 1u, Base: 2u, Alternate: 0u),
            0x59 => (Resource: 70u, Base: 71u, Alternate: 72u),
            0x5B => (Resource: 76u, Base: 77u, Alternate: 78u),
            0x5C => (Resource: 79u, Base: 80u, Alternate: 0u),
            0x5E => (Resource: 94u, Base: 95u, Alternate: 0u),
            0x5F => (Resource: 97u, Base: 98u, Alternate: 99u),
            _ => throw new InvalidDataException($"Original model texture format {pixelFormat:X2} has no verified resource/SRV row.")
        };
        uint shader = explicitColorSpace == 0 ? row.Base : row.Alternate;
        if (shader == 0) throw new InvalidDataException("Original model texture has no source view in the requested color space.");
        return new(row.Resource, shader);
    }
}
