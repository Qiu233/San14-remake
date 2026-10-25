using System.Text.Json.Nodes;

namespace San14.Core;

internal static class ArmyDistrictSaveFields
{
    // Inspect the external document before historical migrations construct
    // today's owner types with their fresh reset defaults.
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 161) return;
        if (state["ArmyLifecycle"]?["ArmyCautions"]?["GroupDistrictIds"] is not null ||
            state["ArmyLifecycle"]?["ArmyGroups"]?["CautionTargets"] is not null)
            throw new InvalidDataException("Legacy save contains future army district associations.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (state["ArmyLifecycle"] is not JsonObject lifecycle) return;
        if (lifecycle["ArmyCautions"] is JsonObject cautions)
        {
            if (version < 161) cautions["GroupDistrictIds"] = null;
            else
            {
                if (!cautions.ContainsKey("GroupDistrictIds"))
                    throw new InvalidDataException("Saved caution district knowledge is missing.");
                if (cautions["GroupDistrictIds"] is JsonNode saved)
                {
                    if (saved is not JsonObject districts)
                        throw new InvalidDataException("Saved caution districts are invalid.");
                    foreach (var pair in districts)
                        if (!Identity(pair.Key) || pair.Value is JsonNode node &&
                            (node is not JsonValue field || !field.TryGetValue<int>(out int district) || district is < 0 or > 51))
                            throw new InvalidDataException("Saved caution district is invalid.");
                }
            }
        }
        if (lifecycle["ArmyGroups"] is JsonObject groups)
        {
            if (version < 161) groups["CautionTargets"] = null;
            else
            {
                if (!groups.ContainsKey("CautionTargets"))
                    throw new InvalidDataException("Saved army group caution target knowledge is missing.");
                if (groups["CautionTargets"] is JsonNode saved)
                {
                    if (saved is not JsonObject targets)
                        throw new InvalidDataException("Saved army group caution targets are invalid.");
                    foreach (var pair in targets)
                        if (!Identity(pair.Key) || pair.Value is not JsonObject target ||
                            target["IsKnown"] is not JsonValue known || !known.TryGetValue<bool>(out _) ||
                            !target.ContainsKey("CautionGroupId") || target["CautionGroupId"] is JsonNode node &&
                            (node is not JsonValue field || !field.TryGetValue<int>(out int id) || id < 0))
                            throw new InvalidDataException("Saved army group caution target is invalid.");
                }
            }
        }
    }

    private static bool Identity(string text) => int.TryParse(text, out int id) && id >= 0;
}
