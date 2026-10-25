using System.Text.Json.Nodes;

namespace San14.Core;

internal static class ApplicationCommandAccountingSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 169 && state["ApplicationCommandAccounting"] is not null)
            throw new InvalidDataException("Legacy world contains future command-book accounting.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        const string key = "ApplicationCommandAccounting";
        if (version < 169)
        {
            state[key] = null;
            return;
        }
        if (!state.ContainsKey(key) || state[key] is JsonNode node &&
            (node is not JsonObject accounting || !Counter(accounting, "CommandBooksUsed") ||
             !Counter(accounting, "CommandBooksGranted")))
            throw new InvalidDataException("Saved command-book accounting knowledge is missing or invalid.");
    }

    private static bool Counter(JsonObject owner, string key) => owner.ContainsKey(key) &&
        (owner[key] is null || owner[key] is JsonValue value && value.TryGetValue<uint>(out _));
}
