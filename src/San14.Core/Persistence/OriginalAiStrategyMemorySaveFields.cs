using System.Globalization;
using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Bounded format checks at the save boundary. Normal information
/// queries never scan, recompute or upgrade these phase-owned facts.</summary>
internal static class OriginalAiStrategyMemorySaveFields
{
    private const string Field = "OriginalAiStrategyMemory";

    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 193 && state.ContainsKey(Field))
            throw new InvalidDataException("Legacy world contains future planning memory.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 193)
        {
            state[Field] = null;
            return;
        }
        if (version == 193)
        {
            // Check the old shape before adding unknown fields. A legacy
            // payload cannot smuggle future fields through this migration.
            ValidateWorld(state, legacy193: true);
            if (state[Field] is JsonObject owner)
            {
                foreach (var pair in (JsonObject)owner["Footholds"]!)
                {
                    var row = (JsonObject)pair.Value!;
                    row["ThreateningFootholdLocationId"] = null;
                    row["FriendlyRouteAreaId"] = null;
                }
                foreach (var pair in (JsonObject)owner["Forces"]!)
                    ((JsonObject)pair.Value!)["DeepInteriorLocationIds"] = null;
            }
        }
        if (version < 195)
        {
            if (version == 194) ValidateWorld(state, legacy193: false, legacy194: true);
            if (state[Field] is JsonObject owner)
                foreach (var pair in (JsonObject)owner["Footholds"]!)
                    ((JsonObject)pair.Value!)["TransportTroopCeiling"] = null;
        }
        ValidateWorld(state);
    }

    internal static void ValidateWorld(JsonObject state)
        => ValidateWorld(state, legacy193: false);

    private static void ValidateWorld(JsonObject state, bool legacy193, bool legacy194 = false)
    {
        if (!state.ContainsKey(Field)) throw Invalid();
        if (state[Field] is not JsonNode saved) return;
        if (saved is not JsonObject owner || !Keys(owner, "Footholds", "Forces") ||
            owner["Footholds"] is not JsonObject footholds || owner["Forces"] is not JsonObject forces)
            throw Invalid();
        foreach (var pair in footholds)
        {
            if (!Identity(pair.Key, 61) || pair.Value is not JsonObject row ||
                !(legacy193 ? Keys(row, "FoodNeed", "GoldNeed", "GoldForecast", "FoodForecast") :
                    legacy194 ? Keys(row, "FoodNeed", "GoldNeed", "GoldForecast", "FoodForecast",
                        "ThreateningFootholdLocationId", "FriendlyRouteAreaId") :
                    Keys(row, "FoodNeed", "GoldNeed", "GoldForecast", "FoodForecast",
                        "ThreateningFootholdLocationId", "FriendlyRouteAreaId", "TransportTroopCeiling")) ||
                !Quantity(row["FoodNeed"]) || !Quantity(row["GoldNeed"]) ||
                !Quantity(row["GoldForecast"]) || !Quantity(row["FoodForecast"]) ||
                (!legacy193 && (!NullableIdentity(row["ThreateningFootholdLocationId"], 61) ||
                    !NullableIdentity(row["FriendlyRouteAreaId"], 500))) ||
                (!legacy193 && !legacy194 && !Quantity(row["TransportTroopCeiling"]))) throw Invalid();
        }
        foreach (var pair in forces)
        {
            if (!Identity(pair.Key, 51) || pair.Value is not JsonObject row ||
                !(legacy193 ? Keys(row, "FrontierLocationIds", "RearLocationIds") :
                    Keys(row, "FrontierLocationIds", "RearLocationIds", "DeepInteriorLocationIds")) ||
                !Locations(row["FrontierLocationIds"]) || !Locations(row["RearLocationIds"]) ||
                (!legacy193 && row["DeepInteriorLocationIds"] is JsonNode deep && !Locations(deep))) throw Invalid();
        }
        //Snapshot identities can be stale after later ownership changes.
        //Preserve list order and duplicates; no current-world reconstruction,
        //frontier/rear classification or ownership proof occurs while loading.
    }

    private static bool Keys(JsonObject row, params string[] fields) =>
        row.Count == fields.Length && fields.All(row.ContainsKey);
    private static bool Identity(string text, int maximum) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) &&
        value >= 1 && value <= maximum && value.ToString(CultureInfo.InvariantCulture) == text;
    private static bool Quantity(JsonNode? node) => node is null ||
        node is JsonValue value && value.TryGetValue<int>(out _);
    private static bool NullableIdentity(JsonNode? node, int maximum) => node is null ||
        node is JsonValue value && value.TryGetValue<int>(out int id) && id >= 0 && id <= maximum;
    private static bool Locations(JsonNode? node) => node is JsonArray items && items.All(item =>
        item is JsonValue value && value.TryGetValue<int>(out int id) && id is >= 1 and <= 61);
    private static InvalidDataException Invalid() =>
        new("Saved strategy planning memory is missing or invalid.");
}
