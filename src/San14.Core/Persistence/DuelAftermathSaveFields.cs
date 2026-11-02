using System.Text.Json.Nodes;

namespace San14.Core;

internal static class DuelAftermathSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 178 && (state["DuelAftermathContinuation"] is not null ||
            Integer(state["NativePeriodContinuation"]?["Cursor"]?["StageIndex"], 5, 5)))
            throw new InvalidDataException("Legacy save contains a future duel aftermath continuation.");
    }

    internal static void Migrate(JsonObject state, int version, CampaignOperationWorldContext? operationContext = null)
    {
        const string key = "DuelAftermathContinuation";
        if (version < 178) { state[key] = null; return; }
        if (!state.ContainsKey(key)) throw Invalid();
        bool normalStage5 = Integer(state["NativePeriodContinuation"]?["Cursor"]?["StageIndex"], 5, 5);
        var external = state["NativePeriodContinuation"] is null && operationContext?.Moment is { SchedulerStageIndex: 5 }
            ? operationContext.Moment : null;
        bool stage5 = normalStage5 || external is not null;
        if (state[key] is not JsonNode saved)
        {
            if (stage5) throw Invalid();
            return;
        }
        if (saved is not JsonObject value || !stage5 ||
            state["NormalCombatImmediateContinuation"] is not null ||
            !Integer(state["ArmyDefeatContinuation"]?["SettlementIndex"], -1, -1) ||
            value["DuelSequence"] is not JsonValue sequence || !sequence.TryGetValue<long>(out long id) || id <= 0 ||
            !Integer(value["ResumePoint"], 0, 1) || value["Injuries"] is not JsonArray injuries ||
            !injuries.All(Injury) || state["CampaignDuelService"] is not JsonObject service ||
            !True(service["Active"]) || !True(service["Busy"]) || service["Registered"] is not JsonObject registered ||
            registered["Sequence"] is not JsonValue registeredSequence || !registeredSequence.TryGetValue<long>(out long registeredId) ||
            registeredId != id || registered["Pair"] is not JsonObject || registered["Result"] is not JsonObject result ||
            !Integer(result["Actor"]?["Outcome"], (int)CampaignDuelOutcome.Won, (int)CampaignDuelOutcome.Lost) ||
            (Integer(value["ResumePoint"], 0, 0)
                ? !Integer(result["Aftermath"], (int)CampaignDuelAftermath.Death, (int)CampaignDuelAftermath.Capture)
                : !Integer(result["Aftermath"], (int)CampaignDuelAftermath.None, (int)CampaignDuelAftermath.None)) ||
            registered["Date"] is not JsonObject ||
            (normalStage5
                ? !JsonNode.DeepEquals(registered["Date"], state["NativePeriodContinuation"]?["Cursor"]?["Date"])
                : external is null || !Date(registered["Date"], external.PreWriterThirdDate) ||
                    registered["CalendarCycleOrdinal"] is not JsonValue ordinalValue ||
                    !ordinalValue.TryGetValue<long>(out long ordinal) || ordinal != external.CalendarCycleOrdinal)) throw Invalid();
    }

    private static bool Date(JsonNode? node, NativeMapSeasonDate expected) => node is JsonObject value && value.Count == 4 &&
        Integer(value["Year"], expected.Year, expected.Year) && Integer(value["Month"], expected.Month, expected.Month) &&
        Integer(value["Day"], expected.Day, expected.Day) && Integer(value["Hour"], expected.Hour, expected.Hour);

    private static bool Injury(JsonNode? node) => node is JsonObject value &&
        Integer(value["OfficerId"], 0, 6000) && Integer(value["BeforeHealthStateId"], 0, 3) &&
        Integer(value["AfterHealthStateId"], 0, 3) && value["PlayerDistrictNotice"] is JsonValue player &&
        player.TryGetValue<bool>(out _) && value.ContainsKey("ArmyId") && value.ContainsKey("ArmyHexId") &&
        (value["ArmyId"] is null ? value["ArmyHexId"] is null :
            Integer(value["ArmyId"], 1, int.MaxValue) && Integer(value["ArmyHexId"], 0, 48400));
    private static bool True(JsonNode? node) => node is JsonValue value && value.TryGetValue<bool>(out bool flag) && flag;
    private static bool Integer(JsonNode? node, int minimum, int maximum) => node is JsonValue value &&
        value.TryGetValue<int>(out int number) && number >= minimum && number <= maximum;
    private static InvalidDataException Invalid() => new("Saved duel aftermath continuation is incomplete or invalid.");
}
