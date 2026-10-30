namespace San14.Data;

/// <summary>Storage owned by an actual probe-field instance. Allocation does
/// not assert that capture, SH integration, or filtering has produced pixels.</summary>
public sealed record ReflectionCaptureFieldResourcePlan(
    ReflectionCaptureFieldDefinition Field, uint ProbeCount, uint AtlasWidth,
    uint AtlasHeight, uint AtlasLayers, uint RadianceMipLevels,
    uint CaptureArrayLayers, ulong SphericalHarmonicsBytes, ulong SkyVisibilityBytes)
{
    public const uint ColorPixelFormat = 0x6B;
    public const uint SphericalHarmonicsStride = 108;

    /// <summary>The normal field factory sets capture storage present. The
    /// grid owns one 27-float SH row and one sky scalar per actual cell.</summary>
    public static ReflectionCaptureFieldResourcePlan Prepare(ReflectionCaptureFieldDefinition field)
    {
        ArgumentNullException.ThrowIfNull(field);
        // These are necessary allocation/division checks, not authored value
        // defaults: the source divides by tile size and tiles-per-row before
        // allocating its atlas. An invalid declaration must not become Empty.
        if (field.TileSize == 0 || field.GBufSize / field.TileSize == 0)
            throw new InvalidDataException("reflection.capture.field.atlas_tile_extent");
        uint count = checked(field.FieldDimension.X * field.FieldDimension.Y * field.FieldDimension.Z);
        uint row = field.GBufSize / field.TileSize;
        uint cellsPerLayer = checked(row * row);
        // FD95A0 converts the actual integer population and layer capacity to
        // float before ceil, rather than rounding with an integer substitute.
        float layersValue = MathF.Ceiling((float)count / (float)cellsPerLayer);
        if (!float.IsFinite(layersValue) || layersValue < 1 || layersValue > uint.MaxValue)
            throw new InvalidDataException("reflection.capture.field.atlas_array_extent");
        uint layers = checked((uint)layersValue);
        uint levels = 1;
        for (uint tile = field.TileSize; tile > 4; tile >>= 1) levels++;
        return new(field, count, field.GBufSize, field.GBufSize, layers, levels,
            checked(layers * 3), checked((ulong)count * SphericalHarmonicsStride), checked((ulong)count * 4));
    }
}
