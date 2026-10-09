public sealed partial class MapPostEffect3Composite2GraphicsOwner
{
    private (MapPostEffect3FullscreenCompletedPass Completed,MapPostEffect3CompiledPass Program)? lastSubmittedProgram;
    internal MapPostEffect3CompiledPass CaptureLastHistoricalProgram(MapPostEffect3FullscreenCompletedPass actual)
    {
        MapPostEffect3Functor1SubmissionLedger.RequireActualIdentity(Executor.LastCompletedPass,actual);
        var captured=lastSubmittedProgram??throw new InvalidDataException("Actual Composite program was not retained at submission.");
        if(!ReferenceEquals(captured.Completed,actual))throw new InvalidDataException("Historical Composite program belongs to another actual command.");
        return captured.Program;
    }
}
