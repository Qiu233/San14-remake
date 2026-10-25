using System.Text.Json.Nodes;

namespace San14.Core;

internal static class ForeignRelationsSaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 164)
        {
            if (state["ForeignCountryRelations"] is not null)
                throw new InvalidDataException("Legacy save contains future foreign country relations.");
            state["ForeignCountryRelations"] = null;
            return;
        }
        if (!state.ContainsKey("ForeignCountryRelations"))
            throw new InvalidDataException("Saved foreign country relation knowledge is missing.");
        if (state["ForeignCountryRelations"] is not JsonNode saved) return;
        if (saved is not JsonObject owner || owner["Countries"] is not JsonObject countries || countries.Count != 5)
            throw new InvalidDataException("Saved foreign countries are incomplete.");
        foreach (var pair in countries)
        {
            if (!int.TryParse(pair.Key, out int id) || id is < 0 or > 4 ||
                pair.Value is not JsonObject country || country["Id"] is not JsonValue idValue ||
                !idValue.TryGetValue<int>(out int storedId) || storedId != id ||
                country["Name"] is not JsonValue name || !name.TryGetValue<string>(out _) ||
                country["Relations"] is not JsonObject relations || relations.Count != 5)
                throw new InvalidDataException("Saved foreign country is invalid.");
            foreach (var relation in relations)
                if (!int.TryParse(relation.Key, out int peer) || peer is < 0 or > 4 ||
                    relation.Value is not JsonValue relationValue || !relationValue.TryGetValue<byte>(out byte value) || value > 2)
                    throw new InvalidDataException("Saved foreign country relation is invalid.");
        }
    }
}
