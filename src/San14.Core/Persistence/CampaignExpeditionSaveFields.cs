using System.Text.Json.Nodes;

namespace San14.Core;

internal static class CampaignExpeditionSaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 158)
        {
            if (state["CampaignExpeditions"] is not null)
                throw new InvalidDataException("Legacy save contains a future expedition controller.");
            state["CampaignExpeditions"] = null;
            return;
        }
        if (!state.ContainsKey("CampaignExpeditions"))
            throw new InvalidDataException("Saved expedition controller knowledge is missing.");
        if (state["CampaignExpeditions"] is not JsonNode saved) return;
        if (saved is not JsonObject owner || owner["Expeditions"] is not JsonObject expeditions ||
            !owner.ContainsKey("ProvinceSequences"))
            throw new InvalidDataException("Saved expedition controller is incomplete.");
        foreach (var pair in expeditions)
        {
            if (!int.TryParse(pair.Key, out int id) || id is < 1 or > 50 ||
                pair.Value is not JsonObject row || !Integer(row, "Id", id, id) ||
                !Integer(row, "ProvinceId", 1, 22) || !Integer(row, "SequenceNumber", 0, 100) ||
                !Integer(row, "StartYear", 0, 350) || !Integer(row, "StartMonth", 1, 12) ||
                row["IsWithdrawing"] is not JsonValue withdrawing || !withdrawing.TryGetValue<bool>(out _))
                throw new InvalidDataException("Saved expedition is incomplete or invalid.");
        }
        if (owner["ProvinceSequences"] is not JsonNode sequences) return;
        if (sequences is not JsonObject counters)
            throw new InvalidDataException("Saved province expedition counters are invalid.");
        foreach (var pair in counters)
            if (!int.TryParse(pair.Key, out int province) || province is < 0 or > 22 ||
                pair.Value is not JsonValue value || !value.TryGetValue<int>(out int number) || number is < 0 or > 100)
                throw new InvalidDataException("Saved province expedition sequence is invalid.");
    }

    private static bool Integer(JsonObject row, string name, int minimum, int maximum) =>
        row[name] is JsonValue field && field.TryGetValue<int>(out int value) && value >= minimum && value <= maximum;
}
