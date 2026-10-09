using San14.Data;

public sealed partial class PostProcessResourceBank
{
    public IMapPostEffect3ComputeMipTextureLease PublishMiddleTextureCoverage(
        MapPostEffect3MiddleWriteCoverage coverage,
        MapPostEffect3ComputeExecutor actualExecutor,
        MapPostEffect3ComputeCompletedPass actualCompleted)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        return PublishSourceTextureCoverage(actualExecutor,actualCompleted,coverage.FrameNumber,coverage.ContextOwnerIdentity,
            coverage.SourceSha256,coverage.SourcePassIndex,coverage.ProgramSha256,coverage.ParameterIndex,
            coverage.TextureMipLevel,coverage.TextureWidth,coverage.TextureHeight,coverage.ActualDispatch,
            coverage.LocalSize,coverage.SourceEvidence,coverage.RequiredConstantBufferBytesSha256,coverage.RequiredInputTextureParameters);
    }
}
