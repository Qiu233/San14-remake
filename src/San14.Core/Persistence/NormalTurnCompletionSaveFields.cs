using System.Text.Json.Nodes;

namespace San14.Core;

internal static class NormalTurnCompletionSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 168) return;
        if (state["PlayerDistrictResourceHistory"] is not null || state["ChallengeServiceTurnStopPending"] is not null ||
            state["NativeArmySimulation"]?["World"]?["LeadingDoctrineYearCount"] is not null ||
            state["NativeArmySimulation"]?["Forces"] is JsonObject forces && forces.Any(pair =>
                pair.Value?["BestExceptionalOfficerCount"] is not null || pair.Value?["DevelopmentWarningFlags"] is not null) ||
            Integer(state["CampaignTurnStop"]?["Reason"], (int)CampaignTurnStopReason.ChallengeService, int.MaxValue))
            throw new InvalidDataException("Legacy world contains future normal Turn completion state.");
        foreach (var request in Requests(state))
            if (request["NormalTurnFoodWarningPresentation"] is not null ||
                request["NormalTurnForceResourceNotice"] is not null ||
                Integer(request["MapObjectPresentation"]?["Event"], (int)MapObjectPresentationEvent.RelationVisibilityAttach, int.MaxValue) ||
                Integer(request["MonthlyMaintenance"]?["Operation"], (int)MonthlyMaintenanceOperation.FinalOrderSceneEnter, int.MaxValue) ||
                Integer(request["TalentCommandPresentation"]?["SoundId"], 18, 18))
                throw new InvalidDataException("Legacy request contains future normal Turn feedback.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        NullableField(state, "ChallengeServiceTurnStopPending", version, node => node is JsonValue value && value.TryGetValue<bool>(out _));
        NullableField(state, "PlayerDistrictResourceHistory", version, node => node is JsonObject histories && histories.All(pair =>
            int.TryParse(pair.Key, out int district) && district is >= 1 and <= 8 && pair.Value is JsonObject row &&
            new[] { "Gold", "Food", "Troops", "MilitaryIntelligence" }.All(key => row[key] is JsonArray values &&
                values.Count == 10 && values.All(value => Integer(value, int.MinValue, int.MaxValue)))));
        if (state["NativeArmySimulation"]?["World"] is JsonObject world)
            NullableField(world, "LeadingDoctrineYearCount", version, node => Integer(node, 0, byte.MaxValue));
        if (state["NativeArmySimulation"]?["Forces"] is JsonObject forces)
            foreach (var pair in forces)
            {
                if (pair.Value is not JsonObject force) throw new InvalidDataException("Saved native force is invalid.");
                NullableField(force, "BestExceptionalOfficerCount", version, node => Integer(node, 0, ushort.MaxValue));
                NullableField(force, "DevelopmentWarningFlags", version, node => Integer(node, 0, byte.MaxValue));
            }
        foreach (var request in Requests(state))
        {
            NullableField(request, "NormalTurnFoodWarningPresentation", version, node => node is JsonObject warning &&
                warning["WarningBaseLocationIds"] is JsonArray ids && ids.All(id => Integer(id, 1, 61)) &&
                warning["TextAtSource"] is JsonValue text && text.TryGetValue<string>(out _));
            NullableField(request, "NormalTurnForceResourceNotice", version, node => node is JsonObject notice &&
                Integer(notice["ForceId"], 1, 45) && Integer(notice["BaseLocationId"], 1, 61) &&
                Integer(notice["ResourceKind"], 0, int.MaxValue) && Integer(notice["MessageRoot"], 0, int.MaxValue) &&
                Integer(notice["Amount"], int.MinValue, int.MaxValue) && Integer(notice["CurrentAmount"], int.MinValue, int.MaxValue));
        }
    }

    private static IEnumerable<JsonObject> Requests(JsonObject state)
    {
        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) yield break;
        foreach (var batch in batches.OfType<JsonObject>())
            if (batch["Requests"] is JsonArray requests)
                foreach (var request in requests.OfType<JsonObject>()) yield return request;
    }

    private static void NullableField(JsonObject owner, string key, int version, Func<JsonNode, bool> valid)
    {
        if (version < 168) { owner[key] = null; return; }
        if (!owner.ContainsKey(key) || owner[key] is JsonNode saved && !valid(saved))
            throw new InvalidDataException($"Saved {key} knowledge is missing or invalid.");
    }
    private static bool Integer(JsonNode? node, int min, int max) => node is JsonValue value &&
        value.TryGetValue<int>(out int number) && number >= min && number <= max;
}
