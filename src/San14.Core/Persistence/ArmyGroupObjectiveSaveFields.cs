using System.Globalization;
using System.Text.Json.Nodes;

namespace San14.Core;

internal static class ArmyGroupObjectiveSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 215 && state["ArmyLifecycle"]?["ArmyGroups"]?["Objectives"] is JsonObject oldObjectives)
            foreach (var item in oldObjectives)
                if (item.Value?["Purpose"] is JsonObject purpose &&
                    (Integer(purpose["Target"]?["Kind"], out int targetKind) && targetKind >= (int)ArmyGroupPurposeTargetKind.MapObject ||
                     Integer(purpose["Kind"], out int kind) && kind is not (0 or 1 or 7 or 12 or 13 or 17 or 18)))
                    throw new InvalidDataException("Legacy world contains future army group purpose families.");
        if (version < 195 && state["ArmyLifecycle"]?["ArmyGroups"] is JsonObject groups && groups.ContainsKey("Objectives"))
            throw new InvalidDataException("Legacy world contains future army group objectives.");
    }
    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 195 && state["ArmyLifecycle"]?["ArmyGroups"] is JsonObject groups)
            groups["Objectives"] = null;
        ValidateWorld(state);
    }
    internal static void ValidateWorld(JsonObject state)
    {
        if (state["ArmyLifecycle"]?["ArmyGroups"] is not JsonObject groups) return;
        if (!groups.ContainsKey("Objectives")) throw Invalid();
        if (groups["Objectives"] is not JsonNode saved) return;
        if (saved is not JsonObject objectives || !Integer(state["MapWidth"], out int width) ||
            !Integer(state["MapHeight"], out int height) || width <= 0 || height <= 0 ||
            (long)width * height > int.MaxValue) throw Invalid();
        int hexCount = width * height;
        foreach (var pair in objectives)
        {
            if (!int.TryParse(pair.Key, NumberStyles.None, CultureInfo.InvariantCulture, out int group) ||
                group is < 0 or > 500 || group.ToString(CultureInfo.InvariantCulture) != pair.Key ||
                pair.Value is not JsonObject row || row.Count != 2 || !row.ContainsKey("Purpose") ||
                !row.ContainsKey("RelayHexIds") || !Purpose(row["Purpose"], hexCount) ||
                row["RelayHexIds"] is JsonNode plan && (plan is not JsonArray hexes || hexes.Count > 10 ||
                    hexes.Any(hex => !Integer(hex, out int id) || id < 0 || id >= hexCount))) throw Invalid();
        }
    }
    private static bool Purpose(JsonNode? node, int hexCount)
    {
        if (node is null) return true;
        return node is JsonObject value && value.Count == 2 && Integer(value["Kind"], out int kind) &&
            value["Target"] is JsonObject target && target.Count == 2 && Integer(target["Kind"], out int targetKind) &&
            Integer(target["Id"], out int id) && ArmyGroupObjectives.IsPurposeValid(
                new((ArmyGroupPurposeKind)kind, new((ArmyGroupPurposeTargetKind)targetKind, id)), hexCount);
    }
    private static bool Integer(JsonNode? node, out int number)
    {
        number = 0;
        return node is JsonValue value && value.TryGetValue<int>(out number);
    }
    private static InvalidDataException Invalid() => new("Saved army group objectives are missing or invalid.");
}
