using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //32F100: territory and object/Army lifecycle precede the final240AA0.
    //The caller owns the transaction, current, list pool and final publication.
    internal GameState ApplyDiplomacyAreaTransferCurrent(GameState state, SourceWorldContext current,
        AreaReference area, ForceReference recipient, List<CampaignEffectRequest> effects,
        NativeMapSeasonDate? operationDate = null)
    {
        var pool = current.ReadOwnedCurrentListPool() ?? throw AreaTransferMissing("current_pool");
        int areaId = AK(area.PhysicalSlot, "Area_identity");
        int oldRaw = AK(current.ReadAreaCurrentForceSlot(area), "original_Area_Force");
        var former = current.ReadForceTableReference(oldRaw <= 51 ? oldRaw : 0)
            ?? throw AreaTransferMissing("original_Force_pointer");
        var working = state;
        var changedHexes = new HashSet<int>();
        for (int h = AreaHexIterator.FindNextIndex(0, areaId, current); h < 48400;
             h = AreaHexIterator.FindNextIndex(h + 1, areaId, current))
        {
            var hex = current.ReadHexTableReference(h) ?? throw AreaTransferMissing("Area_Hex_pointer");
            if (AK(current.ReadHexForceSlot(hex), "Hex14") !=
                AK(current.ReadForcePhysicalSlot(former), "original_Force_identity")) continue;
            var obj = AreaTransferObject(current, hex);
            bool obstacle = false;
            if (AK(current.ReadObjectNativeValidity(obj), "Object_validity"))
            {
                int definition = current.ReadPhysicalObject(AK(obj.PhysicalSlot, "Object_identity"))?.RawField12
                    ?? throw AreaTransferMissing("Object12");
                var data = current.ReadObjectDefinitionReference(definition <= 30 ? definition : 0)
                    ?? throw AreaTransferMissing("Object_definition");
                if (AK(current.ReadObjectDefinitionValidity(data), "Object_definition_validity"))
                {
                    int category = AK(current.ReadObjectDefinitionRaw2C(data), "Object_category");
                    obstacle = category == 3;
                    if (AK(current.ReadObjectForceSlot13(obj), "Object13") ==
                        AK(current.ReadForcePhysicalSlot(former), "removal_original_Force") && category is 2 or 4)
                        working = RemoveDiplomacyAreaObject(working, current, obj, effects);
                }
            }
            int occupant = unchecked((short)AK(current.ReadHexRaw1C(hex), "Hex1C"));
            if (occupant < 0) throw AreaTransferMissing("negative_Hex_Army_pointer");
            var army = current.ReadArmyUnitTableReference(occupant < 501 ? occupant : 0)
                ?? throw AreaTransferMissing("Hex_Army_pointer");
            if (AK(current.ReadArmyUnitNativeValidity(army), "Hex_Army_validity"))
            {
                int armyForce = AK(current.ReadForcePhysicalSlot(current.ReadArmyUnitForceReference(army)), "Army_Force");
                int recipientForce = AK(current.ReadForcePhysicalSlot(recipient), "return_recipient_Force");
                if (!new DiplomacyCommandSource(working, current).HasTreaty(armyForce, recipientForce))
                {
                    //The source returns this Army to its own actual Home; it
                    //does not transfer or destroy it on behalf of the recipient.
                    var home = current.FindArmyUnitCurrentBase(army);
                    var world = OwnedArmyReturnWorld.Borrow(working, current, pool);
                    var returned = PrepareOwnedArmyReturnCurrent(working, army, home, 1,
                        current, pool, CreateCaptureGovernorQueries(working), new(false, false),
                        AK(current.ReadArmyReturnUnitStatus10(army), "return_Army_status") == 2
                            ? NotificationArmy(working, AK(army.PhysicalSlot, "return_Army_identity")).NativeTacticRuntime?.Flags26 : null,
                        operationDate: operationDate);
                    var packet = returned.Packet ?? throw AreaTransferMissing(returned.UnresolvedOwner ?? "29F220");
                    working = world.CommitPrepared(working, packet) ?? throw AreaTransferMissing("29F220_publication");
                    working = OwnedArmyLifecycleDetachedInputs.PublishCurrent(working, current);
                }
            }
            int force = AK(current.ReadForcePhysicalSlot(recipient), "transfer_recipient_Force");
            if (GateCaptureTerritorySource.Eligible(working, current, hex, force, false) || obstacle)
            {
                //185D00(...,City0,1,1,override1): ordinary center/Scenery
                //placement branches requiring City1..51 are not entered.
                if (AK(current.ReadHexForceSlot(hex), "transfer_Hex14") != force)
                {
                    if (force is < 1 or > 45) throw AreaTransferMissing("special_recipient_185D00");
                    current.AssignHexForce(hex, force);
                    var mode = current.ArmyReturnTransferMode ?? throw AreaTransferMissing("transfer_mode");
                    if (mode.Present && AK(mode.Value, "transfer_mode_value") != 0)
                        throw AreaTransferMissing("transfer_mode_counter");
                    changedHexes.Add(h);
                }
            }
        }
        working = PublishDiplomacyAreaHexes(working, current, changedHexes);
        working = PublishDiplomacyAreaGates(working, current, area);
        working = RetireDiplomacyAreaObjects(working, current, pool, effects);
        working = ConsumeArmyLifecyclePendingCurrent(working, current, pool)
            ?? throw AreaTransferMissing("28E6D0");
        var order = current.ReadRegistryReferences(RegistryKind.Army) ?? throw AreaTransferMissing("274D70_Army60");
        if (order.Count != 0)
        {
            var date = operationDate ?? throw AreaTransferMissing("274D70_actual_cut_date");
            var operation = new FacilityOperation(() => date,
                working.NativeMapSeason?.AdvancedCalendarSteps ?? throw AreaTransferMissing("calendar_ordinal"),
                latest => (ReadArmyDestructionSampler(latest, current)
                    ?? throw AreaTransferMissing("campaign_day450")).Day450);
            foreach (var entry in order)
            {
                int physical = AK(entry?.PhysicalSlot, "274D70_Army_pointer");
                var image = NotificationArmy(working, physical);
                current.SetOwnedPersonCommandManager(working.PersonnelCommands);
                working = MaintainOrdinaryArmyCommand(working, image, operation, null, current);
                var latest = NotificationArmy(working, physical);
                if (!ReferenceEquals(image.OverrideTarget, latest.OverrideTarget))
                    current.SetArmyCommandOverride(physical, ArmyOverrideTargetSourceAdapter.Read(working, latest));
            }
        }
        int selected = SelectDiplomacyAreaCity(working, current, area, recipient);
        var assignment = current.AssignAreaCity(area, selected);
        var officers = new Dictionary<int, OfficerState>(working.Officers);
        foreach (int person in assignment.PersonPhysicalSlotsWritten)
        {
            int id = AK(current.ReadPersonIdentity(person), "240AA0_Person10");
            var before = officers.GetValueOrDefault(id) ?? throw AreaTransferMissing("240AA0_Person_owner");
            int home = AK(current.ReadPersonFirstLocation(person), "240AA0_PersonE2");
            officers[id] = before with { LocationId = home == 0 ? null : home };
        }
        working = working with { Officers = officers };
        working = PersonReturnDistrictTransferSource.PublishAreaAssignments(working, current, [areaId], []);
        var areas = new Dictionary<int, AreaState>(working.Areas);
        var previousArea = areas.GetValueOrDefault(areaId) ?? throw AreaTransferMissing("Area_owner");
        int finalForce = AK(current.ReadAreaCurrentForceSlot(area), "final_Area_Force");
        areas[areaId] = previousArea with { ForceId = finalForce == 0 ? null : finalForce };
        working = PublishDiplomacyAreaGates(working with { Areas = areas }, current, area);
        return working.WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = pool.ProjectOwnedRoles() });
    }

    private GameState RemoveDiplomacyAreaObject(GameState state, SourceWorldContext current,
        ObjectReference obj, List<CampaignEffectRequest> effects)
    {
        var removal = ApplyDemolitionNumbers(state, current, obj, new ArmyUnitReference(null));
        if (removal is null) return state;
        string definition = MapObjectSourceDirectory.DefinitionKey(removal.DefinitionSlot);
        effects.Add(new(null, null) { MapObjectPresentation = new(MapObjectPresentationEvent.DestructionVisual,
            removal.Receiver, removal.HexId, definition) });
        effects.Add(new(null, null) { MapObjectPresentation = new(MapObjectPresentationEvent.DestructionSound,
            removal.Receiver, removal.HexId, definition) });
        //Actual report=0: no visibility, actor reward, history or speech query.
        return removal.State;
    }

    private GameState RetireDiplomacyAreaObjects(GameState state, SourceWorldContext current,
        WorldIndexContext pool, List<CampaignEffectRequest> effects)
    {
        var pending = pool.PendingObjectRetirements ?? throw AreaTransferMissing("28E7F0_pendingF0");
        var working = state.WithArmyLifecycle(state.ReadArmyLifecycle()! with { Indexes = pool.ProjectOwnedRoles() });
        foreach (var entry in pending.ToArray())
        {
            if (entry is not { } id) continue;
            int physical = (current.CurrentMapObjectDirectory ?? throw AreaTransferMissing("Object_directory")).PhysicalOf(id);
            //The standalone retirement helper's Gate publication captures a
            //new context. A real pending Gate requires a borrowed Gate leaf.
            if (working.Gates.ContainsKey(physical)) throw AreaTransferMissing("28E7F0_pending_Gate_borrowed_publication");
            var removal = ApplyMapObjectRetirementNumbers(working, id);
            working = removal.State;
            current.SetObjectRecord(new(physical, 48400, 0));
            current.SetObjectForceSlot13(physical, 0); current.SetObjectRawField14(physical, 0);
            current.WriteMapObjectFields16(new(physical), 0);
            var descriptor = current.ReadRegistryDescriptor(RegistryKind.Object) ?? throw AreaTransferMissing("ObjectE0");
            var node = current.ReadRegistryHead(descriptor) ?? throw AreaTransferMissing("ObjectE0_head");
            while (node.Identity.HasValue)
            {
                var next = current.ReadRegistryNodeNext(node) ?? throw AreaTransferMissing("ObjectE0_next");
                if (current.ReadRegistryNodePayload(node) == new RegistryObjectReference(RegistryKind.Object, physical))
                    current.UnlinkArmyReturnTraitNode(descriptor, node);
                node = next;
            }
            effects.Add(new(null, null) { MapObjectPresentation = new(MapObjectPresentationEvent.RetirementVisualDetach,
                id, removal.PreviousHexId, removal.DefinitionKey) });
        }
        pool.PendingObjectRetirements = [];
        return working.WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = pool.ProjectOwnedRoles() });
    }

    private int SelectDiplomacyAreaCity(GameState state, SourceWorldContext current, AreaReference area,
        ForceReference recipient)
    {
        int original = AK(current.ReadAreaValidityCode(area), "original_City34");
        var city = current.ReadCityTableReference(original <= 51 ? original : 0) ?? throw AreaTransferMissing("original_City_pointer");
        int force = AK(current.ReadForcePhysicalSlot(recipient), "city_recipient_Force");
        int selected = 0;
        if (AK(current.ReadBaseNativeValidity(city), "original_City_validity") &&
            AK(current.ReadBaseForceSlot(city), "original_City_Force") == force)
            selected = AK(city.PhysicalSlot, "original_City_identity");
        else
        {
            var neighbors = AreaTransferTemporary(current);
            var candidates = AreaTransferTemporary(current);
            try
            {
                (_captiveFreedomHomes ??= new(_scenario)).FillCities22F430(current, city, neighbors);
                foreach (var candidate in AreaTransferBases(current, neighbors))
                    if (AK(current.ReadBaseForceSlot(candidate), "neighbor_City_Force") == force)
                        current.AppendCaptureBaseTemporary(candidates, candidate);
                var order = AreaTransferBases(current, candidates).ToList();
                if (order.Count != 0)
                {
                    int province = AK(_growthQueries.ReadCityState(state, AK(city.PhysicalSlot, "original_City_physical")), "original_CityCE");
                    for (int i = 1; i < order.Count; i++)
                    {
                        var item = order[i]; int at = i;
                        while (at > 0 && Before(item, order[at - 1])) { order[at] = order[at - 1]; at--; }
                        order[at] = item;
                    }
                    selected = AK(order[0].PhysicalSlot, "selected_City_identity");
                    bool Before(BaseReference a, BaseReference b)
                    {
                        int ap = AK(a.PhysicalSlot, "sort_City_a"), bp = AK(b.PhysicalSlot, "sort_City_b");
                        int ac = AK(_growthQueries.ReadCityState(state, ap), "sort_CityCE_a"), bc = AK(_growthQueries.ReadCityState(state, bp), "sort_CityCE_b");
                        if (ac == province && bc != province) return true;
                        if (ac != province && bc == province) return false;
                        return ac == bc && ap < bp;
                    }
                }
            }
            finally { AreaTransferRelease(current, candidates); AreaTransferRelease(current, neighbors); }
        }
        if (selected is >= 1 and <= 51) return selected;
        int physical = AK(recipient.PhysicalSlot, "fallback_Force_pointer");
        ushort ruler = current.ReadForceRecord(physical)?.RawRuler10 ?? throw AreaTransferMissing("fallback_ruler10");
        int person = AK((ruler <= 6000 ? current.ReadPersonLookupReference(ruler) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "fallback_ruler_pointer");
        var home = BaseLookup.Find(AK(current.ReadPersonFirstLocation(person), "fallback_rulerE2"), current);
        return AK(CurrentGateMapQueries.FindCity(home, current).PhysicalSlot, "fallback_ruler_City");
    }

    private static GameState PublishDiplomacyAreaHexes(GameState state, SourceWorldContext current, HashSet<int> changed)
    {
        if (changed.Count == 0) return state;
        var territory = new Dictionary<int, TerritoryState>(state.Territory);
        foreach (int h in changed)
        {
            var old = territory.GetValueOrDefault(h) ?? throw AreaTransferMissing("Territory_owner");
            int force = AK(current.ReadHexForceSlot(new(h)), "published_Hex14");
            territory[h] = old with { ForceId = force == 0 ? null : force };
        }
        return state with { Territory = territory };
    }
    private static GameState PublishDiplomacyAreaGates(GameState state, SourceWorldContext current, AreaReference area)
    {
        var gates = new HashSet<int>();
        foreach (var gate in state.Gates.Values)
        {
            var at = new BaseReference(BaseKind.Gate, gate.Id - 51);
            if (CurrentGateMapQueries.FindArea(at, current) == area) gates.Add(gate.Id - 51);
        }
        return PersonReturnDistrictTransferSource.PublishAreaAssignments(state, current, [], gates);
    }
    private static ObjectReference AreaTransferObject(SourceWorldContext current, HexReference hex)
    {
        int raw = AK(current.ReadHexObjectLink(hex), "Hex1A");
        return current.ReadObjectTableReference(raw <= 3000 ? raw : 0) ?? throw AreaTransferMissing("Object_pointer");
    }
    private static IEnumerable<BaseReference> AreaTransferBases(SourceWorldContext current, HexListHandle list)
    {
        var node = current.ReadRegistryHead(new(list.Identity)) ?? throw AreaTransferMissing("Base_list_head");
        while (node.Identity.HasValue)
        {
            yield return current.ReadBaseRegistryNodePayload(node) ?? throw AreaTransferMissing("Base_list_payload");
            node = current.ReadRegistryNodeNext(node) ?? throw AreaTransferMissing("Base_list_next");
        }
    }
    private static HexListHandle AreaTransferTemporary(SourceWorldContext current) => current.CreateTemporaryHexList()
        ?? throw AreaTransferMissing("temporary_allocation");
    private static void AreaTransferRelease(SourceWorldContext current, HexListHandle list)
    { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
    private static T AK<T>(T? value, string field) where T : struct => value ?? throw AreaTransferMissing(field);
    private static InvalidDataException AreaTransferMissing(string field) => new("diplomacy.Area-transfer." + field);
}
