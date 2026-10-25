using System.Text.Json.Nodes;

namespace San14.Core;

internal static class NormalTurnStorySaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 167 || state["NativeNormalTurnContinuation"] is not JsonObject turn) return;
        if (turn["StoryPlayback"] is not null || turn["NextAction"] is JsonValue value &&
            value.TryGetValue<int>(out int action) && action is 48 or 49)
            throw new InvalidDataException("Legacy normal Turn contains a future story playback.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (state["NativeNormalTurnContinuation"] is not JsonObject turn) return;
        const string key = "StoryPlayback";
        if (version < 167) { turn[key] = null; return; }
        if (!turn.ContainsKey(key)) throw new InvalidDataException("Saved normal Turn story knowledge is missing.");
        if (turn[key] is not JsonNode saved) return;
        if (saved is not JsonObject playback || playback["Story"] is not JsonObject story ||
            story["Sequence"] is not JsonValue sequence || !sequence.TryGetValue<long>(out long id) || id < 1 ||
            story["Source"] is not JsonObject || story["ContentRevision"] is not JsonValue revision ||
            !revision.TryGetValue<string>(out _) || story["Steps"] is not JsonArray steps || steps.Count == 0 ||
            !Integer(story["NextStep"], 0, steps.Count) || story["Scene"] is not JsonObject ||
            playback["IsReturningCamera"] is not JsonValue returning || !returning.TryGetValue<bool>(out _) ||
            playback["RemainingEventIds"] is not JsonArray remaining || remaining.Any(item => !Integer(item, 0, 1023)) ||
            playback["Date"] is not JsonObject date || !Integer(date["Year"], 0, 350) ||
            !Integer(date["Month"], 1, 12) || !Integer(date["Day"], 1, 30) ||
            !playback.ContainsKey("ReturnCamera") || playback["ReturnCamera"] is JsonNode camera && !Camera(camera) ||
            playback["ServiceChanges"] is not JsonObject service || service["Events"] is not JsonArray ||
            service["Achievements"] is not JsonArray || service["ClockReads"] is not JsonArray ||
            service["Dirty"] is not JsonValue dirty || !dirty.TryGetValue<bool>(out _))
            throw new InvalidDataException("Saved normal Turn story playback is incomplete or invalid.");
    }

    private static bool Camera(JsonNode node) => node is JsonObject camera &&
        new[] { "FocusX", "FocusY", "FocusZ", "FocusW", "Distance", "Yaw", "Pitch" }
            .All(key => camera[key] is JsonValue value && value.TryGetValue<double>(out double number) && double.IsFinite(number));
    private static bool Integer(JsonNode? node, int min, int max) => node is JsonValue value &&
        value.TryGetValue<int>(out int number) && number >= min && number <= max;
}
