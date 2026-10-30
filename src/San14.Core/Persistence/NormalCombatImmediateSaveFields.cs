using System.Text.Json.Nodes;

namespace San14.Core;

internal static class NormalCombatImmediateSaveFields
{
    private const string Key = "NormalCombatImmediateContinuation";

    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 181 && state[Key]?["PendingAutomaticFatalFeedback"] is JsonNode feedback &&
            (feedback is not JsonValue flag || !flag.TryGetValue<bool>(out bool pending) || pending))
            throw new InvalidDataException("Legacy combat continuation contains future fatal feedback.");
        BaseAutomaticTacticExecutionSaveFields.RejectLegacyFields(state, version);
        AutomaticTacticExecutionSaveFields.RejectLegacyFields(state, version);
        //Earlier snapshots already retained Base descriptors elsewhere in the
        //mixed list. Only their current paused pair was restricted to Army/Army.
        if (version < 179 && state[Key] is JsonObject snapshot &&
            snapshot["OrderedPairs"] is JsonArray pairs && snapshot["PairIndex"] is JsonValue cursor &&
            cursor.TryGetValue<int>(out int index) && index >= 0 && index < pairs.Count &&
            pairs[index] is JsonObject pair && pair["Actor"] is JsonObject actor && pair["Target"] is JsonObject target &&
            (!Integer(actor["Kind"], 27, 27) || !Integer(target["Kind"], 27, 27)))
            throw new InvalidDataException("Legacy immediate combat continuation contains a future current pair mode.");
        if (version >= 176) return;
        if (state[Key] is not null ||
            Integer(state["NativePeriodContinuation"]?["Cursor"]?["StageIndex"], 13, 13) ||
            Integer(state["ArmyDefeatContinuation"]?["SettlementIndex"], -1, -1))
            throw new InvalidDataException("Legacy save contains a future immediate combat continuation.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 176) { state[Key] = null; return; }
        if (!state.ContainsKey(Key)) Invalid();
        if (state[Key] is not JsonNode node)
        {
            if (Integer(state["NativePeriodContinuation"]?["Cursor"]?["StageIndex"], 13, 13) ||
                Integer(state["ArmyDefeatContinuation"]?["SettlementIndex"], -1, -1) &&
                !(version >= 178 && state["DuelAftermathContinuation"] is JsonObject &&
                  Integer(state["NativePeriodContinuation"]?["Cursor"]?["StageIndex"], 5, 5)) &&
                !(version >= 181 && state["ManualTacticExecutionContinuation"] is JsonObject &&
                  (state["NativePeriodContinuation"]?["Cursor"] is null ||
                   Integer(state["NativePeriodContinuation"]?["Cursor"]?["StageIndex"], 6, 6)))) Invalid();
            return;
        }
        if (node is not JsonObject value) { Invalid(); return; }
        if (version < 181) value["PendingAutomaticFatalFeedback"] = false;
        if (value["PendingAutomaticFatalFeedback"] is not JsonValue feedback || !feedback.TryGetValue<bool>(out _) ||
            value["Actors"] is not JsonArray actors || !actors.All(Participant) ||
            value["OrderedPairs"] is not JsonArray pairs || pairs.Count == 0 || !pairs.All(Pair) ||
            value["ArmyDescriptors"] is not JsonObject armies ||
            !armies.All(row => int.TryParse(row.Key, out int id) && id > 0 && Army(row.Value, id)) ||
            !Integer(value["PairIndex"], 0, pairs.Count - 1) ||
            !Integer(value["ReportWord20"], 0, ushort.MaxValue) ||
            !Integer(value["SampleDay"], int.MinValue, int.MaxValue) ||
            !Integer(value["SampleSalt"], int.MinValue, int.MaxValue) ||
            !Integer(state["NativePeriodContinuation"]?["Cursor"]?["StageIndex"], 13, 13) ||
            !(Integer(state["ArmyDefeatContinuation"]?["SettlementIndex"], -1, -1) ||
              version >= 181 && Integer(state["BaseCaptureContinuation"]?["SettlementIndex"], -1, -1))) Invalid();
        AutomaticTacticExecutionSaveFields.Migrate((JsonObject)node, version);
        BaseAutomaticTacticExecutionSaveFields.Migrate((JsonObject)node, version);
    }

    private static bool Participant(JsonNode? node) => node is JsonObject value &&
        (Integer(value["Kind"], 5, 6) || Integer(value["Kind"], 11, 11) || Integer(value["Kind"], 27, 27)) &&
        Integer(value["PhysicalSlot"], 0, int.MaxValue) &&
        value.ContainsKey("UnitId") && (value["UnitId"] is null || Integer(value["UnitId"], 1, int.MaxValue)) &&
        Integer(value["Morale"], 0, byte.MaxValue) && Integer(value["Troops"], 0, ushort.MaxValue) &&
        Integer(value["HexId"], 0, 48400) && Integer(value["ForceSlot"], 0, 51) &&
        Integer(value["AcceptedCount"], 0, byte.MaxValue);

    private static bool Pair(JsonNode? node) => node is JsonObject value &&
        Integer(value["GenerationOrdinal"], 0, int.MaxValue) &&
        Participant(value["Actor"]) && Participant(value["Target"]);

    private static bool Army(JsonNode? node, int id) => node is JsonObject value &&
        Integer(value["ArmyId"], id, id) && Integer(value["NativePoolSlot"], 0, ushort.MaxValue) &&
        Integer(value["OfficerId"], 0, 6000) && Integer(value["CappedMorale"], 0, byte.MaxValue) &&
        Integer(value["Troops"], 0, ushort.MaxValue) && Integer(value["HexId"], 0, 48400) &&
        Integer(value["ForceId"], 0, 51) && Integer(value["AcceptedCount"], 0, byte.MaxValue);

    private static bool Integer(JsonNode? node, int min, int max) => node is JsonValue field &&
        field.TryGetValue<int>(out int value) && value >= min && value <= max;
    private static void Invalid() => throw new InvalidDataException("Saved immediate combat snapshot is missing or invalid.");
}
