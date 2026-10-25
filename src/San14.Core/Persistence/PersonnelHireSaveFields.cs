using System.Text.Json.Nodes;

namespace San14.Core;

internal static class PersonnelHireSaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 160)
        {
            if (state["PersonnelHire"] is not null)
                throw new InvalidDataException("Legacy save contains a future personnel hire manager.");
            state["PersonnelHire"] = null;
            return;
        }

        if (!state.ContainsKey("PersonnelHire"))
            throw new InvalidDataException("Saved personnel hire manager knowledge is missing.");
        if (state["PersonnelHire"] is not JsonNode saved) return;
        if (saved is not JsonObject owner || !Integer(owner["RemainingSlots"], 0, 200) ||
            owner["Forces"] is not JsonObject forces || owner["Contracts"] is not JsonObject contracts ||
            !owner.ContainsKey("Templates") || !owner.ContainsKey("GenerationPools") ||
            !owner.ContainsKey("PendingReleaseOfficerIds"))
            throw new InvalidDataException("Saved personnel hire manager is incomplete.");

        foreach (var pair in forces)
            if (!int.TryParse(pair.Key, out int force) || force is < 0 or > 51 ||
                pair.Value is not JsonObject roster ||
                !OfficerList(roster["AvailableTemplateOfficerIds"], 1, 520) ||
                !OfficerList(roster["HiredOfficerIds"], 4201, 4400))
                throw new InvalidDataException("Saved personnel hire roster is invalid.");

        foreach (var pair in contracts)
            if (!int.TryParse(pair.Key, out int officer) || officer is < 4201 or > 4400 ||
                pair.Value is not JsonObject contract || !Integer(contract["OfficerId"], officer, officer) ||
                !Integer(contract["TemplateOfficerId"], 1, 520) || !Integer(contract["HiredYear"], 0, 350))
                throw new InvalidDataException("Saved personnel hire contract is invalid.");

        if (owner["PendingReleaseOfficerIds"] is JsonNode pending && !OfficerList(pending, 4201, 4400))
            throw new InvalidDataException("Saved personnel hire release queue is invalid.");
        if (owner["GenerationPools"] is JsonNode savedPools)
        {
            if (savedPools is not JsonObject pools || !OfficerList(pools["TypeCodes"], 1, 4) ||
                !OfficerList(pools["FormationIds"], int.MinValue, int.MaxValue) ||
                !Pools(pools["Traits"], traits: true) || !Pools(pools["Tactics"], traits: false))
                throw new InvalidDataException("Saved personnel hire generation pools are invalid.");
        }

        if (owner["Templates"] is not JsonNode savedTemplates) return;
        if (savedTemplates is not JsonObject templates)
            throw new InvalidDataException("Saved personnel hire templates are invalid.");
        foreach (var pair in templates)
            if (!int.TryParse(pair.Key, out int template) || template is < 1 or > 520 ||
                pair.Value is not JsonObject entry || !Integer(entry["Id"], template, template) ||
                !entry.ContainsKey("PreviousForceId") ||
                entry["PreviousForceId"] is JsonNode previous && !Integer(previous, 0, 51) ||
                entry["Person"] is not JsonObject person || !Integer(person["Id"], int.MinValue, int.MaxValue) ||
                person["Name"] is not JsonValue name || !name.TryGetValue<string>(out _) ||
                !person.ContainsKey("ForceId") || !person.ContainsKey("LocationId") ||
                person["Deployed"] is not JsonValue deployed || !deployed.TryGetValue<bool>(out _))
                throw new InvalidDataException("Saved personnel hire template is incomplete.");
    }

    private static bool Pools(JsonNode? node, bool traits)
    {
        if (node is not JsonArray list || list.Count != (traits ? 8 : 4)) return false;
        var identities = new HashSet<(int Kind, int? Sex)>();
        foreach (var item in list)
        {
            if (item is not JsonObject pool || !Integer(pool["KindId"], 1, 4) ||
                !pool.ContainsKey("Sex") || !OfficerList(pool["Candidates"], int.MinValue, int.MaxValue)) return false;
            int? sex = null;
            if (traits)
            {
                if (!Integer(pool["Sex"], 0, 1)) return false;
                sex = pool["Sex"]!.GetValue<int>();
            }
            else if (pool["Sex"] is not null) return false;
            if (!identities.Add((pool["KindId"]!.GetValue<int>(), sex))) return false;
        }
        return true;
    }

    private static bool OfficerList(JsonNode? node, int minimum, int maximum) =>
        node is JsonArray list && list.All(value => Integer(value, minimum, maximum));

    private static bool Integer(JsonNode? node, int minimum, int maximum) =>
        node is JsonValue field && field.TryGetValue<int>(out int value) && value >= minimum && value <= maximum;
}
