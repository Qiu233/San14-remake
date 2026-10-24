using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>2A1470, after2A11A0's speech/history and flag150 prefix.
    /// Retargets the existing expedition member; it does not leave the group,
    /// clear an override, or perform an extra route refresh.</summary>
    private GameState CompleteCampaignExpeditionArmyCommand(GameState state, SourceWorldContext current,
        UnitState image, FacilityOperation operation)
    {
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw ExpeditionCompletionMissing("Army_receiver");
        byte group = image.NativeMapUiByte13C ?? throw ExpeditionCompletionMissing("Army_expedition_reference");
        var owner = state.CampaignExpeditions ?? throw ExpeditionCompletionMissing("expedition_owner");
        //The fixed lookup clamps an out-of-range byte to the real reset row0.
        //A free record in a known controller has the actual reset Province0.
        int province = group is >= 1 and <= 50 && owner.Expeditions.TryGetValue(group, out var expedition)
            ? expedition.ProvinceId : 0;
        var actor = new ArmyUnitReference(physical);
        int selected = 0;
        int leastTroops = int.MaxValue;
        foreach (var city in Registered(RegistryKind.City, BaseKind.City))
        {
            if (CityProvince(city) != province || !Hostile(city)) continue;
            int troops = Need(current.ReadBaseResource(city, BaseResourceKind.Troops), "candidate_City_troops");
            if (troops >= leastTroops) continue;
            int slot = Need(city.PhysicalSlot, "candidate_City_identity");
            selected = slot < 52 ? slot : 0;
            leastTroops = troops;
        }
        foreach (var gate in Registered(RegistryKind.Gate, BaseKind.Gate))
        {
            if (AreaProvince(gate) != province || !Hostile(gate)) continue;
            int troops = Need(current.ReadBaseResource(gate, BaseResourceKind.Troops), "candidate_Gate_troops");
            if (troops >= leastTroops) continue;
            int slot = Need(gate.PhysicalSlot, "candidate_Gate_identity");
            selected = slot is >= 1 and <= 10 ? slot + 51 : 0;
            leastTroops = troops;
        }
        if (selected == 0)
            return ApplyAutomaticArmyWithdrawal(state, current, image, operation, reportReason: false);

        var oldTarget = current.ReadArmyLifecycleTargetFields(physical)
            ?? throw ExpeditionCompletionMissing("previous_target");
        ushort identity = Need(oldTarget.Identity3A, "previous_target_identity");
        int oldLocation;
        switch (Need(oldTarget.Kind38, "previous_target_kind"))
        {
            case 5: oldLocation = identity < 52 ? identity : 0; break;
            case 6: oldLocation = identity is >= 1 and <= 10 ? identity + 51 : 0; break;
            default: return state; //The original reaches neither Base writer.
        }
        if (oldLocation == 0) return state;
        var oldBase = BaseLookup.Find(oldLocation, current);
        if (oldBase.PhysicalSlot is null) return state;
        var neighbors = current.CreateTemporaryHexList() ?? throw ExpeditionCompletionMissing("neighbor_descriptor");
        try
        {
            (_captiveFreedomHomes ??= new(_scenario)).FillBases22F720(current, oldBase, neighbors);
            bool foundNeighbor = false;
            var at = current.ReadRegistryHead(new(neighbors.Identity)) ?? throw ExpeditionCompletionMissing("neighbor_head");
            while (at.Identity.HasValue)
            {
                var candidate = current.ReadBaseRegistryNodePayload(at) ?? throw ExpeditionCompletionMissing("neighbor_payload");
                if (AreaProvince(candidate) == province && Hostile(candidate))
                {
                    int troops = Need(current.ReadBaseResource(candidate, BaseResourceKind.Troops), "neighbor_troops");
                    //The first eligible neighbor replaces the global choice,
                    //even when it has more troops. Later ties keep its order.
                    if (!foundNeighbor || troops < leastTroops)
                    {
                        selected = Need(current.ReadBaseObjectLink(candidate), "neighbor_Base4E");
                        leastTroops = troops;
                    }
                    foundNeighbor = true;
                }
                at = current.ReadRegistryNodeNext(at) ?? throw ExpeditionCompletionMissing("neighbor_next");
            }

            var target = BaseLookup.Find(selected, current);
            ushort location = Need(current.ReadBaseObjectLink(target), "selected_Base4E");
            byte action, kind;
            ushort nextIdentity;
            if (location < 52) { action = 4; kind = 5; nextIdentity = location; }
            else if (location is >= 52 and <= 61) { action = 5; kind = 6; nextIdentity = (ushort)(location - 51); }
            else return state;
            //The original repeats native validity and control0 before each
            //scalar setter. No callback or intervening writer changes them.
            if (!Need(current.ReadArmyUnitNativeValidity(actor), "Army_validity")) return state;
            if (Need(ArmySavedCommandKnowledge.ReadControl(state, image), "Army_control") != 0) return state;
            var lifecycle = state.ReadArmyLifecycle() ?? throw ExpeditionCompletionMissing("Army_owner");
            var receiver = lifecycle.Receivers.Find(lifecycle.Slots[physical].ReceiverId)
                ?? throw ExpeditionCompletionMissing("Army_receiver_owner");
            var fields = new ArmyTargetSourceFields(kind, nextIdentity);
            var result = OwnedArmyLifecycleTargets.Write(state, receiver.ReceiverId, fields);
            current.SetArmyLifecycleTargetFields(physical, fields);
            return result with
            {
                Units = new(result.Units) { [image.Id] = image with
                    { FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(action) } },
                ResourceState = ArmyConfigurationSourceAdapter.WriteAction(result.ResourceState, image.Id, action)
            };
        }
        finally { current.ClearTemporaryHexList(neighbors); current.ReleaseTemporaryHexList(neighbors); }

        int CityProvince(BaseReference city) => Need(_growthQueries.ReadCityState(state,
            Need(city.PhysicalSlot, "City_province_receiver")), "City_province");
        int AreaProvince(BaseReference location)
        {
            var area = PersonFreedomHomeSource.FindBaseArea229FD0(current, location);
            byte rawCity = Need(current.ReadAreaValidityCode(area), "original_Area34");
            var city = current.ReadCityTableReference(rawCity < 52 ? rawCity : 0)
                ?? throw ExpeditionCompletionMissing("original_City_pointer");
            return CityProvince(city);
        }
        bool Hostile(BaseReference target)
        {
            int targetForce = Need(current.ReadBaseForceSlot(target), "candidate_Force");
            int sourceForce = current.ReadArmyUnitRawForceSlot(actor);
            return Need(ArmyLogisticsPreparation.ReadNativeRelationGate(state, sourceForce, targetForce), "candidate_relation");
        }
        IEnumerable<BaseReference> Registered(RegistryKind registry, BaseKind kind)
        {
            var handle = current.ReadRegistryDescriptor(registry) ?? throw ExpeditionCompletionMissing("Base_registry");
            var at = current.ReadRegistryHead(handle) ?? throw ExpeditionCompletionMissing("Base_registry_head");
            while (at.Identity.HasValue)
            {
                var entry = current.ReadRegistryNodePayload(at) ?? throw ExpeditionCompletionMissing("Base_registry_payload");
                if (entry.Kind != registry || entry.PhysicalSlot is null)
                    throw ExpeditionCompletionMissing("Base_registry_pointer");
                yield return new(kind, entry.PhysicalSlot);
                at = current.ReadRegistryNodeNext(at) ?? throw ExpeditionCompletionMissing("Base_registry_next");
            }
        }
        static T Need<T>(T? value, string field) where T : struct => value ?? throw ExpeditionCompletionMissing(field);
    }

    private static InvalidDataException ExpeditionCompletionMissing(string field) =>
        new("campaign.expedition.completion." + field);
}
