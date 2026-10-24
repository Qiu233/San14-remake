using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed record ArmyCommandTarget(byte Action, ArmyTargetSourceFields Fields, int GoalHex);

    /// <summary>Normal Make/Target packet action and receiver resolution.
    /// No Army has been allocated yet: compare the real commander's current
    /// Force, and preserve the selected Base/Army receiver independently from
    /// the destination Hex. This does not issue a post-creation replacement.</summary>
    private static CommandResult? OCReadDeploymentTarget(GameState state, SourceWorldContext current,
        int commanderPhysical, IReadOnlyList<DeploymentAction> actions, out ArmyCommandTarget? result)
    {
        result = null;
        if (actions is not [var action]) throw OCMissing("single_initial_action");
        if (action.ActionKey == "build")
            return OCReadInitialFacilityTarget(state, current, commanderPhysical, action, out result);
        if (action.StructureKindId is not null)
            return CommandResult.Reject("The selected ordinary target has an invalid structure argument.", "deployment.target");
        if (action.ActionKey == "capture")
            return OCReadInitialFalseReportTarget(state, current, commanderPhysical, action, out result);
        if (action.ActionKey == "occupy")
            return OCReadInitialOccupationTarget(current, action, out result);
        if (action.ActionKey == "fire")
        {
            if (action.Target is not { Kind: DeploymentTargetKind.Hex, Id: >= 0 and < 48400 })
                return CommandResult.Reject("The selected initial Fire Hex is invalid.", "deployment.target");
            if (action.ClickedHexId is not (>= 0 and < 48400))
                throw OCMissing("initial_fire_original_click");
            result = new(16, new(9, checked((ushort)action.Target.Id)), action.Target.Id);
            return null;
        }
        if (action.ActionKey is "move" or "stay")
        {
            if (action.Target is not { Kind: DeploymentTargetKind.Hex, Id: >= 0 and < 48400 })
                return CommandResult.Reject("The selected initial movement Hex is invalid.", "deployment.target");
            _ = current.ReadHexTableReference(action.Target.Id) ?? throw OCMissing("initial_Hex_pointer");
            result = new(action.ActionKey == "move" ? (byte)1 : (byte)13,
                new(9, checked((ushort)action.Target.Id)), action.Target.Id);
            return null;
        }
        if (action.ActionKey is not ("attack" or "enter" or "repair" or "demolish")) throw OCMissing("initial_action:" + action.ActionKey);
        var actorForce = current.ReadPersonForce(commanderPhysical) ?? throw OCMissing("initial_Commander_Force_pointer");
        return action.ActionKey is "repair" or "demolish"
            ? OCReadInitialObjectTarget(state, current, actorForce, action, out result)
            : ReadCurrentTargetCommand(state, current, actorForce, null, action, out result);
    }

    //Shared with a real existing Army's command setter. Permission compares
    //actual Force pointers and returns a physical receiver plus independent
    //Hex; new creation never needs an artificial Army to use these rules.
    private static CommandResult? ReadCurrentTargetCommand(GameState state, SourceWorldContext current,
        ForceReference actorForce, int? actorUnitId, DeploymentAction action, out ArmyCommandTarget? result)
    {
        result = null;
        if (action.StructureKindId is not null)
            return CommandResult.Reject("The selected attack target has an invalid structure argument.", "unit.destination");
        if (action.ActionKey == "enter")
        {
            if (action.Target is not { Kind: DeploymentTargetKind.Location, Id: >= 1 and <= 61 })
                return CommandResult.Reject("The selected friendly Base is invalid.", "unit.destination");
            int location = action.Target.Id;
            var target = BaseLookup.Find(location, current) ?? throw OCMissing("initial_entry_Base_pointer");
            if (!OCKnown(current.ReadBaseNativeValidity(target), "initial_entry_Base_validity"))
                return CommandResult.Reject("The selected Base is no longer available.", "unit.destination");
            int rawForce = OCKnown(current.ReadBaseForceSlot(target), "initial_entry_Base_Force");
            var owner = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0)
                ?? throw OCMissing("initial_entry_Base_Force_pointer");
            if (!OCKnown(current.ReadForceNativeValidity(owner), "initial_entry_Base_Force_validity") || owner != actorForce)
                return CommandResult.Reject("The selected Base does not belong to this force.", "unit.destination");
            ushort link = OCKnown(current.ReadBaseObjectLink(target), "initial_entry_Base4E");
            var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0)
                ?? throw OCMissing("initial_entry_Base_Object");
            int physical = OCKnown(obj.PhysicalSlot, "initial_entry_Object_identity");
            int goal = OCKnown(current.ReadObjectHexIndex(obj), "initial_entry_Object10");
            if (location <= 51)
            {
                if (physical is < 1 or > 51 || state.Cities.GetValueOrDefault(location)?.HexId != goal)
                    throw OCMissing("initial_entry_exact_City_projection");
                result = new(2, new(5, checked((ushort)physical)), goal);
            }
            else
            {
                if (physical is < 52 or > 61 || state.Gates.GetValueOrDefault(location)?.HexId != goal)
                    throw OCMissing("initial_entry_exact_Gate_projection");
                result = new(3, new(6, checked((ushort)(physical - 51))), goal);
            }
            return null;
        }
        if (action.Target.Kind == DeploymentTargetKind.Unit)
        {
            if (action.Target.Id == actorUnitId || !state.Units.TryGetValue(action.Target.Id, out var targetArmy))
                return CommandResult.Reject("The selected target army is unavailable.", "unit.destination");
            int physical = targetArmy.NativeArmyOrigin?.NativeSlotIndex ?? throw OCMissing("initial_target_Army_receiver");
            var selected = new ArmyUnitReference(physical);
            if (!OCKnown(current.ReadArmyUnitNativeValidity(selected), "initial_target_Army_validity"))
                return CommandResult.Reject("The selected target army is no longer available.", "unit.destination");
            if (current.ReadArmyUnitForceReference(selected) == actorForce)
                return CommandResult.Reject("The selected target army belongs to this force.", "unit.destination");
            int goal = OCKnown(current.ReadArmyUnitRawHex2A(selected), "initial_target_Army2A");
            if (goal >= 48400 || ArmyMarchSourceAdapter.ReadCurrentHex(state, targetArmy) != goal)
                throw OCMissing("initial_exact_Army_projection");
            result = new(7, new(27, checked((ushort)physical)), goal);
            return null;
        }
        if (action.Target is not { Kind: DeploymentTargetKind.Location, Id: >= 1 and <= 61 })
            return CommandResult.Reject("The selected initial attack Base is invalid.", "unit.destination");
        {
            int location = action.Target.Id;
            //The selected domain location already identifies its actual physical
            //Base, matching the normal target menu and existing Army command path.
            var target = new BaseReference(location <= 51 ? BaseKind.City : BaseKind.Gate,
                location <= 51 ? location : location - 51);
            if (!OCKnown(current.ReadBaseNativeValidity(target), "initial_target_Base_validity"))
                return CommandResult.Reject("The selected Base is no longer available.", "unit.destination");
            int rawForce = OCKnown(current.ReadBaseForceSlot(target), "initial_target_Base_Force");
            var owner = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0)
                ?? throw OCMissing("initial_target_Base_Force_pointer");
            if (OCKnown(current.ReadForceNativeValidity(owner), "initial_target_Base_Force_validity") && owner == actorForce)
                return CommandResult.Reject("The selected Base belongs to this force.", "unit.destination");
            ushort link = OCKnown(current.ReadBaseObjectLink(target), "initial_target_Base4E");
            var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0)
                ?? throw OCMissing("initial_target_Base_Object");
            int destination = OCKnown(current.ReadObjectHexIndex(obj), "initial_target_Base_Object10");
            int? projection = location <= 51 ? state.Cities.GetValueOrDefault(location)?.HexId
                : state.Gates.GetValueOrDefault(location)?.HexId;
            if (destination >= 48400 || projection != destination) throw OCMissing("initial_exact_Base_projection");
            //7EB310 case3 writes4/5/7 and5/6/27 into kind0 packet. Gate receiver
            //ordinals remain1..10, rather than storing domain locations52..61.
            result = new(location <= 51 ? (byte)4 : (byte)5,
                new(location <= 51 ? (byte)5 : (byte)6, checked((ushort)target.PhysicalSlot!.Value)), destination);
            return null;
        }
    }
}
