using System.Text.Json.Nodes;

namespace San14.Core;

internal static class AutomaticEmperorCaptureSaveFields
{
    private static readonly string[] Fields = ["AutomaticEmperorCapture", "CityCaptureEmperorTail",
        "BaseWinnerEmperorTail", "ForceTransferEmperorTail", "StoryForceTransferContinuation", "ForceAbsorptionContinuation",
        "DemandSurrenderAbsorptionTail", "DemandSurrenderCompletionTail", "PlayerCoalitionAbsorptionTail",
        "NpcForceAbsorptionTail", "StoryForceAbsorptionTail", "StrategicPoisonEmperorContinuation",
        "EditedEventForceAbsorptionTail", "EditedEventResultCursor"];

    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 211 && (Fields.Any(key => state[key] is not null) ||
            state["NativePeriodContinuation"]?["Cursor"]?["AwaitingStrategicTaskId"] is not null))
            throw new InvalidDataException("Legacy world contains a future emperor capture continuation.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 211)
        {
            foreach (string key in Fields) state[key] = null;
            if (state["NativePeriodContinuation"]?["Cursor"] is JsonObject legacyCursor) legacyCursor["AwaitingStrategicTaskId"] = null;
            return;
        }
        if (state["NativePeriodContinuation"]?["Cursor"] is JsonObject cursor &&
            (!cursor.ContainsKey("AwaitingStrategicTaskId") || cursor["AwaitingStrategicTaskId"] is JsonNode task &&
             (task is not JsonValue taskValue || !taskValue.TryGetValue<int>(out int taskId) || taskId < 0))) throw Invalid();
        foreach (string key in Fields)
            if (!state.ContainsKey(key) || state[key] is not null and not JsonObject) throw Invalid();
        if (state["AutomaticEmperorCapture"] is JsonObject progress)
        {
            Require(progress, "Context", "Phase", "SelectedMode", "ChoiceText", "LoyaltyPeople",
                "UnfavorableForces", "FavorableForces", "PendingDecision", "NeedsPresentationReceipt", "AwaitingEffectBatchSequence");
            if (progress["Context"] is not JsonObject context) throw Invalid();
            Require(context, "CityPhysicalSlot", "CityLocationId", "EmperorPhysicalSlot", "EmperorId",
                "NewForcePhysicalSlot", "RulerPhysicalSlot", "RulerId", "ExcludedForcePhysicalSlot", "NewForceIsPlayer", "KingdomName");
            if (progress["PendingDecision"] is JsonObject decision)
                Require(decision, "CapturingForceId", "CityId", "EmperorId", "TextAtSource");
        }
        if (state["CityCaptureEmperorTail"] is JsonObject city)
        {
            Require(city, "SettlementIndex", "Operation", "CapturedOldDistrictPhysicalSlot",
                "RelatedAreaPhysicalSlots", "StatePhysicalSlots", "RegionPhysicalSlots", "BeforeStateCount", "BeforeRegionCount",
                "CapturedPersonPhysicalSlots", "NoncapturedPersonPhysicalSlots", "SpecialPersonPhysicalSlots", "ChangedForcePersonPhysicalSlots");
            if (city["Operation"] is not JsonObject operation) throw Invalid();
            Require(operation, "Request", "PreviousForceId", "WinnerForceId", "WinnerDistrictId", "CommanderOfficerId",
                "WinnerArmyPhysicalSlot", "WinnerReceiverId", "ReturnedTroops", "SampleDay", "SampleSalt");
        }
        if (state["BaseWinnerEmperorTail"] is JsonObject winner)
            Require(winner, "Request", "CapturedOldDistrictPhysicalSlot", "OldForcePhysicalSlot", "NewForcePhysicalSlot",
                "DestinationDistrictPhysicalSlot", "OperationDate", "SampleDay", "SampleSalt", "SettlementIndex",
                "RelatedAreaPhysicalSlots", "StatePhysicalSlots", "RegionPhysicalSlots", "BeforeStateCount", "BeforeRegionCount",
                "CapturedPersonPhysicalSlots", "NoncapturedPersonPhysicalSlots", "SpecialPersonPhysicalSlots", "ChangedForcePersonPhysicalSlots");
        if (state["ForceTransferEmperorTail"] is JsonObject transfer)
            Require(transfer, "CityPhysicalSlot", "OldForcePhysicalSlot", "NewForcePhysicalSlot", "DestinationDistrictPhysicalSlot",
                "CapturedOldDistrictPhysicalSlot", "OperationDate", "RelatedAreaPhysicalSlots");
        if (state["StoryForceTransferContinuation"] is JsonObject story)
            Require(story, "Code", "TargetCityPhysicalSlot", "PreviousForcePhysicalSlot", "ExecutorPhysicalSlot",
                "DestinationDistrictPhysicalSlot", "CityPhysicalSlots", "CityIndex", "ManagerPresent");
        if (state["ForceAbsorptionContinuation"] is JsonObject absorption)
            Require(absorption, "OriginalForcePhysicalSlot", "RecipientForcePhysicalSlot", "PrimaryDistrictPhysicalSlot",
                "RecipientRulerId", "OperationDate", "SuppressEliminationPresentation", "DistrictPhysicalSlots", "ProcessedBases", "RemainingBases", "BaseIndex");
        if (state["DemandSurrenderAbsorptionTail"] is JsonObject demand)
            Require(demand, "Task", "OwnPlayer", "ChronicleSlot");
        if (state["DemandSurrenderCompletionTail"] is JsonObject completion)
        {
            Require(completion, "Task", "Accepted", "OperationDate", "Report");
            if (completion["Report"] is not JsonObject report) throw Invalid();
            Require(report, "Actor", "OwnRuler", "TargetRuler", "ActorDistrict", "TargetDistrict", "TargetSpeaker", "TargetHex", "OwnPlayer", "TargetPlayer", "TargetName");
        }
        if (state["PlayerCoalitionAbsorptionTail"] is JsonObject coalition)
            Require(coalition, "PlayerForcePhysicalSlot", "HeadForcePhysicalSlot", "RemainingMemberForcePhysicalSlots", "CooldownSourceDate", "Visible", "MembershipText");
        if (state["NpcForceAbsorptionTail"] is JsonObject npc)
            Require(npc, "PlayerForcePhysicalSlot", "SurvivorForcePhysicalSlot", "Visible", "Narration", "CameraHex");
        if (state["StoryForceAbsorptionTail"] is JsonObject absorbed)
            Require(absorbed, "OriginalForcePhysicalSlot", "RecipientForcePhysicalSlot", "RestoreRecordingGate");
        if (state["StrategicPoisonEmperorContinuation"] is JsonObject poison)
            Require(poison, "TaskId", "TargetCityPhysicalSlot", "ExecutorPhysicalSlot", "OwnForcePhysicalSlot", "PreviousForcePhysicalSlot", "CompletionDate", "ResultCode");
        if (state["EditedEventForceAbsorptionTail"] is JsonObject edited)
            Require(edited, "OriginalForcePhysicalSlot", "RecipientForcePhysicalSlot", "RestoreRecordingGate");
        if (state["EditedEventResultCursor"] is JsonObject resultCursor)
            Require(resultCursor, "Frames", "SuccessCount", "EvaluatedCount", "RootFold");
    }

    private static void Require(JsonObject value, params string[] fields)
    { if (fields.Any(field => !value.ContainsKey(field))) throw Invalid(); }
    private static InvalidDataException Invalid() => new("Saved emperor capture continuation is incomplete.");
}
