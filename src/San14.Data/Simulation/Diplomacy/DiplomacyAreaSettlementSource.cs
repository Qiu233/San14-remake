using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The normal offer selector uses333F20, with a real Area receiver. A zero
    //selection is the absent offer, not an Area to transfer in the other direction.
    internal bool ReadDiplomacyAreaOfferEligibilityCurrent(GameState state, SourceWorldContext current,
        int areaPhysical, int giverForceId, int recipientForceId)
    {
        if (areaPhysical is < 1 or > 500) return false;
        var area = new AreaReference(areaPhysical);
        if (!DNeed(current.ReadAreaNativeValidity(area), "offer_Area_validity")) return false;
        return ReadDiplomacyAreaPredicate333F20(current, area, DiplomacyAreaForce(current, giverForceId),
            DiplomacyAreaForce(current, recipientForceId));
    }

    internal void RequireDiplomacyAreaOfferCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, DiplomacyOffer offer)
    {
        if (offer.AreaId is null or 0) return;
        if (offer.AreaId is not (>= 1 and <= 500))
            throw new ArgumentException("The diplomatic Area identity is invalid.");
        //825850's real Area choice clears Item and both resource amounts.
        if (offer.Gold != 0 || offer.Supplies != 0 || offer.ItemId is not (null or 0))
            throw new ArgumentException("A diplomatic Area offer cannot include another payment.");
        var q = new DiplomacyQuery(this, state, current, task);
        var area = current.ReadAreaTableReference(offer.AreaId.Value) ?? throw DiplomacyNumericMissing("offer_Area_pointer");
        if (!DNeed(current.ReadAreaNativeValidity(area), "offer_Area_validity") ||
            !ReadDiplomacyAreaPredicate333F20(current, area, q.Own, q.Other))
            throw new ArgumentException("The diplomatic Area no longer satisfies the transfer rules.");
    }

    //1E81E0 calls333F20 for a retained positive Cmd82. The original does not
    //first add an Area native-validity gate; retain the actual predicate here.
    internal bool IsDiplomacyInitialAreaStillQualifiedCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task)
    {
        if (task.InitialOffer.AreaId is not (>= 1 and <= 500)) return true;
        var area = current.ReadAreaTableReference(task.InitialOffer.AreaId.Value)
            ?? throw DiplomacyNumericMissing("continuation_offer_Area_pointer");
        var q = new DiplomacyQuery(this, state, current, task);
        return ReadDiplomacyAreaPredicate333F20(current, area, q.Own, q.Other);
    }

    //81E4C0's Area step follows Item/resource settlement.1F5570 only stores
    //the initial Area identity in Cmd82; it does not withdraw its territory.
    //A changed offer, rejection or cancellation therefore has no Area refund
    //or fake transfer back. Only the accepted final valid receiver is written.
    internal GameState ApplyDiplomacyAreaOfferSettlementCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, DiplomacyOffer offer, bool accepted, List<CampaignEffectRequest> effects,
        NativeMapSeasonDate? operationDate = null)
    {
        if (!accepted || offer.AreaId is not (>= 1 and <= 500)) return state;
        var area = current.ReadAreaTableReference(offer.AreaId.Value) ?? throw DiplomacyNumericMissing("settlement_Area_pointer");
        if (!DNeed(current.ReadAreaNativeValidity(area), "settlement_Area_validity")) return state;
        var q = new DiplomacyQuery(this, state, current, task);
        return ApplyDiplomacyAreaTransferCurrent(state, current, area, q.Other, effects, operationDate);
    }

    //333F20: current assigned-City center and Gate centers are excluded;
    //the source must own every counted traversable cell and have at least one
    //actual recipient-owned neighbor selected by the fixed eight-edge mask.
    private bool ReadDiplomacyAreaPredicate333F20(SourceWorldContext current, AreaReference area,
        ForceReference giver, ForceReference recipient)
    {
        byte rawCity = DNeed(current.ReadAreaCitySlot(area), "offer_Area35");
        var city = current.ReadCityTableReference(rawCity < 52 ? rawCity : 0)
            ?? throw DiplomacyNumericMissing("offer_assigned_City_pointer");
        if (DNeed(current.ReadBaseNativeValidity(city), "offer_assigned_City_validity"))
        {
            ushort cityHex = current.ReadBaseCurrentHexIndex(city);
            if (cityHex == DNeed(current.ReadAreaHexIndex(area), "offer_city_test_Area3A")) return false;
        }
        var center = DiplomacyAreaCenter(current, area);
        if (DNeed(current.ReadHexSurfaceCode(center), "offer_center_Hex10") == 17) return false;
        int giverId = DNeed(current.ReadForcePhysicalSlot(giver), "offer_giver_Force60");
        if (DNeed(current.ReadAreaCurrentForceSlot(area), "offer_center_Force14") != giverId) return false;
        if (DNeed(current.ReadAreaRawField68(area), "offer_Area68") !=
            DNeed(current.ReadAreaRawField76(area), "offer_Area76")) return false;

        int physical = DNeed(area.PhysicalSlot, "offer_Area_identity");
        if (physical < 0 || physical >= _scenario.Snapshot.Areas.Count)
            throw DiplomacyNumericMissing("offer_Area66_definition");
        byte mask = _scenario.Snapshot.Areas[physical].RegionalNeighborMask;
        bool adjacent = false;
        for (int index = 0; index < 8; index++)
        {
            ushort raw = DNeed(current.ReadAreaNeighborSlot(area, index), "offer_neighbor56");
            var neighbor = current.ReadAreaTableReference(raw < 501 ? raw : 0)
                ?? throw DiplomacyNumericMissing("offer_neighbor_pointer");
            if (neighbor.PhysicalSlot is null || !DNeed(current.ReadAreaNativeValidity(neighbor), "offer_neighbor_validity")) continue;
            int recipientId = DNeed(current.ReadForcePhysicalSlot(recipient), "offer_recipient_Force60");
            if (DNeed(current.ReadAreaCurrentForceSlot(neighbor), "offer_neighbor_Force14") == recipientId &&
                (mask & (1 << index)) != 0) adjacent = true;
        }
        return adjacent;
    }

    private static HexReference DiplomacyAreaCenter(SourceWorldContext current, AreaReference area)
    {
        ushort raw = DNeed(current.ReadAreaHexIndex(area), "offer_center_Area3A");
        return current.ReadHexTableReference(raw < 48400 ? raw : 0)
            ?? throw DiplomacyNumericMissing("offer_center_Hex_pointer");
    }

    private static ForceReference DiplomacyAreaForce(SourceWorldContext current, int physical)
        => current.ReadForceTableReference(physical is >= 0 and < 52 ? physical : 0)
            ?? throw DiplomacyNumericMissing("offer_Force_pointer");
}
