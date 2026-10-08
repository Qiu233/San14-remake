using San14.Data;

public sealed partial class OriginalMapPostEffect3ResourceBank
{
    public IOriginalMapPostEffect3ComputeMipTextureLease PublishMiddleTextureCoverage(
        OriginalMapPostEffect3MiddleWriteCoverage coverage,
        OriginalMapPostEffect3ComputeExecutor actualExecutor,
        OriginalMapPostEffect3ComputeCompletedPass actualCompleted)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        return PublishSourceTextureCoverage(actualExecutor,actualCompleted,coverage.FrameNumber,coverage.ContextOwnerIdentity,
            coverage.SourceSha256,coverage.SourcePassIndex,coverage.ProgramSha256,coverage.ParameterIndex,
            coverage.TextureMipLevel,coverage.TextureWidth,coverage.TextureHeight,coverage.ActualDispatch,
            coverage.LocalSize,coverage.SourceEvidence,coverage.RequiredConstantBufferBytesSha256,coverage.RequiredInputTextureParameters);
    }
}
