using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //1EFD40's Item4A == executor Force ruler qualification. The existing
    //task's initial item is already escrowed by1F5570/324580 to its envoy;
    //that exact task-owned item is not a second arbitrary inventory choice.
    internal void RequireDiplomacyItemOfferCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, DiplomacyOffer offer)
    {
        if (offer.ItemId is not int physical) return;
        if (physical is < 1 or > 201)
            throw new ArgumentException("The diplomatic item identity is invalid.");
        //825A10 selects an item and clears both resource amounts and Area.
        if (offer.Gold != 0 || offer.Supplies != 0 || offer.AreaId is not null)
            throw new ArgumentException("A diplomatic item offer cannot include another payment.");
        if (task.InitialOffer.ItemId == physical) return;
        var reference = current.ReadRegistryTableReference(RegistryKind.Item, physical)
            ?? throw DiplomacyNumericMissing("offer_Item_pointer");
        if (!DNeed(current.ReadRegistryObjectNativeValidity(reference), "offer_Item_validity"))
            throw new ArgumentException("The diplomatic item is no longer available.");
        var q = new DiplomacyQuery(this, state, current, task);
        int holder = DNeed(current.ReadCaptureItemOwner4A(physical), "offer_Item4A");
        int ruler = q.Id(q.Ruler(q.Own));
        if (holder != ruler)
            throw new ArgumentException("The diplomatic item is not held by the initiating force's ruler.");
    }

    //1F5570 uses the same324580 inventory acquisition as a found item:
    //clear reservation, assign envoy, and register only a state50 transition.
    internal static GameState ReserveDiplomacyItemCurrent(GameState state, SourceWorldContext current,
        int itemId, int envoyId) => SearchAcquireItem(state, current, itemId, checked((ushort)envoyId));

    //81E4C0: this head precedes the four ordinary money/food virtual calls.
    private static GameState DApplyAcceptedDiplomaticItemHead(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, DiplomacyOffer offer, int ownRuler, int otherRuler)
    {
        if (offer.ItemId is not (>= 1 and <= 201)) return state;
        int physical = offer.ItemId.Value;
        var reference = current.ReadRegistryTableReference(RegistryKind.Item, physical)
            ?? throw DiplomacyNumericMissing("settlement_Item_pointer");
        if (!DNeed(current.ReadRegistryObjectNativeValidity(reference), "settlement_Item_validity")) return state;
        state = DWriteDiplomaticItem(state, current, physical, otherRuler, clearReservationFlag: true);
        if (task.InitialOffer.ItemId is >= 1 and <= 201 && task.InitialOffer.ItemId != physical)
            state = DWriteDiplomaticItem(state, current, task.InitialOffer.ItemId.Value, ownRuler, clearReservationFlag: true);
        return state;
    }

    //81E4C0's later original-item write is independent of the final item's
    //validity. This one only writes4A; unlike the head it does not touch4F.
    private static GameState DApplyAcceptedDiplomaticItemRefund(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, DiplomacyOffer offer, int ownRuler)
        => task.InitialOffer.ItemId is >= 1 and <= 201 && (offer.Gold != 0 || offer.Supplies != 0)
            ? DWriteDiplomaticItem(state, current, task.InitialOffer.ItemId.Value, ownRuler, clearReservationFlag: false)
            : state;

    //The rejected branch releases the original item before either resource
    //refund. It deliberately has no additional native-validity admission.
    private static GameState DApplyRejectedDiplomaticItem(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, int ownRuler)
        => task.InitialOffer.ItemId is >= 1 and <= 201
            ? DWriteDiplomaticItem(state, current, task.InitialOffer.ItemId.Value, ownRuler, clearReservationFlag: true)
            : state;

    private static GameState DWriteDiplomaticItem(GameState state, SourceWorldContext current,
        int physical, int holder, bool clearReservationFlag)
        => WriteDirectItemHolderCurrent(state, current, physical, holder, clearReservationFlag);
}
