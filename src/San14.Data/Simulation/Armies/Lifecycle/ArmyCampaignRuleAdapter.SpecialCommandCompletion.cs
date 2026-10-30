using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //One current2C96E0 consumer shared by movement and nearby selection.
    internal bool? ReadArmyAtHomeCurrent(GameState state, SourceWorldContext current, int physical) =>
        new ArmyMapGroupingCurrentQueries(state, current, _scenario, _governorInputs,
            Seed.Language, SimulationRuleIdentity.Version).IsAtHome2C96E0(physical);

    //2A1980 is the common policy, after the caller's own report/flag prefix.
    private GameState CompleteArmyCommandPolicyCurrent(GameState state, SourceWorldContext current,
        UnitState image, FacilityOperation operation)
    {
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw MaintenanceMissing("policy_Army_receiver");
        var army = new ArmyUnitReference(physical);
        int force = MaintenanceKnown(current.ReadForcePhysicalSlot(current.ReadArmyUnitForceReference(army)), "policy_Force");
        if (force is >= 46 and <= 51 && !current.IsArmyUnitBoundToSpecialForce(army))
            //This branch ends even when no current target can be selected.
            return RetargetSpecialArmyAfterCompletionCurrent(state, current, image);

        bool transport = image.NativeTacticRuntime?.Status10 == 2;
        byte choice = MaintenanceKnown(transport ? TransportRouteSourceAdapter.ReadChoice(state, image)
            : image.NativeCommandChoice41, "policy_Choice41");
        if (choice == 1)
            return ApplyCampaignFieldWithdrawalCurrent(state, current, image, operation);
        if (choice != 2) return state;
        if (!MaintenanceKnown(current.ReadArmyUnitNativeValidity(army), "policy_hold_Army_validity"))
        {
            //304A20's invalid receiver still reads2A and clears48. It does
            //not replace its command, points, saved target or active lifetime.
            _ = MaintenanceKnown(current.ReadArmyUnitRawHex2A(army), "policy_invalid_hold_Hex2A");
            var held = image with { March = (image.March ?? throw MaintenanceMissing("policy_invalid_hold_March")) with
                { NextStep = ArmyMarchDestination.None } };
            return WriteCompletionArmyImage(state, held);
        }
        return transport ? HoldTransportPositionAfterCommand(state, current, image, operation)
            : HoldArmyPositionAfterCommand(state, current, image, operation);
    }

    //2FDA30 replaces the actual current target/action, then refreshes the
    //same route. It does not clear points, override, credit or stored command.
    private GameState RetargetSpecialArmyAfterCompletionCurrent(GameState state,
        SourceWorldContext current, UnitState image)
    {
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw MaintenanceMissing("special_retarget_Army");
        var army = new ArmyUnitReference(physical);
        if (!MaintenanceKnown(current.ReadArmyUnitNativeValidity(army), "special_retarget_validity")) return state;
        int force = MaintenanceKnown(current.ReadForcePhysicalSlot(current.ReadArmyUnitForceReference(army)), "special_retarget_Force");
        if (force is < 46 or > 51 || current.IsArmyUnitBoundToSpecialForce(army)) return state;
        ushort raw = MaintenanceKnown(current.ReadArmyUnitRawHex2A(army), "special_retarget_Hex2A");
        var position = current.ReadHexTableReference(raw < 48400 ? raw : 0)
            ?? throw MaintenanceMissing("special_retarget_Hex_pointer");
        int from = MaintenanceKnown(position.PhysicalSlot, "special_retarget_Hex_physical");
        int? selected = ReadIncitementTargetHexCurrent(current, from);
        if (selected is not int targetHex) return state;
        var target = current.ReadHexTableReference(targetHex) ?? throw MaintenanceMissing("special_retarget_target_Hex");
        ushort link = MaintenanceKnown(current.ReadHexObjectLink(target), "special_retarget_Hex1A");
        var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0)
            ?? throw MaintenanceMissing("special_retarget_Object_pointer");
        byte action, kind;
        ushort identity;
        DeploymentAction? projection;
        UnitMission mission;
        if (!MaintenanceKnown(current.ReadObjectNativeValidity(obj), "special_retarget_Object_validity"))
        {
            action = 14; kind = 9; identity = checked((ushort)targetHex);
            int area = MaintenanceKnown(current.ReadHexAreaSlot(target), "special_retarget_Area");
            projection = new("occupy", new(DeploymentTargetKind.Area, area));
            mission = new(UnitMissionKind.OccupyArea, TargetAreaId: area);
        }
        else
        {
            int objectPhysical = MaintenanceKnown(obj.PhysicalSlot, "special_retarget_Object_physical");
            if (objectPhysical is >= 1 and <= 51)
            {
                action = 4; kind = 5; identity = checked((ushort)objectPhysical);
                projection = new("attack", new(DeploymentTargetKind.Location, objectPhysical));
            }
            else if (objectPhysical is >= 52 and <= 61)
            {
                action = 5; kind = 6; identity = checked((ushort)(objectPhysical - 51));
                projection = new("attack", new(DeploymentTargetKind.Location, objectPhysical));
            }
            else
            {
                //22DFF0 returns the physical ordinal, not the Hex1A alias.
                action = 6; kind = 11; identity = checked((ushort)objectPhysical);
                projection = null; //The legacy UI plan has no attack-Object action.
            }
            mission = new(UnitMissionKind.Combat);
        }
        var working = state;
        if (MaintenanceKnown(ArmySavedCommandKnowledge.ReadControl(working, image), "special_retarget_control140") == 0)
        {
            var receiver = state.ReadArmyLifecycle()?.Receivers.FindUnit(image.Id)
                ?? throw MaintenanceMissing("special_retarget_receiver_owner");
            var fields = new ArmyTargetSourceFields(kind, identity);
            working = OwnedArmyLifecycleTargets.Write(working, receiver.ReceiverId, fields);
            current.SetArmyLifecycleTargetFields(physical, fields);
            PlayableMilitaryPlan? plan = projection is null ? null : image.MilitaryPlan is { } old
                ? old with { Action = projection, Completed = false, RestingHexId = null, IncidentalEnemyId = null }
                : new([], 0, projection);
            image = image with { FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(action),
                DestinationHexId = targetHex, Mission = mission, MilitaryPlan = plan };
            working = WriteCompletionArmyImage(working, image) with
                { ResourceState = ArmyConfigurationSourceAdapter.WriteAction(working.ResourceState, image.Id, action) };
        }
        image = image with { March = (image.March ?? throw MaintenanceMissing("special_retarget_March")) with
            { NextStep = ArmyMarchDestination.None } };
        working = WriteCompletionArmyImage(working, image);
        return FRoute(working, image.Id, true, current, false);
    }

    private static GameState WriteCompletionArmyImage(GameState state, UnitState image)
    {
        var arena = state.ReadArmyLifecycle() ?? throw MaintenanceMissing("completion_image_lifecycle");
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw MaintenanceMissing("completion_image_physical");
        var receiver = arena.Receivers.Find(arena.Slots[physical].ReceiverId)
            ?? throw MaintenanceMissing("completion_image_receiver");
        if (receiver.ActiveUnitId == image.Id && state.Units.ContainsKey(image.Id))
            return state with { Units = new(state.Units) { [image.Id] = image } };
        if (receiver.PendingImage?.Id != image.Id) throw MaintenanceMissing("completion_image_owner");
        return state.WithArmyLifecycle(arena with { Receivers = arena.Receivers.With(receiver with { PendingImage = image }) });
    }
}
