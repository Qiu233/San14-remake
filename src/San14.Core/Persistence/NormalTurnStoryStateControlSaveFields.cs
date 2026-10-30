using System.Text.Json;
using System.Text.Json.Nodes;

namespace San14.Core;

internal static class NormalTurnStoryStateControlSaveFields
{
    internal static void Migrate(JsonObject playback, int version)
    {
        const string key = "StateControl";
        bool entry = Integer(playback["Phase"], 3, 4);
        bool camera = playback["IsReturningCamera"] is JsonValue flag && flag.TryGetValue<bool>(out bool returning) && returning;
        bool active = !entry && !camera;
        if (version < 221)
        {
            if (playback[key] is not null)
                throw new InvalidDataException("Legacy Story contains future province-control continuation knowledge.");
            //The source-time transition pairs cannot be recovered from a later
            //world. Old active invocations remain explicitly unavailable.
            playback[key] = active ? new JsonObject
            {
                ["Pairs"] = null, ["HasChanges"] = null, ["NextOperation"] = 0,
                ["PendingNotice"] = null, ["AwaitingPresentationBatch"] = null
            } : null;
            return;
        }
        if (!playback.ContainsKey(key))
            throw new InvalidDataException("Saved Story province-control continuation knowledge is missing.");
        if (playback[key] is null)
        {
            if (active) throw new InvalidDataException("Active Story province-control continuation is missing.");
            return;
        }
        if (!active || playback[key] is not JsonObject progress ||
            !Keys(progress, "Pairs", "HasChanges", "NextOperation", "PendingNotice", "AwaitingPresentationBatch") ||
            !Integer(progress["NextOperation"], 0, 69) ||
            progress["HasChanges"] is JsonNode changed && (changed is not JsonValue value || !value.TryGetValue<bool>(out _)) ||
            progress["Pairs"] is JsonNode pairs && (pairs is not JsonArray rows || rows.Count != 23 || rows.Any(pair =>
                pair is not JsonObject row || !Keys(row, "GainedForcePhysicalSlot", "LostForcePhysicalSlot") ||
                !Force(row["GainedForcePhysicalSlot"]) || !Force(row["LostForcePhysicalSlot"]))) ||
            progress["AwaitingPresentationBatch"] is JsonNode batch &&
                (batch is not JsonValue number || !number.TryGetValue<long>(out long sequence) || sequence < 1) ||
            progress["PendingNotice"] is JsonNode pending && (pending is not JsonObject notice ||
                !Keys(notice, "Province", "Force", "Gained", "TextAtSource") ||
                !Integer(notice["Province"], 0, 22) || !Integer(notice["Force"], 0, 51) ||
                notice["Gained"] is not JsonValue gained || !gained.TryGetValue<bool>(out _) ||
                notice["TextAtSource"] is not JsonValue text || !text.TryGetValue<string>(out string? message) || message is null))
            throw new InvalidDataException("Saved Story province-control continuation has an invalid format.");
        try
        {
            var saved = progress.Deserialize<CampaignStoryStateControlProgress>()
                ?? throw new InvalidDataException("Saved Story province-control continuation is null.");
            saved.Validate();
            if (Integer(playback["Phase"], 5, 6) && !saved.IsSourceTimeUnavailable && !saved.IsCompleted)
                throw new InvalidDataException("Exiting Story province-control work has not completed.");
        }
        catch (JsonException error)
        { throw new InvalidDataException("Saved Story province-control continuation has an invalid format.", error); }
    }

    private static bool Force(JsonNode? node) => node is null || Integer(node, 0, 51);
    private static bool Keys(JsonObject value, params string[] keys) => value.Count == keys.Length && keys.All(value.ContainsKey);
    private static bool Integer(JsonNode? node, int min, int max) => node is JsonValue value &&
        value.TryGetValue<int>(out int number) && number >= min && number <= max;
}
