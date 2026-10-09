using San14.Data;

public sealed partial class MapPostEffect3EyeBloomExecutionOwner
{
    private (MapPostEffect3FullscreenCompletedPass Completed,MapPostEffect3EyeAdaptation2Frame Frame,
        MapPostEffect3EyeBeforeBindings Before,MapPostEffect3CompiledPass Program,string FrozenInputs)? lastHistoricalGraphicsInputs;
    internal MapPostEffect3CompiledPass CaptureLastHistoricalProgram(MapPostEffect3FullscreenCompletedPass actual,
        MapPostEffect3EyeAdaptation2Frame actualFrame,MapPostEffect3EyeBeforeBindings actualBefore)
    {CaptureLastHistoricalInputEvidence(actual,actualFrame,actualBefore);return lastHistoricalGraphicsInputs!.Value.Program;}
    internal string CaptureLastHistoricalInputEvidence(MapPostEffect3FullscreenCompletedPass actual,
        MapPostEffect3EyeAdaptation2Frame actualFrame,MapPostEffect3EyeBeforeBindings actualBefore)
    {
        MapPostEffect3Functor1SubmissionLedger.RequireActualIdentity(LastCompleted,actual);
        MapPostEffect3Functor1SubmissionLedger.RequireActualIdentity(Graphics.LastCompletedPass,actual);
        var captured=lastHistoricalGraphicsInputs??throw new InvalidDataException("Actual Eye inputs were not frozen at submission.");
        if(!ReferenceEquals(captured.Completed,actual)||!ReferenceEquals(captured.Frame,actualFrame)||!ReferenceEquals(captured.Before,actualBefore))
            throw new InvalidDataException("Historical Eye inputs belong to another exact preparation or command.");
        return captured.FrozenInputs;
    }
}
