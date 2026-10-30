using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private static IReadOnlyList<uint?> ReadIntelligenceJourneyMembersCurrent(SourceWorldContext current, PersonnelIntelligenceTask task)
        => current.ReadLinkedEntityMemberIds(new(task.Id)) ?? throw IntelligenceMissing("journey_member_chain");

    //The saved/current member is a lookup operand, never a physical receiver.
    //Identity is read only at the reached report/health consumer after its gates.
    private static int? ReadIntelligenceJourneyActorCurrent(SourceWorldContext current, PersonnelIntelligenceTask task)
    {
        var members = ReadIntelligenceJourneyMembersCurrent(current, task);
        if (members.Count == 0) return null;
        uint raw = IntelligenceNeed(members[0], "journey_first_raw_member");
        var actor = raw <= 6000 ? current.ReadPersonLookupReference((int)raw) : current.ReadTransferPersonTableReference(0);
        return IntelligenceNeed(actor?.PhysicalSlot, "journey_current_member_receiver");
    }

    private static int[] ReadIntelligenceReturnMembersCurrent(SourceWorldContext current, PersonnelIntelligenceTask task)
        => ReadIntelligenceJourneyMembersCurrent(current, task).Select(raw =>
        {
            uint member = IntelligenceNeed(raw, "return_raw_member");
            if (member > int.MaxValue) throw IntelligenceMissing("return_member_storage_range");
            return (int)member;
        }).ToArray();

    private static int ReadIntelligenceStartHexCurrent(SourceWorldContext current, PersonnelIntelligenceTask task)
        => IntelligenceNeed((current.ReadHexTableReference((uint)task.StartHexId < 48400 ? task.StartHexId : 0)
            ?? throw IntelligenceMissing("report_start_Hex")).PhysicalSlot, "report_start_Hex_identity");

    private static bool IsIntelligenceCancellationPlayerCurrent(GameState state, SourceWorldContext current, PersonnelIntelligenceTask task)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw IntelligenceMissing("cancel_current_player");
        var primary = ForcePrimaryDistrictSource.Resolve(current, player);
        if (!IntelligenceNeed(current.ReadDistrictNativeValidity(primary), "cancel_current_primary_validity")) return false;
        var saved = current.ReadDistrictTableReference((uint)task.SourceDistrictId < 52 ? task.SourceDistrictId : 0)
            ?? throw IntelligenceMissing("cancel_saved_District");
        return primary.PhysicalSlot == saved.PhysicalSlot;
    }

    //1EDFA0 is a distinct Inspect outer. No Away leader/relation gate belongs here.
    private bool HasIntelligenceInspectCommandCurrent(GameState state, SourceWorldContext current, BaseReference city)
    {
        if (!IntelligenceNeed(current.ReadBaseNativeValidity(city), "inspect_marker_City_validity")) return false;
        var tasks = current.ReadOwnedPersonCommandManager()?.Intelligences ?? throw IntelligenceMissing("inspect_marker_manager");
        foreach (var linked in current.ReadCurrentPersonLinkedEntities() ?? throw IntelligenceMissing("inspect_marker_chain"))
        {
            int id = IntelligenceNeed(linked?.Identity, "inspect_marker_receiver");
            if (!tasks.TryGetValue(id, out var task) || task.Kind != PersonnelIntelligenceKind.Inspect) continue;
            var district = current.ReadDistrictTableReference((uint)task.SourceDistrictId < 52 ? task.SourceDistrictId : 0)
                ?? throw IntelligenceMissing("inspect_marker_header_District");
            if (!IntelligenceNeed(current.ReadDistrictNativeValidity(district), "inspect_marker_header_validity")) continue;
            int rawForce = IntelligenceNeed(current.ReadDistrictForceId(IntelligenceNeed(district.PhysicalSlot,
                "inspect_marker_header_identity")), "inspect_marker_header_Force");
            var force = current.ReadForceTableReference((uint)rawForce < 52 ? rawForce : 0) ?? throw IntelligenceMissing("inspect_marker_Force");
            if (IntelligenceNeed(current.ReadForcePhysicalSlot(force), "inspect_marker_Force_identity") !=
                IntelligenceNeed(current.ReadBaseForceSlot(city), "inspect_marker_City_Force")) continue;
            var target = current.ReadCityTableReference((uint)task.TargetId < 52 ? task.TargetId : 0) ?? throw IntelligenceMissing("inspect_marker_target");
            if (target.PhysicalSlot != city.PhysicalSlot) continue;
            if (CheckIntelligenceJourneyCurrent(state, current, task) == IntelligenceJourneyDisposition.Continue) return true;
        }
        return false;
    }
}
