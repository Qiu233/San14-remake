using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The selection label reads the command manager's actual association chain.
    //It neither starts a task nor runs the execution-time cancellation path.
    private bool HasIntelligenceAwayMarkerCurrent(GameState state, SourceWorldContext current,
        ForceReference own, ForceReference target)
    {
        var tasks = current.ReadOwnedPersonCommandManager()?.Intelligences
            ?? throw IntelligenceMissing("selection.spy_marker.command_manager");
        var commands = current.ReadCurrentPersonLinkedEntities()
            ?? throw IntelligenceMissing("selection.spy_marker.command_chain");
        int player = IntelligenceNeed(current.ReadForcePhysicalSlot(own), "selection.spy_marker.player_identity");
        int targetId = IntelligenceNeed(current.ReadForcePhysicalSlot(target), "selection.spy_marker.target_identity");
        foreach (var linked in commands)
        {
            if (linked?.Identity is not int identity)
                throw IntelligenceMissing("selection.spy_marker.command_receiver");
            if (!tasks.TryGetValue(identity, out var task) || task.Kind != PersonnelIntelligenceKind.Away) continue;
            var district = current.ReadDistrictTableReference((uint)task.SourceDistrictId < 52 ? task.SourceDistrictId : 0)
                ?? throw IntelligenceMissing("selection.spy_marker.header_district");
            if (!IntelligenceNeed(current.ReadDistrictNativeValidity(district), "selection.spy_marker.header_district_validity")) continue;
            int districtId = IntelligenceNeed(district.PhysicalSlot, "selection.spy_marker.header_district_identity");
            int rawForce = IntelligenceNeed(current.ReadDistrictForceId(districtId), "selection.spy_marker.header_force");
            var force = current.ReadForceTableReference((uint)rawForce < 52 ? rawForce : 0)
                ?? throw IntelligenceMissing("selection.spy_marker.header_force_pointer");
            if (IntelligenceNeed(current.ReadForcePhysicalSlot(force), "selection.spy_marker.header_force_identity") != player) continue;
            //An Away task's actual target variant is a Force. Inspect has a
            //different native command kind and never reaches this branch.
            var taskTarget = current.ReadForceTableReference((uint)task.TargetId < 52 ? task.TargetId : 0)
                ?? throw IntelligenceMissing("selection.spy_marker.task_target");
            if (IntelligenceNeed(current.ReadForcePhysicalSlot(taskTarget), "selection.spy_marker.task_target_identity") != targetId) continue;
            if (IsIntelligenceAwayCommandCurrent(state, current, linked, task, taskTarget)) return true;
        }
        return false;
    }

    //The query-only command admission returns its result without reports,
    //retirement, Started changes, time advancement or random draws.
    private bool IsIntelligenceAwayCommandCurrent(GameState state, SourceWorldContext current,
        PersonLinkedEntityReference linked, PersonnelIntelligenceTask task, ForceReference target)
    {
        var members = current.ReadLinkedEntityMemberIds(linked)
            ?? throw IntelligenceMissing("selection.spy_marker.command_members");
        if (members.Count == 0) return false;
        uint raw = IntelligenceNeed(members[0], "selection.spy_marker.first_member");
        int actor = IntelligenceMissionPersonCurrent(current, raw <= 6000 ? (int)raw : 6001);
        if (!IntelligenceNeed(current.ReadPersonNativeValidity(actor), "selection.spy_marker.actor_validity")) return false;
        var force = current.ReadPersonForce(actor) ?? throw IntelligenceMissing("selection.spy_marker.actor_force");
        if (!IntelligenceNeed(current.ReadForceNativeValidity(force), "selection.spy_marker.actor_force_validity") ||
            IntelligenceNeed(current.ReadForcePhysicalSlot(force), "selection.spy_marker.actor_force_identity") != task.ForceId) return false;
        int rawDistrict = IntelligenceNeed(current.ReadPersonDistrictSlot(actor), "selection.spy_marker.actor_district");
        var district = current.ReadDistrictTableReference(rawDistrict < 52 ? rawDistrict : 0)
            ?? throw IntelligenceMissing("selection.spy_marker.actor_district_pointer");
        int districtId = IntelligenceNeed(district.PhysicalSlot, "selection.spy_marker.actor_district_identity");
        int leader = IntelligenceMissionPersonCurrent(current,
            IntelligenceNeed(current.ReadDistrictLeader(districtId), "selection.spy_marker.district_leader"));
        if (!IntelligenceNeed(current.ReadPersonNativeValidity(leader), "selection.spy_marker.district_leader_validity") ||
            IntelligenceNeed(current.ReadPersonStatus(leader), "selection.spy_marker.district_leader_status") != 1) return false;
        if (!IntelligenceNeed(current.ReadDistrictNativeValidity(district), "selection.spy_marker.actor_district_validity") ||
            districtId != task.SourceDistrictId) return false;
        var home = BaseLookup.Find(checked((ushort)task.SourceLocationId), current);
        int currentHome = IntelligenceNeed(current.ReadBaseObjectLink(home), "selection.spy_marker.current_home_identity");
        if (IntelligenceNeed(current.ReadPersonFirstLocation(actor), "selection.spy_marker.actor_home") != currentHome) return false;
        if (!IntelligenceNeed(current.ReadForceNativeValidity(target), "selection.spy_marker.target_validity")) return false;
        if (IntelligenceNeed(ArmyMarchDevelopmentSource.ReadFriendlyRelation(state, target, force),
                "selection.spy_marker.target_relation")) return false;
        return !task.Cancelled;
    }
}
