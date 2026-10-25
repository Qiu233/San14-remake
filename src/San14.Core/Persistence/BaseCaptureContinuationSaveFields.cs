using System.Text.Json.Nodes;

namespace San14.Core;

internal static class BaseCaptureContinuationSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 179 && state["BaseCaptureContinuation"] is not null)
            throw new InvalidDataException("Legacy world contains a future Base capture continuation.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        const string key = "BaseCaptureContinuation";
        if (version < 179) { state[key] = null; return; }
        if (!state.ContainsKey(key)) throw Invalid();
        if (state[key] is not JsonNode saved) return;
        if (saved is not JsonObject value || !I(value, "SettlementIndex", 0) ||
            value["Operation"] is not JsonObject operation || !Operation(operation) ||
            value["People"] is not JsonObject people || !People(people) ||
            !Nullable(value, "PendingCapturedRulerTreatment", Captive) ||
            !Nullable(value, "ChosenDisposition", node => Integer(node, 0, 1)) ||
            !Integer(state["NativePeriodContinuation"]?["Cursor"]?["StageIndex"], 25, 25) ||
            state["ArmyDefeatContinuation"] is not null ||
            state["PendingCombatSettlements"]?["Requests"] is not JsonArray queue ||
            value["SettlementIndex"]!.GetValue<int>() >= queue.Count) throw Invalid();
        //The exact retained request must still occupy this queue slot. The
        //polymorphic queue also carries its discriminator; compare domain fields.
        var queued = queue[value["SettlementIndex"]!.GetValue<int>()] as JsonObject;
        var request = (JsonObject)operation["Request"]!;
        if (queued is null || !JsonNode.DeepEquals(queued["Target"], request["Target"]) ||
            !JsonNode.DeepEquals(queued["WinnerUnitId"], request["WinnerUnitId"]) ||
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

    private static bool People(JsonObject value) => Array(value, "HomeArmySlots", int.MaxValue) &&
        Array(value, "HomeOfficerIds", 6000) && Array(value, "CapturedOfficerIds", 6000) &&
        Array(value, "SpecialCapturedOfficerIds", 6000) && Array(value, "NoncapturedOfficerIds", 6000) &&
        I(value, "CapturedIndex", 0, ((JsonArray)value["CapturedOfficerIds"]!).Count - 1);

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
