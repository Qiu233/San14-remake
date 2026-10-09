namespace San14.Data;

public readonly record struct RenderSurface(uint MipLevel, uint FirstArraySlice, uint ArraySize);

/// <summary>Finite original CPU target-view layout recovered from the source loaders.
/// This resolves declaration IDs, without observing current GPU attachments.</summary>
public static class RenderSurfaceMath
{
    public static RenderSurface Resolve(RenderTextureDefinition texture, uint surfaceId)
    {
        ArgumentNullException.ThrowIfNull(texture);
        if (texture.LevelNum != 1 || texture.MinMipLevel != 0)
            throw new InvalidDataException("Original target mip layout is outside the verified single-level source range.");
        if (texture.Kind == RenderTextureKind.Texture2D)
        {
            if (surfaceId != 0)
                throw new InvalidDataException("Original single-level 2D target has no requested surface.");
            return new(0, 0, 1);
        }
        if (texture.NeedSliceTargets != 1 || texture.NeedAllTargets != 0 || texture.ArrayFlag != 0 ||
            texture.ArrayDepth is not > 0)
            throw new InvalidDataException("Original array target mode is outside the verified per-slice source range.");
        if (surfaceId >= (uint)texture.ArrayDepth.Value)
            throw new InvalidDataException("Original array SurfaceID exceeds its declared layer count.");
        return new(0, surfaceId, 1);
    }
}
