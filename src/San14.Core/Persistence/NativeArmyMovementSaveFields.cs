using System.Text.Json.Nodes;

namespace San14.Core;

internal static class NativeArmyMovementSaveFields
{
    private static JsonObject? Cursor(JsonObject state) =>
        state["NativePeriodContinuation"] is JsonObject period && period["Cursor"] is JsonObject cursor ? cursor : null;

    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 227 && Cursor(state) is { } cursor &&
            (cursor["MovementContinuation"] is not null ||
             cursor["StageIndex"] is JsonValue stage && stage.TryGetValue<int>(out int value) && value == 8))
            throw new InvalidDataException("Legacy world contains a future movement continuation.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (Cursor(state) is not { } cursor) return;
        if (version < 227) { cursor["MovementContinuation"] = null; return; }
        if (!cursor.ContainsKey("MovementContinuation")) throw Invalid();
        if (cursor["MovementContinuation"] is null) return;
        if (cursor["MovementContinuation"] is not JsonObject movement ||
            new[] { "PreparationMoment", "RemainingActors", "AppliedActorOrder", "CompletedBatchCount", "ActorsIntoBatch" }
                .Any(key => !movement.ContainsKey(key)) ||
            movement["PreparationMoment"] is not JsonObject || movement["RemainingActors"] is not JsonArray remaining ||
            movement["AppliedActorOrder"] is not JsonArray ||
            !Integer(movement["CompletedBatchCount"]) || !Integer(movement["ActorsIntoBatch"])) throw Invalid();
        foreach (var item in remaining)
            if (item is not JsonObject actor ||
                new[] { "UnitId", "ReceiverId", "CreditAtPreparation", "HexAtPreparation" }.Any(key => !actor.ContainsKey(key)) ||
                !Integer(actor["UnitId"]) || !Integer(actor["CreditAtPreparation"]) || !Integer(actor["HexAtPreparation"])) throw Invalid();
    }

    private static bool Integer(JsonNode? item) => item is JsonValue value && value.TryGetValue<int>(out _);
    private static InvalidDataException Invalid() => new("Saved movement continuation fields are incomplete.");
}
