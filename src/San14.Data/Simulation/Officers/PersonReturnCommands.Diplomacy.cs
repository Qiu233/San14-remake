using San14.Core;

namespace San14.Data;

public static partial class PersonReturnCommands
{
    //1F5570 registers before assigning its member. Source and destination
    //are actual Bases, while its travel calls231670 with a known-null Force.
    internal static GameState StartDiplomacyTask(GameState state, SourceWorldContext current,
        PrepareDiplomaticOrder order, DiplomacySubmission packet, NativeMapSeasonDate date,
        Func<GameState, BaseReference, GameState> repairGovernor,
        Func<GameState,SourceWorldContext,PrepareDiplomaticOrder,(GameState State,int Outcome)> readDispatchAdvice)
    {
        var owner = CurrentCommandOwner(state, current);
        if (owner.Diplomacies is null) throw Missing("saved diplomacy task owner");
        if (owner.AvailableDispatchSlots.Length == 0) throw Missing("diplomacy dispatch allocation");
        int id = owner.NextTaskId, slot = owner.AvailableDispatchSlots[0], actor = packet.ExecutorPhysical;
        var task = new PersonnelDiplomacyTask(id, slot, [], order.Kind, packet.NativeKind,
            order.ForceId, order.SourceCityId, packet.SourceDistrictId, order.TargetForceId,
            packet.TargetRulerOfficerId, packet.TargetCapitalCityId, packet.AllianceMonths,
            order.TargetCityId, new(order.GiftGold, order.GiftSupplies, order.GiftItemId, order.GiftAreaId), 48400, 48400,
            date, new(0, 1, 1, 0), false, false, false, false, false, true)
        { RequestedCaptiveOfficerIds=order.RequestedCaptiveOfficerIds, RequestedOffer=order.RequestedOffer };
        owner = owner with { NextTaskId = checked(id + 1), AvailableDispatchSlots = owner.AvailableDispatchSlots[1..],
            Diplomacies = new(owner.Diplomacies) { [id] = task } };
        var handle = current.OwnedIndexes?.AssociatedTaskHandle ?? throw Missing("diplomacy association index");
        if (!current.AppendPersonReturnAssociated(new(handle.Identity), id)) throw Missing("diplomacy registration");
        current.SetOwnedPersonCommandManager(owner);
        current.SetLinkedEntityRawState58(id, slot);
        current.SetLinkedEntityMemberIds(id, []);
        current.SetAssociatedPayloadPostLoadCallback(id, PostLoadCallback.NoOp);
        ushort flags = Get(current.ReadArmyReturnPersonFlags196(actor), "diplomacy Person15E");
        current.WriteArmyReturnPersonFlags196(actor, (ushort)(flags | 1));
        var position = ResolvePersonPosition(state, current, actor, date);
        ushort area = Get(current.ReadHexAreaSlot(position), "diplomacy actor Area");
        current.WritePersonSecondLocation(actor, area is >= 1 and <= 500 ? (ushort)(area + 561) : (ushort)0);
        ushort personId = Get(current.ReadPersonIdentity(actor), "diplomacy actor Person10");
        task = task with { MemberIds = [order.ExecutorOfficerId] };
        owner.Diplomacies![id] = task;
        current.SetLinkedEntityMemberIds(id, [(uint?)personId]);
        var working = state with { PersonnelCommands = owner };
        var start = Hex(current.ReadBaseCurrentHexIndex(packet.SourceBase));
        var end = Hex(current.ReadBaseCurrentHexIndex(packet.TargetCapitalBase));
        var temporary = current.CreateTemporaryHexList() ?? throw Missing("diplomacy travel temporary");
        int days;
        try
        {
            var route = PersonReturnPathSource.Fill(working, current, start, end, new(null), temporary);
            days = Get(route.Days, route.UnresolvedOwner ?? "diplomacy travel231670");
        }
        finally { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); }
        task = task with { StartHexId = Get(start.PhysicalSlot, "diplomacy start Hex"),
            EndHexId = Get(end.PhysicalSlot, "diplomacy end Hex"), DueDate = DateFromOrdinal(unchecked(DateOrdinal(date) + days)) };
        owner.Diplomacies[id] = task;
        var district = working.Districts.GetValueOrDefault(packet.SourceDistrictId) ?? throw Missing("diplomacy source District");
        var treasuryBase = BaseCurrencyAdditionSource.CurrencyCity(packet.SourceTreasuryBase, current);
        int treasuryId = Get(treasuryBase.PhysicalSlot, "diplomacy source treasury City");
        var treasury = working.Cities.GetValueOrDefault(treasuryId) ?? throw Missing("diplomacy source treasury");
        int books = Get(district.OrdersRemaining, "diplomacy source District14");
        var capacities = BasePostLoad.ReadResourceCapacities(treasuryBase, current);
        int gold = ArmyCampaignRuleAdapter.ReadDiplomacyBaseResourceCurrent(current, packet.SourceTreasuryBase, BaseResourceKind.Gold);
        int supplies = ArmyCampaignRuleAdapter.ReadDiplomacyBaseResourceCurrent(current, packet.SourceTreasuryBase, BaseResourceKind.Supplies);
        var paid = treasury with { Gold = Math.Clamp(unchecked(gold - packet.PaidGold), 0, capacities.Gold),
            Supplies = Math.Clamp(unchecked(supplies - packet.PaidSupplies), 0, capacities.Supplies) };
        current.SetBaseResource(treasuryBase, BaseResourceKind.Gold, paid.Gold);
        current.SetBaseResource(treasuryBase, BaseResourceKind.Supplies, paid.Supplies);
        working = working with
        {
            PersonnelCommands = owner,
            Districts = new(working.Districts) { [district.Id] = district with { OrdersRemaining = Math.Clamp(unchecked(books - packet.Books), 0, 200) } },
            Cities = new(working.Cities) { [paid.Id] = paid }
        };
        if (order.GiftItemId is >= 1 and <= 201)
            working = ArmyCampaignRuleAdapter.ReserveDiplomacyItemCurrent(working, current, order.GiftItemId.Value, personId);
        if (Get(current.ReadArmyReturnBaseGovernor2E(packet.SourceBase), "diplomacy source Governor2E") == personId)
            working = repairGovernor(working, packet.SourceBase);
        var officer = working.Officers.GetValueOrDefault(order.ExecutorOfficerId) ?? throw Missing("diplomacy actor domain");
        working = working with { Officers = new(working.Officers) { [officer.Id] = officer with { Behavior = current.ReadPersonBehavior(actor) } } };
        working = OfficerLocationSourceAdapter.PublishPosition(working, officer.Id, current);
        var advice=readDispatchAdvice(working,current,order);
        working=advice.State;
        owner=current.ReadOwnedPersonCommandManager()??throw Missing("dispatch advice task owner");
        task=(owner.Diplomacies?.GetValueOrDefault(id)??throw Missing("dispatch advice task"))
            with {AdvisorOutcomeAtDispatch=advice.Outcome};
        owner=owner with {Diplomacies=new(owner.Diplomacies!){[id]=task}};
        current.SetOwnedPersonCommandManager(owner);
        working=working with {PersonnelCommands=owner};
        BindCurrent(working, current);
        return working;

        HexReference Hex(int raw) => current.ReadHexTableReference(raw is >= 0 and < 48400 ? raw : 0)
            ?? throw Missing("diplomacy route Hex");
    }
}
