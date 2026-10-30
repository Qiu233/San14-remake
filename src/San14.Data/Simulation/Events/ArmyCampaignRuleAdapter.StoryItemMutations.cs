using San14.Core;

namespace San14.Data;

internal sealed record StoryItemGainOccurrence(int PersonPhysicalSlot, int ItemPhysicalSlot);
internal sealed record StoryItemMutationResult(GameState State, int NativeReturn,
    IReadOnlyList<StoryItemGainOccurrence> OrderedItemGains);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Ordinary Item ownership instructions retain their separate
    /// admissions and notification tails. Person selectors are resolved only
    /// at their reached position, including the actual negative selectors.</summary>
    internal StoryItemMutationResult ApplyStoryItemMutationCurrent(GameState state,
        SourceWorldContext current, int code, IReadOnlyList<int> arguments, Func<int, int> resolvePerson)
    {
        if (arguments.Count != 8) throw StoryItemMissing("arguments");
        if (code == 166)
        {
            int source = resolvePerson(arguments[0]);
            int target = resolvePerson(arguments[1]);
            if (!ValidPerson(source) || !ValidPerson(target)) return new(state, 1, []);
            ushort targetId = Identity(target);
            var descriptor = current.ReadRegistryDescriptor(RegistryKind.Item) ?? throw StoryItemMissing("held_items_registry");
            var node = current.ReadRegistryHead(descriptor) ?? throw StoryItemMissing("held_items_head");
            while (node.Identity.HasValue)
            {
                //The live inventory iterator filters only by Person identity
                //and current holder. It adds no Item validity/status gate.
                ushort sourceId = Identity(source);
                if (sourceId is >= 1 and <= 6000)
                {
                    var item = current.ReadRegistryNodePayload(node) ?? throw StoryItemMissing("held_items_payload");
                    int physical = Need(item.PhysicalSlot, "held_item_pointer");
                    if (Need(current.ReadCaptureItemOwner4A(physical), "held_item_owner") == sourceId)
                        state = WriteDirectItemHolderCurrent(state, current, physical, targetId, clearReservationFlag: true);
                }
                node = current.ReadRegistryNodeNext(node) ?? throw StoryItemMissing("held_items_live_next");
            }
            return new(state, 1, []);
        }

        if (code is not (377 or 426 or 447 or 448)) throw StoryItemMissing("opcode");
        int literal = arguments[0];
        if (literal < 0) throw StoryItemMissing("negative_Item_table_selector");
        var pointer = current.ReadRegistryTableReference(RegistryKind.Item, literal < 202 ? literal : 0)
            ?? throw StoryItemMissing("Item_table_pointer");
        int itemSlot = Need(pointer.PhysicalSlot, "Item_physical_slot");
        if (code == 377)
        {
            if (!ValidItem(pointer)) return new(state, 0, []);
            int target = resolvePerson(arguments[1]);
            if (!ValidPerson(target)) return new(state, 0, []);
            state = WriteDirectItemHolderCurrent(state, current, itemSlot, Identity(target), clearReservationFlag: true);
            return new(state, 1, []);
        }

        //These instructions use physical identity, then the exact raw status;
        //generic Item validity would incorrectly reject their unacquired items.
        if (itemSlot == 0) return new(state, 0, []);
        byte status = Need(current.ReadRegistryItemRawField50(pointer), "Item_status");
        if (status != (code == 448 ? 2 : 6)) return new(state, 0, []);
        int receiver = resolvePerson(arguments[1]);
        if (!ValidPerson(receiver)) return new(state, 0, []);
        if (code != 448 && arguments[2] == 1)
        {
            var force = current.ReadPersonForce(receiver) ?? throw StoryItemMissing("receiver_Force");
            TransferPersonReference ruler;
            if (Need(current.ReadForceNativeValidity(force), "receiver_Force_validity"))
            {
                int forceSlot = Need(force.PhysicalSlot, "receiver_Force_pointer");
                ushort id = Need(current.ReadForceRecord(forceSlot)?.RawRuler10, "receiver_Force_ruler");
                ruler = (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))
                    ?? throw StoryItemMissing("receiver_ruler_lookup");
            }
            else ruler = current.ReadPersonLookupReference(0) ?? throw StoryItemMissing("invalid_Force_ruler_lookup");
            receiver = Need(ruler.PhysicalSlot, "receiver_ruler_pointer");
            if (!ValidPerson(receiver)) return new(state, 0, []);
        }

        ushort owner = Identity(receiver);
        //The mature acquisition writer clears the reservation even if its
        //owner range gate rejects the write. Unlike377/166 it does not clamp.
        state = SearchAcquireItem(state, current, itemSlot, owner);
        if (owner is >= 1 and <= 6000)
        {
            var pool = current.ReadOwnedCurrentListPool() ?? throw StoryItemMissing("acquisition_shared_pool");
            var lifecycle = state.ReadArmyLifecycle() ?? throw StoryItemMissing("acquisition_lifecycle");
            state = state.WithArmyLifecycle(lifecycle with { Indexes = pool.ExportOwnedState() });
        }
        if (code != 447) return new(state, 1, []);
        bool manager = Need(current.ReadEventManagerPresent(), "event_manager_presence");
        //3D4DB0 checks current validity after the writer. Its per-Person vector
        //then appends, preserving repeated Item occurrences without cleanup.
        return new(state, 1, manager && ValidPerson(receiver) && ValidItem(pointer)
            ? new[] { new StoryItemGainOccurrence(receiver, itemSlot) } : []);

        bool ValidPerson(int physical) => Need(current.ReadPersonNativeValidity(physical), "Person_validity");
        bool ValidItem(RegistryObjectReference item) => Need(current.ReadRegistryObjectNativeValidity(item), "Item_validity");
        ushort Identity(int physical) => Need(current.ReadPersonIdentity(physical), "Person_identity");
        static T Need<T>(T? value, string owner) where T : struct => value ?? throw StoryItemMissing(owner);
    }

    private static InvalidDataException StoryItemMissing(string owner) => new("story.item.current." + owner);
}
