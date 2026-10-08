public sealed partial class OriginalMapPostEffect3Composite2GraphicsOwner
{
    private (OriginalMapPostEffect3FullscreenCompletedPass Completed,OriginalMapPostEffect3CompiledPass Program,string FrozenInputs)? lastHistoricalGraphicsInputs;
    internal OriginalMapPostEffect3CompiledPass CaptureLastHistoricalProgram(OriginalMapPostEffect3FullscreenCompletedPass actual)
    {CaptureLastHistoricalInputEvidence(actual);return lastHistoricalGraphicsInputs!.Value.Program;}
    internal string CaptureLastHistoricalInputEvidence(OriginalMapPostEffect3FullscreenCompletedPass actual)
    {
        OriginalMapPostEffect3Functor1SubmissionLedger.RequireActualIdentity(Executor.LastCompletedPass,actual);
        var captured=lastHistoricalGraphicsInputs??throw new InvalidDataException("Actual Composite inputs were not frozen at submission.");
        if(!ReferenceEquals(captured.Completed,actual))throw new InvalidDataException("Historical Composite inputs belong to another actual command.");
        return captured.FrozenInputs;
    }
}
