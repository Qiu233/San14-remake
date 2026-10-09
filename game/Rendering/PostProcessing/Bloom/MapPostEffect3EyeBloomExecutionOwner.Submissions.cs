using San14.Data;

public sealed partial class MapPostEffect3EyeBloomExecutionOwner
{
    private (MapPostEffect3FullscreenCompletedPass Completed,MapPostEffect3EyeAdaptation2Frame Frame,
        MapPostEffect3EyeBeforeBindings Before,MapPostEffect3CompiledPass Program)? lastSubmittedProgram;
    internal MapPostEffect3CompiledPass CaptureLastHistoricalProgram(MapPostEffect3FullscreenCompletedPass actual,
        MapPostEffect3EyeAdaptation2Frame actualFrame,MapPostEffect3EyeBeforeBindings actualBefore)
    {
        MapPostEffect3Functor1SubmissionLedger.RequireActualIdentity(LastCompleted,actual);
        MapPostEffect3Functor1SubmissionLedger.RequireActualIdentity(Graphics.LastCompletedPass,actual);
        var captured=lastSubmittedProgram??throw new InvalidDataException("Actual Eye program was not retained at submission.");
        if(!ReferenceEquals(captured.Completed,actual)||!ReferenceEquals(captured.Frame,actualFrame)||!ReferenceEquals(captured.Before,actualBefore))
            throw new InvalidDataException("Historical Eye program belongs to another exact preparation or command.");
        return captured.Program;
    }
}
