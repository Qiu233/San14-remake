using System.Text.Json.Nodes;

namespace San14.Core;

internal static class BaseCapturePresentationSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 172 && Presentations(state).Any(presentation => presentation["BaseCapture"] is not null))
            throw new InvalidDataException("Legacy world contains future Base capture presentation context.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        foreach (var presentation in Presentations(state))
        {
            if (version < 172) presentation["BaseCapture"] = null;
            else if (!presentation.ContainsKey("BaseCapture") || presentation["BaseCapture"] is JsonNode node &&
                (node is not JsonObject snapshot || !Integer(snapshot, "EffectKind", 7, 8) ||
                 !Integer(snapshot, "TargetDistrictId", 0, 51) || !Integer(snapshot, "PlayerPrimaryDistrictId", 0, 51) ||
                 !Coordinate(snapshot, "SourceX") || !Coordinate(snapshot, "SourceY") || !Coordinate(snapshot, "SourceZ") ||
                 !Coordinate(snapshot, "Scale") || !Integer(snapshot, "Category", int.MinValue, int.MaxValue) ||
                 !Integer(snapshot, "SoundId", 0, int.MaxValue)))
                throw new InvalidDataException("Saved Base capture presentation context is missing or invalid.");
        }
    }

    private static IEnumerable<JsonObject> Presentations(JsonObject state)
    {
        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) yield break;
        foreach (var batch in batches.OfType<JsonObject>())
            if (batch["Requests"] is JsonArray requests)
                foreach (var request in requests.OfType<JsonObject>())
                    if (request["MapObjectPresentation"] is JsonObject presentation) yield return presentation;
    }
    private static bool Integer(JsonObject owner, string key, int minimum, int maximum) => owner[key] is JsonValue value &&
        value.TryGetValue<int>(out int number) && number >= minimum && number <= maximum;
    private static bool Coordinate(JsonObject owner, string key) => owner[key] is JsonValue value &&
        value.TryGetValue<float>(out float number) && float.IsFinite(number);
}
