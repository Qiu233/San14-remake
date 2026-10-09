using System.Collections;

namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalAssociatedObjectPostLoadState
{
    private OriginalAssociatedContainerReference? _currentAssociatedContainer;
    private readonly Dictionary<int, OriginalAssociatedTreeNodeReference?> _associatedTreeHeads = [];
    private readonly Dictionary<(int Node, int Offset), OriginalAssociatedTreeNodeReference?> _associatedTreeLinks = [];
    private readonly Dictionary<int, byte?> _associatedTreeNilFlags = [];
    private readonly Dictionary<int, OriginalPersonLinkedEntityReference?> _associatedTreePayloads = [];
    private readonly Dictionary<int, OriginalRegistryListHandle?> _associatedChainDescriptors = [];
    private readonly Dictionary<int, OriginalPostLoadCallback?> _associatedPayloadPostLoadCallbacks = [];
    public Dictionary<string, Action<OriginalPersonLinkedEntityReference>> AssociatedPayloadPostLoadHandlers { get; } = new(StringComparer.Ordinal);

    public OriginalAssociatedContainerReference? ReadCurrentAssociatedContainer() => _currentAssociatedContainer;
    public void SetCurrentAssociatedContainer(OriginalAssociatedContainerReference? container)
    { if (container?.Identity is int identity) CheckOpaque(identity, "associated container"); _currentAssociatedContainer = container; }
    public void SetAssociatedTreeHead(int container, OriginalAssociatedTreeNodeReference? head)
    { CheckOpaque(container, "associated container"); _associatedTreeHeads[container] = head; }
    public OriginalAssociatedTreeNodeReference? ReadAssociatedTreeHead(OriginalAssociatedContainerReference container)
        => _associatedTreeHeads.GetValueOrDefault(AssociatedIdentity(container.Identity, "container"));
    public void SetAssociatedTreeLink(int node, int offset, OriginalAssociatedTreeNodeReference? link)
    { CheckOpaque(node, "associated tree node"); TreeLinkOffset(offset); _associatedTreeLinks[(node, offset)] = link; }
    public OriginalAssociatedTreeNodeReference? ReadAssociatedTreeLink(OriginalAssociatedTreeNodeReference node, int offset)
    { TreeLinkOffset(offset); return _associatedTreeLinks.GetValueOrDefault((AssociatedIdentity(node.Identity, "tree node"), offset)); }
    public void SetAssociatedTreeNil19(int node, byte? value)
    { CheckOpaque(node, "associated tree node"); _associatedTreeNilFlags[node] = value; }
    public byte? ReadAssociatedTreeNil19(OriginalAssociatedTreeNodeReference node)
        => _associatedTreeNilFlags.GetValueOrDefault(AssociatedIdentity(node.Identity, "tree node"));
    public void SetAssociatedTreePayload28(int node, OriginalPersonLinkedEntityReference? payload)
    { CheckOpaque(node, "associated tree node"); _associatedTreePayloads[node] = payload; }
    public OriginalPersonLinkedEntityReference? ReadAssociatedTreePayload28(OriginalAssociatedTreeNodeReference node)
        => _associatedTreePayloads.GetValueOrDefault(AssociatedIdentity(node.Identity, "tree node"));
    public void SetAssociatedChainDescriptor(int container, OriginalRegistryListHandle? descriptor)
    { CheckOpaque(container, "associated container"); _associatedChainDescriptors[container] = descriptor; }
    public OriginalRegistryListHandle? ReadAssociatedChainDescriptor(OriginalAssociatedContainerReference container)
        => _associatedChainDescriptors.GetValueOrDefault(AssociatedIdentity(container.Identity, "container"));
    public OriginalRegistryListNodeReference? ReadAssociatedChainHead(OriginalRegistryListHandle handle) => ReadRegistryHead(handle);
    public OriginalPersonLinkedEntityReference? ReadAssociatedChainPayload(OriginalRegistryListNodeReference node)
        => ReadNativeListPayload<OriginalPersonLinkedEntityReference>(NodeIdentity(new(node.Identity)));
    public void SetAssociatedChainPayload(OriginalRegistryListNodeReference node, OriginalPersonLinkedEntityReference payload)
    { ArgumentNullException.ThrowIfNull(payload); WriteCurrentListPayload(NodeIdentity(new(node.Identity)), payload); }
    public void SetAssociatedPayloadPostLoadCallback(int identity, OriginalPostLoadCallback? callback)
    { CheckOpaque(identity, "associated payload"); _associatedPayloadPostLoadCallbacks[identity] = callback; }
    public void InvokeAssociatedPayloadSlot7(OriginalPersonLinkedEntityReference payload)
    {
        int identity = AssociatedIdentity(payload.Identity, "payload");
        var callback = _associatedPayloadPostLoadCallbacks.GetValueOrDefault(identity)
            ?? throw new InvalidDataException("Current associated payload post-load callback is unknown.");
        if (callback == OriginalPostLoadCallback.NoOp) return;
        if (callback.Kind == OriginalPostLoadCallbackKind.External && callback.HandlerIdentity is { } handlerIdentity &&
            AssociatedPayloadPostLoadHandlers.TryGetValue(handlerIdentity, out var handler)) { handler(payload); return; }
        throw new InvalidDataException($"Unsupported associated payload post-load callback {callback.Kind}/{callback.HandlerIdentity}.");
    }
    public OriginalAssociatedPostLoadResult PostProcessAssociatedObjects() => OriginalAssociatedObjectPostLoad.Apply(this);

    private sealed class AssociatedChainSequence(OriginalEventInitializationContext state,
        OriginalAssociatedContainerReference container) : IReadOnlyList<OriginalPersonLinkedEntityReference?>
    {
        public int Count => this.Count(_ => true);
        public OriginalPersonLinkedEntityReference? this[int index]
            => index < 0 ? throw new ArgumentOutOfRangeException(nameof(index)) : this.Skip(index).First();
        public IEnumerator<OriginalPersonLinkedEntityReference?> GetEnumerator()
        {
            var handle = state.ReadAssociatedChainDescriptor(container)
                ?? throw new InvalidDataException("Current person-linked associated descriptor is unknown.");
            var node = state.ReadAssociatedChainHead(handle)
                ?? throw new InvalidDataException("Current person-linked associated head is unknown.");
            var visited = new HashSet<OriginalRegistryListNodeReference>();
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
