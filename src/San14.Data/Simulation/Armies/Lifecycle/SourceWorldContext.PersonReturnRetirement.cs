namespace San14.Data;

public sealed partial class SourceWorldContext
{
    /// <summary>3DFB50 removes every matching receiver from manager18,
    /// preserving saved-next traversal and the original free-node payload.</summary>
    internal int RemovePersonReturnAssociated(RegistryListHandle descriptor, int receiver)
    {
        ConditionalRegistryEnter();
        try
        {
            int key = RegistryKey(descriptor), removed = 0;
            var node = ReadAssociatedChainHead(descriptor) ?? throw new InvalidDataException("Current Return association head is unknown.");
            while (node.Identity.HasValue)
            {
                var next = ReadRegistryNodeNext(node) ?? throw new InvalidDataException("Current Return association next is unknown.");
                var payload = ReadAssociatedChainPayload(node) ?? throw new InvalidDataException("Current Return association receiver is unknown.");
                if (payload.Identity == receiver) { RemoveAddressableRegistryNode(key, node); removed++; }
                node = next;
            }
            return removed;
        }
        finally { ConditionalRegistryExit(); }
    }
}
