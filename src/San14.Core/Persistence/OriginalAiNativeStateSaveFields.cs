using System.Globalization;
using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Format migration for the owned action/caution facts. Runs only
/// when writing or loading a save, never during an AI query or tree update.</summary>
internal static class OriginalAiNativeStateSaveFields
{
    private const string Action = "AiActionFlags";
    private const string Planning = "GroupPlanning";

    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 213) return;
        if (Units(state).Any(unit => unit.ContainsKey(Action)) || Cautions(state)?.ContainsKey(Planning) == true)
            throw new InvalidDataException("Legacy world contains future AI native state.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 213)
        {
            foreach (var unit in Units(state)) unit[Action] = null;
            if (Cautions(state) is { } cautions) cautions[Planning] = null;
        }
        ValidateWorld(state);
    }

    internal static void ValidateWorld(JsonObject state)
    {
        foreach (var unit in Units(state))
            if (!unit.ContainsKey(Action) || unit[Action] is JsonNode node && !Integer(node, 0, ushort.MaxValue)) throw Invalid();
        if (Cautions(state) is not { } cautions) return;
        if (!cautions.ContainsKey(Planning)) throw Invalid();
        if (cautions[Planning] is null) return;
        if (cautions[Planning] is not JsonObject groups) throw Invalid();
        foreach (var pair in groups)
        {
            if (!int.TryParse(pair.Key, NumberStyles.None, CultureInfo.InvariantCulture, out int group) ||
                group is < 0 or > 500 || group.ToString(CultureInfo.InvariantCulture) != pair.Key ||
                pair.Value is not JsonObject row || row.Count != 3 ||
                !row.ContainsKey("EstimatedStrength") || !row.ContainsKey("Status") || !row.ContainsKey("VirtualTargetLocationId")) throw Invalid();
            if (row["EstimatedStrength"] is JsonNode strength && (strength is not JsonValue number ||
                !number.TryGetValue<float>(out float value) || !float.IsFinite(value))) throw Invalid();
            if (row["Status"] is JsonNode status && (status is not JsonValue boolean || !boolean.TryGetValue<bool>(out _))) throw Invalid();
            if (row["VirtualTargetLocationId"] is JsonNode target && !Integer(target, 0, 61)) throw Invalid();
        }
    }

    private static JsonObject? Cautions(JsonObject state) =>
        (state["ArmyLifecycle"] ?? state["NativeArmyLifecycle"])?["ArmyCautions"] as JsonObject;

    // These are the actual persisted UnitState owners. Operation-world frames
    // pass through this same codec independently; no recursive world proof is
    // needed to reach pending, withdrawn or source-time departure images.
    private static IEnumerable<JsonObject> Units(JsonObject state)
    {
        if (state["Units"] is JsonObject active)
            foreach (var item in active.Select(pair => pair.Value).OfType<JsonObject>()) yield return item;
        if ((state["ArmyLifecycle"] ?? state["NativeArmyLifecycle"])?["Receivers"]?["Entries"] is JsonArray receivers)
            foreach (var item in receivers.Select(row => row?["PendingImage"]).OfType<JsonObject>()) yield return item;
        if (state["ForceDefeats"] is JsonObject defeats)
            foreach (var row in defeats)
                if (row.Value?["WithdrawnUnits"] is JsonArray withdrawn)
                    foreach (var item in withdrawn.OfType<JsonObject>()) yield return item;
        if (state["CampaignEffectRequests"]?["PendingBatches"] is JsonArray batches)
            foreach (var batch in batches)
                if (batch?["Requests"] is JsonArray requests)
                    foreach (var item in requests.Select(row => row?["ArmyDeparture"]?["ArmyAtSource"]).OfType<JsonObject>()) yield return item;
    }

    private static bool Integer(JsonNode? node, int minimum, int maximum) => node is JsonValue value &&
        value.TryGetValue<int>(out int number) && number >= minimum && number <= maximum;
    private static InvalidDataException Invalid() => new("Saved AI native state is missing or invalid.");
}
