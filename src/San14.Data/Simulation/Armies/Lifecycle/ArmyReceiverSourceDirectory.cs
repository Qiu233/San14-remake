using San14.Core;

namespace San14.Data;

/// <summary>The explicit link between campaign receiver identities and army
/// source-table ordinals. Empty and retired receivers retain their link.</summary>
public sealed class ArmyReceiverSourceDirectory
{
    private readonly Dictionary<int, ArmyReceiverId> _receiversByOrdinal = new();
    private readonly Dictionary<ArmyReceiverId, int> _ordinalsByReceiver = new();

    private ArmyReceiverSourceDirectory() { }

    public ArmyReceiverSourceDirectory(NativeArmyLifecycleState lifecycle)
    {
        foreach (var slot in lifecycle.Slots)
            Add(slot.PhysicalSlot, slot.ReceiverId);
    }

    /// <summary>Allocates the initial receiver directory before either source
    /// position decoding or lifecycle construction. Both consume these links.</summary>
    public static ArmyReceiverSourceDirectory CreateFresh(int sourceReceiverCount)
    {
        var directory = new ArmyReceiverSourceDirectory();
        int nextReceiverId = 1;
        for (int ordinal = 0; ordinal < sourceReceiverCount; ordinal++)
            directory.Add(ordinal, new(nextReceiverId++));
        return directory;
    }

    private void Add(int sourceOrdinal, ArmyReceiverId receiverId)
    {
        _receiversByOrdinal.Add(sourceOrdinal, receiverId);
        _ordinalsByReceiver.Add(receiverId, sourceOrdinal);
    }

    public bool TryResolveReceiverId(int sourceOrdinal, out ArmyReceiverId receiverId) =>
        _receiversByOrdinal.TryGetValue(sourceOrdinal, out receiverId);

    public bool TryResolveSourceOrdinal(ArmyReceiverId receiverId, out int sourceOrdinal) =>
        _ordinalsByReceiver.TryGetValue(receiverId, out sourceOrdinal);
}
