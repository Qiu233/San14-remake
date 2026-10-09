using San14.Data;

public sealed partial class MapPostEffect3WorldNativeFullscreenFrontendOwner
{
    private MapPostEffect3Functor1SubmissionLedger? submissionLedger;
    private MapPostEffect3EyeBeforeBindings? actualEyeGraphicsHistoryBefore;
    internal void AttachSubmissionLedger(MapPostEffect3Functor1SubmissionLedger actual)
    {RequireCurrent();actual.RequireFrontend(this);if(submissionLedger is not null)throw new InvalidOperationException("Frontend already has an actual module submission ledger.");submissionLedger=actual;}
    internal MapPostEffect3Functor1SubmissionLedger.PendingSubmission? PrepareCompositeHistory(
        MapPostEffect3Composite2GraphicsCoverageReceipt coverage)=>submissionLedger?.CaptureComposite(this,coverage);
    internal void AcceptCompositeHistoryAfterActualRelease(MapPostEffect3Functor1SubmissionLedger.PendingSubmission? pending)
    {if(pending is not null)submissionLedger!.AcceptAfterActualRelease(pending);}
}
