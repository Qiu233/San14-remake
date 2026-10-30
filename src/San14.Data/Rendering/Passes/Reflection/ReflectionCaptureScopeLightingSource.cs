using System.Numerics;

namespace San14.Data;

/// <summary>Retained class18 J30 ambient parameters for one created renderer.
/// EC7100 supplies a null modifier, so capture changes the rate only.</summary>
public sealed class ReflectionCaptureRendererAmbientState
{
    private Vector4 diffuse, specular;
    private ReflectionCaptureRendererAmbientState(Vector4 diffuse, Vector4 specular)
        => (this.diffuse, this.specular) = (diffuse, specular);

    /// <summary>981F00 -> v170 -> 997530/FA3B60 -> 9973E0 writes both
    /// base parameters to float4(1). This is renderer creation, not a per-frame reset.</summary>
    public static ReflectionCaptureRendererAmbientState CreateFreshClass18() =>
        new(Vector4.One, Vector4.One);

    /// <summary>Import the same renderer's actual retained endpoint. Unknown
    /// imported state must not call the fresh-constructor entry.</summary>
    public static ReflectionCaptureRendererAmbientState ImportRetained(
        Vector4 actualDiffuseScale, Vector4 actualSpecularScale) =>
        new(actualDiffuseScale, actualSpecularScale);

    public void ApplyActualModifier(DeferredAmbientSourceParameters actualModifier,
        float actualCameraMapOcclusionRate)
    {
        var values = DeferredAmbientSource.Prepare(actualModifier, actualCameraMapOcclusionRate);
        diffuse = values.DiffuseScale; specular = values.SpecularScale;
    }

    /// <summary>Read the same renderer's retained endpoints without preparing
    /// unrelated current capture Eye or View values.</summary>
    public (Vector4 Diffuse, Vector4 Specular) CaptureRetainedScales() => (diffuse, specular);

    /// <summary>D74970(null modifier, scratch3CC). Scope light values below
    /// are EC7100 literals, independently of Main's selected sun/light group.</summary>
    public ReflectionCaptureModelLighting PrepareCapture(Vector4 actualCaptureEye,
        float actualCopiedCameraMapOcclusionRate) => new(new(0,-1,0,0), Vector4.One,
            Vector4.Zero, actualCaptureEye, actualCopiedCameraMapOcclusionRate, diffuse, specular);
}

/// <summary>The single admitted null-Cube read profile is ordinary state12
/// PS2's float4 SampleLevel(t5,s5,0). No dimensions, loads, comparison or other
/// image operations are represented by this backend adaptation.</summary>
public static class ReflectionCaptureKnownNullAmbientSource
{
    public const uint ShaderFileId = 0xD1BBCAC4;
    public const string ShaderSha256 = "4FF6E3CAB6EEC6102DFE09F89425D275167CF1C12F910C5899DABB6BFBF5C7BC";
    public static void RequireProfile(LoadedShaderContainer actualShader, int actualCombination)
    {
        ArgumentNullException.ThrowIfNull(actualShader);
        var combination = actualShader.ReadCombination(actualCombination);
        int pixel = combination.Stages.Single(s => s.StageCode == 0 && s.Entry is not null).Entry!.ProgramIndex;
        if (actualShader.Source.FileId != ShaderFileId || actualShader.Source.DecodedSha256 != ShaderSha256 || pixel != 2)
            throw new NotSupportedException("reflection.capture.null_ambient.unimplemented_sample_profile");
    }
}
