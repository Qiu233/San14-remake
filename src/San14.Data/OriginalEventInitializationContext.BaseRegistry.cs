using System.Collections;

namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalBaseRegistrationState
{
    private bool _baseRegistryConfigured;
    private OriginalRegistryListHandle? _baseRegistryHandle;

    public void SetBaseRegistryHandle(OriginalRegistryListHandle? handle)
    {
        if (handle?.Identity is int identity) CheckOpaque(identity, "base registry handle");
        _baseRegistryConfigured = true;
        _baseRegistryHandle = handle;
    }
    public OriginalRegistryListHandle? ReadBaseRegistryHandle() => _baseRegistryConfigured ? _baseRegistryHandle : null;

    public OriginalRegistryListNodeReference? AllocateBaseRegistryNode(OriginalRegistryListHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        OriginalHexListNodeReference node = AllocateHexListNode(new(handle.Identity));
        return new(node.Identity);
    }
    public void WriteBaseRegistryNodePayload(OriginalRegistryListNodeReference node, OriginalBaseReference reference)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(reference);
        _ = BasePhysical(reference);
        _nativeListPayloads[NodeIdentity(new(node.Identity))] = reference;
    }
    public OriginalBaseReference? ReadBaseRegistryNodePayload(OriginalRegistryListNodeReference node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return ReadNativeListPayload<OriginalBaseReference>(NodeIdentity(new(node.Identity)));
    }
    public void SetKnownEmptyBaseRegistry(OriginalRegistryListHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        SetKnownEmptyHexList(new(handle.Identity));
    }

    private sealed class NativeBaseSequence(OriginalEventInitializationContext current) : IReadOnlyList<OriginalBaseReference?>
    {
        public IEnumerator<OriginalBaseReference?> GetEnumerator()
        {
            OriginalRegistryListHandle handle = current.ReadBaseRegistryHandle()
                ?? throw new InvalidDataException("Current base registry descriptor is unknown.");
            OriginalHexListNodeReference node = current.ReadHexListHead(new(handle.Identity))
                ?? throw new InvalidDataException("Current base registry head is unknown.");
            while (node.Identity.HasValue)
            {
                yield return current.ReadBaseRegistryNodePayload(new(node.Identity));
                // Read next after the consumer has performed its current-state operations.
                node = current.ReadHexListNodeNext(node)
                    ?? throw new InvalidDataException("Current base registry next node is unknown.");
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        // These inspection operations traverse live state; native consumers use
        // enumeration and do not ask for a whole-chain count before head access.
        public int Count { get { int count = 0; foreach (var _ in this) count = checked(count + 1); return count; } }
        public OriginalBaseReference? this[int index]
        {
            get
            {
                if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
                foreach (OriginalBaseReference? item in this) if (index-- == 0) return item;
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }
}
