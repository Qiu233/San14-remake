using San14.Data;

public sealed partial class OriginalMapPostEffect3WorldControlledFactoryFrame
{
    public OriginalMapPostEffect3Functor1SubmissionLedger SubmissionLedger{get;private set;}=null!;
    private void InitializeSubmissionLedger()
    {
        if(SubmissionLedger is not null)throw new InvalidOperationException("Current module submission ledger already exists.");
        SubmissionLedger=OriginalMapPostEffect3Functor1SubmissionLedger.CreateOwned(this);
        Frontend.AttachSubmissionLedger(SubmissionLedger);
    }
}
