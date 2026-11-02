using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>2F8F00's held-item temporary, then each2B3B history before
    /// its direct4F/4A/50 stores. This is not2EACB0's protected-item transfer
    /// and does not acquire/register an Item or resolve captive treatment.</summary>
    internal GameState ApplyReturnCaptiveItemsCurrent(GameState state,
        SourceWorldContext current, BaseReference target, int person, int reportRuler,
        Func<GameState, int>? readDay, List<CampaignEffectRequest> effects)
    {
        var district = current.ReadBaseDistrictReference(target) ?? throw ReturnItemMissing("target_District80");
        int districtSlot = Need(district.PhysicalSlot, "District_pointer");
        int leader = Person(Need(current.ReadDistrictLeader(districtSlot), "District12"));
        var receiver = current.ReadPersonForce(leader) ?? throw ReturnItemMissing("leader_Force22EA50");
        if (!Need(current.ReadForceNativeValidity(receiver), "receiver_Force_validity")) return state;
        var temporary = current.CreateTemporaryHexList() ?? throw ReturnItemMissing("Item_temporary");
        try
        {
            current.ClearTemporaryHexList(temporary);
            var descriptor = current.ReadRegistryDescriptor(RegistryKind.Item) ?? throw ReturnItemMissing("Item_registry");
            var node = current.ReadRegistryHead(descriptor) ?? throw ReturnItemMissing("Item_head");
            while (node.Identity.HasValue)
            {
                ushort id = Need(current.ReadPersonIdentity(person), "Person10");
                if (id is >= 1 and <= 6000)
                {
                    var item = current.ReadRegistryNodePayload(node) ?? throw ReturnItemMissing("Item_payload");
                    if (item is not { Kind: RegistryKind.Item, PhysicalSlot: int physical }) throw ReturnItemMissing("Item_pointer");
                    if (Need(current.ReadCaptureItemOwner4A(physical), "Item4A") == id)
                    {
                        var added = current.AllocateRegistryNode(new(temporary.Identity)) ?? throw ReturnItemMissing("Item_temporary_node");
                        if (added.Identity.HasValue) current.WriteRegistryNodePayload(added, item);
                    }
                }
                node = current.ReadRegistryNodeNext(node) ?? throw ReturnItemMissing("Item_next");
            }
            var at = current.ReadRegistryHead(new(temporary.Identity)) ?? throw ReturnItemMissing("transfer_head");
            while (at.Identity.HasValue)
            {
                var reference = current.ReadRegistryNodePayload(at) ?? throw ReturnItemMissing("transfer_payload");
                if (reference is not { Kind: RegistryKind.Item, PhysicalSlot: int physical }) throw ReturnItemMissing("transfer_pointer");
                var item = state.Items?.GetValueOrDefault(physical) ?? throw ReturnItemMissing("Item_owner");
                string name = state.ItemDefinitions?.GetValueOrDefault(item.DefinitionId)?.Name ?? throw ReturnItemMissing("Item_definition_name");
                int receiverSlot = Need(receiver.PhysicalSlot, "receiver_Force_pointer");
                ushort ruler = current.ReadForceRecord(receiverSlot)?.RawRuler10 ?? throw ReturnItemMissing("receiver_ruler10_argument");
                var formatted = FormatHistoryReport(state, current, 0x2B3B,
                    new(String0: name, Person0: ruler, Person1: Need(current.ReadPersonIdentity(person), "report_Person10")));
                state = formatted.State;
                int districtId = Need(current.ReadPersonDistrictSlot(reportRuler), "report_ruler_E0");
                int payload = Need(current.ReadPersonIdentity(reportRuler), "report_ruler10");
                int day = readDay is null ? FacilityOperation.Playable(state).ReadCampaignDay(state) : readDay(state);
                state = WriteHistory(state, current, new(0, districtId, 0, day, formatted.Text,
                    CampaignHistoryWriter.Supplemental(state), payload), effects);

                //The source does not test4F bit0 here. Preserve location,
                //other flag bits and registration; only these direct stores run.
                item = state.Items![physical];
                byte flags = Need(item.Lifecycle?.RetainedFlags, "Item4F");
                state = state with { Items = new(state.Items) { [physical] = item with
                    { Lifecycle = item.Lifecycle! with { RetainedFlags = (byte)(flags & 0xF7) } } } };
                ruler = current.ReadForceRecord(receiverSlot)?.RawRuler10 ?? throw ReturnItemMissing("receiver_ruler10_store");
                state = WriteDirectItemHolderCurrent(state, current, physical, ruler, clearReservationFlag: false);
                current.SetRegistryItemRawField50(physical, 1);
                item = state.Items![physical];
                state = state with { Items = new(state.Items) { [physical] = item with
                    { Lifecycle = (item.Lifecycle ?? throw ReturnItemMissing("Item_lifecycle")) with { Status = ItemStatusKnowledge.Exact(1) } } } };
                at = current.ReadRegistryNodeNext(at) ?? throw ReturnItemMissing("transfer_next");
            }
            return state;
        }
        finally { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); }

        int Person(ushort id) => (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0)) is { PhysicalSlot: int physical }
            ? physical : throw ReturnItemMissing("leader_lookup_or_pointer");
        T Need<T>(T? value, string owner) where T : struct => value ?? throw ReturnItemMissing(owner);
    }

    private static InvalidDataException ReturnItemMissing(string owner) => new("return.captive-items." + owner);
}
