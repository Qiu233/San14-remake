using System.Collections;

namespace San14.Data;

public sealed partial class SourceWorldContext : IAssociatedObjectPostLoadState
{
    private AssociatedContainerReference? _currentAssociatedContainer;
    private readonly Dictionary<int, AssociatedTreeNodeReference?> _associatedTreeHeads = [];
    private readonly Dictionary<(int Node, int Offset), AssociatedTreeNodeReference?> _associatedTreeLinks = [];
    private readonly Dictionary<int, byte?> _associatedTreeNilFlags = [];
    private readonly Dictionary<int, PersonLinkedEntityReference?> _associatedTreePayloads = [];
    private readonly Dictionary<int, RegistryListHandle?> _associatedChainDescriptors = [];
    private readonly Dictionary<int, PostLoadCallback?> _associatedPayloadPostLoadCallbacks = [];
    public Dictionary<string, Action<PersonLinkedEntityReference>> AssociatedPayloadPostLoadHandlers { get; } = new(StringComparer.Ordinal);

    public AssociatedContainerReference? ReadCurrentAssociatedContainer() => _currentAssociatedContainer;
    public void SetCurrentAssociatedContainer(AssociatedContainerReference? container)
    { if (container?.Identity is int identity) CheckOpaque(identity, "associated container"); _currentAssociatedContainer = container; }
    public void SetAssociatedTreeHead(int container, AssociatedTreeNodeReference? head)
    { CheckOpaque(container, "associated container"); _associatedTreeHeads[container] = head; }
    public AssociatedTreeNodeReference? ReadAssociatedTreeHead(AssociatedContainerReference container)
        => _associatedTreeHeads.GetValueOrDefault(AssociatedIdentity(container.Identity, "container"));
    public void SetAssociatedTreeLink(int node, int offset, AssociatedTreeNodeReference? link)
    { CheckOpaque(node, "associated tree node"); TreeLinkOffset(offset); _associatedTreeLinks[(node, offset)] = link; }
    public AssociatedTreeNodeReference? ReadAssociatedTreeLink(AssociatedTreeNodeReference node, int offset)
    { TreeLinkOffset(offset); return _associatedTreeLinks.GetValueOrDefault((AssociatedIdentity(node.Identity, "tree node"), offset)); }
    public void SetAssociatedTreeNil19(int node, byte? value)
    { CheckOpaque(node, "associated tree node"); _associatedTreeNilFlags[node] = value; }
    public byte? ReadAssociatedTreeNil19(AssociatedTreeNodeReference node)
        => _associatedTreeNilFlags.GetValueOrDefault(AssociatedIdentity(node.Identity, "tree node"));
    public void SetAssociatedTreePayload28(int node, PersonLinkedEntityReference? payload)
    { CheckOpaque(node, "associated tree node"); _associatedTreePayloads[node] = payload; }
    public PersonLinkedEntityReference? ReadAssociatedTreePayload28(AssociatedTreeNodeReference node)
        => _associatedTreePayloads.GetValueOrDefault(AssociatedIdentity(node.Identity, "tree node"));
    public void SetAssociatedChainDescriptor(int container, RegistryListHandle? descriptor)
    { CheckOpaque(container, "associated container"); _associatedChainDescriptors[container] = descriptor; }
    public RegistryListHandle? ReadAssociatedChainDescriptor(AssociatedContainerReference container)
        => _associatedChainDescriptors.GetValueOrDefault(AssociatedIdentity(container.Identity, "container"));
    public RegistryListNodeReference? ReadAssociatedChainHead(RegistryListHandle handle) => ReadRegistryHead(handle);
    public PersonLinkedEntityReference? ReadAssociatedChainPayload(RegistryListNodeReference node)
        => ReadNativeListPayload<PersonLinkedEntityReference>(NodeIdentity(new(node.Identity)));
    public void SetAssociatedChainPayload(RegistryListNodeReference node, PersonLinkedEntityReference payload)
    { ArgumentNullException.ThrowIfNull(payload); WriteCurrentListPayload(NodeIdentity(new(node.Identity)), payload); }
    public void SetAssociatedPayloadPostLoadCallback(int identity, PostLoadCallback? callback)
    { CheckOpaque(identity, "associated payload"); _associatedPayloadPostLoadCallbacks[identity] = callback; }
    public void InvokeAssociatedPayloadSlot7(PersonLinkedEntityReference payload)
    {
        int identity = AssociatedIdentity(payload.Identity, "payload");
        var callback = _associatedPayloadPostLoadCallbacks.GetValueOrDefault(identity)
            ?? throw new InvalidDataException("Current associated payload post-load callback is unknown.");
        if (callback == PostLoadCallback.NoOp) return;
        if (callback.Kind == PostLoadCallbackKind.External && callback.HandlerIdentity is { } handlerIdentity &&
            AssociatedPayloadPostLoadHandlers.TryGetValue(handlerIdentity, out var handler)) { handler(payload); return; }
        throw new InvalidDataException($"Unsupported associated payload post-load callback {callback.Kind}/{callback.HandlerIdentity}.");
    }
    public AssociatedPostLoadResult PostProcessAssociatedObjects() => AssociatedObjectPostLoad.Apply(this);

    private sealed class AssociatedChainSequence(SourceWorldContext state,
        AssociatedContainerReference container) : IReadOnlyList<PersonLinkedEntityReference?>
    {
        public int Count => this.Count(_ => true);
        public PersonLinkedEntityReference? this[int index]
            => index < 0 ? throw new ArgumentOutOfRangeException(nameof(index)) : this.Skip(index).First();
        public IEnumerator<PersonLinkedEntityReference?> GetEnumerator()
        {
            var handle = state.ReadAssociatedChainDescriptor(container)
                ?? throw new InvalidDataException("Current person-linked associated descriptor is unknown.");
            var node = state.ReadAssociatedChainHead(handle)
                ?? throw new InvalidDataException("Current person-linked associated head is unknown.");
            var visited = new HashSet<RegistryListNodeReference>();
            while (node.Identity.HasValue)
            {
                if (!visited.Add(node)) throw new InvalidDataException("Current person-linked shared chain contains a cycle.");
                yield return state.ReadAssociatedChainPayload(node);
                node = state.ReadRegistryNodeNext(node) ?? throw new InvalidDataException("Current person-linked next is unknown.");
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private static int AssociatedIdentity(int? identity, string field)
    {
        int value = identity ?? throw new InvalidDataException($"Null associated {field} receiver.");
        CheckOpaque(value, $"associated {field}"); return value;
    }
    private static void TreeLinkOffset(int offset)
    { if (offset is not (0 or 8 or 0x10)) throw new InvalidDataException("Unsupported associated tree link field."); }
}
