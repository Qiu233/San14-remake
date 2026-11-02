using San14.Core;
using System.Collections.ObjectModel;

namespace San14.Data;

/// <summary>The explicit link between campaign receiver identities and army
/// source-table ordinals. Empty and retired receivers retain their link.</summary>
public sealed class ArmyReceiverSourceDirectory
{
    private readonly Dictionary<int, ArmyReceiverId> _receiversByOrdinal;
    private readonly Dictionary<ArmyReceiverId, int> _ordinalsByReceiver;

    private ArmyReceiverSourceDirectory()
    { _receiversByOrdinal = new(); _ordinalsByReceiver = new(); }

    public ArmyReceiverSourceDirectory(ArmyLifecycleSourceView lifecycle) : this(lifecycle.Source) { }
    public ArmyReceiverSourceDirectory(ArmyLifecycleSourceState source)
    {
        var shared = source.ReceiverDirectory;
        _receiversByOrdinal = shared._receiversByOrdinal;
        _ordinalsByReceiver = shared._ordinalsByReceiver;
    }

    internal static ArmyReceiverSourceDirectory FromOwnedSlots(IReadOnlyList<ArmyReceiverSourceSlot> slots)
    {
        var directory = new ArmyReceiverSourceDirectory();
        directory._receiversByOrdinal.EnsureCapacity(slots.Count);
        directory._ordinalsByReceiver.EnsureCapacity(slots.Count);
        foreach (var slot in slots) directory.Add(slot.PhysicalSlot, slot.ReceiverId);
        return directory;
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

// The immutable admitted slot collection owns its derived identity index.
// A record `with` that keeps Slots shares it; replacing Slots creates a new owner.
internal sealed class ArmyReceiverSourceSlots(ArmyReceiverSourceSlot[] ownedSlots)
    : ReadOnlyCollection<ArmyReceiverSourceSlot>(ownedSlots)
{
    private ArmyReceiverSourceDirectory? _directory;
    internal ArmyReceiverSourceDirectory Directory => LazyInitializer.EnsureInitialized(ref _directory,
        () => ArmyReceiverSourceDirectory.FromOwnedSlots(this));
}
