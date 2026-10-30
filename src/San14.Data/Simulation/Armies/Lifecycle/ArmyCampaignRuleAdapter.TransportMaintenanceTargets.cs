using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The type2 prefix tests the actual main receiver before actor validity.
    //A changed destination clears Next and suppresses only this call's route.
    private static bool TransportMaintenanceTargetChanged(GameState state, SourceWorldContext current, UnitState image)
    {
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw MaintenanceMissing("transport_target_Army");
        var target = OwnedArmyLifecycleTargets.Read(state, state.ReadArmyLifecycle()!.ReceiverSlots()[physical].ReceiverId)
            ?? throw MaintenanceMissing("transport_target_main");
        byte kind = MaintenanceKnown(target.Kind38, "transport_target_kind");
        ushort identity = MaintenanceKnown(target.Identity3A, "transport_target_identity");
        if (kind is not (2 or 5 or 6 or 9)) return false;
        int actorForce = current.ReadArmyUnitRawForceSlot(new(physical));
        if (kind is 5 or 6)
        {
            var selected = kind == 5 ? current.ReadCityTableReference(identity <= 51 ? identity : 0)
                : current.ReadGateTableReference(identity <= 10 ? identity : 0);
            return MaintenanceKnown(current.ReadBaseForceSlot(selected ?? throw MaintenanceMissing("transport_target_Base")),
                "transport_target_Base_Force") != actorForce;
        }
        if (kind == 2)
        {
            //229EE0 resolves a generic Foothold through the live Base4E
            //lookup. Runtime kind2 is not the AreaData class (kind10).
            var selected = BaseLookup.Find(identity, current);
            return MaintenanceKnown(current.ReadBaseForceSlot(selected), "transport_target_Foothold_Force") != actorForce;
        }
        if (kind == 9)
        {
            var selected = current.ReadHexTableReference(identity < 48400 ? identity : 0)
                ?? throw MaintenanceMissing("transport_target_Hex");
            int targetForce = MaintenanceKnown(current.ReadHexForceSlot(selected), "transport_target_Hex_Force14");
            return MaintenanceKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(state, actorForce, targetForce),
                "transport_target_Hex_relation");
        }
        //All recognized type2 destinations above have returned.
        return false;
    }
}
