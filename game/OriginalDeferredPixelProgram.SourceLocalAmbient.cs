using San14.Data;

public sealed partial class OriginalDeferredPixelProgram
{
    /// <summary>Only the exact successful fresh SourceCommon/global LAS data and
    /// module construction may use original null-read semantics. Current or
    /// subsequently assigned LAS state requires its real image/typed buffers.
    /// The complete original42 RDEF rows and translation remain available.</summary>
    public static Task<OriginalDeferredPixelProgram> BuildAsync(LoadedShaderContainer source,
        int explicitCombinationIndex, Vkd3dShaderCompiler compiler,
        OriginalDeferredSourceLocalAmbient explicitFreshLocalAmbient,
        IReadOnlyList<OriginalDeferredSourceNull2D>? explicitFresh2DNulls = null)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(explicitFreshLocalAmbient);
        explicitFreshLocalAmbient.Validate();
        if (explicitFreshLocalAmbient.Shader.Source.ResourceUri != source.Source.ResourceUri ||
            explicitFreshLocalAmbient.Shader.Source.DecodedSha256 != source.Source.DecodedSha256 ||
            explicitFreshLocalAmbient.CombinationIndex != explicitCombinationIndex)
            throw new InvalidDataException("Source LAS null proof belongs to another original shader or explicit combination.");
        return BuildCoreAsync(source, explicitCombinationIndex, compiler,
            (explicitFresh2DNulls ?? []).ToArray(), explicitFreshLocalAmbient);
    }
}
