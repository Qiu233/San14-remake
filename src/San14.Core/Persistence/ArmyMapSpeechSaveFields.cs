using System.Text.Json.Nodes;

namespace San14.Core;

internal static class ArmyMapSpeechSaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 162)
        {
            if (state["ArmyMapSpeech"] is not null)
                throw new InvalidDataException("Legacy save contains a future Army map speech manager.");
            state["ArmyMapSpeech"] = null;
            return;
        }
        if (!state.ContainsKey("ArmyMapSpeech"))
            throw new InvalidDataException("Saved Army map speech knowledge is missing.");
        if (state["ArmyMapSpeech"] is not JsonNode saved) return;
        if (saved is not JsonObject manager || manager["NextSequence"] is not JsonValue nextValue ||
            !nextValue.TryGetValue<long>(out long next) || next < 1 ||
            manager["Entries"] is not JsonArray entries || entries.Count > 100)
            throw new InvalidDataException("Saved Army map speech manager is invalid.");
        long previous = 0;
        foreach (var item in entries)
        {
            if (item is not JsonObject entry || entry["Sequence"] is not JsonValue sequenceValue ||
                !sequenceValue.TryGetValue<long>(out long sequence) || sequence <= previous || sequence >= next ||
                !Integer(entry, "ArmyId", 1, int.MaxValue) || !Integer(entry, "OfficerId", 0, 6000) ||
                !Integer(entry, "ActionCode", 0, int.MaxValue) || !Integer(entry, "HexId", 0, 48400) ||
                !entry.ContainsKey("SceneryId") || entry["SceneryId"] is JsonNode scenery &&
                (scenery is not JsonValue sceneryValue || !sceneryValue.TryGetValue<int>(out int sceneryId) || sceneryId is < 0 or > 100) ||
                !Integer(entry, "DelayFrames", 0, 9) || entry["ElapsedSeconds"] is not JsonValue elapsedValue ||
                !elapsedValue.TryGetValue<double>(out double elapsed) || !double.IsFinite(elapsed) || elapsed < 0 ||
                !entry.ContainsKey("TextAtSource") || entry["TextAtSource"] is JsonNode text &&
                (text is not JsonValue textValue || !textValue.TryGetValue<string>(out _)))
                throw new InvalidDataException("Saved Army map speech entry is invalid.");
            previous = sequence;
        }
    }

    private static bool Integer(JsonObject owner, string name, int minimum, int maximum) =>
        owner[name] is JsonValue field && field.TryGetValue<int>(out int value) && value >= minimum && value <= maximum;
}
