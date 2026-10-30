using System.Text.Json.Nodes;

namespace San14.Core;

internal static class FacilityEffectsPresentationSaveFields
{
    private static IEnumerable<JsonObject> Requests(JsonObject state) =>
        ((state["CampaignEffectRequests"]?["PendingBatches"] as JsonArray)?.OfType<JsonObject>()
            .SelectMany(batch => (batch["Requests"] as JsonArray)?.OfType<JsonObject>() ?? []) ?? []);

    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 184) return;
        if (Requests(state).Any(request => request["FacilityStatisticPresentation"] is not null ||
            request["FacilitySupportAura"] is not null || request["FacilityProjectile"]?["DefinitionId"] is not null ||
            request["MilitaryMusicPulse"]?["SourceView"] is not null || request["MilitaryMusicPulse"]?["Positive"] is not null ||
            request["ArmyTrapPresentation"]?["SourceView"] is not null ||
            request["ArmyTrapPresentation"]?["TargetReceiverId"] is not null ||
            request["ArmyFacilityDamagePresentation"]?["TargetReceiverId"] is not null))
            throw new InvalidDataException("Legacy world contains future facility presentation operands.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        foreach (var request in Requests(state))
        {
            if (version < 184)
            {
                request["FacilityStatisticPresentation"] = null;
                request["FacilitySupportAura"] = null;
                if (request["FacilityProjectile"] is JsonObject projectile) projectile["DefinitionId"] = null;
                if (request["MilitaryMusicPulse"] is JsonObject pulse)
                {
                    pulse["SourceView"] = null;
                    pulse["Positive"] = null;
                }
                if (request["ArmyTrapPresentation"] is JsonObject trap)
                {
                    trap["SourceView"] = null;
                    trap["TargetReceiverId"] = null;
                }
                if (request["ArmyFacilityDamagePresentation"] is JsonObject damage) damage["TargetReceiverId"] = null;
            }
            else
            {
                Require(request, "FacilityStatisticPresentation", "FacilitySupportAura");
                if (request["FacilityProjectile"] is JsonObject projectile) Require(projectile, "DefinitionId");
                if (request["MilitaryMusicPulse"] is JsonObject pulse) Require(pulse, "SourceView", "Positive");
                if (request["ArmyTrapPresentation"] is JsonObject trap) Require(trap, "SourceView", "TargetReceiverId");
                if (request["ArmyFacilityDamagePresentation"] is JsonObject damage) Require(damage, "TargetReceiverId");
            }
            if (request["FacilityStatisticPresentation"] is JsonNode statistic)
                Exact(statistic, "FacilityReceiverId", "TargetReceiverId", "ArmyId", "SourceHexOperand", "TargetHexOperand",
                    "DefinitionId", "Dimension", "Positive", "Amount", "SourceView");
            if (request["FacilitySupportAura"] is JsonNode support)
                Exact(support, "FacilityReceiverId", "TargetReceiverId", "ArmyId", "DefinitionId", "TargetHexOperand", "Scale");
            foreach (string key in new[] { "MilitaryMusicPulse", "FacilityStatisticPresentation", "ArmyTrapPresentation" })
                if (request[key]?["SourceView"] is JsonNode view) Exact(view, "Present", "X", "Y", "Z");
        }
    }

    private static void Require(JsonObject value, params string[] fields)
    {
        if (fields.Any(field => !value.ContainsKey(field)))
            throw new InvalidDataException("Saved facility presentation operands are missing.");
    }
    private static void Exact(JsonNode node, params string[] fields)
    {
        if (node is not JsonObject value || value.Count != fields.Length)
            throw new InvalidDataException("Saved facility presentation shape is invalid.");
        Require(value, fields);
    }
}
