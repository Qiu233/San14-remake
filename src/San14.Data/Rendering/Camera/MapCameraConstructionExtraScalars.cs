namespace San14.Data;

/// <summary>Exact fresh source-to-prepared scalar copies outside the early3C0 prefix.
/// Later per-view functors remain their own current-state writers.</summary>
public readonly record struct MapCameraConstructionExtraScalars(
    float FxaaEdgeSensitivity404, float FxaaEdgeSensitivityMin408,
    float FxaaSharpness40C, float FxaaPixelRange410,
    float LimbDarkeningWeight7B0, float DistantBlurZThresholdE78, float DistantBlurIntensityE7C,
    int Lut1Type13B0, int Lut2Type13B4, float LutInterpolation13B8, float LutIntensity13BC)
{
    /// <summary>C42070 copies each source CPU word verbatim; no synthetic complete tail is needed.</summary>
    public static MapCameraConstructionExtraScalars FromSource(
        LoadedMapCameraConstructionSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        float F(int offset) => BitConverter.UInt32BitsToSingle(source.CpuWord(offset));
        return new(F(0x1A8), F(0x1AC), F(0x1B0), F(0x1B4),
            F(0x524), F(0xBE8), F(0xBEC),
            unchecked((int)source.CpuWord(0x10EC)), unchecked((int)source.CpuWord(0x10F0)),
            F(0x10F4), F(0x10F8));
    }
}
