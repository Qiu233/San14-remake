using San14.Data;

public sealed partial class OriginalMapPostEffect3WorldNativeFullscreenFrontendOwner
{
    private OriginalMapPostEffect3Functor1SubmissionLedger? submissionLedger;
    private OriginalMapPostEffect3EyeBeforeBindings? actualEyeGraphicsHistoryBefore;
    internal void AttachSubmissionLedger(OriginalMapPostEffect3Functor1SubmissionLedger actual)
    {RequireCurrent();actual.RequireFrontend(this);if(submissionLedger is not null)throw new InvalidOperationException("Frontend already has an actual module submission ledger.");submissionLedger=actual;}
    internal OriginalMapPostEffect3Functor1SubmissionLedger.PendingSubmission? PrepareCompositeHistory(
        OriginalMapPostEffect3Composite2GraphicsCoverageReceipt coverage)=>submissionLedger?.CaptureComposite(this,coverage);
    internal void AcceptCompositeHistoryAfterActualRelease(OriginalMapPostEffect3Functor1SubmissionLedger.PendingSubmission? pending)
    {if(pending is not null)submissionLedger!.AcceptAfterActualRelease(pending);}
}
