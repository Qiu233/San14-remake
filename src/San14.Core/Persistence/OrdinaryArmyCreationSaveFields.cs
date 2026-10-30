using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>The old integer destination had no known-none projection before
///215. Actual owner/admission checks remain in the existing state validator.</summary>
internal static class OrdinaryArmyCreationSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 215) return;
        if (state["Units"] is JsonObject units)
            foreach (var item in units) Reject(item.Value);
        if (state["ArmyLifecycle"]?["Receivers"]?["Entries"] is JsonArray receivers)
            foreach (var item in receivers) Reject(item?["PendingImage"]);
        if (state["ForceDefeats"] is JsonObject defeats)
            foreach (var item in defeats)
                if (item.Value?["WithdrawnUnits"] is JsonArray images)
                    foreach (var image in images) Reject(image);
    }
    private static void Reject(JsonNode? unit)
    {
        if (unit?["DestinationHexId"] is JsonValue value && value.TryGetValue<int>(out int destination) && destination < 0)
            throw new InvalidDataException("Legacy world contains a future no-destination Army projection.");
    }
}
