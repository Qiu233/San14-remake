using San14.Core;

namespace San14.Data;

internal sealed record DiplomacyContinuationCheck(bool Allowed, bool ReportCancellation);

public sealed partial class ArmyCampaignRuleAdapter
{
    //1E81E0's pure admission branch. The enclosing task manager owns the
    //optional1E48C0 notification and2247F0 retirement/return effects.
    internal DiplomacyContinuationCheck DiplomacyContinuationAllowedCurrent(GameState state,
        SourceWorldContext current, PersonnelDiplomacyTask task)
    {
        if (task.NativeKind is not (4 or 5 or 6 or 9)) throw DiplomacyReportMissing("continuation_kind");
        if (task.MemberIds.Length == 0) return new(false, false);
        int actor = DiplomacyLookupPerson(current, task.MemberIds[0]);
        var own = current.ReadPersonForce(actor) ?? throw DiplomacyReportMissing("continuation_executor_Force");
        byte rawDistrict = DiplomacyNeed(current.ReadPersonDistrictSlot(actor), "continuation_executor_E0");
        var district = current.ReadDistrictTableReference(rawDistrict < 52 ? rawDistrict : 0)
            ?? throw DiplomacyReportMissing("continuation_executor_District");
        var target = DiplomacyForce(current, task.TargetForceId);
        if (!DiplomacyNeed(current.ReadPersonNativeValidity(actor), "continuation_executor_validity") ||
            !DiplomacyNeed(current.ReadForceNativeValidity(own), "continuation_executor_Force_validity") ||
            DiplomacyNeed(current.ReadForcePhysicalSlot(own), "continuation_executor_Force60") != task.ForceId ||
            !DiplomacyNeed(current.ReadDistrictNativeValidity(district), "continuation_District_validity") ||
            DiplomacyNeed(district.PhysicalSlot, "continuation_District_ordinal") != task.SourceDistrictId ||
            DiplomacyNeed(current.ReadPersonFirstLocation(actor), "continuation_executor_E2") !=
                DiplomacyNeed(current.ReadBaseObjectLink(BaseLookup.Find(task.SourceCityId, current)), "continuation_original_Base4E"))
            return new(false, false);
        if (!DiplomacyNeed(current.ReadForceNativeValidity(target), "continuation_target_validity")) return new(false, true);
        var queries = new DiplomacyCommandSource(state, current);
        try { queries.RequireInteraction(task.ForceId, task.TargetForceId); }
        catch (ArgumentException) { return new(false, true); }
        int months = queries.TreatyMonths(task.ForceId, task.TargetForceId);
        if (task.NativeKind == 5 && (long)months + task.AllianceMonths > 48 || task.NativeKind == 6 && months == 0)
            return new(false, true);
        if (task.NativeKind == 9)
        {
            var requested = BaseLookup.Find(task.TargetCityId ?? throw DiplomacyReportMissing("continuation_request_Base"), current);
            int victim = DiplomacyNeed(current.ReadBaseForceSlot(requested), "continuation_request_ForceB0");
            if (months == 0 || queries.HasTreaty(task.TargetForceId, victim) || queries.HasTreaty(task.ForceId, victim))
                return new(false, true);
        }
        //The initial Area remains owned by its giver until accepted settlement.
        if (!IsDiplomacyInitialAreaStillQualifiedCurrent(state, current, task))
            return new(false, true);
        return task.Cancelled ? new(false, false) : new(true, false);
    }

    private static ForceReference DiplomacyForce(SourceWorldContext current, int raw)
        => current.ReadForceTableReference(raw is >= 0 and < 52 ? raw : 0) ?? throw DiplomacyReportMissing("Force_pointer");
    private static int DiplomacyLookupPerson(SourceWorldContext current, int raw)
        => DiplomacyNeed(((raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
            ?? throw DiplomacyReportMissing("Person_lookup")).PhysicalSlot, "Person_pointer");
    private static T DiplomacyNeed<T>(T? value, string field) where T : struct => value ?? throw DiplomacyReportMissing(field);
    private static InvalidDataException DiplomacyReportMissing(string field) => new("diplomacy.current_" + field);
}
