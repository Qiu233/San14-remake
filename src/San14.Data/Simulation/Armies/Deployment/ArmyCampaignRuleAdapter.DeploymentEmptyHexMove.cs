using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>2AB640/7E3400/7E3890 and the ordinary empty-Hex branch of
    /// 7C70D0. Reads current source fields; never searches, assigns a target,
    /// changes the selection, or allocates an Army.</summary>
    public DeploymentEmptyHexMove QueryDeploymentEmptyHexMove(GameState state, int hexId,
        int targetForceId, DeploymentEmptyHexSelection selection)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(selection.SelectedOfficerIds);
        bool? empty = null, pointer = null, move = null;
        string? classMissing = null, pointerMissing = null, menuMissing = null;
        SourceWorldContext current;
        try
        {
            if (hexId is < 0 or >= 48400) throw EmptyMoveMissing("hex_identity");
            current = CaptureOfficerCommandCapacityCurrent(state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
        }
        catch (InvalidDataException error)
        {
            return new(hexId, null, null, selection.SelectedOfficerIds.Count == 0 ? false : null,
                error.Message, error.Message, selection.SelectedOfficerIds.Count == 0 ? null : error.Message);
        }
        bool armiesPrepared = false;
        try
        {
            var hex = Hex();
            if (hex.PhysicalSlot is null) empty = false;
            else
            {
                var obj = Object(hex);
                int physical = obj.PhysicalSlot ?? throw EmptyMoveMissing("object_receiver");
                if (physical != 0) empty = false; //Identity, not native-validity.
                else
                {
                    current.BindArmyMapOccupancy(state);
                    int occupant = unchecked((short)EmptyMoveNeed(current.ReadHexRaw1C(hex), "hex_signed_occupant"));
                    empty = occupant is < 1 or > 500; //No Army validity gate here.
                }
            }
        }
        catch (InvalidDataException error) { classMissing = error.Message; }
        if (empty == false) { pointer = false; move = false; }
        else
        {
            try { pointer = PointerGate(); }
            catch (InvalidDataException error) { pointerMissing = error.Message; }
            if (pointer == false) move = false;
            else if (pointer is null) menuMissing = pointerMissing;
            else
            {
                try
                {
                    move = selection.SelectedOfficerIds.Count != 0;
                    if (move == true) move = MenuTutorialGate();
                    if (move == true && selection.SelectedOfficerIds.Count == 1 &&
                        CurrentPosition(selection.SelectedOfficerIds[0]) == hexId && selection.HasActiveRouteGroup)
                    {
                        int index = selection.FirstActiveWaypointIndex ?? throw EmptyMoveMissing("active_group_waypoint_index");
                        if (index == 0) move = false;
                    }
                }
                catch (InvalidDataException error) { move = null; menuMissing = error.Message; }
            }
        }
        // Absence of any selected normal packet proves no Move independently
        // of fields that would be needed to open a menu for a selected row.
        if (selection.SelectedOfficerIds.Count == 0) { move = false; menuMissing = null; }
        return new(hexId, empty, pointer, move, classMissing, pointerMissing, menuMissing);

        HexReference Hex() => current.ReadHexTableReference(hexId) ?? throw EmptyMoveMissing("hex_pointer");
        ObjectReference Object(HexReference hex)
        {
            int link = EmptyMoveNeed(current.ReadHexObjectLink(hex), "hex_object_link");
            return current.ReadObjectTableReference(link <= 3000 ? link : 0) ?? throw EmptyMoveMissing("object_pointer");
        }
        bool PointerGate()
        {
            var hex = Hex();
            byte code = EmptyMoveNeed(current.ReadHexSurfaceCode(hex), "surface_code");
            var terrain = current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0) ?? throw EmptyMoveMissing("surface_pointer");
            if (terrain.PhysicalSlot is not int physical) return false;
            if (EmptyMoveNeed(current.ReadSurfaceTypeRawField2A(terrain), "surface_raw_cost") == 0) return false;
            if (physical == 24 && !NavalPointerGate()) return false;
            if (empty is null) throw new InvalidDataException(classMissing);
            // Empty classification already proves Object physical0, so the
            // 2C6AD0 Gate exception cannot be reached. param2/param5 are zero.
            var obj = Object(hex);
            var fields = current.ReadPhysicalObject(obj.PhysicalSlot ?? throw EmptyMoveMissing("pointer_object_receiver"))
                ?? throw EmptyMoveMissing("pointer_object_fields");
            var definition = current.ReadObjectDefinitionReference(fields.RawField12 <= 30 ? fields.RawField12 : 0)
                ?? throw EmptyMoveMissing("object_definition_pointer");
            return !EmptyMoveNeed(current.ReadObjectDefinitionValidity(definition), "object_definition_validity") ||
                EmptyMoveNeed(current.ReadObjectDefinitionRaw2C(definition), "object_definition_passage") == 4;
        }
        bool MenuTutorialGate()
        {
            // 7E4890 consults tutorial target qualification only after a
            // selected row and accepted press. It is not a 7E3400/7E3890 gate.
            return TutorialTargetQualification.AllowsHex(ReadTutorialControlState(state), current, hexId,
                selection.HasActiveRouteGroup ? selection.FirstActiveWaypointIndex : null);
        }
        int? Person(int id)
        {
            if (id < 0) throw EmptyMoveMissing("negative_person_identity");
            var reference = current.ReadPersonLookupReference(id <= 6000 ? id : 0) ?? throw EmptyMoveMissing("person_pointer");
            if (reference.PhysicalSlot is not int physical) return null;
            return EmptyMoveNeed(current.ReadPersonNativeValidity(physical), "person_validity") ? physical : null;
        }
        void PrepareArmies()
        {
            if (armiesPrepared) return;
            var arena = state.ReadArmyLifecycle() ?? throw EmptyMoveMissing("saved_army_receivers");
            _ = WorldIndexContext.ImportOwnedState(current, arena.Indexes, state);
            current.RestoreOwnedArmyReturnSlots(state, arena); armiesPrepared = true;
        }
        ArmyUnitReference PersonArmy(int person)
        {
            var location = PersonPositionRepair.DecodeSecondLocation(EmptyMoveNeed(current.ReadPersonSecondLocation(person), "person_second_location"));
            int slot = location.Kind == 27 ? location.Index : 0;
            if (slot is < 0 or > 500) slot = 0;
            if (slot != 0) PrepareArmies();
            var army = current.ReadArmyUnitTableReference(slot) ?? throw EmptyMoveMissing("person_army_pointer");
            if (slot == 0) return new(0);
            int identity = EmptyMoveNeed(current.ReadPersonIdentity(person), "person_identity");
            return EmptyMoveNeed(current.ReadArmyUnitCommanderId(army), "person_army_commander") == identity ? army : new(0);
        }
        bool NavalPointerGate()
        {
            bool hasValidPerson = false;
            foreach (int id in selection.SelectedOfficerIds)
            {
                if (Person(id) is not int person) continue;
                hasValidPerson = true;
                var army = PersonArmy(person);
                int naval;
                if (EmptyMoveNeed(current.ReadArmyUnitNativeValidity(army), "person_army_validity"))
                {
                    int physical = army.PhysicalSlot ?? throw EmptyMoveMissing("army_receiver");
                    var slot = state.ReadArmyLifecycle()!.ReceiverSlots()[physical];
                    var unit = slot.ActiveUnitId is int unitId ? state.Units.GetValueOrDefault(unitId) : slot.PendingImage;
                    naval = EmptyMoveNeed(unit is null ? null : ArmyConfigurationSourceAdapter.ReadNaval(state, unit), "current_army_naval");
                }
                else naval = DeploymentNavalFormationSource.Resolve(state, current,
                    EmptyMoveNeed(current.ReadPersonIdentity(person), "person_identity"), person);
                if (naval is not (12 or 13 or 17 or 18)) return false;
            }
            if (hasValidPerson) return true;
            if (targetForceId is < 0 or > 51) throw EmptyMoveMissing("target_force_identity");
            int force = targetForceId;
            var values = (state.NativeArmySimulation?.Parameters ?? throw EmptyMoveMissing("parameters")).Int32Values;
            if (!values.TryGetValue(BattleRemakeCurrentForcePolicies.ForceFieldKey(force, 0x15E), out int fallback) || fallback is < 0 or > 255)
                throw EmptyMoveMissing("target_force_naval");
            return fallback is 12 or 13 or 17 or 18;
        }
        int? CurrentPosition(int id)
        {
            if (Person(id) is not int person) return null;
            var location = PersonPositionRepair.DecodeSecondLocation(EmptyMoveNeed(current.ReadPersonSecondLocation(person), "person_second_location"));
            if (location.Kind == 27)
            {
                PrepareArmies();
                var army = current.ReadArmyUnitTableReference(location.Index) ?? throw EmptyMoveMissing("position_army_pointer");
                if (!EmptyMoveNeed(current.ReadArmyUnitNativeValidity(army), "position_army_validity")) return null;
                int raw = EmptyMoveNeed(current.ReadArmyUnitRawHex2A(army), "position_army_hex");
                return (current.ReadHexTableReference(raw <= 48399 ? raw : 0) ?? throw EmptyMoveMissing("position_hex_pointer")).PhysicalSlot;
            }
            BaseReference? source = location.Kind switch
            {
                5 => current.ReadCityTableReference(location.Index),
                6 => current.ReadGateTableReference(location.Index),
                _ => new(BaseKind.City, null)
            };
            if (source is null) throw EmptyMoveMissing("position_base_pointer");
            return EmptyMoveNeed(current.ReadBaseNativeValidity(source), "position_base_validity")
                ? CurrentGateMapQueries.FindHex(source, current).PhysicalSlot : null;
        }
    }
    private static T EmptyMoveNeed<T>(T? value, string field) where T : struct => value ?? throw EmptyMoveMissing(field);
    private static InvalidDataException EmptyMoveMissing(string field) => new("deployment.empty_hex_move." + field);
}


