public sealed partial class MapPostEffect3Composite2GraphicsOwner
{
    private (MapPostEffect3FullscreenCompletedPass Completed,MapPostEffect3CompiledPass Program,string FrozenInputs)? lastHistoricalGraphicsInputs;
    internal MapPostEffect3CompiledPass CaptureLastHistoricalProgram(MapPostEffect3FullscreenCompletedPass actual)
    {CaptureLastHistoricalInputEvidence(actual);return lastHistoricalGraphicsInputs!.Value.Program;}
    internal string CaptureLastHistoricalInputEvidence(MapPostEffect3FullscreenCompletedPass actual)
    {
        MapPostEffect3Functor1SubmissionLedger.RequireActualIdentity(Executor.LastCompletedPass,actual);
        var captured=lastHistoricalGraphicsInputs??throw new InvalidDataException("Actual Composite inputs were not frozen at submission.");
        if(!ReferenceEquals(captured.Completed,actual))throw new InvalidDataException("Historical Composite inputs belong to another actual command.");
        return captured.FrozenInputs;
    }
}
