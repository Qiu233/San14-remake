using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>2725E0 case16 after its common current-owner/automatic-policy
    /// prefix. Stage21 attempts Fire; this later switch decides whether the
    /// selected target still permits that command, then retains its real route.</summary>
    private GameState MaintainFireArmyCommand(GameState state, SourceWorldContext current, UnitState army,
        FacilityOperation operation, int force, ushort position)
    {
        ushort goal = OrdinaryArmyRouteSource.ReadCurrentTarget(state, army, current)
            ?? throw MaintenanceMissing("fire_current_goal2B5610");
        var hex = current.ReadHexTableReference(goal < 48400 ? goal : 0)
            ?? throw MaintenanceMissing("fire_target_Hex_pointer");
        if (hex.PhysicalSlot is null) return FinishInvalidArmyCommand(state, current, army, operation);
        var obj = FacilityConstructionLegality.ObjectAt(current, hex);
        bool objectValid = MaintenanceKnown(current.ReadObjectNativeValidity(obj), "fire_target_Object_validity");
        int distance = objectValid && obj.PhysicalSlot is >= 1 and <= 51 ? 2 : 1;
        ushort first = MaintenanceKnown(ArmyMarchSourceAdapter.EncodeDestination(army.March?.Target), "fire_first_word2C");
        if (FireCommandDistance(position, goal) == distance && first >= 48400)
        {
            int targetPhysical = MaintenanceKnown(hex.PhysicalSlot, "fire_target_Hex_receiver");
            if (MaintenanceKnown(current.ReadCaptureHexWord16(targetPhysical), "fire_target_word16") != 0)
                return FinishInvalidArmyCommand(state, current, army, operation);
            if (objectValid)
            {
                var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
                if (MaintenanceKnown(current.ReadObjectDefinitionValidity(definition), "fire_target_definition_validity") &&
                    MaintenanceKnown(current.ReadObjectDefinitionRaw2C(definition), "fire_target_definition_category") != 4 &&
                    !MaintenanceKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(state, force,
                        MaintenanceKnown(current.ReadObjectForceSlot13(obj), "fire_target_Object_force")), "fire_target_Object_relation"))
                    return FinishInvalidArmyCommand(state, current, army, operation);
            }
            int code = MaintenanceKnown(current.ReadHexSurfaceCode(hex), "fire_target_surface");
            var terrain = current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0)
                ?? throw MaintenanceMissing("fire_target_surface_pointer");
            if (terrain.PhysicalSlot is int terrainPhysical && _terrain.Catalog.Types[terrainPhysical].RawField41 == 0)
            {
                //2C6D20(goal) uses its integer operand through the Hex table;
                //the subsequent231660 reads the original target receiver.
                var tested = current.ReadHexTableReference(goal < 48400 ? goal : 0)
                    ?? throw MaintenanceMissing("fire_target_occupancy_test_Hex");
                int testMarker = unchecked((short)MaintenanceKnown(current.ReadHexRaw1C(tested), "fire_target_occupancy_test1C"));
                if (testMarker is < 1 or > 500)
                    return FinishInvalidArmyCommand(state, current, army, operation);
                int marker = unchecked((short)MaintenanceKnown(current.ReadHexRaw1C(hex), "fire_target_Army1C"));
                if (marker < 0) throw MaintenanceMissing("fire_target_negative_Army_pointer");
                var targetArmy = current.ReadArmyUnitTableReference(marker <= 500 ? marker : 0)
                    ?? throw MaintenanceMissing("fire_target_Army_pointer");
                if (MaintenanceKnown(current.ReadArmyUnitNativeValidity(targetArmy), "fire_target_Army_validity") &&
                    !MaintenanceKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(state, force,
                        current.ReadArmyUnitRawForceSlot(targetArmy)), "fire_target_Army_relation"))
                    return FinishInvalidArmyCommand(state, current, army, operation);
            }
        }
        return CompleteCommandRouteTail(state, current, army, operation, 16, force, position);
    }
}
