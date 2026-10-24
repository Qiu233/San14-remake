using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>CCmdActionDiplomacy virtual90 (1F1CE0), before the caller's
    /// normal return/retirement. The cancellation report belongs to its caller.
    /// Refunds use the source Force's current ruler Home, not the dispatch City.
    /// An invalid source Force retains the actual journey Area as the Item's
    /// placement. The original empty City-Area candidate chain would dereference
    /// a null head; that reached branch remains explicit. Command books and
    /// Area82 are untouched.</summary>
    internal GameState RefundCancelledDiplomacyCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, List<CampaignEffectRequest> effects, NativeMapSeasonDate date)
    {
        var source = current.ReadForceTableReference(task.ForceId <= 51 ? task.ForceId : 0)
            ?? throw DiplomacyNumericMissing("cancel_refund_source_Force_pointer");
        int rawItem = task.InitialOffer.ItemId ?? 0;
        var item = current.ReadRegistryTableReference(RegistryKind.Item, rawItem <= 201 ? rawItem : 0)
            ?? throw DiplomacyNumericMissing("cancel_refund_Item_pointer");
        if (!DNeed(current.ReadForceNativeValidity(source), "cancel_refund_source_Force_validity"))
        {
            if (DNeed(current.ReadRegistryObjectNativeValidity(item), "cancel_refund_orphan_Item_validity"))
                return PlaceCancelledDiplomacyItemCurrent(state, current, task, item, date);
            return state;
        }
        int force = DNeed(source.PhysicalSlot, "cancel_refund_source_Force_identity");
        ushort ruler = current.ReadForceRecord(force)?.RawRuler10
            ?? throw DiplomacyNumericMissing("cancel_refund_ruler10");
        var rulerPointer = ruler <= 6000 ? current.ReadPersonLookupReference(ruler) : current.ReadTransferPersonTableReference(0);
        int person = DNeed(rulerPointer?.PhysicalSlot, "cancel_refund_ruler_pointer");
        var home = BaseLookup.Find(DNeed(current.ReadPersonFirstLocation(person), "cancel_refund_rulerE2"), current);
        if (!DNeed(current.ReadBaseNativeValidity(home), "cancel_refund_Home_validity")) return state;
        if (DNeed(current.ReadRegistryObjectNativeValidity(item), "cancel_refund_Item_validity"))
        {
            int physical = DNeed(item.PhysicalSlot, "cancel_refund_Item_identity");
            int rulerId = DNeed(current.ReadForcePhysicalSlot(source), "cancel_refund_Force60") == 0 ? 5014 : ruler;
            //The existing324580 writer clears4F bit3 before its owner-ID
            //admission, and registers only an actual Item50 transition.
            state = ReserveDiplomacyItemCurrent(state, current, physical, rulerId);
        }
        state = DAddDiplomacyResource(state, current, home, BaseResourceKind.Gold, task.InitialOffer.Gold);
        return DAddDiplomacyResource(state, current, home, BaseResourceKind.Supplies, task.InitialOffer.Supplies);
    }

    private static GameState PlaceCancelledDiplomacyItemCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, RegistryObjectReference item, NativeMapSeasonDate date)
    {
        //The virtualD8 receiver is the command itself, independently of the
        //first member's current association.22B5F0 retains its actual route.
        var position = TalentTravelSource.ResolvePosition(state, current, task.StartHexId, task.EndHexId,
            task.SourceDistrictId, task.StartDate, task.DueDate, date);
        if (position.PhysicalSlot is null) throw DiplomacyNumericMissing("cancel_orphan_CommandD8_null_Hex");
        int placement = DNeed(current.ReadHexAreaSlot(position), "cancel_orphan_CommandD8_Hex12");
        var members = current.ReadLinkedEntityMemberIds(new(task.Id))
            ?? throw DiplomacyNumericMissing("cancel_orphan_Command20_members");
        if (members.Count != 0)
        {
            uint first = DNeed(members[0], "cancel_orphan_first_member");
            if (first > int.MaxValue) throw DiplomacyNumericMissing("cancel_orphan_negative_member_lookup");
            var pointer = first <= 6000 ? current.ReadPersonLookupReference((int)first) : current.ReadTransferPersonTableReference(0);
            int person = DNeed(pointer?.PhysicalSlot, "cancel_orphan_first_member_pointer");
            if (DNeed(current.ReadPersonNativeValidity(person), "cancel_orphan_first_member_validity"))
            {
                var home = BaseLookup.Find(DNeed(current.ReadPersonFirstLocation(person), "cancel_orphan_first_memberE2"), current);
                if (DNeed(current.ReadBaseNativeValidity(home), "cancel_orphan_member_Home_validity") &&
                    DNeed(current.ReadBaseObjectLink(home), "cancel_orphan_member_Home4E") < 52)
                {
                    if (home.Kind != BaseKind.City) throw DiplomacyNumericMissing("cancel_orphan_nonCity_CityAreaIterator");
                    var candidates = current.CreateNearbyTemporary();
                    try
                    {
                        int city = DNeed(home.PhysicalSlot, "cancel_orphan_member_City_identity");
                        foreach (var entry in current.ReadRegisteredAreaReferences()
                            ?? throw DiplomacyNumericMissing("cancel_orphan_AreaB0"))
                        {
                            var area = entry ?? throw DiplomacyNumericMissing("cancel_orphan_Area_pointer");
                            //CCityAreaIterator tests only City1..51 and Area35;
                            //neither current Area validity nor Force is a gate.
                            if (city is < 1 or > 51 || DNeed(current.ReadAreaCitySlot(area), "cancel_orphan_Area35") != city) continue;
                            var node = current.AllocateRegistryNode(new(candidates.Identity));
                            if (node?.Identity is not null)
                                current.WriteRegistryNodePayload(node, new(RegistryKind.Area, area.PhysicalSlot));
                        }
                        var head = current.ReadRegistryHead(new(candidates.Identity))
                            ?? throw DiplomacyNumericMissing("cancel_orphan_CityArea_head");
                        //Cached ASM1F1FF7/1F1FFC explicitly jumps OVER shuffle
                        //when head!=null. Do not reverse it or draw a sample.
                        //A null chain passes unchanged through1D7220 and is
                        //then dereferenced at1F206E: no actual fallback exists.
                        if (head.Identity is null)
                            throw DiplomacyNumericMissing("cancel_orphan_empty_CityArea_native_null_dereference_1F206E");
                    }
                    finally { current.ClearNearbyTemporary(candidates, true); }
                }
            }
        }
        int physical = DNeed(item.PhysicalSlot, "cancel_orphan_Item_identity");
        var items = state.Items ?? throw DiplomacyNumericMissing("cancel_orphan_Item_inventory");
        var before = items.GetValueOrDefault(physical) ?? throw DiplomacyNumericMissing("cancel_orphan_Item_owner");
        var lifecycle = before.Lifecycle ?? throw DiplomacyNumericMissing("cancel_orphan_Item_lifecycle");
        byte flags = DNeed(lifecycle.RetainedFlags, "cancel_orphan_Item4F");
        lifecycle = lifecycle with { RetainedFlags = (byte)(flags & 0xF7) };
        var changed = before with { Lifecycle = lifecycle };
        //324580(Item,0,placement,2,1) rejects Area0/outside1..500 after
        //the caller's flag clear. Such an Item retains its actual holder.
        if (placement is >= 1 and <= 500)
        {
            current.SetCaptureItemOwner4A(physical, 0);
            current.SetRegistryItemRawField50(physical, 2);
            changed = changed with { Holder = new(null), Lifecycle = lifecycle with
                { RetainedLocationCode = (ushort)placement, Status = ItemStatusKnowledge.Exact(2) } };
            _ = current.RemoveAllRegisteredItemReferences(item);
        }
        return state with { Items = new(items) { [physical] = changed } };
    }
}
