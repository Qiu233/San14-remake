using San14.Core;

namespace San14.Data;

internal static partial class ArmyRouteSearch
{
    //The same current occupant predicate serves route cost and stage28's
    //first-waypoint consumption. A real pool actor supplies its identity;
    //the original route-copy identity remains separately unknown.
    internal static bool OccupantBlocksCurrent(GameState state, SourceWorldContext current,
        ArmyUnitReference occupant, bool hasSearchArmy, int? registeredSearchArmyPhysical)
    {
        if (!hasSearchArmy || occupant.PhysicalSlot is not int physical) return true;
        var arena = state.ReadArmyLifecycle() ?? throw Missing("occupant_Army_receiver");
        if ((uint)physical >= (uint)arena.ReceiverSlots().Length) throw Missing("occupant_Army_receiver");
        var slot = arena.ReceiverSlots()[physical];
        var other = slot.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : slot.PendingImage;
        var runtime = other?.NativeTacticRuntime ?? throw Missing("occupant_Army24");
        if (runtime.Abnormal24 >= K(TransportArmyRouteInputs.ReadCurrent(state,
            SimulationParameterKeys.TransportRoute.BlockingAbnormalThreshold), "parameter_blocking_abnormal")) return true;
        byte action = K(ArmyConfigurationSourceAdapter.ReadAction(state, other), "occupant_Action37");
        if (action is 2 or 3 or 14) return false;
        if (action is 1 or 15)
        {
            ushort position = K(ArmyMarchSourceAdapter.ReadCurrentHex(state, other), "occupant_Army2A");
            int hex = Hex(position < Count ? position : 0).PhysicalSlot ?? throw Missing("occupant_current_Hex_pointer");
            ushort destination = K(OwnedArmyLifecycleTargets.Read(state, slot.ReceiverId)?.Identity3A, "occupant_Army3A");
            if (Distance(destination, hex) != 1) return false;
            var goal = Hex(destination < Count ? destination : 0);
            if (goal.PhysicalSlot is null) return false;
            bool hasObject = K(current.ReadHexObjectLink(goal), "occupant_goal_Hex1A") != 0;
            short marker = unchecked((short)K(current.ReadHexRaw1C(goal), "occupant_goal_Hex1C"));
            if (marker is >= 1 and <= 500 && !hasObject)
            {
                int searchPhysical = registeredSearchArmyPhysical ?? throw Missing("occupant_move_search_receiver_identity");
                return marker != searchPhysical;
            }
            return hasObject;
        }
        if (action is not (4 or 5 or 6 or 7 or 8 or 9 or 10 or 16 or 17)) return true;
        ushort target = OrdinaryArmyRouteSource.ReadCurrentTarget(state, other, current)
            ?? throw Missing("occupant_work_effective_target");
        int range = K(current.ReadHexSurfaceCode(Hex(target < Count ? target : 0)), "occupant_work_target_Hex10") == 16 ? 2 : 1;
        if (action is >= 4 and <= 7)
        {
            ushort rawHex = K(current.ReadArmyUnitRawHex2A(occupant), "occupant_range_Army2A");
            var at = Hex(rawHex < Count ? rawHex : 0);
            byte surface = K(current.ReadHexSurfaceCode(at), "occupant_range_Hex10");
            int terrain = K(current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0), "occupant_range_Terrain").PhysicalSlot
                ?? throw Missing("occupant_range_Terrain_pointer");
            byte flags = K(PersonArmyCaptureInputs.ReadTerrainFlags2C(state, terrain), "occupant_range_Terrain2C");
            byte selected = K((flags & 2) != 0 ? ArmyConfigurationSourceAdapter.ReadNaval(state, other)
                : ArmyConfigurationSourceAdapter.ReadCurrentLand(state, other), "occupant_range_formation");
            int formation = selected <= 20 ? selected : 0;
            byte bits = K(TacticRemakeCurrentFormations.ReadParameter(state, formation, 0xA6), "occupant_range_FormationA6");
            bool extended = (bits & 1) != 0;
            if (formation == 10)
            {
                int force = current.ReadArmyUnitForceReference(occupant).PhysicalSlot ?? throw Missing("occupant_range_Force_pointer");
                byte forceFlags = K(state.NativeArmySimulation?.Forces.GetValueOrDefault(force)?.Flags193, "occupant_range_Force193");
                if ((forceFlags & 0x80) != 0) extended = true;
            }
            if (extended) range++;
        }
        ushort workPosition = K(ArmyMarchSourceAdapter.ReadCurrentHex(state, other), "occupant_work_Army2A");
        int physicalHex = Hex(workPosition < Count ? workPosition : 0).PhysicalSlot ?? throw Missing("occupant_work_current_Hex_pointer");
        return Distance(physicalHex, target) <= range;

        HexReference Hex(int raw) => K(current.ReadHexTableReference(raw), "Hex_pointer");
    }
}
