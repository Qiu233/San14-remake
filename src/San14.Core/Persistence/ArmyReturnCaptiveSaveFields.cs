using System.Text.Json.Nodes;

namespace San14.Core;

internal static class ArmyReturnCaptiveSaveFields
{
    private const string Owner = "ArmyReturnCaptiveContinuation";
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 227 && state[Owner] is not null)
            throw new InvalidDataException("Legacy world contains a future army-return captive continuation.");
        if (version < 228 && state[Owner] is JsonObject owner && owner["ArrivalTail"] is JsonObject arrival &&
            arrival.ContainsKey("MarchCallerTailPending"))
            throw new InvalidDataException("Legacy world contains a future pending march caller tail.");
    }
    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 227) { state[Owner] = null; return; }
        if (!state.ContainsKey(Owner)) throw Invalid();
        if (state[Owner] is null) return;
        if (state[Owner] is not JsonObject value || new[]
            {
                "BatchId", "ArmyUnitId", "ArmyPhysicalSlot", "CapturingForceId", "ReturningCommanderId",
                "Destination", "SavedCommanderDistrictId", "SavedTargetDistrictId", "ReturningTransportPerson",
                "SourceDate", "SourceRevision", "SampleDay", "SampleSalt", "OriginalOfficerIds",
                "RemainingOfficerIds", "RecruitmentTrials", "Phase", "Round", "ActiveDisposition",
                "ActiveBatchOfficerIds", "ActiveBatchIndex", "ExecutionConfirmation", "PendingPresentation",
                "AwaitingPersonDeathOfficerId", "RequestedOfficerIds", "FirstResultPresentation", "FirstResultQueued",
                "Caller", "ArrivalTail", "CaptureTail", "CampaignTail", "ImmediateEntryReportRuleId", "DeploymentCancellationTail"
            }.Any(key => !value.ContainsKey(key)) ||
            value["ReturningTransportPerson"] is not JsonValue personReturn || !personReturn.TryGetValue<bool>(out _) ||
            value["OriginalOfficerIds"] is not JsonArray || value["RemainingOfficerIds"] is not JsonArray ||
            value["RecruitmentTrials"] is not JsonArray trials || value["ActiveBatchOfficerIds"] is not JsonArray ||
            value["RequestedOfficerIds"] is not JsonArray || !Boolean(value["FirstResultQueued"]))
            throw Invalid();
        foreach (var row in trials)
            if (row is not JsonObject trial || new[] { "OfficerId", "RecruitmentAccepted", "SampleAfter", "Probability",
                    "ExecutionProtected", "FailedRecruitNoticeShown", "ExecutionProtectionNoticeShown", "AdviceTextAtSource" }
                .Any(key => !trial.ContainsKey(key)) ||
                !Boolean(trial["RecruitmentAccepted"]) || !Boolean(trial["ExecutionProtected"]) ||
                !Boolean(trial["FailedRecruitNoticeShown"]) || !Boolean(trial["ExecutionProtectionNoticeShown"])) throw Invalid();
        RequireOptional(value, "ArrivalTail", "Action", "GoalHexId", "DestinationLocationId", "Moment");
        if (value["ArrivalTail"] is JsonObject arrival)
        {
            //World227 published the caller tail before suspending29F220.
            //Its stored prefix must not be replayed after the saved reply.
            if (version < 228) arrival["MarchCallerTailPending"] = false;
            else if (!Boolean(arrival["MarchCallerTailPending"])) throw Invalid();
        }
        RequireOptional(value, "CaptureTail", "Request", "WinningForceId", "WinningDistrictId", "StatePhysicalSlots",
            "RegionPhysicalSlots", "BeforeStateCount", "BeforeRegionCount", "CompletingEmperorCapture", "Operation", "SettlementIndex");
        RequireOptional(value, "CampaignTail", "CurrentExpeditionId", "RemainingExpeditionIds", "OperationDate",
            "OperationRevision", "RemainingArmyPhysicalSlots", "RetainedCommanderPhysicalSlot", "OperationCampaignDay");
        RequireOptional(value, "DeploymentCancellationTail", "CityId", "RetainedCommanderPhysicalSlot", "OperationDate",
            "OperationRevision", "OperationCampaignDay");
        RequireOptional(value, "ExecutionConfirmation", "BatchId", "Round", "CapturingForceId", "SelectedOfficerIds", "TextAtSource");
        RequireOptional(value, "PendingPresentation", "BatchId", "Round", "CapturingForceId", "EffectBatchSequence");
        RequireOptional(value, "FirstResultPresentation", "Operation", "SoundId", "OfficerId", "TextAtSource");
    }
    private static void RequireOptional(JsonObject owner, string key, params string[] fields)
    {
        if (owner[key] is null) return;
        if (owner[key] is not JsonObject value || fields.Any(field => !value.ContainsKey(field))) throw Invalid();
    }
    private static bool Boolean(JsonNode? value) => value is JsonValue item && item.TryGetValue<bool>(out _);
    private static InvalidDataException Invalid() => new("Saved army-return captive continuation fields are incomplete.");
}
