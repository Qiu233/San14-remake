using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private static GameState AbsorptionWriteRelation(GameState state, SourceWorldContext current,
        int force, int other, bool alliance, byte value)
    {
        if (force == 0)
        {
            var placeholder = state.NativeForceUnregisterCurrent ?? throw ForceAbsorptionMissing("placeholder_relations");
            var rows = (alliance ? placeholder.PlaceholderRelationsEA : placeholder.PlaceholderRelations13).ToArray();
            rows[other] = value;
            if (alliance) current.SetForceRelationByteEA(force, other, value);
            else current.SetRulerReleaseRelation13(force, other, value);
            return state with { NativeForceUnregisterCurrent = alliance
                ? placeholder with { PlaceholderRelationsEA = rows }
                : placeholder with { PlaceholderRelations13 = rows } };
        }
        var native = state.NativeArmySimulation ?? throw ForceAbsorptionMissing("relation_native_owner");
        var owner = native.Forces.GetValueOrDefault(force) ?? throw ForceAbsorptionMissing("relation_Force_owner");
        var values = (alliance ? owner.RelationsEA : owner.NormalAftermath?.Relations13) is { Length: 52 } before
            ? before.ToArray() : throw ForceAbsorptionMissing("relation_array");
        values[other] = value;
        var after = alliance ? owner with { RelationsEA = values }
            : owner with { NormalAftermath = (owner.NormalAftermath ?? throw ForceAbsorptionMissing("relation13_owner")) with { Relations13 = values } };
        if (alliance) current.SetForceRelationByteEA(force, other, value);
        else current.SetRulerReleaseRelation13(force, other, value);
        return state with { NativeArmySimulation = native with { Forces = new(native.Forces) { [force] = after } } };
    }

    //185D00(layout,Hex,Force,City,0,1,0). Presentation flag0 really
    //suppresses map4/5 and soundAD/AE; center/HomeArmy side effects remain.
    private GameState TransferAbsorptionHexCurrent(GameState state, SourceWorldContext current,
        HexReference hex, int force, int cityId, bool enqueueEncirclement = true)
    {
        if (!GateCaptureTerritorySource.Eligible(state, current, hex, force, false)) return state;
        if (AbsorptionK(current.ReadHexForceSlot(hex), "occupation_previous_Force") == force) return state;
        EnsureOccupationSceneryDefinitionsCurrent(current);
        int surface = AbsorptionK(current.ReadHexSurfaceCode(hex), "occupation_surface");
        bool ordinaryWithCity = (force is >= 1 and <= 45 or 51) && cityId is >= 1 and <= 51;
        if (surface is >= 15 and <= 18)
        {
            if (ordinaryWithCity)
                state = PrepareRegionCenterTransfer(state, current, hex, force, cityId, enqueueEncirclement: enqueueEncirclement);
            else if (force is >= 46 and <= 51)
            {
                int areaId = AbsorptionK(current.ReadHexAreaSlot(hex), "occupation_Area");
                var area = current.ReadAreaTableReference(areaId <= 500 ? areaId : 0) ?? throw ForceAbsorptionMissing("occupation_Area_pointer");
                var changed = current.AssignAreaCity(area, AbsorptionK(current.ReadAreaValidityCode(area), "occupation_original_City"));
                if (changed.PersonPhysicalSlotsWritten.Count != 0)
                {
                    var people = new Dictionary<int, OfficerState>(state.Officers);
                    foreach (int person in changed.PersonPhysicalSlotsWritten)
                    {
                        int id = AbsorptionK(current.ReadPersonIdentity(person), "occupation_Person_identity");
                        var owner = people.GetValueOrDefault(id) ?? throw ForceAbsorptionMissing("occupation_Person_owner");
                        int home = AbsorptionK(current.ReadPersonFirstLocation(person), "occupation_Person_home");
                        people[id] = owner with { LocationId = home == 0 ? null : home };
                    }
                    state = state with { Officers = people };
                }
            }
        }
        int rawScenery = AbsorptionK(current.ReadHexScenerySlot(hex), "occupation_scenery");
        var scenery = current.ReadSceneryTableReference(rawScenery <= 100 ? rawScenery : 0)
            ?? throw ForceAbsorptionMissing("occupation_scenery_pointer");
        if (AbsorptionK(current.ReadSceneryNativeValidity(scenery), "occupation_scenery_validity") &&
            (AbsorptionK(current.ReadSceneryFlags47(scenery), "occupation_scenery_flags") & 1) != 0 && ordinaryWithCity)
        {
            var city = current.ReadCityTableReference(cityId) ?? throw ForceAbsorptionMissing("occupation_notify_City");
            if (AbsorptionK(current.ReadBaseNativeValidity(city), "occupation_notify_City_validity"))
                state = QueueAbsorptionScenerySpeech(state, current, city, hex,
                    AbsorptionK(scenery.PhysicalSlot, "occupation_scenery_identity"));
        }
        int requested = force == 51 ? 0 : force;
        bool write = true;
        if (force is >= 46 and <= 50)
        {
            int rawArea = AbsorptionK(current.ReadHexAreaSlot(hex), "occupation_special_Area");
            var area = current.ReadAreaTableReference(rawArea <= 500 ? rawArea : 0) ?? throw ForceAbsorptionMissing("occupation_special_Area_pointer");
            if (area.PhysicalSlot is null) requested = 0;
            else
            {
                int rawCity = AbsorptionK(current.ReadAreaCitySlot(area), "occupation_special_City");
                var city = current.ReadCityTableReference(rawCity <= 51 ? rawCity : 0) ?? throw ForceAbsorptionMissing("occupation_special_City_pointer");
                if (AbsorptionK(current.ReadBaseForceSlot(city), "occupation_special_City_Force") != force)
                {
                    var owner = current.ReadForceTableReference(force) ?? throw ForceAbsorptionMissing("occupation_special_Force_pointer");
                    if (AbsorptionK(current.ReadForceNativeValidity(owner), "occupation_special_Force_validity"))
                        write = AbsorptionK(current.ReadConditionForceDependence(new(force)), "occupation_special_partner") !=
                            AbsorptionK(current.ReadHexForceSlot(hex), "occupation_special_previous_Force");
                    requested = 0;
                }
            }
        }
        if (write) current.AssignHexForce(hex, requested);
        var mode = current.ArmyReturnTransferMode ?? throw ForceAbsorptionMissing("occupation_tutorial_owner");
        if (mode.Present && AbsorptionK(mode.Value, "occupation_tutorial_value") != 0)
            throw ForceAbsorptionMissing("occupation_tutorial_counter");
        //The outer publication projects all changed Hex/Area ownership once.
        return state;
    }

    private GameState QueueAbsorptionScenerySpeech(GameState state, SourceWorldContext current,
        BaseReference home, HexReference hex, int scenery)
    {
        int location = AbsorptionK(current.ReadBaseObjectLink(home), "scenery_home_location");
        foreach (var pointer in ArmyMovementManagerLifecycle.ReadCurrentPointerOrder(current))
        {
            var army = new ArmyUnitReference(pointer.PhysicalSlot);
            int rawCommander = AbsorptionK(current.ReadArmyUnitCommanderId(army), "scenery_Commander");
            int person = AbsorptionPerson(current, rawCommander);
            int actualHome = AbsorptionK(current.ReadPersonNativeValidity(person), "scenery_Person_validity")
                ? AbsorptionK(current.ReadPersonFirstLocation(person), "scenery_Person_home") : 0;
            if (actualHome != location || !AbsorptionK(current.ReadArmyUnitNativeValidity(army), "scenery_Army_validity") ||
                AbsorptionK(current.ReadArmyUnitRawHex2A(army), "scenery_Army_hex") != hex.PhysicalSlot) continue;
            var date = state.NativeMapSeason?.ThirdDate ?? throw ForceAbsorptionMissing("scenery_source_date");
            int position = PersonReturnCommands.ResolvePersonPosition(state, current, person, date).PhysicalSlot ?? 48400;
            var arena = state.ReadArmyLifecycle() ?? throw ForceAbsorptionMissing("scenery_lifecycle");
            int physical = AbsorptionK(army.PhysicalSlot, "scenery_Army_pointer");
            var receiver = arena.Receivers.Find(arena.Slots[physical].ReceiverId) ?? throw ForceAbsorptionMissing("scenery_Army_receiver");
            int id = receiver.ActiveUnitId ?? receiver.PendingImage?.Id ?? throw ForceAbsorptionMissing("scenery_Army_identity");
            state = QueueSceneryControlArmySpeechCurrent(state, current, id,
                AbsorptionK(current.ReadPersonIdentity(person), "scenery_speaker"), position, scenery);
            state = state with { PersonnelCommands = current.ReadOwnedPersonCommandManager() ?? throw ForceAbsorptionMissing("scenery_command_owner") };
        }
        return state;
    }
}
