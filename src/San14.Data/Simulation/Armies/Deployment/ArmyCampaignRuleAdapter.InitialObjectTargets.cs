using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //7EB310 items5/6 select9/10 and22DFF0's actual Object identity. Resolve
    //the typed Structure through the current Hex/Object owner, without an Army.
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
        bool repair = action.ActionKey == "repair";
        //Original item5 permits Obstacles independently of affiliation. Both
        //items require the actual own Force for Facility/Trap receivers.
        if (!(repair && category == 3))
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
        result = new(repair ? (byte)9 : (byte)10, new(11, checked((ushort)physical)), goal);
        return null;
    }
}
