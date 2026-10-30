using System.Text.Json.Nodes;

namespace San14.Core;

internal static class OriginalAiStrategyScheduleSaveFields
{
    private const string Field = "OriginalAiStrategySchedule";
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 214 && state.ContainsKey(Field)) throw Invalid();
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 214) state[Field] = null;
        ValidateWorld(state);
    }

    internal static void ValidateWorld(JsonObject state)
    {
        if (!state.ContainsKey(Field)) throw Invalid();
        if (state[Field] is null) return;
        if (state[Field] is not JsonObject row || row.Count != 3 ||
            !row.ContainsKey("ForceIds") || !row.ContainsKey("NextForceIndex") ||
            !row.ContainsKey("AwaitingPlayerCompletion") || row["ForceIds"] is not JsonArray ids ||
            row["NextForceIndex"] is not JsonValue index || !index.TryGetValue<int>(out int next) ||
            next < 0 || next > ids.Count || row["AwaitingPlayerCompletion"] is not JsonValue waiting ||
            !waiting.TryGetValue<bool>(out bool awaiting) || awaiting && next == 0) throw Invalid();
        // The original linked list may retain repetition. Do not sort or
        // deduplicate its persisted encounter order.
        foreach (var node in ids)
            if (node is not JsonValue value || !value.TryGetValue<int>(out int id) || id is < 0 or > 51)
                throw Invalid();
    }

    private static InvalidDataException Invalid() => new("Saved original-policy Strategy queue is missing or invalid.");
}
