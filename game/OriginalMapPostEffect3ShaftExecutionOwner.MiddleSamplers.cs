public sealed partial class OriginalMapPostEffect3ShaftExecutionOwner
{
    public IOriginalMapPostEffect3MiddleNativeSamplerScope CreateMiddleSamplerContinuation(
        OriginalMapPostEffect3MiddleExecutor actualMiddle)
    {
        ArgumentNullException.ThrowIfNull(actualMiddle);
        lock(lifetime)ObjectDisposedException.ThrowIf(retiring,this);
        var family=LastCompletedFamily??throw new InvalidDataException("Middle native state needs the actual finished or source-skipped Shaft family.");
        RequirePrefix(family.ComputeChain.Prefix);
        return firstWorld.NativeSamplers.CaptureMiddleContinuation(actualMiddle,this,family,
            actualSamplerContinuation);
    }
}
