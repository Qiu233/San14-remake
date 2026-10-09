public sealed partial class MapPostEffect3DepthOfField2ExecutionOwner
{
    private (MapPostEffect3FullscreenCompletedPass Completed,MapPostEffect3CompiledPass Program)? lastSubmittedProgram;
    internal MapPostEffect3CompiledPass CaptureLastHistoricalProgram(MapPostEffect3FullscreenCompletedPass actual)
    {
        MapPostEffect3Functor1SubmissionLedger.RequireActualIdentity(Graphics.LastCompletedPass,actual);
        var captured=lastSubmittedProgram??throw new InvalidDataException("Actual DOF program was not retained at submission.");
        if(!ReferenceEquals(captured.Completed,actual))throw new InvalidDataException("Historical DOF program belongs to another actual command.");
        return captured.Program;
    }
}
