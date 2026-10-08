namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalTransferServiceQueries, IOriginalTransferServiceEligibilityQueries
{
    private readonly Dictionary<int, int?> _transferPersonPointers = [];
    public OriginalTransferServiceState? TransferService { get; set; }
    public Func<bool?>? TransferServiceEligibilityReader { get; set; }
    public uint? RawTransferServiceFlags16A8 { get; set; }
    public Func<OriginalTransferModeState?>? TransferModeStateReader { get; set; }
    public bool? ReadTransferServiceEligibility() => TransferServiceEligibilityReader is not null
        ? TransferServiceEligibilityReader() : OriginalTransferServiceEligibility.IsEligible(this);
    public uint? ReadWorldTransferServiceFlags() => RawTransferServiceFlags16A8;
    public OriginalTransferModeState? ReadTransferModeState() => TransferModeStateReader?.Invoke();
    public void SetTransferPersonTablePointer(int slot, int? physicalSlot) =>
        SetPointer(_transferPersonPointers, slot, 1851, physicalSlot, 1851);
    public OriginalTransferPersonReference? ReadTransferPersonTableReference(int tableSlot)
    {
        Check(tableSlot, 1851, "transfer person table slot");
        return _transferPersonPointers.TryGetValue(tableSlot, out int? physical) ? new(physical) : null;
    }
    public byte? ReadPersonCurrentRawForceSlot(int physicalSlot) => ReadPersonOldForceSlot(physicalSlot);
}
