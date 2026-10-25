using San14.Core;

namespace San14.Data;

public sealed partial class ForceUnregisterSource
{
    private sealed record DeathDistrictRemovalResult(GameState State, int[] Districts,
        int RemovedNodes, ArmyReturnGovernorPacket[] Governors);

    //293BD0 retains the already filled District list across2924B0. This
    //leaf borrows that list; only293490's own two Gate lists are temporary.
    private DeathDistrictRemovalResult RemoveDeathDistrictsCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int victim, NativeMapSeasonDate date,
        List<CampaignEffectRequest> effects, HexListHandle districtTemporary, int[] orderedDistricts)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw Missing("death_District_current_pool");
        _ = districtTemporary; //Its lifetime remains with the outer293BD0 caller.
        _ = victim; //The caller selected the retained ForceDistrict sequence.
        var working = state;
        var governors = new List<ArmyReturnGovernorPacket>();
        var changedBases = new HashSet<BaseReference>();
        var operation = package.CreateCaptureDistrictOperationQueries(current, () => working,
            value => working = value, date, effects);
        var descriptor = current.ReadRegistryDescriptor(RegistryKind.District) ?? throw Missing("death_District_D0");
        int before = Need(current.ReadTemporaryHexListCount(new(descriptor.Identity)), "death_District_D0_count");
        var target = current.ReadDistrictTableReference(0) ?? throw Missing("death_District0_pointer");
        foreach (int physical in orderedDistricts)
        {
            //The retained sequence may contain aliases or repeated pointers;
            //there is no new validity filter before the actual293490 call.
            var removed = PersonReturnDistrictReorganizationSource.MergeCurrent(current,
                new(physical), target, operation, NeutralizeCity);
            governors.AddRange(removed);
            foreach (var packet in removed) changedBases.Add(packet.Base);
        }
        int after = Need(current.ReadTemporaryHexListCount(new(descriptor.Identity)), "death_District_D0_count_after");
        working = PersonReturnDistrictReorganizationSource.PublishCurrent(working, current, operation);
        working = PersonAssignmentPrecallInputs.PublishCurrent(working, current);
        working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
        working = OwnedArmyLifecycleDetachedInputs.PublishCurrent(working, current);
        foreach (var at in changedBases)
        {
            int physical = Need(at.PhysicalSlot, "death_District_changed_Base");
            int force = Need(current.ReadBaseForceSlot(at), "death_District_Base_Force");
            int district = Need(current.ReadBaseDistrictSlot(at), "death_District_Base_District");
            int governor = Need(current.ReadArmyReturnBaseGovernor2E(at), "death_District_Base_governor");
            int rawObject = Need(current.ReadBaseObjectLink(at), "death_District_Base4E");
            var obj = current.ReadObjectTableReference(rawObject < 3001 ? rawObject : 0)
                ?? throw Missing("death_District_Base_Object");
            int wall = Need(current.ReadObjectRawField14(obj), "death_District_Object14");
            if (at.Kind == BaseKind.City)
            {
                var city = working.Cities.GetValueOrDefault(physical) ?? throw Missing("death_District_City_owner");
                working = working with { Cities = new(working.Cities) { [physical] = city with
                { ForceId = force == 0 ? null : force, Durability = wall,
                    DamageTier = current.ReadBasePostLoadFlag66(at) } } };
            }
            else
            {
                int id = physical + 51;
                var gate = working.Gates.GetValueOrDefault(id) ?? throw Missing("death_District_Gate_owner");
                working = working with { Gates = new(working.Gates) { [id] = gate with
                { ForceId = force == 0 ? null : force, DistrictId = district == 0 ? null : district,
                    GovernorId = governor == 0 ? null : governor, Durability = wall,
                    DamageTier = current.ReadBasePostLoadFlag66(at) } } };
            }
        }
        working = working.WithArmyLifecycle((working.ReadArmyLifecycle() ?? throw Missing("death_District_lifecycle"))
            with { Indexes = pool.ProjectOwnedRoles() });
        return new(working, orderedDistricts, unchecked(before - after), governors.ToArray());

        void NeutralizeCity(BaseReference city, List<ArmyReturnGovernorPacket> updates)
        {
            NeutralizeResources(city);
            var gateResources = Temporary(current);
            try
            {
                RetainGates(city, gateResources);
                foreach (var gate in GatePayloads(gateResources)) NeutralizeResources(gate);
                //2AA9B0 updates only the City's66, while this first Gate
                //list is still live. Gates retain their own old66 here.
                current.SetBasePostLoadFlag66(city, BasePostLoad.ReadFlag66(city, current));
            }
            finally { ReleaseGates(gateResources); }
            UpdateGovernor(city, updates);
            var gateGovernors = Temporary(current);
            try
            {
                //This is a second fresh22D110 traversal/allocation, after
                //the City governor assignment and first list destruction.
                RetainGates(city, gateGovernors);
                foreach (var gate in GatePayloads(gateGovernors)) UpdateGovernor(gate, updates);
            }
            finally { ReleaseGates(gateGovernors); }
        }
        void NeutralizeResources(BaseReference at)
        {
            int troopCap = BasePostLoad.ReadResourceCapacities(at, current).Troops;
            current.SetBaseResource(at, BaseResourceKind.Troops, Math.Min(troopCap, 0));
            BasePostLoad.WriteMorale(at, 0, current);
            int wallCap = BasePostLoad.ReadCapacities(at, current).Field46;
            //The cached City and Gate call sites both use signed /4.
            BasePostLoad.WriteLinkedField14(at, wallCap / 4, current);
            int woundedCap = BasePostLoad.ReadResourceCapacities(at, current).Troops;
            current.SetArmyReturnBaseWounded40(at, Math.Min(woundedCap, 0));
            changedBases.Add(at);
        }
        void UpdateGovernor(BaseReference at, List<ArmyReturnGovernorPacket> updates)
        {
            if (!Known(current.ReadBaseNativeValidity(at), "death_District_Base_validity")) return;
            var prepared = ArmyReturnGovernorUpdater.PrepareCurrent(at, current, operation);
            var packet = prepared.Packet ?? throw Missing(prepared.UnresolvedOwner ?? "death_District_Governor");
            ArmyReturnGovernorUpdater.ApplyCurrentWrites(current, packet);
            updates.Add(packet); changedBases.Add(at);
        }
        void RetainGates(BaseReference city, HexListHandle list)
        {
            foreach (var entry in current.ReadArmyReturnRegisteredGates() ?? throw Missing("death_District_GateA0"))
            {
                var gate = entry ?? throw Missing("death_District_Gate_payload");
                if (CurrentGateMapQueries.FindCity(gate, current) != city) continue;
                var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("death_District_Gate_node");
                if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(RegistryKind.Gate, gate.PhysicalSlot));
            }
        }
        IEnumerable<BaseReference> GatePayloads(HexListHandle list)
        {
            var node = current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("death_District_Gate_head");
            while (node.Identity.HasValue)
            {
                var row = current.ReadRegistryNodePayload(node) ?? throw Missing("death_District_Gate_reference");
                if (row.Kind != RegistryKind.Gate) throw Missing("death_District_Gate_kind");
                yield return new(BaseKind.Gate, row.PhysicalSlot);
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("death_District_Gate_next");
            }
        }
        void ReleaseGates(HexListHandle list)
        { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
    }
}
