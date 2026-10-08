using San14.Data;

/// <summary>Accept a coherent explicit lighting frame into the scene's complete
/// Deferred packet. Device resources, two shadow CBs and unwritten CB0 variables
/// remain in that caller-owned packet. The main map chooses this provider.</summary>
public static class OriginalMapLightingDeferredProvider
{
    public static OriginalNativeDeferredPacket Apply(OriginalDeferredPixelProgram program,
        OriginalNativeMapFrameContext frame, OriginalNativeDeferredPacket explicitTemplate,
        OriginalMapLightingPacket lighting, out OriginalDeferredSceneConstantPacket writes)
    {
        ArgumentNullException.ThrowIfNull(program); ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(explicitTemplate); ArgumentNullException.ThrowIfNull(lighting);
        if (frame.FrameNumber != lighting.FrameNumber ||
            frame.PreparedCamera is null || frame.PreparedCamera != lighting.SceneInputs.Camera)
            throw new InvalidDataException("Deferred lighting must use the accepted map frame and the same prepared Camera as terrain/models.");
        if (program.Combination.Index != lighting.Binding.DeferredCombination ||
            lighting.Binding.DeferredShaderIdentity != program.Source.Source.ResourceUri + "@" + program.Source.Source.DecodedSha256)
            throw new InvalidDataException("Deferred lighting must use its explicitly selected original shader source and combination.");
        return explicitTemplate.ApplySceneFields(program, lighting.SceneInputs, out writes);
    }
}
