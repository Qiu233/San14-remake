namespace San14.Data;

public sealed record AssociatedContainerReference(int? Identity);
public sealed record AssociatedTreeNodeReference(int? Identity);
public sealed record AssociatedPostLoadResult(int TreeCalls, int ListCalls);

public interface IAssociatedObjectPostLoadState
{
    AssociatedContainerReference? ReadCurrentAssociatedContainer();
    AssociatedTreeNodeReference? ReadAssociatedTreeHead(AssociatedContainerReference container);
    AssociatedTreeNodeReference? ReadAssociatedTreeLink(AssociatedTreeNodeReference node, int offset);
    byte? ReadAssociatedTreeNil19(AssociatedTreeNodeReference node);
    PersonLinkedEntityReference? ReadAssociatedTreePayload28(AssociatedTreeNodeReference node);
    RegistryListHandle? ReadAssociatedChainDescriptor(AssociatedContainerReference container);
    RegistryListNodeReference? ReadAssociatedChainHead(RegistryListHandle handle);
    PersonLinkedEntityReference? ReadAssociatedChainPayload(RegistryListNodeReference node);
    RegistryListNodeReference? ReadRegistryNodeNext(RegistryListNodeReference node);
    void InvokeAssociatedPayloadSlot7(PersonLinkedEntityReference payload);
}

/// <summary>Complete1EB420: current ordered tree, then its separate primary
/// shared chain. The operator itself performs no clear/free/count writes.</summary>
public static class AssociatedObjectPostLoad
{
    public static AssociatedPostLoadResult Apply(IAssociatedObjectPostLoadState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        AssociatedContainerReference container = Pointer(state.ReadCurrentAssociatedContainer(), "container");
        if (!container.Identity.HasValue) throw new InvalidDataException("Null associated container receiver.");
        var head = Node(state.ReadAssociatedTreeHead(container), "tree header");
        var node = Link(head, 0);
        int treeCalls = 0, listCalls = 0;
        var visitedTree = new HashSet<AssociatedTreeNodeReference>();
        while (node != head)
        {
            if (!visitedTree.Add(node)) throw new InvalidDataException("Associated tree traversal contains a cycle.");
            state.InvokeAssociatedPayloadSlot7(Pointer(state.ReadAssociatedTreePayload28(node), "tree payload"));
            treeCalls++;
            if (Nil(node) == 0)
            {
                var candidate = Link(node, 0x10);
                var successorPath = new HashSet<AssociatedTreeNodeReference> { node };
                if (Nil(candidate) == 0)
                {
                    node = candidate;
                    candidate = Link(candidate, 0);
                    while (Nil(candidate) == 0)
                    {
                        if (!successorPath.Add(candidate)) throw new InvalidDataException("Associated tree left path contains a cycle.");
                        node = candidate; candidate = Link(candidate, 0);
                    }
                }
                else
                {
                    candidate = Link(node, 8);
                    if (Nil(candidate) == 0)
                        while (node == Link(candidate, 0x10))
                        {
                            if (!successorPath.Add(candidate)) throw new InvalidDataException("Associated tree parent path contains a cycle.");
                            node = candidate; candidate = Link(candidate, 8);
                            if (Nil(candidate) != 0) break;
                        }
                    node = candidate;
                }
            }
            head = Node(state.ReadAssociatedTreeHead(container), "fresh tree header");
        }

        RegistryListHandle handle = Pointer(state.ReadAssociatedChainDescriptor(container), "chain descriptor");
        RegistryListNodeReference chain = Pointer(state.ReadAssociatedChainHead(handle), "chain head");
        var visitedList = new HashSet<RegistryListNodeReference>();
        while (chain.Identity.HasValue)
        {
            if (!visitedList.Add(chain)) throw new InvalidDataException("Associated shared chain contains a cycle.");
            state.InvokeAssociatedPayloadSlot7(Pointer(state.ReadAssociatedChainPayload(chain), "chain payload"));
            listCalls++;
            chain = Pointer(state.ReadRegistryNodeNext(chain), "fresh chain next");
        }
        return new(treeCalls, listCalls);

        AssociatedTreeNodeReference Link(AssociatedTreeNodeReference current, int offset)
            => Node(state.ReadAssociatedTreeLink(current, offset), "tree link");
        byte Nil(AssociatedTreeNodeReference current) => state.ReadAssociatedTreeNil19(current)
            ?? throw new InvalidDataException("Current associated tree nil19 is unknown.");
    }
    private static T Pointer<T>(T? value, string field) where T : class
        => value ?? throw new InvalidDataException($"Current associated {field} is unknown.");
    private static AssociatedTreeNodeReference Node(AssociatedTreeNodeReference? value, string field)
    {
        var node = Pointer(value, field);
        return node.Identity.HasValue ? node : throw new InvalidDataException($"Null associated {field} is unsafe.");
    }
}
