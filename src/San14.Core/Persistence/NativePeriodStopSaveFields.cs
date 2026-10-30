using System.Text.Json.Nodes;

namespace San14.Core;

internal static class NativePeriodStopSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 181 && state["NativePeriodStop"] is not null)
            throw new InvalidDataException("Legacy world contains a future terminal Army period checkpoint.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        const string key = "NativePeriodStop";
        if (version < 181) { state[key] = null; return; }
        if (!state.ContainsKey(key)) throw Invalid();
        if (state[key] is not JsonNode node) return;
        if (node is not JsonObject value || value.Count != 3 ||
            value["CalendarCycleOrdinal"] is not JsonValue ordinal || !ordinal.TryGetValue<long>(out long at) || at < 0 ||
            value["StageIndex"] is not JsonValue stage || !stage.TryGetValue<byte>(out byte index) || index is not (5 or 6 or 13 or 25) ||
            value["Date"] is not JsonObject date || date.Count != 4 ||
            !Integer(date["Year"], short.MinValue, short.MaxValue) || !Integer(date["Month"], 1, 12) ||
            !Integer(date["Day"], 1, 30) || !Integer(date["Hour"], 0, 22)) throw Invalid();
    }

    private static bool Integer(JsonNode? node, int min, int max) => node is JsonValue value &&
        value.TryGetValue<int>(out int number) && number >= min && number <= max;
    private static InvalidDataException Invalid() => new("Saved terminal Army period checkpoint is missing or invalid.");
}
