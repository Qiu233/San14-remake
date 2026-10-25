using System.Text.Json.Nodes;

namespace San14.Core;

internal static class NormalTurnEconomySaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 167) return;
        if (state["FacilityFoodReduction"] is not null || state["TerritoryExpansion"] is not null ||
            state["Cities"] is JsonObject cities && cities.Any(pair => pair.Value?["TurnIncomeTotals"] is not null) ||
            state["Areas"] is JsonObject areas && areas.Any(pair => pair.Value?["TurnIncomeTotals"] is not null) ||
            state["NativeArmySimulation"]?["Forces"] is JsonObject forces &&
            forces.Any(pair => pair.Value?["BestAdministrationLevelSum"] is not null ||
                pair.Value?["AssociationIntelligenceGain"] is not null || pair.Value?["AssociationGoldExpense"] is not null))
            throw new InvalidDataException("Legacy world contains future normal Turn economy state.");
        if (state["CampaignEffectRequests"]?["PendingBatches"] is JsonArray batches)
            foreach (var batch in batches.OfType<JsonObject>())
                if (Integer(batch["Source"], (int)CampaignEffectSource.MonthlyMaintenance, (int)CampaignEffectSource.MonthlyMaintenance) &&
                    batch["Requests"] is JsonArray requests && requests.OfType<JsonObject>().Any(request => request["TalentForceExperiencePresentation"] is not null))
                    throw new InvalidDataException("Legacy monthly presentation contains a future force experience request.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        MigrateFoodReduction(state, version);
        MigrateTerritoryExpansion(state, version);
        if (state["Cities"] is JsonObject cities)
            foreach (var pair in cities)
            {
                if (pair.Value is not JsonObject city) throw new InvalidDataException("Saved City is invalid.");
                MigrateTotals(city, version, false);
            }
        if (state["Areas"] is JsonObject areas)
            foreach (var pair in areas)
            {
                if (pair.Value is not JsonObject area) throw new InvalidDataException("Saved Area is invalid.");
                MigrateTotals(area, version, true);
            }
        if (state["NativeArmySimulation"]?["Forces"] is not JsonObject forces) return;
        foreach (var pair in forces)
        {
            if (pair.Value is not JsonObject force) throw new InvalidDataException("Saved native force is invalid.");
            const string key = "BestAdministrationLevelSum";
            if (version < 167)
            {
                force[key] = null;
                force["AssociationIntelligenceGain"] = null;
                force["AssociationGoldExpense"] = null;
                continue;
            }
            if (!force.ContainsKey(key) || force[key] is JsonNode saved &&
                (saved is not JsonValue value || !value.TryGetValue<uint>(out _)))
                throw new InvalidDataException("Saved best administration-level total is missing or invalid.");
            if (!NullableInteger(force, "AssociationIntelligenceGain", int.MinValue, int.MaxValue) ||
                !NullableInteger(force, "AssociationGoldExpense", int.MinValue, int.MaxValue))
                throw new InvalidDataException("Saved association economy totals are missing or invalid.");
        }
    }

    private static void MigrateFoodReduction(JsonObject state, int version)
    {
        const string key = "FacilityFoodReduction";
        if (version < 167) { state[key] = null; return; }
        if (!state.ContainsKey(key) || state[key] is JsonNode saved &&
            (saved is not JsonObject reduction || reduction["PercentByHex"] is not JsonArray values ||
             values.Count != 48400 || values.Any(value => !Integer(value, 0, 100))))
            throw new InvalidDataException("Saved facility food reduction is missing or invalid.");
    }

    private static void MigrateTotals(JsonObject owner, int version, bool area)
    {
        const string key = "TurnIncomeTotals";
        if (version < 167) { owner[key] = null; return; }
        int min = area ? 0 : int.MinValue, max = area ? ushort.MaxValue : int.MaxValue;
        if (!owner.ContainsKey(key) || owner[key] is JsonNode saved &&
            (saved is not JsonObject totals || !Integer(totals["Gold"], min, max) ||
             !Integer(totals["Food"], min, max) || !Integer(totals["RecruitedTroops"], min, max)))
            throw new InvalidDataException("Saved Turn income totals are missing or invalid.");
    }

    private static void MigrateTerritoryExpansion(JsonObject state, int version)
    {
        const string key = "TerritoryExpansion";
        if (version < 167) { state[key] = null; return; }
        if (!state.ContainsKey(key) || state[key] is JsonNode saved &&
            (saved is not JsonObject expansion || expansion["PendingAssignments"] is not JsonObject assignments ||
             assignments.Any(pair => !int.TryParse(pair.Key, out int hex) || hex is < 0 or >= 48400 || !Integer(pair.Value, 0, 51))))
            throw new InvalidDataException("Saved territory expansion assignments are missing or invalid.");
    }

    private static bool NullableInteger(JsonObject owner, string key, int min, int max) =>
        owner.ContainsKey(key) && (owner[key] is null || Integer(owner[key], min, max));
    private static bool Integer(JsonNode? field, int min, int max) => field is JsonValue value &&
        value.TryGetValue<int>(out int number) && number >= min && number <= max;
}
