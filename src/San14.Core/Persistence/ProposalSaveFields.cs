using System.Text.Json.Nodes;

namespace San14.Core;

internal static class ProposalSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 181 && state["Proposals"] is not null)
            throw new InvalidDataException("Legacy world contains future proposal state.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        const string key = "Proposals";
        if (version < 181) { state[key] = null; return; }
        if (!state.ContainsKey(key)) throw Invalid();
        if (state[key] is not JsonNode node) return;
        if (node is not JsonObject board || board.Count != 1 ||
            board["Slots"] is not JsonArray slots || slots.Count != ProposalBoard.SlotCount)
            throw Invalid();
        for (int slot = 0; slot < slots.Count; slot++)
        {
            if (slots[slot] is not JsonObject entry || entry.Count != 5 ||
                !Integer(entry["SlotId"], slot, slot) ||
                !Integer(entry["Kind"], 0, 24) || !Integer(entry["Status"], 0, 4) ||
                !Integer(entry["ProposerPersonId"], 0, int.MaxValue) ||
                entry["Arguments"] is not JsonObject arguments || arguments.Count != 5 ||
                !Enumerable.Range(0, 5).All(index => Integer(arguments["Parameter" + index], int.MinValue, int.MaxValue)))
                throw Invalid();
        }
    }

    private static bool Integer(JsonNode? node, int min, int max) =>
        node is JsonValue value && value.TryGetValue<int>(out int number) && number >= min && number <= max;
    private static InvalidDataException Invalid() => new("Saved proposal state is missing or invalid.");
}
