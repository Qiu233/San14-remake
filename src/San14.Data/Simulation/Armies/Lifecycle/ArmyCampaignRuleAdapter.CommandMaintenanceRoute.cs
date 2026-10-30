using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState CompleteCommandRouteTail(GameState working, SourceWorldContext current, UnitState image,
        FacilityOperation operation, byte action, int force, ushort at)
    {
        ushort next = MaintenanceKnown(ArmyMarchSourceAdapter.EncodeDestination(image.March?.NextStep), "next_word48");
        if (next != ArmyMarchSourceAdapter.SourceHexCount)
        {
            ushort first = MaintenanceKnown(ArmyMarchSourceAdapter.EncodeDestination(image.March?.Target), "first_waypoint2C");
            if (first == next)
            {
                var hex = current.ReadHexTableReference(next < 48400 ? next : 0)
                    ?? throw MaintenanceMissing("next_waypoint_Hex");
                ushort objectLink = MaintenanceKnown(current.ReadHexObjectLink(hex), "next_waypoint_Object_link");
                var obj = current.ReadObjectTableReference(objectLink <= 3000 ? objectLink : 0)
                    ?? throw MaintenanceMissing("next_waypoint_Object");
                current.BindArmyMapOccupancy(working);
                int occupant = hex.PhysicalSlot is not null
                    ? unchecked((short)MaintenanceKnown(current.ReadHexRaw1C(hex), "next_waypoint_occupant")) : 0;
                bool consume;
                if (occupant is >= 1 and <= 500)
                {
                    var other = current.ReadArmyUnitTableReference(occupant)
                        ?? throw MaintenanceMissing("next_waypoint_Army_lookup");
                    consume = other.PhysicalSlot is not null && ArmyRouteSearch.OccupantBlocksCurrent(working, current,
                        other, hasSearchArmy: true, image.NativeArmyOrigin?.NativeSlotIndex
                            ?? throw MaintenanceMissing("next_waypoint_actor_receiver"));
                }
                else consume = MaintenanceKnown(current.ReadObjectNativeValidity(obj), "next_waypoint_Object_validity") &&
                    obj.PhysicalSlot is not (>= 1 and <= 61);
                if (consume)
                {
                    working = ArmyWaypointArrivalSource.ConsumeCurrent(working, image)
                        ?? throw MaintenanceMissing("next_waypoint_consume_owner");
                    image = working.Units[image.Id];
                }
            }
        }
        //Waypoint consumption leaves Next48 alone. Only its actual None
        //reaches301DD0 and the obstruction search, never a guessed reroute.
        if (image.March?.NextStep != ArmyMarchDestination.None || action == 13) return working;
        ushort effectiveGoal = OrdinaryArmyRouteSource.ReadCurrentTarget(working, image, current)
            ?? throw MaintenanceMissing("no_next_effective_goal");
        var goalHex = current.ReadHexTableReference(effectiveGoal < 48400 ? effectiveGoal : 0)
            ?? throw MaintenanceMissing("no_next_goal_Hex");
        if (goalHex.PhysicalSlot is null) return working;
        working = FRoute(working, image.Id, false, current, false);
        image = working.Units[image.Id];
        if (image.March?.NextStep != ArmyMarchDestination.None) return working;
        ushort link = MaintenanceKnown(current.ReadHexObjectLink(goalHex), "no_next_goal_Object");
        var goalObject = current.ReadObjectTableReference(link <= 3000 ? link : 0)
            ?? throw MaintenanceMissing("no_next_Object_pointer");
        bool objectValid = MaintenanceKnown(current.ReadObjectNativeValidity(goalObject), "no_next_Object_validity");
        int requiredDistance = objectValid && goalObject.PhysicalSlot is >= 1 and <= 51 ? 2 : 1;
        if (FireCommandDistance(at, effectiveGoal) == requiredDistance) return working;
        ushort searchGoal = OrdinaryArmyRouteSource.ReadCurrentTarget(working, image, current)
            ?? throw MaintenanceMissing("obstacle_effective_goal");
        var routeList = current.CreateTemporaryHexList() ?? throw MaintenanceMissing("obstacle_route_descriptor");
        try
        {
            var target = OrdinaryArmyRouteSource.ReadEffectiveSearchTarget(working, image)
                ?? throw MaintenanceMissing("obstacle_effective_descriptor");
            int[] path = ArmyRouteSearch.Search(working, current, image, at, searchGoal,
                target.Kind, target.Identity, image.NativeArmyOrigin?.NativeSlotIndex, out _, 0x71);
            foreach (int raw in path)
            {
                var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0)
                    ?? throw MaintenanceMissing("obstacle_Hex");
                if (hex.PhysicalSlot is null) continue;
                ushort obstacleLink = MaintenanceKnown(current.ReadHexObjectLink(hex), "obstacle_Object_link");
                var obstacle = current.ReadObjectTableReference(obstacleLink <= 3000 ? obstacleLink : 0)
                    ?? throw MaintenanceMissing("obstacle_Object");
                if (!MaintenanceKnown(current.ReadObjectNativeValidity(obstacle), "obstacle_validity") ||
                    obstacle.PhysicalSlot is >= 1 and <= 61) continue;
                var data = current.ReadPhysicalObject(obstacle.PhysicalSlot ?? throw MaintenanceMissing("obstacle_identity"))
                    ?? throw MaintenanceMissing("obstacle_fields");
                var definition = current.ReadObjectDefinitionReference(data.RawField12 <= 30 ? data.RawField12 : 0)
                    ?? throw MaintenanceMissing("obstacle_definition");
                if (MaintenanceKnown(current.ReadObjectDefinitionRaw2C(definition), "obstacle_category") == 4) continue;
                int obstacleForce = MaintenanceKnown(current.ReadObjectForceSlot13(obstacle), "obstacle_force");
                if (!MaintenanceKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(working, force, obstacleForce), "obstacle_relation")) continue;
                //The first reached hostile object writes only3C/3E. The
                //lookup alias remains in the resource boundary, while the
                //domain override owns the actual reusable object receiver.
                working = ArmyOverrideTargetSourceAdapter.Write(working, image, new(11, obstacleLink),
                    current.CurrentMapObjectDirectory ?? throw MaintenanceMissing("obstacle_directory"));
                var changed = working.Units[image.Id];
                var arena = working.ReadArmyLifecycle()!;
                var receiver = arena.Receivers.FindUnit(image.Id) ?? throw MaintenanceMissing("obstacle_actor_receiver");
                //The existing receiver and its active image each retain an
                //override projection. Preserve the same actual alias at both
                //serialization owners before either can outlive this image.
                var receiverResidue = ArmyOverrideTargetSourceAdapter.Residue(new(receiver.ReceiverId, null),
                    new(11, obstacleLink), changed.OverrideTarget!, current.CurrentMapObjectDirectory);
                working = (working with { ResourceState = CampaignSourceKnowledge.From(working.ResourceState)
                    .WithArmyOverride(receiverResidue) }).WithArmyLifecycle(arena with
                    { Receivers = arena.Receivers.With(receiver with { OverrideTarget = changed.OverrideTarget }) });
                current.SetArmyCommandOverride(image.NativeArmyOrigin!.NativeSlotIndex!.Value, new(11, obstacleLink));
                return working;
            }
            return FinishNoPathArmyCommand(working, current, image, operation);
        }
        finally { current.ClearTemporaryHexList(routeList); current.ReleaseTemporaryHexList(routeList); }
    }
}
