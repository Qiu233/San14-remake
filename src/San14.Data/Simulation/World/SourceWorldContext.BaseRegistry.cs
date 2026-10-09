using System.Collections;

namespace San14.Data;

public sealed partial class SourceWorldContext : IBaseRegistrationState
{
    private bool _baseRegistryConfigured;
    private RegistryListHandle? _baseRegistryHandle;

    public void SetBaseRegistryHandle(RegistryListHandle? handle)
    {
        if (handle?.Identity is int identity) CheckOpaque(identity, "base registry handle");
        _baseRegistryConfigured = true;
        _baseRegistryHandle = handle;
    }
    public RegistryListHandle? ReadBaseRegistryHandle() => _baseRegistryConfigured ? _baseRegistryHandle : null;

    public RegistryListNodeReference? AllocateBaseRegistryNode(RegistryListHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        HexListNodeReference node = AllocateHexListNode(new(handle.Identity));
        return new(node.Identity);
    }
    public void WriteBaseRegistryNodePayload(RegistryListNodeReference node, BaseReference reference)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(reference);
        _ = BasePhysical(reference);
        WriteCurrentListPayload(NodeIdentity(new(node.Identity)), reference);
    }
    public BaseReference? ReadBaseRegistryNodePayload(RegistryListNodeReference node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return ReadNativeListPayload<BaseReference>(NodeIdentity(new(node.Identity)));
    }
    public void SetKnownEmptyBaseRegistry(RegistryListHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        SetKnownEmptyHexList(new(handle.Identity));
    }

    private sealed class NativeBaseSequence(SourceWorldContext current) : IReadOnlyList<BaseReference?>
    {
        public IEnumerator<BaseReference?> GetEnumerator()
        {
            RegistryListHandle handle = current.ReadBaseRegistryHandle()
                ?? throw new InvalidDataException("Current base registry descriptor is unknown.");
            HexListNodeReference node = current.ReadHexListHead(new(handle.Identity))
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
        public BaseReference? this[int index]
        {
            get
            {
                if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
                foreach (BaseReference? item in this) if (index-- == 0) return item;
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }
}
