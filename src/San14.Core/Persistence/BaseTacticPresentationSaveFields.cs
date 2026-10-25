using System.Text.Json.Nodes;

namespace San14.Core;

internal static class BaseTacticPresentationSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 179 && (Actors(state).Any(value => value["BaseCasterLocationId"] is not null) ||
            Reports(state).Any(value => value["SourceBaseLocationId"] is not null)))
            throw new InvalidDataException("Legacy tactic presentation contains a future Base source.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        foreach (var actor in Actors(state))
        {
            Field(actor, "BaseCasterLocationId", version);
            if (actor["BaseCasterLocationId"] is not null && !Integer(actor["Actor"]?["Value"], 0, 0))
                throw new InvalidDataException("A Base tactic actor cannot identify an Army receiver.");
        }
        foreach (var report in Reports(state))
        {
            Field(report, "SourceBaseLocationId", version);
            if (report["SourceBaseLocationId"] is not null && !Integer(report["UnitId"], 0, 0))
                throw new InvalidDataException("A Base tactic report cannot identify a source Army.");
        }
    }

    private static void Field(JsonObject value, string key, int version)
    {
        if (version < 179) { value[key] = null; return; }
        if (!value.ContainsKey(key) || value[key] is not null && !Integer(value[key], 1, 61))
            throw new InvalidDataException("Saved tactic Base source knowledge is missing or invalid.");
    }

    private static IEnumerable<JsonObject> Actors(JsonObject state)
    {
        if (state["ManualTacticIntervention"] is not JsonObject owner) yield break;
        foreach (string key in new[] { "Effects", "Directions" })
            if (owner[key] is JsonArray requests)
                foreach (var request in requests.OfType<JsonObject>())
                    if (request["Actors"] is JsonArray actors)
                        foreach (var actor in actors.OfType<JsonObject>()) yield return actor;
    }

    private static IEnumerable<JsonObject> Reports(JsonObject state)
    {
        foreach (var node in new[] { state["TacticReports"], state["NativePeriodContinuation"]?["Preparation"]?["TacticReports"],
                     state["NormalCombatImmediateContinuation"]?["AutomaticExecution"]?["Details"],
                     state["NormalCombatImmediateContinuation"]?["BaseAutomaticExecution"]?["Details"] })
            if (node is JsonArray reports)
                foreach (var report in reports.OfType<JsonObject>()) yield return report;
    }
    private static bool Integer(JsonNode? node, int min, int max) => node is JsonValue scalar &&
        scalar.TryGetValue<int>(out int number) && number >= min && number <= max;
}
