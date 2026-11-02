using System.Text.Json.Nodes;

namespace San14.Core;

internal static class BaseCaptureContinuationSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 224 && state["BaseCaptureContinuation"] is JsonObject capture &&
            capture.ContainsKey("PersonPrefixProgress"))
            throw new InvalidDataException("Legacy world contains future Base capture people progress.");
        if (version < 179 && state["BaseCaptureContinuation"] is not null)
            throw new InvalidDataException("Legacy world contains a future Base capture continuation.");
        if (version < 181 && state["BaseCaptureContinuation"]?["SettlementIndex"] is JsonValue index &&
            index.TryGetValue<int>(out int at) && at == -1)
            throw new InvalidDataException("Legacy world contains a future immediate Base capture continuation.");
    }

    internal static void Migrate(JsonObject state, int version, CampaignOperationWorldContext? operationContext = null)
    {
        const string key = "BaseCaptureContinuation";
        if (version < 179) { state[key] = null; return; }
        if (!state.ContainsKey(key)) throw Invalid();
        if (state[key] is not JsonNode saved) return;
        if (saved is not JsonObject value) throw Invalid();
        //An old active capture has no source-time notice or merge knowledge.
        //Retain that unknown explicitly; its runtime resume is unavailable.
        if (version < 224) value["PersonPrefixProgress"] = null;
        if (!I(value, "SettlementIndex", -1) ||
            value["Operation"] is not JsonObject operation || !Operation(operation) ||
            value["People"] is not JsonObject people || !People(people, value["PersonPrefixProgress"]) ||
            !PrefixProgress(value, people) || !NoticeOutbox(state, value) ||
            !Nullable(value, "PendingCapturedRulerTreatment", Captive) ||
            !Nullable(value, "ChosenDisposition", node => Integer(node, 0, 1)) ||
            state["ArmyDefeatContinuation"] is not null) throw Invalid();
        if (value["SettlementIndex"]!.GetValue<int>() == -1)
        {
            if (version < 181 ||
                !((Integer(state["NativePeriodContinuation"]?["Cursor"]?["StageIndex"], 13, 13) || operationContext?.Moment is { SchedulerStageIndex: 13 }) && state["NormalCombatImmediateContinuation"] is JsonObject ||
                  state["ManualTacticExecutionContinuation"] is JsonObject && (state["NativePeriodContinuation"]?["Cursor"] is null ||
                    Integer(state["NativePeriodContinuation"]?["Cursor"]?["StageIndex"], 6, 6)))) throw Invalid();
            return;
        }
        if (!Integer(state["NativePeriodContinuation"]?["Cursor"]?["StageIndex"], 25, 25) && operationContext?.Moment is not { SchedulerStageIndex: 25 } ||
            state["PendingCombatSettlements"]?["Requests"] is not JsonArray queue ||
            value["SettlementIndex"]!.GetValue<int>() >= queue.Count) throw Invalid();
        //The exact retained request must still occupy this queue slot. The
        //polymorphic queue also carries its discriminator; compare domain fields.
        var queued = queue[value["SettlementIndex"]!.GetValue<int>()] as JsonObject;
        var request = (JsonObject)operation["Request"]!;
        if (queued is null || !JsonNode.DeepEquals(queued["Target"], request["Target"]) ||
            !JsonNode.DeepEquals(queued["WinnerUnitId"], request["WinnerUnitId"]) ||
            !JsonNode.DeepEquals(queued["HasNoWinner"], request["HasNoWinner"]) ||
            !JsonNode.DeepEquals(queued["WinnerDistrictId"], request["WinnerDistrictId"])) throw Invalid();
    }

    private static bool Operation(JsonObject value) => value["Request"] is JsonObject request &&
        request["Target"] is JsonObject target && I(target, "Kind", 0, 1) && I(target, "Id", 1) &&
        Nullable(request, "WinnerUnitId", node => Integer(node, 1, int.MaxValue)) &&
        I(value, "PreviousForceId", 0, 51) && I(value, "WinnerForceId", 0, 51) && I(value, "WinnerDistrictId", 0, 51) &&
        I(value, "CommanderOfficerId", 0, 6000) && I(value, "WinnerArmyPhysicalSlot", 0) &&
        value["WinnerReceiverId"] is JsonObject receiver && I(receiver, "Value", 1) && I(value, "ReturnedTroops") &&
        Nullable(value, "SampleDay", node => Integer(node, int.MinValue, int.MaxValue)) &&
        Nullable(value, "SampleSalt", node => Integer(node, int.MinValue, int.MaxValue)) &&
        (value["SampleDay"] is null) == (value["SampleSalt"] is null);

    private static bool People(JsonObject value, JsonNode? progress)
    {
        bool beforeCallbacks = progress is JsonObject known && Integer(known["Stage"], 0, 1);
        return Array(value, "HomeArmySlots", int.MaxValue) && Array(value, "HomeOfficerIds", 6000) &&
            Array(value, "CapturedOfficerIds", 6000) && Array(value, "SpecialCapturedOfficerIds", 6000) &&
            Array(value, "NoncapturedOfficerIds", 6000) &&
            (beforeCallbacks ? I(value, "CapturedIndex", -1, -1) :
                I(value, "CapturedIndex", 0, ((JsonArray)value["CapturedOfficerIds"]!).Count - 1));
    }

    private static bool PrefixProgress(JsonObject capture, JsonObject people)
    {
        const string key = "PersonPrefixProgress";
        if (!capture.ContainsKey(key)) return false;
        if (capture[key] is null) return true; //Retained legacy source-time unknown.
        if (capture[key] is not JsonObject progress || !I(progress, "Stage", 0, 2) ||
            !I(progress, "OrdinaryCapturedCount", 0, ((JsonArray)people["CapturedOfficerIds"]!).Count) ||
            !Nullable(progress, "AnnouncementBatchSequence", node => node is JsonValue sequence &&
                sequence.TryGetValue<long>(out long number) && number > 0) ||
            !Nullable(progress, "AnnouncementTextAtSource", node => node is JsonValue text && text.TryGetValue<string>(out _)) ||
            (progress["AnnouncementBatchSequence"] is null) != (progress["AnnouncementTextAtSource"] is null)) return false;
        int stage = progress["Stage"]!.GetValue<int>();
        if (stage == 2) return progress["AnnouncementBatchSequence"] is null;
        return progress["OrdinaryCapturedCount"]!.GetValue<int>() == ((JsonArray)people["CapturedOfficerIds"]!).Count &&
            ((JsonArray)people["SpecialCapturedOfficerIds"]!).Count > 0 &&
            progress["AnnouncementBatchSequence"] is not null && capture["PendingCapturedRulerTreatment"] is null &&
            capture["ChosenDisposition"] is null;
    }

    private static bool NoticeOutbox(JsonObject state, JsonObject capture)
    {
        if (capture["PersonPrefixProgress"] is not JsonObject progress ||
            Integer(progress["Stage"], 2, 2)) return true;
        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) return false;
        long sequence = progress["AnnouncementBatchSequence"]!.GetValue<long>();
        var matching = batches.OfType<JsonObject>().Where(batch => batch["Sequence"] is JsonValue value &&
            value.TryGetValue<long>(out long found) && found == sequence).Take(2).ToArray();
        //The producer appends one dedicated notice batch. Its actual typed
        //receipt removes that batch before the source can merge the people.
        if (Integer(progress["Stage"], 1, 1)) return matching.Length == 0;
        return matching.Length == 1 && Integer(matching[0]["Source"],
                (int)CampaignEffectSource.ArmyBattleDefeat, (int)CampaignEffectSource.ArmyBattleDefeat) &&
            matching[0]["Requests"] is JsonArray { Count: 1 } requests && requests[0]?["TalentCommandPresentation"] is JsonObject notice &&
            Integer(notice["Operation"], (int)TalentCommandPresentationOperation.Announcement,
                (int)TalentCommandPresentationOperation.Announcement) &&
            notice["TextAtSource"] is JsonValue text && text.TryGetValue<string>(out string? saved) &&
            string.Equals(saved, progress["AnnouncementTextAtSource"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    private static bool Captive(JsonNode? node) => node is JsonObject value && I(value, "OfficerId", 1, 6000) &&
        I(value, "CapturingForceId", 1, 45) && I(value, "CapturingRulerId", 1, 6000) &&
        value["CanExecute"] is JsonValue can && can.TryGetValue<bool>(out _) &&
        value["TextAtSource"] is JsonValue text && text.TryGetValue<string>(out _);
    private static bool Array(JsonObject value, string key, int max) => value[key] is JsonArray items && items.All(node => Integer(node, 0, max));
    private static bool Nullable(JsonObject value, string key, Func<JsonNode?, bool> valid) => value.ContainsKey(key) && (value[key] is null || valid(value[key]));
    private static bool I(JsonObject value, string key, int min = int.MinValue, int max = int.MaxValue) => Integer(value[key], min, max);
    private static bool Integer(JsonNode? node, int min, int max) => node is JsonValue value && value.TryGetValue<int>(out int number) && number >= min && number <= max;
    private static InvalidDataException Invalid() => new("Saved Base capture continuation is incomplete or invalid.");
}
