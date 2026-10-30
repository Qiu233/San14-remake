using System.Collections;
using System.Collections.Frozen;

namespace San14.Data;

public sealed partial class SourceWorldContext : IObjectRegistrationState
{
    private static readonly FrozenSet<RegistryKind> RegisteredTableKinds = ObjectRegistration.Ranges
        .Select(range => range.Kind).ToFrozenSet();
    private readonly Dictionary<RegistryKind, RegistryListHandle?> _registryDescriptors = [];
    public bool? RegistryHeadTablePresent { get; set; }
    public bool? RegistryLockEnabled { get; set; }
    public Func<bool?>? RegistryLockEnabledReader { get; set; }
    public Action? RegistryLockEnterer { get; set; }
    public Action? RegistryLockExiter { get; set; }
    // Existing logical handles carry a known stable key. An explicit provider
    // may supply a current key instead; physical descriptor addresses stay external.
    public Func<RegistryListHandle,int?>? RegistryDescriptorKeyReader { get; set; }
    public Func<bool?>? RegistryHeadTablePresenceReader { get; set; }

    public void SetRegistryDescriptor(RegistryKind kind,RegistryListHandle? handle)
    {
        CheckRegistryKind(kind);
        if (handle?.Identity is int id) CheckOpaque(id,"registry descriptor");
        _registryDescriptors[kind]=handle;
    }
    public RegistryListHandle? ReadRegistryDescriptor(RegistryKind kind)
    { CheckRegistryKind(kind); return _registryDescriptors.GetValueOrDefault(kind); }
    private bool RegistryConfigured(RegistryKind kind)=>_registryDescriptors.ContainsKey(kind);
    private void UseResolvedRegistrySequence(RegistryKind kind)=>_registryDescriptors.Remove(kind);
    public void SetKnownEmptyRegistry(RegistryListHandle handle)
        => SetKnownEmptyHexList(new(RegistryKey(handle)));

    public RegistryListNodeReference? ReadRegistryHead(RegistryListHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (!handle.Identity.HasValue) return new(null);
        if (!KnownPoolPresent()) return new(null);
        // Native saves the head-table pointer, then reads the key, and only then
        // tests the saved pointer. A missing head table does not suppress key access.
        bool? headTable=RegistryHeadTablePresenceReader is null ? RegistryHeadTablePresent : RegistryHeadTablePresenceReader();
        int key=RegistryKey(handle);
        if (!headTable.HasValue) throw new InvalidDataException("Current registry head-table pointer is unknown.");
        if (!headTable.Value) return new(null);
        if (!NativeListKeyInRange(key)) return new(null);
        HexListNodeReference? node=ReadHexListHead(new(key));
        return node is null ? null : new(node.Identity);
    }
    public RegistryListNodeReference? ReadRegistryNodeNext(RegistryListNodeReference node)
    {
        ArgumentNullException.ThrowIfNull(node);
        HexListNodeReference? next=ReadHexListNodeNext(new(node.Identity));
        return next is null ? null : new(next.Identity);
    }
    public RegistryObjectReference? ReadRegistryNodePayload(RegistryListNodeReference node)
    {
        ArgumentNullException.ThrowIfNull(node);
        object? payload=ReadCurrentListPayload(NodeIdentity(new(node.Identity)));
        return payload switch
        {
            null=>null,
            RegistryObjectReference reference=>reference,
            BaseReference reference=>new(reference.Kind==BaseKind.City ? RegistryKind.City : RegistryKind.Gate,reference.PhysicalSlot),
            HexReference reference=>new(RegistryKind.Hex,reference.PhysicalSlot),
            //271320's caller and Base88 lists store the same physical Person
            //pointer through the transfer API, not a personnel task identity.
            TransferPersonReference reference=>new(RegistryKind.Person,reference.PhysicalSlot),
            _=>throw new InvalidDataException($"Current shared node pointer kind {payload.GetType().Name} has no defined identity profile.")
        };
    }
    public RegistryListNodeReference? AllocateRegistryNode(RegistryListHandle handle)
    {
        int key=RegistryKey(handle);
        HexListNodeReference node=AllocateNativeListNode(key);
        return new(node.Identity);
    }
    public void WriteRegistryNodePayload(RegistryListNodeReference node,RegistryObjectReference reference)
    {
        ArgumentNullException.ThrowIfNull(node); ArgumentNullException.ThrowIfNull(reference);
        if (reference.PhysicalSlot is int physical) CheckOpaque(physical,"registry payload pointer");
        WriteCurrentListPayload(NodeIdentity(new(node.Identity)), reference.Kind switch
        {
            RegistryKind.City=>new BaseReference(BaseKind.City,reference.PhysicalSlot),
            RegistryKind.Gate=>new BaseReference(BaseKind.Gate,reference.PhysicalSlot),
            _=>(object)reference
        });
    }
    public bool? ReadRegistryLockEnabled()=>RegistryLockEnabledReader is null ? RegistryLockEnabled : RegistryLockEnabledReader();
    public void EnterRegistryLock()=> (RegistryLockEnterer ?? throw new InvalidDataException("Current registry lock operation is unknown."))();
    public void ExitRegistryLock()=> (RegistryLockExiter ?? throw new InvalidDataException("Current registry unlock operation is unknown."))();
    public void InitializeObjectRegistries()=>ObjectRegistration.Apply(this);
    private int RegistryKey(RegistryListHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (!handle.Identity.HasValue) return NativeNullHexListKey;
        return RegistryDescriptorKeyReader is null ? handle.Identity.Value : RegistryDescriptorKeyReader(handle)
            ?? throw new InvalidDataException("Current registry descriptor key is unknown.");
    }
    private static void CheckRegistryKind(RegistryKind kind)
    {
        if (!RegisteredTableKinds.Contains(kind))
            throw new InvalidDataException("No native registration table exists for the requested kind.");
    }
    public IReadOnlyList<RegistryObjectReference?>? ReadRegistryReferences(RegistryKind kind)
    {
        CheckRegistryKind(kind);
        return ReadRegistryDescriptor(kind) is null ? null : new NativeRegistrySequence<RegistryObjectReference?>(this,kind,reference=>reference);
    }
    private IReadOnlyList<T>? ProjectRegistry<T>(RegistryKind kind,Func<RegistryObjectReference?,T> project)
        => ReadRegistryDescriptor(kind) is null ? null : new NativeRegistrySequence<T>(this,kind,reference=>
        {
            if (reference is not null && reference.Kind!=kind)
                throw new InvalidDataException("Current registered consumer has a different receiver kind.");
            return project(reference);
        });

    private sealed class NativeRegistrySequence<T>(SourceWorldContext current,RegistryKind kind,
        Func<RegistryObjectReference?,T> project) : IReadOnlyList<T>
    {
        public IEnumerator<T> GetEnumerator()
        {
            RegistryListHandle descriptor=current.ReadRegistryDescriptor(kind)
                ?? throw new InvalidDataException("Current registry descriptor is unknown.");
            RegistryListNodeReference node=current.ReadRegistryHead(descriptor)
                ?? throw new InvalidDataException("Current registry head is unknown.");
            while (node.Identity.HasValue)
            {
                yield return project(current.ReadRegistryNodePayload(node));
                node=current.ReadRegistryNodeNext(node)
                    ?? throw new InvalidDataException("Current registered node next is unknown.");
            }
        }
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
        public int Count { get { int count=0;foreach(var _ in this)count=checked(count+1);return count; } }
        public T this[int index]
        {
            get
            {
                if(index<0)throw new ArgumentOutOfRangeException(nameof(index));
                foreach(T item in this)if(index--==0)return item;
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }
}
