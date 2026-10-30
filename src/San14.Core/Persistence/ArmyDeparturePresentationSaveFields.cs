using System.Text.Json;
using System.Text.Json.Nodes;

namespace San14.Core;

internal static class ArmyDeparturePresentationSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 182) return;
        if (Requests(state).Any(request => request["ArmyDeparture"] is not null))
            throw new InvalidDataException("Legacy world contains future Army departure presentation.");
    }
    internal static void Migrate(JsonObject state, int version)
    {
        foreach (var request in Requests(state))
        {
            const string key = "ArmyDeparture";
            if (version < 182) { request[key] = null; continue; }
            if (!request.ContainsKey(key)) throw Invalid();
            if (request[key] is not JsonNode node) continue;
            if (node is not JsonObject fields || fields.Count != 5 ||
                !new[] { "ArmyAtSource", "OfficerAppearanceAtSource", "ReconcileModel", "RebuildInfoLayout", "SourceSnapshot" }.All(fields.ContainsKey) ||
                !Integer(fields["OfficerAppearanceAtSource"], 0, ushort.MaxValue) || !Boolean(fields["ReconcileModel"]) ||
                fields["RebuildInfoLayout"] is JsonNode layout && !Boolean(layout) ||
                fields["ArmyAtSource"] is not JsonObject army || !Integer(army["Id"], 1, int.MaxValue) ||
                !Integer(army["ForceId"], 0, 51) || !Integer(army["OfficerId"], 0, 6000) ||
                !Integer(army["HexId"], 0, 48400)) throw Invalid();
            if (fields["SourceSnapshot"] is JsonNode snapshot)
            {
                if (snapshot is not JsonObject source || source.Count != 14 ||
                    !new[] { "ReceiverId", "CommanderId", "Status", "Mobility", "Label", "Outline", "Flag", "GraphicsUnavailable",
                        "CurrentHex", "Direction", "ArmyValid", "Flags", "Abnormal", "Personalities" }.All(source.ContainsKey)) throw Invalid();
                if (source["Label"] is JsonObject label && (label.Count != 25 ||
                    !new[] { "UnitId", "ArmyPhysicalSlot", "ArmyValid", "Commander12", "Ready16", "Morale1A", "MoraleCapacity",
                        "EffectiveMorale", "PersonPhysicalSlot", "PersonValid", "AppearanceA6", "PersonDistrictE0", "DistrictPhysicalSlot",
                        "DistrictValid", "DistrictNumber11", "DistrictPredicate233A90", "ForcePhysicalSlot", "SourceForceColor49",
                        "IsCurrentPlayerForce", "Army13C", "SelectedFormation", "FormationClass7E", "IconSpriteId", "IconPalette", "MissingFields" }.All(label.ContainsKey))) throw Invalid();
                if (source["Outline"] is JsonObject outline && (outline.Count != 7 ||
                    !new[] { "UsesOutline", "PrimaryKind", "PrimaryValue", "PrimaryMultiplier", "DisabledKind", "DisabledValue", "DisabledMultiplier" }.All(outline.ContainsKey))) throw Invalid();
                if (source["Flag"] is JsonObject flag && (flag.Count != 7 ||
                    !new[] { "ForcePhysical", "Color", "BannerText", "CarriedText", "CarriedMissing", "BannerForceValid", "CarriedForceValid" }.All(flag.ContainsKey))) throw Invalid();
                if (source["Personalities"] is not JsonArray personalities || personalities.Count > 9 ||
                    personalities.Any(item => item is not JsonObject entry || entry.Count != 7 ||
                        !new[] { "Slot", "PersonalityId", "Valid", "Eligible", "Active", "Style", "Missing" }.All(entry.ContainsKey))) throw Invalid();
                (source.Deserialize<ArmyDepartureSourceSnapshot>() ?? throw Invalid()).Validate();
            }
        }
    }
    private static IEnumerable<JsonObject> Requests(JsonObject state) =>
        (state["CampaignEffectRequests"]?["PendingBatches"] as JsonArray)?.OfType<JsonObject>()
            .SelectMany(batch => (batch["Requests"] as JsonArray)?.OfType<JsonObject>() ?? []) ?? [];
    private static bool Boolean(JsonNode? node) => node is JsonValue value && value.TryGetValue<bool>(out _);
    private static bool Integer(JsonNode? node, int min, int max) => node is JsonValue value &&
        value.TryGetValue<int>(out int number) && number >= min && number <= max;
    private static InvalidDataException Invalid() => new("Saved Army departure presentation is missing or invalid.");
}
