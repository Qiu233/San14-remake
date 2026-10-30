using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //7EB310 item3 Object attack and items5/6 share22DFF0's actual physical
    //Object identity. Resolve the current Structure without allocating an Army.
    private static CommandResult? OCReadInitialObjectTarget(GameState state, SourceWorldContext current,
        ForceReference actorForce, DeploymentAction action, out ArmyCommandTarget? result)
    {
        result = null;
        if (action.Target.Kind != DeploymentTargetKind.Structure || state.Structures is not { } structures ||
            !structures.TryGetValue(action.Target.Id, out var structure))
            return CommandResult.Reject("The selected facility is unavailable.", "deployment.target");
        int hex = structure.HexId;
        if (hex is < 0 or >= 48400) throw OCMissing("initial_Object_Hex_range");
        var obj = FacilityConstructionLegality.ObjectAt(current, new(hex));
        if (!OCKnown(current.ReadObjectNativeValidity(obj), "initial_Object_validity"))
            return CommandResult.Reject("The selected facility is no longer present.", "deployment.target");
        int physical = OCKnown(obj.PhysicalSlot, "initial_Object_identity");
        int goal = OCKnown(current.ReadObjectHexIndex(obj), "initial_Object_Hex");
        var directory = current.CurrentMapObjectDirectory ?? throw OCMissing("initial_Object_directory");
        var owner = state.MapObjects?.Find(directory.ReceiverAtPhysical(physical));
        if (owner?.Subject?.StructureId != structure.Id || goal != hex)
            return CommandResult.Reject("The selected facility has changed.", "deployment.target");

        var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
        if (!OCKnown(current.ReadObjectDefinitionValidity(definition), "initial_Object_definition_validity"))
            return CommandResult.Reject("The selected object is not a facility.", "deployment.target");
        int category = OCKnown(current.ReadObjectDefinitionRaw2C(definition), "initial_Object_category");
        bool attack = action.ActionKey == "attack", repair = action.ActionKey == "repair";
        if (attack)
        {
            //7C70D0 includes item3 only for Other Army/Base/Facility/Obstacle;
            //a generic Object here must be category2/3, never a Trap4.
            if (category is not (2 or 3))
                return CommandResult.Reject("This object is not an ordinary attack target.", "deployment.target");
            int rawForce = OCKnown(current.ReadObjectForceSlot13(obj), "initial_attack_Object_Force");
            var targetForce = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0)
                ?? throw OCMissing("initial_attack_Object_Force_pointer");
            if (OCKnown(current.ReadForceNativeValidity(targetForce), "initial_attack_Object_Force_validity") &&
                (targetForce == actorForce ||
                    OCKnown(current.ReadForceNativeValidity(actorForce), "initial_attack_Commander_Force_validity") &&
                    OCKnown(ArmyMarchDevelopmentSource.ReadFriendlyRelation(state, actorForce, targetForce),
                        "initial_attack_Object_friendly_relation")))
                return CommandResult.Reject("The selected facility is not an Other-force target.", "deployment.target");
        }
        //Original item5 permits Obstacles independently of affiliation. Both
        //work items require the actual own Force for Facility/Trap receivers.
        else if (!(repair && category == 3))
        {
            if (category is not (2 or 4))
                return CommandResult.Reject("This object does not support the selected work order.", "deployment.target");
            int rawForce = OCKnown(current.ReadObjectForceSlot13(obj), "initial_Object_Force");
            var force = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0)
                ?? throw OCMissing("initial_Object_Force_pointer");
            if (!OCKnown(current.ReadForceNativeValidity(force), "initial_Object_Force_validity") || force != actorForce)
                return CommandResult.Reject("This facility does not belong to the selected force.", "deployment.target");
        }
        //The command packet itself has no full-durability or distance gate.
        //Keep source Included semantics, but expose the actual unfinished
        //stage16/28 consumers instead of accepting an unexecutable new order.
        if (repair && !FacilityConstructionDefinitions.IsSupported(OCKnown(definition.PhysicalSlot, "initial_repair_definition_identity")))
            throw OCMissing(category == 3 ? "initial_repair_continuous_obstacle_consumer"
                : "initial_repair_definition_consumer");
        //The packet stores physical Object identity, not the Hex1A lookup
        //alias or the domain Structure ID. Later lookups keep their own owner.
        result = new(attack ? (byte)6 : repair ? (byte)9 : (byte)10,
            new(11, checked((ushort)physical)), goal);
        return null;
    }
}
