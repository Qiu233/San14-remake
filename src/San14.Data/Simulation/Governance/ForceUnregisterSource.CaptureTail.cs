using San14.Core;

namespace San14.Data;

public sealed partial class ForceUnregisterSource
{
    //293BD0 neutralizes the remaining Gates before its Army/Person cleanup.
    //2851B0 receives the real District0/Army0 placeholders and mode1; no
    //winning Army is created and the Gate's map receiver stays registered.
    private GameState RemoveRemainingGatesCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int victim, NativeMapSeasonDate date,
        List<CampaignEffectRequest> effects)
    {
        var descriptor = current.ReadBaseRegistryHandle()
            ?? throw Missing("Gate_manager80_descriptor");
        var node = current.ReadRegistryHead(descriptor) ?? throw Missing("Gate_manager80_head");
        while (node.Identity.HasValue)
        {
            var target = current.ReadBaseRegistryNodePayload(node) ?? throw Missing("Gate_manager80_payload");
            if (Need(current.ReadBaseForceSlot(target), "Gate_iterator_BaseB0") == victim)
            {
                int location = Need(current.ReadBaseObjectLink(target), "Gate_iterator_Base4E");
                if (location is >= 52 and <= 61)
                    state = NeutralizeRemainingGateCurrent(state, current, pool, target, victim, date, effects);
            }
            node = current.ReadRegistryNodeNext(node) ?? throw Missing("Gate_manager80_next");
        }
        return state;
    }

    private GameState NeutralizeRemainingGateCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, BaseReference gate, int victim, NativeMapSeasonDate date,
        List<CampaignEffectRequest> effects)
    {
        if (!Known(current.ReadBaseNativeValidity(gate), "neutral_Gate_validity")) return state;
        int location = Need(current.ReadBaseObjectLink(gate), "neutral_Gate4E");
        int oldDistrict = Need(current.ReadBaseDistrictSlot(gate), "neutral_old_District");
        var winner = current.ReadForceTableReference(0) ?? throw Missing("neutral_Force0");
        var district = current.ReadDistrictTableReference(0) ?? throw Missing("neutral_District0");
        var army = current.ReadArmyUnitTableReference(0) ?? throw Missing("neutral_Army0");
        int person0 = Need(current.ReadTransferPersonTableReference(0)?.PhysicalSlot, "neutral_Person0");
        if (Known(current.ReadForceNativeValidity(winner), "neutral_Force0_validity") ||
            Known(current.ReadDistrictNativeValidity(district), "neutral_District0_validity") ||
            Known(current.ReadArmyUnitNativeValidity(army), "neutral_Army0_validity") ||
            Known(current.ReadPersonNativeValidity(person0), "neutral_Person0_validity"))
            throw Missing("branch.2851B0_nonempty_neutral_placeholder");
        var initial = BaseCaptureRewardSource.ApplyInitialForceResult(state, current, winner, gate);
        state = initial.State ?? throw Missing(initial.UnresolvedOwner ?? "neutral_initial_257670");

        var objectDirectory = current.CurrentMapObjectDirectory ?? throw Missing("neutral_map_receiver_owner");
        var receiver = objectDirectory.ReceiverAtPhysical(location);
        int hex = current.ReadBaseCurrentHexIndex(gate);
        string? definition = state.MapObjects?.Find(receiver)?.Subject?.DefinitionKey;
        effects.Add(new(null, null) { MapObjectPresentation = new(MapObjectPresentationEvent.BaseOwnershipLostVisual,
            receiver, hex, definition) });
        effects.Add(new(null, null) { MapObjectPresentation = new(MapObjectPresentationEvent.BaseOwnershipLostSound,
            receiver, hex, definition) });

        //The first live HomeArmy traversal emits kind18 even for Armies
        //whose independent2C96E0 gate will reject the destruction traversal.
        VisitGateHomeArmies(current, gate, physical =>
        {
            state = package.QueueBaseCaptureArmySpeechCurrent(state, current,
                new(physical), 18, location, date);
        });
        if (current.ReadOwnedPersonCommandManager() is { } commands)
            state = state with { PersonnelCommands = commands };
        VisitGateHomeArmies(current, gate, physical =>
        {
            if (!current.IsArmyUnitMapWriteSkipped(new(physical))) return;
            var result = new OwnedArmyDestructionStage(package).ApplyAbsorptionCommanderCause4Current(
                state, current, pool, physical, date, effects);
            state = result.State ?? throw Missing(result.UnresolvedOwner ?? "neutral_HomeArmy_259360_cause4");
        });

        var lists = new List<HexListHandle>();
        BaseCaptureTerritoryReceipt? counts = null;
        HexListHandle? relatedAreas = null;
        var previousTransferModeReader = current.TransferModeStateReader;
        try
        {
            var captured = NewPeopleList();
            foreach (int person in GateRegisteredPeople(current))
                if (Need(current.ReadPersonStatus(person), "neutral_captiveE6") == 6 &&
                    Need(current.ReadPersonSecondLocation(person), "neutral_captiveE4") == location)
                    current.AppendPersonReturnTemporary(captured, person);
            foreach (var item in GatePeopleNodes(current, captured))
            {
                int person = item.Person;
                state = package.ApplyBaseCaptureExistingCaptiveCurrent(state, current, pool, gate,
                    person, date, effects);
            }
            current.ClearTemporaryHexList(captured);
            var noncaptured = NewPeopleList();
            _ = NewPeopleList(); //The real special-captured output is empty: Force0 rejects27E870.
            foreach (int person in GateRegisteredPeople(current))
                if (Need(current.ReadPersonFirstLocation(person), "neutral_Home_PersonE2") == location &&
                    Need(current.ReadPersonStatus(person), "neutral_Home_PersonE6") is >= 1 and <= 4)
                    current.AppendPersonReturnTemporary(noncaptured, person);
            var changedForce = NewPeopleList();
            foreach (var item in GatePeopleNodes(current, noncaptured))
                if (Need(current.ReadPersonOldForceSlot(item.Person), "neutral_Home_current_Force") != victim)
                    current.AppendPersonReturnTemporary(changedForce, item.Person);
            foreach (var item in GatePeopleNodes(current, changedForce))
                current.RemoveCapturePersonTemporary(noncaptured, item.Person);
            var ordered = GatePeopleNodes(current, noncaptured)
                .OrderBy(item => Need(current.ReadPersonStatus(item.Person), "neutral_sortE6"))
                .ThenBy(item => Need(current.ReadPersonIdentity(item.Person), "neutral_sort10")).ToArray();
            current.ReorderCurrentList(noncaptured, ordered.Select(item => item.Node).ToArray());
            bool lastCity = true;
            for (int city = 0; city < 52; city++)
                if (Need(current.ReadBaseForceSlot(current.ReadCityTableReference(city)
                    ?? throw Missing("neutral_City_table")), "neutral_City_B0") == victim)
                { lastCity = false; break; }
            if (ordered.Length != 0 && lastCity)
                state = package.ApplyLastCitySpecialPersonnelReturnCurrent(state, current, pool, gate, victim, date, effects);
            foreach (var item in GatePeopleNodes(current, noncaptured))
            {
                if (Need(current.ReadPersonStatus(item.Person), "neutral_current_PersonE6") is < 1 or > 4) continue;
                if (lastCity)
                    state = package.ApplyLastCityNoncapturedPersonCurrent(state, current, pool, item.Person,
                        winner, date, Sampler, effects);
                else
                {
                    int id = Need(current.ReadPersonIdentity(item.Person), "neutral_return_Person10");
                    var returned = package.CreateCaptureReturningPersonRules().TryApplyHomeCurrent(
                        state, current, location, victim, id, date);
                    state = returned.State ?? throw Missing(returned.UnresolvedOwner ?? "neutral_260310");
                }
            }

            var begin = BaseCaptureRewardSource.BeginTerritoryRewards(state, current, winner);
            counts = begin.Receipt ?? throw Missing(begin.UnresolvedOwner ?? "neutral_State_Region_lists");
            //26DA10 creates its Area descriptor before26D2E0, even for Gate.
            relatedAreas = Temporary(current);
            current.SetArmyReturnBaseGovernor2E(gate, 0);
            current.AssignAreaCity(CurrentGateMapQueries.FindArea(gate, current), 0);
            current.SetObjectForceSlot13(location, 0);
            current.CurrentSceneryLayout = new(null);
            current.TransferModeStateReader = () => current.ArmyReturnTransferMode;
            GateCaptureTerritorySource.ApplyFootprintBeforeResources(state, current, gate, 0);
            _ = BaseCaptureResourceSource.ApplyCurrent(state, current, gate, new(person0),
                package.CreateArmyDestructionAbilities(current));
            int areaId = Need(CurrentGateMapQueries.FindArea(gate, current).PhysicalSlot, "neutral_Area_pointer");
            ushort assigned = Need(current.ReadAssignmentArea36(areaId), "neutral_Area36");
            if (assigned != 0)
            {
                current.SetAssignmentArea36(areaId, 0);
                var removal = PersonAssignmentPrecallSource.Apply2E01E0AreaRemoval(current, areaId, assigned);
                if (removal.UnresolvedOwner is not null) throw Missing(removal.UnresolvedOwner);
            }
            //param6=1 omits both old-District2E9140 calls and26CDF0.
            //The surviving new-District call receives native-invalid0.
            var queries = package.CreateCaptureDistrictOperationQueries(current, () => state, next => state = next, date, effects);
            var primary = PersonAssignmentPrecallSource.Apply2E9140PrimaryForCapture(current, district, queries);
            if (primary.UnresolvedOwner is not null) throw Missing(primary.UnresolvedOwner);
            var refreshed = PersonAdvisorResetSource.RefreshWithinBaseCapture(state, current, winner);
            state = refreshed.State ?? throw Missing(refreshed.UnresolvedOwner ?? "neutral_27BE40");
            GateCaptureTerritorySource.ApplyTerritoryAfterResources(state, current, gate, 0, oldDistrict, 0);
            int cap = Need(ArmyDestructionParameterSeed.ReadCurrent(state,
                SimulationParameterKeys.Tactics.AbnormalCounterUpperBound), "neutral_threat_cap");
            byte zero = unchecked((byte)Math.Min(cap, 0));
            current.SetCaptureBaseThreatFields(gate, new(zero, zero, 255, 0));
            GateCaptureRoutingSource.ClearTargetRouting(current, gate);
            current.ClearTemporaryHexList(relatedAreas); current.ReleaseTemporaryHexList(relatedAreas); relatedAreas = null;
            current.TransferModeStateReader = previousTransferModeReader;
            state = PersonReturnDistrictReorganizationSource.PublishCurrent(state, current, queries);
            state = PersonReturnDistrictTransferSource.PublishAreaAssignments(state, current, [], queries.ChangedGates);
            var tail = BaseCaptureRewardSource.ApplyTailWithinCapture(state, current, gate, winner, district, army, counts);
            counts = null; //The tail releases both source descriptors, including on failure.
            state = tail.State ?? throw Missing(tail.UnresolvedOwner ?? "neutral_reward_tail");
        }
        finally
        {
            current.TransferModeStateReader = previousTransferModeReader;
            if (relatedAreas is not null)
            { current.ClearTemporaryHexList(relatedAreas); current.ReleaseTemporaryHexList(relatedAreas); }
            if (counts is not null) BaseCaptureRewardSource.ReleaseTerritoryRewards(current, counts);
            for (int index = lists.Count - 1; index >= 0; index--)
            { current.ClearTemporaryHexList(lists[index]); current.ReleaseTemporaryHexList(lists[index]); }
        }
        return BaseCapturePublicationSource.Publish(state, current, pool, projectOwnedRoles: true);

        HexListHandle NewPeopleList() { var list = Temporary(current); lists.Add(list); return list; }
        TacticSourceSamplerWorld Sampler() => package.ReadArmyDestructionSampler(state, current)
            ?? throw Missing("neutral_freedom_World450_45C");
    }

    private static void VisitGateHomeArmies(SourceWorldContext current, BaseReference gate, Action<int> visit)
    {
        int location = Need(current.ReadBaseObjectLink(gate), "neutral_Home_Base4E");
        var descriptor = current.ReadRegistryDescriptor(RegistryKind.Army) ?? throw Missing("neutral_Home_manager60");
        var node = current.ReadRegistryHead(descriptor) ?? throw Missing("neutral_Home_head");
        while (node.Identity.HasValue)
        {
            var entry = current.ReadRegistryNodePayload(node) ?? throw Missing("neutral_Home_payload");
            if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical }) throw Missing("neutral_Home_Army_pointer");
            if (!Known(current.ReadOwnedArmyExclusion233560(entry), "neutral_Home_233560"))
            {
                int person = GateCommander(current, new(physical));
                if (Known(current.ReadPersonNativeValidity(person), "neutral_Home_Commander_validity") &&
                    Need(current.ReadPersonFirstLocation(person), "neutral_Home_CommanderE2") == location) visit(physical);
            }
            node = current.ReadRegistryNodeNext(node) ?? throw Missing("neutral_Home_next");
        }
    }
    private static int GateCommander(SourceWorldContext current, ArmyUnitReference army)
    {
        int id = Need(current.ReadArmyUnitCommanderId(army), "neutral_Army12");
        return Need((id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))
            ?.PhysicalSlot, "neutral_Commander_pointer");
    }
    private static IEnumerable<int> GateRegisteredPeople(SourceWorldContext current)
    {
        foreach (int? person in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("neutral_manager100"))
            yield return Need(person, "neutral_Person_pointer");
    }
    private static List<(HexListNodeReference Node, int Person)> GatePeopleNodes(SourceWorldContext current, HexListHandle list)
    {
        var result = new List<(HexListNodeReference, int)>();
        var node = current.ReadHexListHead(list) ?? throw Missing("neutral_Person_list_head");
        while (node.Identity.HasValue)
        {
            int person = Need(current.ReadPersonRelationListPayload(node)?.PhysicalSlot, "neutral_Person_list_payload");
            result.Add((node, person));
            node = current.ReadHexListNodeNext(node) ?? throw Missing("neutral_Person_list_next");
        }
        return result;
    }
}
