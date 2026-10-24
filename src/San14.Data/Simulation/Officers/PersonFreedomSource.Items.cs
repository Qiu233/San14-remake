using San14.Core;

namespace San14.Data;

public sealed partial class PersonFreedomSource
{
    // 265250 releases ordinary equipment into its already selected Area.
    // Keep removal deferred until the live holder iteration has finished.
    // This is local operation bookkeeping, not another saved registry owner.
    internal static GameState DropFreedomItems(GameState state, SourceWorldContext current,
        int person, int area)
    {
        ushort holderKey = K(current.ReadPersonIdentity(person), "current.freedom_Item_Person_identity");
        var descriptor = current.ReadRegistryDescriptor(RegistryKind.Item)
            ?? throw Missing("current.freedom_Item_manager");
        var node = current.ReadRegistryHead(descriptor) ?? throw Missing("current.freedom_Item_head");
        var removals = new List<RegistryObjectReference>();
        while (node.Identity.HasValue)
        {
            var reference = current.ReadRegistryNodePayload(node) ?? throw Missing("current.freedom_Item_payload");
            if (reference.Kind != RegistryKind.Item || reference.PhysicalSlot is not int physical)
                throw Missing("current.freedom_Item_pointer");
            if (holderKey is >= 1 and <= 6000 &&
                K(current.ReadCaptureItemOwner4A(physical), "current.freedom_Item4A") == holderKey)
            {
                var item = state.Items?.GetValueOrDefault(physical)
                    ?? throw Missing("current.freedom_Item_saved_owner");
                byte flags = K(item.Lifecycle?.RetainedFlags, "current.freedom_Item_flags");
                if ((flags & 1) == 0)
                {
                    var lifecycle = item.Lifecycle! with { RetainedFlags = (byte)(flags & 0xF7) };
                    item = item with { Lifecycle = lifecycle };
                    // The existing 324580 owner=0/mode=2 writer accepts only
                    // Area1..500. Area0 keeps its flag clear but has no drop.
                    if (unchecked((uint)(area - 1)) < 500)
                    {
                        item = item with
                        {
                            Holder = new ItemHolder(null),
                            Lifecycle = lifecycle with
                            {
                                RetainedLocationCode = (ushort)area,
                                Status = ItemStatusKnowledge.Exact(2)
                            }
                        };
                        current.SetCaptureItemOwner4A(physical, 0);
                        current.SetRegistryItemRawField50(physical, 2);
                        removals.Add(reference);
                    }
                    state = state with { Items = new(state.Items!) { [physical] = item } };
                }
            }
            node = current.ReadRegistryNodeNext(node) ?? throw Missing("current.freedom_Item_Next");
        }
        foreach (var item in removals)
        {
            // 32B790 searches before its conditional locked remove-all.
            node = current.ReadRegistryHead(descriptor) ?? throw Missing("current.freedom_Item_remove_head");
            while (node.Identity.HasValue)
            {
                var payload = current.ReadRegistryNodePayload(node) ?? throw Missing("current.freedom_Item_remove_payload");
                if (payload == item)
                {
                    _ = current.RemoveAllRegisteredItemReferences(item);
                    break;
                }
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("current.freedom_Item_remove_next");
            }
        }
        return state;
    }
}
