namespace San14.Data;

public readonly record struct RenderTextureFormats(uint ResourceFormat,
    uint ShaderViewFormat, uint TargetViewFormat);

/// <summary>Verified native format rows and finite color-space modes. Numeric values are
/// original DXGI codes, not Godot device enums. A missing native view is rejected.</summary>
public static class RenderTextureFormatMath
{
    public static RenderTextureFormats Resolve(RenderTextureDefinition texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        return Resolve(texture.PixelFormat, texture.ColorSpace, texture.OutputColorSpace);
    }

    public static RenderTextureFormats Resolve(byte pixelFormat, byte colorSpace, byte outputColorSpace)
    {
        if (colorSpace > 1 || outputColorSpace > 2)
            throw new InvalidDataException("Original render texture color-space mode is outside the verified source range.");
        var row = pixelFormat switch
        {
            0x00 => (Resource: 27u, ShaderBase: 28u, ShaderAlternate: 29u, TargetBase: 28u, TargetAlternate: 29u),
            0x01 => (Resource: 90u, ShaderBase: 87u, ShaderAlternate: 91u, TargetBase: 87u, TargetAlternate: 91u),
            0x09 => (Resource: 27u, ShaderBase: 28u, ShaderAlternate: 0u, TargetBase: 28u, TargetAlternate: 0u),
            0x0A => (Resource: 90u, ShaderBase: 87u, ShaderAlternate: 0u, TargetBase: 87u, TargetAlternate: 0u),
            0x02 => (Resource: 39u, ShaderBase: 41u, ShaderAlternate: 0u, TargetBase: 41u, TargetAlternate: 0u),
            0x2A or 0x72 => (Resource: 61u, ShaderBase: 61u, ShaderAlternate: 0u, TargetBase: 61u, TargetAlternate: 0u),
            0x41 => (Resource: 9u, ShaderBase: 11u, ShaderAlternate: 0u, TargetBase: 11u, TargetAlternate: 0u),
            0x4C => (Resource: 15u, ShaderBase: 16u, ShaderAlternate: 0u, TargetBase: 16u, TargetAlternate: 0u), // RG32 float.
            0x4E => (Resource: 39u, ShaderBase: 41u, ShaderAlternate: 0u, TargetBase: 40u, TargetAlternate: 0u),
            0x69 => (Resource: 33u, ShaderBase: 34u, ShaderAlternate: 0u, TargetBase: 34u, TargetAlternate: 0u),
            0x6B => (Resource: 26u, ShaderBase: 26u, ShaderAlternate: 0u, TargetBase: 26u, TargetAlternate: 0u),
            0x6C => (Resource: 19u, ShaderBase: 21u, ShaderAlternate: 0u, TargetBase: 20u, TargetAlternate: 0u),
            _ => throw new InvalidDataException($"Original render texture format {pixelFormat:X2} has no verified device recipe.")
        };
        byte targetMode = outputColorSpace == 2 ? colorSpace : outputColorSpace;
        uint shader = colorSpace == 0 ? row.ShaderBase : row.ShaderAlternate;
        uint target = targetMode == 0 ? row.TargetBase : row.TargetAlternate;
        if (shader == 0 || target == 0)
            throw new InvalidDataException("Original render texture format has no native view for the selected color-space modes.");
        return new(row.Resource, shader, target);
    }
}
