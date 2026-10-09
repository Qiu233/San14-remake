using System.Collections;

namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalObjectRegistrationState
{
    private readonly Dictionary<OriginalRegistryKind, OriginalRegistryListHandle?> _registryDescriptors = [];
    public bool? RegistryHeadTablePresent { get; set; }
    public bool? RegistryLockEnabled { get; set; }
    public Func<bool?>? RegistryLockEnabledReader { get; set; }
    public Action? RegistryLockEnterer { get; set; }
    public Action? RegistryLockExiter { get; set; }
    // Existing logical handles carry a known stable key. An explicit provider
    // may supply a current key instead; physical descriptor addresses stay external.
    public Func<OriginalRegistryListHandle,int?>? RegistryDescriptorKeyReader { get; set; }
    public Func<bool?>? RegistryHeadTablePresenceReader { get; set; }

    public void SetRegistryDescriptor(OriginalRegistryKind kind,OriginalRegistryListHandle? handle)
    {
        CheckRegistryKind(kind);
        if (handle?.Identity is int id) CheckOpaque(id,"registry descriptor");
        _registryDescriptors[kind]=handle;
    }
    public OriginalRegistryListHandle? ReadRegistryDescriptor(OriginalRegistryKind kind)
    { CheckRegistryKind(kind); return _registryDescriptors.GetValueOrDefault(kind); }
    private bool RegistryConfigured(OriginalRegistryKind kind)=>_registryDescriptors.ContainsKey(kind);
    private void UseResolvedRegistrySequence(OriginalRegistryKind kind)=>_registryDescriptors.Remove(kind);
    public void SetKnownEmptyRegistry(OriginalRegistryListHandle handle)
        => SetKnownEmptyHexList(new(RegistryKey(handle)));

    public OriginalRegistryListNodeReference? ReadRegistryHead(OriginalRegistryListHandle handle)
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
        OriginalHexListNodeReference? node=ReadHexListHead(new(key));
        return node is null ? null : new(node.Identity);
    }
    public OriginalRegistryListNodeReference? ReadRegistryNodeNext(OriginalRegistryListNodeReference node)
    {
        ArgumentNullException.ThrowIfNull(node);
        OriginalHexListNodeReference? next=ReadHexListNodeNext(new(node.Identity));
        return next is null ? null : new(next.Identity);
    }
    public OriginalRegistryObjectReference? ReadRegistryNodePayload(OriginalRegistryListNodeReference node)
    {
        ArgumentNullException.ThrowIfNull(node);
        object? payload=ReadCurrentListPayload(NodeIdentity(new(node.Identity)));
        return payload switch
        {
            null=>null,
            OriginalRegistryObjectReference reference=>reference,
            OriginalBaseReference reference=>new(reference.Kind==OriginalBaseKind.City ? OriginalRegistryKind.City : OriginalRegistryKind.Gate,reference.PhysicalSlot),
            OriginalHexReference reference=>new(OriginalRegistryKind.Hex,reference.PhysicalSlot),
            _=>throw new InvalidDataException("Current shared node pointer kind has no defined identity profile.")
        };
    }
    public OriginalRegistryListNodeReference? AllocateRegistryNode(OriginalRegistryListHandle handle)
    {
        int key=RegistryKey(handle);
        OriginalHexListNodeReference node=AllocateNativeListNode(key);
        return new(node.Identity);
    }
    public void WriteRegistryNodePayload(OriginalRegistryListNodeReference node,OriginalRegistryObjectReference reference)
    {
        ArgumentNullException.ThrowIfNull(node); ArgumentNullException.ThrowIfNull(reference);
        if (reference.PhysicalSlot is int physical) CheckOpaque(physical,"registry payload pointer");
        WriteCurrentListPayload(NodeIdentity(new(node.Identity)), reference.Kind switch
        {
            OriginalRegistryKind.City=>new OriginalBaseReference(OriginalBaseKind.City,reference.PhysicalSlot),
            OriginalRegistryKind.Gate=>new OriginalBaseReference(OriginalBaseKind.Gate,reference.PhysicalSlot),
            _=>(object)reference
        });
    }
    public bool? ReadRegistryLockEnabled()=>RegistryLockEnabledReader is null ? RegistryLockEnabled : RegistryLockEnabledReader();
    public void EnterRegistryLock()=> (RegistryLockEnterer ?? throw new InvalidDataException("Current registry lock operation is unknown."))();
    public void ExitRegistryLock()=> (RegistryLockExiter ?? throw new InvalidDataException("Current registry unlock operation is unknown."))();
    public void InitializeObjectRegistries()=>OriginalObjectRegistration.Apply(this);
    private int RegistryKey(OriginalRegistryListHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (!handle.Identity.HasValue) return NativeNullHexListKey;
        return RegistryDescriptorKeyReader is null ? handle.Identity.Value : RegistryDescriptorKeyReader(handle)
            ?? throw new InvalidDataException("Current registry descriptor key is unknown.");
    }
    private static void CheckRegistryKind(OriginalRegistryKind kind)
    {
        if (!OriginalObjectRegistration.Ranges.Any(range=>range.Kind==kind))
            throw new InvalidDataException("No native registration table exists for the requested kind.");
    }
    public IReadOnlyList<OriginalRegistryObjectReference?>? ReadRegistryReferences(OriginalRegistryKind kind)
    {
        CheckRegistryKind(kind);
        return ReadRegistryDescriptor(kind) is null ? null : new NativeRegistrySequence<OriginalRegistryObjectReference?>(this,kind,reference=>reference);
    }
    private IReadOnlyList<T>? ProjectRegistry<T>(OriginalRegistryKind kind,Func<OriginalRegistryObjectReference?,T> project)
        => ReadRegistryDescriptor(kind) is null ? null : new NativeRegistrySequence<T>(this,kind,reference=>
        {
            if (reference is not null && reference.Kind!=kind)
                throw new InvalidDataException("Current registered consumer has a different receiver kind.");
            return project(reference);
        });

    private sealed class NativeRegistrySequence<T>(OriginalEventInitializationContext current,OriginalRegistryKind kind,
        Func<OriginalRegistryObjectReference?,T> project) : IReadOnlyList<T>
    {
        public IEnumerator<T> GetEnumerator()
        {
            OriginalRegistryListHandle descriptor=current.ReadRegistryDescriptor(kind)
                ?? throw new InvalidDataException("Current registry descriptor is unknown.");
            OriginalRegistryListNodeReference node=current.ReadRegistryHead(descriptor)
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
