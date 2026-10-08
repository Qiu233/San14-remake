using San14.Data;

public sealed partial class OriginalMapPostEffect3EyeBloomExecutionOwner
{
    private (OriginalMapPostEffect3FullscreenCompletedPass Completed,OriginalMapPostEffect3EyeAdaptation2Frame Frame,
        OriginalMapPostEffect3EyeBeforeBindings Before,OriginalMapPostEffect3CompiledPass Program,string FrozenInputs)? lastHistoricalGraphicsInputs;
    internal OriginalMapPostEffect3CompiledPass CaptureLastHistoricalProgram(OriginalMapPostEffect3FullscreenCompletedPass actual,
        OriginalMapPostEffect3EyeAdaptation2Frame actualFrame,OriginalMapPostEffect3EyeBeforeBindings actualBefore)
    {CaptureLastHistoricalInputEvidence(actual,actualFrame,actualBefore);return lastHistoricalGraphicsInputs!.Value.Program;}
    internal string CaptureLastHistoricalInputEvidence(OriginalMapPostEffect3FullscreenCompletedPass actual,
        OriginalMapPostEffect3EyeAdaptation2Frame actualFrame,OriginalMapPostEffect3EyeBeforeBindings actualBefore)
    {
        OriginalMapPostEffect3Functor1SubmissionLedger.RequireActualIdentity(LastCompleted,actual);
        OriginalMapPostEffect3Functor1SubmissionLedger.RequireActualIdentity(Graphics.LastCompletedPass,actual);
        var captured=lastHistoricalGraphicsInputs??throw new InvalidDataException("Actual Eye inputs were not frozen at submission.");
        if(!ReferenceEquals(captured.Completed,actual)||!ReferenceEquals(captured.Frame,actualFrame)||!ReferenceEquals(captured.Before,actualBefore))
            throw new InvalidDataException("Historical Eye inputs belong to another exact preparation or command.");
        return captured.FrozenInputs;
    }
}
