using System.Text.Json.Nodes;

namespace San14.Core;

internal static class ApplicationCommandAccountingSaveFields
{
    private static readonly string[] TacticCounters =
    ["SingleLinkedTacticCount", "DoubleLinkedTacticCount", "TripleLinkedTacticCount", "LargeLinkedTacticCount", "ManualTacticUseCount"];
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 169 && state["ApplicationCommandAccounting"] is not null)
            throw new InvalidDataException("Legacy world contains future command-book accounting.");
        if (version < 181 && state["ApplicationCommandAccounting"] is JsonObject accounting &&
            TacticCounters.Any(key => accounting[key] is not null))
            throw new InvalidDataException("Legacy accounting contains future tactic counters.");
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
        if (state[key] is not JsonObject owner) return;
        foreach (string counter in TacticCounters)
        {
            if (version < 181) owner[counter] = null;
            else if (!Counter(owner, counter, 1000))
                throw new InvalidDataException("Saved tactic accounting knowledge is missing or invalid.");
        }
    }

    private static bool Counter(JsonObject owner, string key, uint maximum = uint.MaxValue) => owner.ContainsKey(key) &&
        (owner[key] is null || owner[key] is JsonValue value && value.TryGetValue<uint>(out uint number) && number <= maximum);
}
