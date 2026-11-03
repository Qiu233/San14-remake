using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //88F000 maps the real6D7660/6D5630 result codes, not page readiness or
    //the existence of a deployment/retreat callback. No application gate is
    //added to these two entrance predicates.
    private MapMainCommandEntryPresentation ReadCampaignCommandEntry(GameState state,
        SourceWorldContext current, MapMenuCommand command, Action bindCurrent)
    {
        try
        {
            if (command is not (MapMenuCommand.Deploy or MapMenuCommand.WithdrawAll))
                throw new ArgumentOutOfRangeException(nameof(command));
            var player = current.CurrentPlayerForce ?? throw MainCommandMissing("campaign_player");
            var district = ForcePrimaryDistrictSource.Resolve(current, player);
            int districtId = MainNeed(district.PhysicalSlot, "campaign_primary_district");
            int cost = command == MapMenuCommand.Deploy ? ReadCampaignLaunchCommandBooks(state)
                : ReadCampaignAllRetreatBookCostCurrent(state);
            if (MainNeed(current.ReadDistrictRawByte(districtId, 0x14), "campaign_books") < cost)
                return new(command, false, 0x5802);
            if (command == MapMenuCommand.WithdrawAll)
            {
                //The canonical expedition owner represents started native
                //records; reset/free records are absent.6D5630 scans the whole
                //51-slot table, not just campaigns attributed to this Force.
                var owner = state.CampaignExpeditions ?? throw MainCommandMissing("campaign_expedition_owner");
                for (int slot = 1; slot <= 50; slot++)
                    if (owner.Expeditions.GetValueOrDefault(slot) is { IsWithdrawing: false })
                        return new(command, true, 0x5804);
                return new(command, false, 0x5805);
            }
            bindCurrent();
            bool available = MainCampaignHasTarget(state, current, districtId);
            return new(command, available, available ? 0x5801 : 0x5803);
        }
        catch (InvalidDataException error) { return new(command, null, null, error.Message); }
    }

    private bool MainCampaignHasTarget(GameState state, SourceWorldContext current, int primaryDistrict)
    {
        int rawForce = MainNeed(current.ReadDistrictForceId(primaryDistrict), "campaign_primary_force");
        var force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0)
            ?? throw MainCommandMissing("campaign_force_pointer");
        int forceId = MainNeed(force.PhysicalSlot, "campaign_force_identity");
        //227FC0/334790 use real D0 receivers and filter the Force ordinal.
        if (forceId is < 1 or > 51) return false;
        int anchor = MainNeed((current.ReadDistrictTableReference(0)
            ?? throw MainCommandMissing("campaign_district_anchor")).PhysicalSlot, "campaign_district_anchor_identity");
        foreach (var reference in current.ReadArmyReturnRegisteredDistricts() ?? throw MainCommandMissing("campaign_district_registry"))
        {
            int physical = MainNeed(reference?.PhysicalSlot, "campaign_district_receiver");
            if (MainNeed(current.ReadDistrictForceId(physical), "campaign_district_force") != forceId) continue;
            int ordinal = physical - anchor;
            if (ordinal is < 1 or > 51) continue;
            bool person = false;
            //234850 mode0 has only serving-status membership;6D7660 adds
            //bit1 clear. Health, generic busy and troop counts belong later.
            foreach (int candidate in NormalTurnServingPeople(current, forceId, ordinal, false))
                if ((MainNeed(current.ReadArmyReturnPersonFlags196(candidate), "campaign_person_flags") & 1) == 0)
                { person = true; break; }
            if (!person) continue;
            foreach (var source in current.ReadRegisteredBases() ?? throw MainCommandMissing("campaign_base_registry"))
            {
                var at = source ?? throw MainCommandMissing("campaign_base_receiver");
                if (MainNeed(current.ReadBaseDistrictSlot(at), "campaign_base_district") != ordinal) continue;
                var neighbors = FinalizationTemporary(current);
                try
                {
                    FillMainCampaignNeighbors(current, at, neighbors);
                    var node = current.ReadHexListHead(neighbors) ?? throw MainCommandMissing("campaign_neighbor_head");
                    while (node.Identity is int cursor)
                    {
                        var receiver = current.ReadRegistryNodePayload(new(cursor))
                            ?? throw MainCommandMissing("campaign_neighbor_payload");
                        if (receiver.Kind is not (RegistryKind.City or RegistryKind.Gate))
                            throw MainCommandMissing("campaign_neighbor_kind");
                        var target = new BaseReference(receiver.Kind == RegistryKind.City ? BaseKind.City : BaseKind.Gate,
                            receiver.PhysicalSlot);
                        int targetForce = MainNeed(CurrentBaseAffiliationSource.ReadForce(state, current, target), "campaign_target_force");
                        if (MainNeed(ArmyLogisticsPreparation.ReadNativeRelationGate(state, forceId, targetForce), "campaign_target_relation"))
                            return true;
                        node = current.ReadHexListNodeNext(node) ?? throw MainCommandMissing("campaign_neighbor_next");
                    }
                }
                finally { FinalizationRelease(current, neighbors); }
            }
        }
        return false;
    }

    private void FillMainCampaignNeighbors(SourceWorldContext current, BaseReference source, HexListHandle output)
    {
        //22F720 reads the ten authored Base58 links. These are the same
        //original-map topology definitions used by the real campaign page.
        int physical = MainNeed(source.PhysicalSlot, "campaign_neighbor_source_identity");
        var links = source.Kind == BaseKind.City ? _scenario.Snapshot.Cities[physical].LinkedLocationSlots
            : _scenario.Snapshot.Gates[physical].LinkedLocationSlots;
        int self = MainNeed(current.ReadBaseObjectLink(source), "campaign_neighbor_source_link");
        current.ClearTemporaryHexList(output);
        foreach (byte raw in links.Take(10))
        {
            if (raw == 0) continue;
            var other = BaseLookup.Find(raw, current);
            if (!MainNeed(current.ReadBaseNativeValidity(other), "campaign_neighbor_validity") || raw == self) continue;
            FinalizationAppend(current, output, new(other.Kind == BaseKind.City ? RegistryKind.City : RegistryKind.Gate,
                other.PhysicalSlot));
        }
    }
}
