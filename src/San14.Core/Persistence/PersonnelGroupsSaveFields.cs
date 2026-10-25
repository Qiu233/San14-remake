using System.Text.Json.Nodes;

namespace San14.Core;

internal static class PersonnelGroupsSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 170 && state["PersonnelGroups"] is not null)
            throw new InvalidDataException("Legacy world contains future personnel groups.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        const string key = "PersonnelGroups";
        if (version < 170)
        {
            state[key] = null;
            return;
        }
        if (!state.ContainsKey(key) || state[key] is JsonNode node &&
            (node is not JsonObject owner || owner["Groups"] is not JsonArray groups || groups.Count != 10 ||
             groups.Any(group => group is not JsonArray members || members.Count > 50 || members.Any(member =>
                 member is not JsonValue value || !value.TryGetValue<int>(out int id) || id is < 1 or > 6000))))
            throw new InvalidDataException("Saved personnel groups are missing or invalid.");
    }
}
