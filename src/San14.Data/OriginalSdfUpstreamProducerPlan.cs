namespace San14.Data;

/// <summary>Current source provider counts and state. Zero operation0 count
/// is an explicit native list claim, never inferred from missing resources.</summary>
public sealed record OriginalSdfUpstreamPreparedState(uint Operation0SelectedCount,uint Operation1SdfCount,
    bool ProviderVolumesAndClustersPrepared,bool NativeTargetsAlreadyInitialized);

public sealed record OriginalSdfUpstreamProducerSource(OriginalSdfFinalProgramSource Final,
    OriginalSdfUpstreamProgramSource HalfGbuffer,OriginalSdfUpstreamProgramSource Occlusion);

public static class OriginalSdfUpstreamProducerPlan
{
    public static OriginalSdfUpstreamProducerSource Read(OriginalSdfFinalProgramSource final,OriginalSdfUpstreamPreparedState state)
    {
        ArgumentNullException.ThrowIfNull(final);ArgumentNullException.ThrowIfNull(state);
        RequireSupported(state);
        if(final.ExplicitPass!=4 || final.ExplicitDimensions.Count!=7 || final.ExplicitDimensions[6]!=3)
            throw new NotSupportedException("SDF graphics with a previous initialized target requires exact original variant[1,1], row19/pass4.");
        var dimensions=final.ExplicitDimensions.ToArray();
        if(final.CombinationIndex!=final.Shader.ReadG1Bindings().ResolveCombination(dimensions,4).Index)
            throw new InvalidDataException("SDF final row differs from its explicitly selected current source dimensions.");
        return new(final,OriginalSdfUpstreamProgramPlan.Read(final.Shader,dimensions,1),
            OriginalSdfUpstreamProgramPlan.Read(final.Shader,dimensions,0));
    }
    public static void RequireSupported(OriginalSdfUpstreamPreparedState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if(state.Operation0SelectedCount!=0)
            throw new NotSupportedException("Original nonempty object occlusion operation0 is not implemented by this SDF stage owner.");
        if(state.Operation1SdfCount==0 || !state.ProviderVolumesAndClustersPrepared || !state.NativeTargetsAlreadyInitialized)
            throw new NotSupportedException("SDF stage needs current prepared SDF/cluster inputs and initialized native previous target.");
    }
}
