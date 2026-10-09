public sealed partial class MapPostEffect3ShaftExecutionOwner
{
    public IMapPostEffect3MiddleNativeSamplerScope CreateMiddleSamplerContinuation(
        MapPostEffect3MiddleExecutor actualMiddle)
    {
        ArgumentNullException.ThrowIfNull(actualMiddle);
        lock(lifetime)ObjectDisposedException.ThrowIf(retiring,this);
        var family=LastCompletedFamily??throw new InvalidDataException("Middle native state needs the actual finished or source-skipped Shaft family.");
        RequirePrefix(family.ComputeChain.Prefix);
        return firstWorld.NativeSamplers.CaptureMiddleContinuation(actualMiddle,this,family,
            actualSamplerContinuation);
    }
}
