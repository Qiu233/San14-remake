using San14.Data;

public sealed partial class MapPostEffect3WorldControlledFactoryFrame
{
    public MapPostEffect3Functor1SubmissionLedger SubmissionLedger{get;private set;}=null!;
    private void InitializeSubmissionLedger()
    {
        if(SubmissionLedger is not null)throw new InvalidOperationException("Current module submission ledger already exists.");
        SubmissionLedger=MapPostEffect3Functor1SubmissionLedger.CreateOwned(this);
        Frontend.AttachSubmissionLedger(SubmissionLedger);
    }
}
