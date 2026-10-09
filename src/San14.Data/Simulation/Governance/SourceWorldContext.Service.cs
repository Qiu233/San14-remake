namespace San14.Data;

public sealed partial class SourceWorldContext : ITransferServiceQueries, ITransferServiceEligibilityQueries
{
    public TransferServiceState? TransferService { get; set; }
    public Func<bool?>? TransferServiceEligibilityReader { get; set; }
    public uint? RawTransferServiceFlags16A8 { get; set; }
    public Func<TransferModeState?>? TransferModeStateReader { get; set; }
    public bool? ReadTransferServiceEligibility() => TransferServiceEligibilityReader is not null
        ? TransferServiceEligibilityReader() : TransferServiceEligibility.IsEligible(this);
    public uint? ReadWorldTransferServiceFlags() => RawTransferServiceFlags16A8;
    public TransferModeState? ReadTransferModeState() => TransferModeStateReader?.Invoke();
    public void SetTransferPersonTablePointer(int slot, int? physicalSlot)
    {
        Check(slot, 1851, "transfer person table slot");
        if (physicalSlot.HasValue) Check(physicalSlot.Value, 1851, "person physical slot");
        _officerSources.SetTableReference(slot, physicalSlot);
    }
    public TransferPersonReference? ReadTransferPersonTableReference(int tableSlot)
    {
        Check(tableSlot, 1851, "transfer person table slot");
        SourceOfficerReference? reference = _officerSources.TableReference(tableSlot);
        return reference is null ? null : new(reference.Record);
    }
    public byte? ReadPersonCurrentRawForceSlot(int physicalSlot) => ReadPersonOldForceSlot(physicalSlot);
}
