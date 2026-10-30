using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //1F2600. The cancelled caravan returns its gold, food and escrowed item;
    //the source does not refund the offered troops or command books.
    internal (GameState State, PersonnelCaravanTask Task) RefundForeignTradeCargoCurrent(GameState state,
        SourceWorldContext current, PersonnelCaravanTask task, NativeMapSeasonDate date)
    {
        var force = current.ReadForceTableReference(task.SourceForceId is >= 0 and < 52 ? task.SourceForceId : 0)
            ?? throw ForeignTradeCommandMissing("refund_source_Force");
        int rawItem = task.OfferedItemId is >= 0 and <= 201 ? task.OfferedItemId : 0;
        var item = current.ReadRegistryTableReference(RegistryKind.Item, rawItem)
            ?? throw ForeignTradeCommandMissing("refund_Item_pointer");
        if (ForeignTradeNeed(current.ReadForceNativeValidity(force), "refund_source_Force_validity"))
        {
            int rulerId = TalentRulerCurrent(current, force);
            int ruler = ForeignTradeNeed((current.ReadPersonLookupReference(rulerId <= 6000 ? rulerId : 0)
                ?? throw ForeignTradeCommandMissing("refund_ruler_lookup")).PhysicalSlot, "refund_ruler_pointer");
            var home = BaseLookup.Find(ForeignTradeNeed(current.ReadPersonFirstLocation(ruler), "refund_ruler_home"), current);
            if (ForeignTradeNeed(current.ReadBaseNativeValidity(home), "refund_home_validity"))
            {
                if (ForeignTradeNeed(current.ReadRegistryObjectNativeValidity(item), "refund_Item_validity"))
                    state = ReserveDiplomacyItemCurrent(state, current, rawItem, rulerId);
                state = DAddDiplomacyResource(state, current, home, BaseResourceKind.Gold, task.GoldOffer);
                state = DAddDiplomacyResource(state, current, home, BaseResourceKind.Supplies, task.SuppliesOffer);
            }
        }
        else if (ForeignTradeNeed(current.ReadRegistryObjectNativeValidity(item), "refund_orphan_Item_validity"))
            state = PlaceCancelledCaravanItemCurrent(state, current, task, item);
        return (state, task with { OfferedItemId = 0, GoldOffer = 0, SuppliesOffer = 0, TroopOffer = 0 });
    }

    private GameState PlaceCancelledCaravanItemCurrent(GameState state, SourceWorldContext current,
        PersonnelCaravanTask task, RegistryObjectReference item)
    {
        //Caravan virtualD8 returns the country's port, not an interpolated
        //movement position. The nonempty City-Area branch preserves that Area.
        var country = ReadForeignTradeCountry(task.CountryId);
        var port = current.ReadCityTableReference(country.HostCityId < 52 ? country.HostCityId : 0)
            ?? throw ForeignTradeCommandMissing("refund_orphan_port");
        var hex = current.ReadHexTableReference(current.ReadBaseCurrentHexIndex(port))
            ?? throw ForeignTradeCommandMissing("refund_orphan_port_Hex");
        int placement = ForeignTradeNeed(current.ReadHexAreaSlot(hex), "refund_orphan_port_Area");
        if (task.MemberIds.Length > 0)
        {
            int member = ForeignTradeNeed((current.ReadPersonLookupReference(task.MemberIds[0] <= 6000 ? task.MemberIds[0] : 0)
                ?? throw ForeignTradeCommandMissing("refund_orphan_member_lookup")).PhysicalSlot, "refund_orphan_member");
            if (ForeignTradeNeed(current.ReadPersonNativeValidity(member), "refund_orphan_member_validity"))
            {
                var home = BaseLookup.Find(ForeignTradeNeed(current.ReadPersonFirstLocation(member), "refund_orphan_member_home"), current);
                if (ForeignTradeNeed(current.ReadBaseNativeValidity(home), "refund_orphan_home_validity") &&
                    ForeignTradeNeed(current.ReadBaseObjectLink(home), "refund_orphan_home_identity") < 52)
                {
                    int city = ForeignTradeNeed(home.PhysicalSlot, "refund_orphan_City");
                    var list = current.CreateNearbyTemporary();
                    try
                    {
                        foreach (var entry in current.ReadRegisteredAreaReferences() ?? throw ForeignTradeCommandMissing("refund_orphan_Area_registry"))
                        {
                            var area = entry ?? throw ForeignTradeCommandMissing("refund_orphan_Area_pointer");
                            if (city is < 1 or > 51 || ForeignTradeNeed(current.ReadAreaCitySlot(area), "refund_orphan_Area_City") != city) continue;
                            var node = current.AllocateRegistryNode(new(list.Identity));
                            if (node?.Identity is not null) current.WriteRegistryNodePayload(node, new(RegistryKind.Area, area.PhysicalSlot));
                        }
                        var head = current.ReadRegistryHead(new(list.Identity)) ?? throw ForeignTradeCommandMissing("refund_orphan_CityArea_head");
                        if (head.Identity is null)
                            throw ForeignTradeCommandMissing("refund_orphan_empty_CityArea_native_null_dereference");
                    }
                    finally { current.ClearNearbyTemporary(list, true); }
                }
            }
        }
        int id = ForeignTradeNeed(item.PhysicalSlot, "refund_orphan_Item_identity");
        var items = state.Items ?? throw ForeignTradeCommandMissing("refund_orphan_Item_inventory");
        var previous = items.GetValueOrDefault(id) ?? throw ForeignTradeCommandMissing("refund_orphan_Item_owner");
        var lifecycle = previous.Lifecycle ?? throw ForeignTradeCommandMissing("refund_orphan_Item_lifecycle");
        byte flags = ForeignTradeNeed(lifecycle.RetainedFlags, "refund_orphan_Item_flags");
        lifecycle = lifecycle with { RetainedFlags = (byte)(flags & 0xF7) };
        var changed = previous with { Lifecycle = lifecycle };
        if (placement is >= 1 and <= 500)
        {
            current.SetCaptureItemOwner4A(id, 0); current.SetRegistryItemRawField50(id, 2);
            changed = changed with { Holder = new(null), Lifecycle = lifecycle with
                { RetainedLocationCode = (ushort)placement, Status = ItemStatusKnowledge.Exact(2) } };
            _ = current.RemoveAllRegisteredItemReferences(item);
        }
        return state with { Items = new(items) { [id] = changed } };
    }
}
