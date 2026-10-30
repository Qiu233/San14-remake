using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Bind once to the host's retained map current, before selecting and publishing compact summaries.</summary>
    public void BindMapSummaryCurrent(GameState state, SourceWorldContext current)
    {
        //The retained map current also serves the normal Force doctrine and
        //Army neighbor terms; bind their actual saved readers once per World.
        _force164Inputs.BindCurrent(state, current);
        _personRelationInputs.BindCurrent(state, current);
        _baseCapacityInputs.BindCurrent(state, current);
        CaptureBaseThreatInputs.BindCurrent(state, current);
        BaseReturnCaptivesInputs.BindCurrent(state, current);
        current.EventConditions.ScenarioMode = state.CampaignEventWorld?.ScenarioMode;
    }

    /// <summary>925FD0's actual accepted Scene hover, or an explicit receiver from a real source hover caller.
    /// No whole-world capture, map tint computation, or selected-Hex substitution occurs here.</summary>
    public MapSummarySelection QueryMapSummaryReceivers(GameState state, SourceWorldContext current,
        HexReference? hoveredHex, int sourceZoomBand, MapSummaryViewOptions? options = null)
    {
        if (sourceZoomBand is < 0 or > 6) throw new ArgumentOutOfRangeException(nameof(sourceZoomBand));
        options ??= new();
        if (options.Mode is < 0 or > 8) throw new ArgumentOutOfRangeException(nameof(options));
        var targets = new int?[16];
        var missing = new Dictionary<int, string>();
        bool geographic = sourceZoomBand == 6 && options.GeographySelected;
        int armyKey = 0, groupKey = 0, areaKey = 0, objectKey = 0, forceKey = 0, sceneryKey = 0, stateKey = 0;
        int? baseKey = 0;
        int? rawForce = null;
        bool? hoverTargetValid = options.HoverTarget is null ? null : false;
        AreaReference? area = null;
        ObjectReference? obj = null;
        bool sceneryEligible = false;
        void Read(int[] slots, Action read)
        {
            try { read(); }
            catch (InvalidDataException error) { foreach (int slot in slots) missing[slot] = error.Message; }
        }
        void SelectArmy(ArmyUnitReference army)
        {
            if (!MapSummaryNeed(current.ReadArmyUnitNativeValidity(army), "Army_validity")) return;
            int physical = MapSummaryNeed(army.PhysicalSlot, "Army_receiver");
            if (MapSummaryNeed(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "Army_excluded")) return;
            targets[0] = targets[8] = physical;
            if (sourceZoomBand is >= 3 and <= 5)
            {
                int root = MapSummaryNeed(ReadMapSummaryGroupRootCurrent(state, current, physical, sourceZoomBand), "Army_group_root");
                if (root != 0 && MapSummaryNeed(current.ReadArmyUnitNativeValidity(new(root)), "group_validity"))
                {
                    var queries = MapArmyGroupingQueries(state, current);
                    int district = MapSummaryNeed(queries.ReadDistrict22BC70(root), "group_District");
                    bool another = false;
                    foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army) ?? throw MapSummaryMissing("Army_registry"))
                    {
                        int at = MapSummaryNeed(entry?.PhysicalSlot, "Army_registry_receiver");
                        if (at == root || MapSummaryNeed(queries.ReadDistrict22BC70(at), "member_District") != district ||
                            MapSummaryNeed(queries.IsExcluded233560(at), "member_excluded")) continue;
                        if (MapSummaryNeed(ReadMapSummaryGroupRootCurrent(state, current, at, sourceZoomBand), "member_group_root") == root)
                        { another = true; break; }
                    }
                    if (another) { targets[10] = root; targets[0] = null; }
                }
            }
        }
        Read([5], () =>
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw MapSummaryMissing("player_reference");
            targets[5] = MapSummaryNeed(player.PhysicalSlot, "player_receiver");
        });
        void SelectForce(int requested)
        {
            if (requested is < 0 or > 51) return;
            bool directHover = options.HoverTarget?.Kind == MapSummaryHoverKind.Force;
            if (directHover) hoverTargetValid = null;
            var force = current.ReadForceTableReference(requested) ?? throw MapSummaryMissing("hover_Force_pointer");
            bool valid = MapSummaryNeed(current.ReadForceNativeValidity(force), "hover_Force_validity");
            if (directHover) hoverTargetValid = valid;
            rawForce = 0;
            if (!valid) return;
            rawForce = MapSummaryNeed(force.PhysicalSlot, "hover_Force_receiver");
            if (rawForce != targets[5]) targets[6] = rawForce;
        }
        if (options.HoverTarget is { } hover)
        {
            switch (hover.Kind)
            {
                case MapSummaryHoverKind.Army:
                case MapSummaryHoverKind.Commander:
                    int? hoveredArmy = null;
                    Read([0, 8, 10], () =>
                    {
                        ArmyUnitReference army;
                        if (hover.Kind == MapSummaryHoverKind.Commander)
                        {
                            //88CE80 supplies the actual card Person to22A200(1).
                            //E4+Army12 identify its commander Army; neither the
                            //hover Hex nor a search through Units is involved.
                            if (!state.Officers.ContainsKey(hover.SourceId)) return;
                            hoverTargetValid = null;
                            int person = MapSummaryNeed(current.ResolveOfficerSource(hover.SourceId), "hover_Commander_Person_pointer");
                            hoverTargetValid = MapSummaryNeed(current.ReadPersonNativeValidity(person), "hover_Commander_Person_validity");
                            if (hoverTargetValid != true) return;
                            hoverTargetValid = null;
                            army = PersonArmyLocationSource.ReadCommanderArmy(current, person)
                                ?? throw MapSummaryMissing("hover_Commander_Army22A200");
                        }
                        else
                        {
                            if (!state.Units.TryGetValue(hover.SourceId, out var unit)) return;
                            hoverTargetValid = null;
                            int native = MapSummaryNeed(unit.NativeArmyOrigin?.NativeSlotIndex, "hover_Army_origin");
                            if (native is < 1 or > 500) { hoverTargetValid = false; return; }
                            army = current.ReadArmyUnitTableReference(native) ?? throw MapSummaryMissing("hover_Army_pointer");
                        }
                        hoverTargetValid = MapSummaryNeed(current.ReadArmyUnitNativeValidity(army), "hover_Army_validity");
                        if (hoverTargetValid != true) return;
                        hoveredArmy = MapSummaryNeed(army.PhysicalSlot, "hover_Army_receiver");
                        SelectArmy(army);
                    });
                    //925FD0 expands an Army's real occupied Object before
                    //the excluded/group gates; it never substitutes the map cursor Hex.
                    Read([4], () =>
                    {
                        if (hoveredArmy is not int physical) return;
                        int rawHex = MapSummaryNeed(current.ReadArmyUnitRawHex2A(new(physical)), "hover_Army_Hex2A");
                        var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0)
                            ?? throw MapSummaryMissing("hover_Army_Hex_pointer");
                        int link = MapSummaryNeed(current.ReadHexObjectLink(hex), "hover_Army_Object1A");
                        obj = current.ReadObjectTableReference(link < 3001 ? link : 0)
                            ?? throw MapSummaryMissing("hover_Army_Object_pointer");
                        if (!MapSummaryNeed(current.ReadObjectNativeValidity(obj), "hover_Army_Object_validity")) return;
                        int objectSlot = MapSummaryNeed(obj.PhysicalSlot, "hover_Army_Object_receiver");
                        if (objectSlot is >= 1 and <= 61) return;
                        int receiver = (current.CurrentMapObjectDirectory ?? throw MapSummaryMissing("Object_directory"))
                            .ReceiverAtPhysical(objectSlot).Value;
                        bool? presented = options.ObjectPresentationAvailable?.Invoke(receiver);
                        if (presented == true) targets[4] = receiver;
                        else if (presented is null) throw MapSummaryMissing("Object_presentation18");
                    });
                    break;
                case MapSummaryHoverKind.Area:
                    Read([1], () =>
                    {
                        if (hover.SourceId is < 0 or > 500) return;
                        hoverTargetValid = null;
                        var selectedArea = current.ReadAreaTableReference(hover.SourceId)
                            ?? throw MapSummaryMissing("hover_Area_pointer");
                        hoverTargetValid = MapSummaryNeed(current.ReadAreaNativeValidity(selectedArea), "hover_Area_validity");
                        if (hoverTargetValid == true)
                            targets[1] = MapSummaryNeed(selectedArea.PhysicalSlot, "hover_Area_receiver");
                    });
                    break;
                case MapSummaryHoverKind.Foothold:
                    BaseReference? hoveredBase = null;
                    Read([2, 3, 14], () =>
                    {
                        if (hover.SourceId is < 1 or > 61) return;
                        hoverTargetValid = null;
                        var basis = BaseLookup.Find(hover.SourceId, current);
                        if (MapSummaryNeed(current.ReadBaseObjectLink(basis), "hover_Base4E") != hover.SourceId)
                            throw MapSummaryMissing("hover_Base_registered_location");
                        hoverTargetValid = MapSummaryNeed(current.ReadBaseNativeValidity(basis), "hover_Base_validity");
                        if (hoverTargetValid != true) return;
                        hoveredBase = basis;
                        int physical = MapSummaryNeed(basis.PhysicalSlot, "hover_Base_receiver");
                        if (basis.Kind == BaseKind.City)
                        {
                            targets[2] = physical;
                            if (hover.SourceId is < 52 or > 61) targets[14] = physical;
                        }
                        else targets[3] = physical + 51;
                    });
                    Read([6], () =>
                    {
                        if (hoveredBase is null) return;
                        int force = MapSummaryNeed(current.ReadBaseForceSlot(hoveredBase), "hover_Base_Force");
                        SelectForce(force);
                    });
                    Read([12], () =>
                    {
                        if (hoveredBase is null) return;
                        var city = hoveredBase;
                        if (city.Kind == BaseKind.Gate)
                        {
                            var gateArea = CurrentGateMapQueries.FindArea(city, current);
                            int originalCity = MapSummaryNeed(current.ReadAreaValidityCode(gateArea), "hover_Gate_Area_originalCity34");
                            city = current.ReadCityTableReference(originalCity < 52 ? originalCity : 0)
                                ?? throw MapSummaryMissing("hover_Gate_originalCity_pointer");
                        }
                        int cityPhysical = MapSummaryNeed(city.PhysicalSlot, "hover_province_City_receiver");
                        int province = MapSummaryNeed(_growthQueries.ReadCityState(state, cityPhysical), "hover_City_provinceCE");
                        targets[12] = province < 23 ? province : 0;
                    });
                    break;
                case MapSummaryHoverKind.Force:
                    Read([6], () => SelectForce(hover.SourceId));
                    break;
                case MapSummaryHoverKind.District:
                    Read([7], () =>
                    {
                        if (hover.SourceId is < 0 or > 51) return;
                        hoverTargetValid = null;
                        var district = current.ReadDistrictTableReference(hover.SourceId)
                            ?? throw MapSummaryMissing("hover_District_pointer");
                        hoverTargetValid = MapSummaryNeed(current.ReadDistrictNativeValidity(district), "hover_District_validity");
                        if (hoverTargetValid == true)
                            targets[7] = MapSummaryNeed(district.PhysicalSlot, "hover_District_receiver");
                    });
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(options));
            }
        }
        else if (options.OverrideReceiver is { } supplied)
        {
            if (supplied.Slot is < 0 or > 15) throw new ArgumentOutOfRangeException(nameof(options));
            targets[supplied.Slot] = supplied.ReceiverId;
            if (supplied.Slot == 0) targets[8] = supplied.ReceiverId;
        }
        else if (hoveredHex?.PhysicalSlot is not null)
        {
            Read([0, 8, 10], () =>
            {
                int raw = unchecked((short)MapSummaryNeed(current.ReadHexRaw1C(hoveredHex), "Hex_Army1C"));
                if (raw is < 1 or > 500) return;
                var army = current.ReadArmyUnitTableReference(raw) ?? throw MapSummaryMissing("Army_pointer");
                SelectArmy(army);
            });
            Read([1, 2, 3, 4, 9, 12, 14, 15], () =>
            {
                int rawArea = MapSummaryNeed(current.ReadHexAreaSlot(hoveredHex), "Hex_Area12");
                area = current.ReadAreaTableReference(rawArea < 501 ? rawArea : 0) ?? throw MapSummaryMissing("Area_pointer");
                int link = MapSummaryNeed(current.ReadHexObjectLink(hoveredHex), "Hex_Object1A");
                obj = current.ReadObjectTableReference(link < 3001 ? link : 0) ?? throw MapSummaryMissing("Object_pointer");
                bool hasObject = MapSummaryNeed(current.ReadObjectNativeValidity(obj), "Object_validity");
                bool hasArmy = targets[0].HasValue || targets[10].HasValue;
                if (hasObject)
                {
                    int physical = MapSummaryNeed(obj.PhysicalSlot, "Object_receiver");
                    if (physical is >= 1 and <= 51)
                    {
                        if (!hasArmy)
                        {
                            var city = current.ReadCityTableReference(physical) ?? throw MapSummaryMissing("City_pointer");
                            if (MapSummaryNeed(current.ReadBaseNativeValidity(city), "City_validity"))
                            {
                                int at = MapSummaryNeed(city.PhysicalSlot, "City_receiver"); targets[2] = at;
                                int location = MapSummaryNeed(current.ReadBaseObjectLink(city), "City4E");
                                if (location is < 52 or > 61) targets[14] = at;
                            }
                        }
                    }
                    else if (physical is >= 52 and <= 61)
                    {
                        if (!hasArmy)
                        {
                            var gate = current.ReadGateTableReference(physical - 51) ?? throw MapSummaryMissing("Gate_pointer");
                            if (MapSummaryNeed(current.ReadBaseNativeValidity(gate), "Gate_validity"))
                                targets[3] = MapSummaryNeed(gate.PhysicalSlot, "Gate_receiver") + 51;
                        }
                    }
                    else
                    {
                        int receiver = (current.CurrentMapObjectDirectory ?? throw MapSummaryMissing("Object_directory"))
                            .ReceiverAtPhysical(physical).Value;
                        bool? presented = options.ObjectPresentationAvailable?.Invoke(receiver);
                        if (presented == true) targets[4] = receiver;
                        else if (presented is null) missing[4] = "map-summary.Object_presentation18";
                    }
                }
                else if (!hasArmy && MapSummaryNeed(current.ReadAreaNativeValidity(area), "Area_validity"))
                {
                    sceneryEligible = true;
                    var scenery = ResolveSceneryMapSummaryReceiver(current, hoveredHex);
                    if (!scenery.Available) throw MapSummaryMissing(scenery.UnresolvedInput ?? "Scenery_receiver");
                    if (scenery.Value is null && (sourceZoomBand > 3 ||
                        MapSummaryNeed(current.ReadHexSurfaceCode(hoveredHex), "Hex_surface10") is 15 or 18))
                        targets[1] = MapSummaryNeed(area.PhysicalSlot, "Area_receiver");
                }
                int Province(bool original)
                {
                    int city = original ? MapSummaryNeed(current.ReadAreaValidityCode(area), "Area_originalCity34")
                        : MapSummaryNeed(current.ReadAreaCitySlot(area), "Area_currentCity35");
                    var pointer = current.ReadCityTableReference(city < 52 ? city : 0) ?? throw MapSummaryMissing("province_City_pointer");
                    int physical = MapSummaryNeed(pointer.PhysicalSlot, "province_City_receiver");
                    return _growthQueries.ReadCityState(state, physical) is byte province ? province : throw MapSummaryMissing("City_provinceCE");
                }
                targets[9] = Province(false); targets[12] = Province(true);
            });
            Read([6], () =>
            {
                rawForce = MapSummaryNeed(current.ReadHexForceSlot(hoveredHex), "Hex_Force14");
                var force = current.ReadForceTableReference(rawForce < 52 ? rawForce.Value : 0) ?? throw MapSummaryMissing("Force_pointer");
                rawForce = 0;
                if (MapSummaryNeed(current.ReadForceNativeValidity(force), "Force_validity"))
                {
                    int physical = MapSummaryNeed(force.PhysicalSlot, "Force_receiver");
                    rawForce = physical;
                    if (physical != targets[5]) targets[6] = physical;
                }
            });
            Read([15], () =>
            {
                if (!sceneryEligible) return;
                var value = ResolveSceneryMapSummaryReceiver(current, hoveredHex);
                if (!value.Available) throw MapSummaryMissing(value.UnresolvedInput ?? "Scenery_receiver");
                targets[15] = value.Value;
            });
            if (targets[1].HasValue) missing[11] = "map-summary.Area_attacking_Force73";
        }
        //925FD0's final Force identity falls back only to the player's real
        //cursor Hex owner when the admitted override Force is absent.
        int? playerForce = targets[5];
        int admitted = MapSummaryAdmissionRules.TargetMask(options.Mode, sourceZoomBand, options.GeographySelected);
        for (int slot = 0; slot < targets.Length; slot++)
            if ((admitted & (1 << slot)) == 0) { targets[slot] = null; missing.Remove(slot); }
        armyKey = targets[0] ?? 0; groupKey = targets[10] ?? 0; areaKey = targets[1] ?? 0;
        if (targets[4].HasValue && obj?.PhysicalSlot is int op) objectKey = op;
        if (targets[2] is int cp) baseKey = current.ReadBaseObjectLink(new(BaseKind.City, cp));
        if (targets[3] is int gp) baseKey = current.ReadBaseObjectLink(new(BaseKind.Gate, gp - 51));
        forceKey = targets[6] ?? 0;
        bool forceKeyUnknown = missing.ContainsKey(6);
        if (forceKey == 0 && hoveredHex?.PhysicalSlot is not null)
        {
            try
            {
                int requested = MapSummaryNeed(current.ReadHexForceSlot(hoveredHex), "identity_Hex_Force14");
                var force = current.ReadForceTableReference(requested < 52 ? requested : 0)
                    ?? throw MapSummaryMissing("identity_Force_pointer");
                if (MapSummaryNeed(current.ReadForceNativeValidity(force), "identity_Force_validity") &&
                    force.PhysicalSlot == playerForce) forceKey = MapSummaryNeed(force.PhysicalSlot, "identity_Force_receiver");
            }
            catch (InvalidDataException) { forceKeyUnknown = true; }
        }
        sceneryKey = targets[15] ?? 0; stateKey = sourceZoomBand == 6 ? targets[12] ?? 0 : 0;
        if (geographic)
        {
            if (options.GeographicSelection is int selected) targets[12] = targets[13] = selected;
            else { missing[12] = missing[13] = "map-summary.Geographic_current_selection"; targets[12] = targets[13] = null; }
        }
        var receivers = targets.Select((id, slot) => (id,slot)).Where(pair => pair.id.HasValue)
            .Select(pair => new MapSummaryReceiver(pair.slot, pair.id!.Value) { SourceZoomBand = sourceZoomBand }).ToArray();
        return new(receivers, missing) { HoverTargetValid = hoverTargetValid, TargetIdentity = new(
            missing.ContainsKey(0) ? null : armyKey, missing.ContainsKey(10) ? null : groupKey,
            missing.ContainsKey(1) ? null : areaKey, missing.ContainsKey(4) ? null : objectKey,
            missing.ContainsKey(2) || missing.ContainsKey(3) ? null : baseKey,
            forceKeyUnknown ? null : forceKey,
            missing.ContainsKey(15) ? null : sceneryKey, missing.ContainsKey(12) ? null : stateKey) };
    }

    public MapSummaryPage QueryMapSummaryPage(GameState state, SourceWorldContext current, MapSummaryReceiver receiver)
        => receiver.Slot switch
        {
            0 => QueryArmyMapSummaryCurrent(state,current,receiver), 1 => QueryAreaMapSummaryCurrent(state,current,receiver),
            2 => QueryCityMapSummaryCurrent(state,current,receiver), 3 => QueryGateMapSummaryCurrent(state,current,receiver),
            4 => QueryFacilityMapSummaryCurrent(state,current,receiver), 5 or 6 => QueryForceMapSummaryCurrent(state,current,receiver),
            7 => QueryDistrictMapSummaryCurrent(state,current,receiver), 8 => QueryPersonalityMapSummaryCurrent(state,current,receiver),
            9 => QueryStateMapSummaryCurrent(state,current,receiver), 10 => QueryArmyGroupMapSummaryCurrent(state,current,receiver),
            11 or 14 => QueryMessageMapSummaryCurrent(state,current,receiver),
            12 or 13 => QueryGroundMapSummaryCurrent(state,current,receiver),
            15 => QueryUnifiedSceneryMapSummaryCurrent(state,current,receiver),
            _ => throw new ArgumentOutOfRangeException(nameof(receiver))
        };
    private static T MapSummaryNeed<T>(T? value,string field) where T:struct => value ?? throw MapSummaryMissing(field);
    private static InvalidDataException MapSummaryMissing(string field) => new("map-summary." + field);
}
