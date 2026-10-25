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
                !Integer(entry, "ArmyId", version >= 181 ? 0 : 1, int.MaxValue) || !Integer(entry, "OfficerId", 0, 6000) ||
                !Integer(entry, "ActionCode", 0, int.MaxValue) || !Integer(entry, "HexId", 0, 48400) ||
                !entry.ContainsKey("SceneryId") || entry["SceneryId"] is JsonNode scenery &&
                (scenery is not JsonValue sceneryValue || !sceneryValue.TryGetValue<int>(out int sceneryId) || sceneryId is < 0 or > 100) ||
                !Integer(entry, "DelayFrames", 0, 9) || entry["ElapsedSeconds"] is not JsonValue elapsedValue ||
                !elapsedValue.TryGetValue<double>(out double elapsed) || !double.IsFinite(elapsed) || elapsed < 0 ||
                !entry.ContainsKey("TextAtSource") || entry["TextAtSource"] is JsonNode text &&
                (text is not JsonValue textValue || !textValue.TryGetValue<string>(out _)))
                throw new InvalidDataException("Saved Army map speech entry is invalid.");
            if (version < 172)
            {
                if (entry["ContextBaseLocationId"] is not null || entry["SpatialVoices"] is not null ||
                    entry["SpatialVoicesAccepted"] is not null)
                    throw new InvalidDataException("Legacy Army speech contains future presentation context.");
                entry["ContextBaseLocationId"] = null;
                entry["SpatialVoices"] = Integer(entry, "ActionCode", 31, 31) ? new JsonArray() : null;
                entry["SpatialVoicesAccepted"] = false;
            }
            else if (!entry.ContainsKey("ContextBaseLocationId") || entry["ContextBaseLocationId"] is not null &&
                !Integer(entry, "ContextBaseLocationId", 0, 61) || !entry.ContainsKey("SpatialVoices") ||
                entry["SpatialVoices"] is JsonNode voices && (voices is not JsonArray requests || !requests.All(SpatialVoice)) ||
                entry["SpatialVoicesAccepted"] is not JsonValue accepted || !accepted.TryGetValue<bool>(out bool hasAccepted) ||
                hasAccepted && (entry["SpatialVoices"] is not JsonArray { Count: > 0 } || entry["TextAtSource"] is null))
                throw new InvalidDataException("Saved Army map speech presentation context is missing or invalid.");
            foreach (var field in new[] { (Name: "ContextTraitId", Maximum: 355), (Name: "ContextAreaId", Maximum: 500) })
            {
                if (version < 173)
                {
                    if (entry[field.Name] is not null)
                        throw new InvalidDataException("Legacy Army speech contains future personality context.");
                    entry[field.Name] = null;
                }
                else if (!entry.ContainsKey(field.Name) || entry[field.Name] is not null && !Integer(entry, field.Name, 0, field.Maximum))
                    throw new InvalidDataException("Saved Army map speech personality context is missing or invalid.");
            }
            if (version < 181)
            {
                if (entry["BaseSpeakerLocationId"] is not null || entry["BaseSpeakerObjectId"] is not null ||
                    entry["BaseSpeakerAppearanceIndex"] is not null || entry["BaseSpeakerForceColor"] is not null)
                    throw new InvalidDataException("Legacy map speech contains a future Base speaker.");
                entry["BaseSpeakerLocationId"] = null;
                entry["BaseSpeakerObjectId"] = null;
                entry["BaseSpeakerAppearanceIndex"] = null;
                entry["BaseSpeakerForceColor"] = null;
            }
            else
            {
                if (!entry.ContainsKey("BaseSpeakerLocationId") || !entry.ContainsKey("BaseSpeakerObjectId") ||
                    !entry.ContainsKey("BaseSpeakerAppearanceIndex") || !entry.ContainsKey("BaseSpeakerForceColor"))
                    throw new InvalidDataException("Saved map speech Base speaker knowledge is missing.");
                //An already-created owned layout survives a higher-priority
                //packet replacement, including a Base-to-Army replacement.
                bool baseSpeaker = entry["BaseSpeakerLocationId"] is not null || entry["BaseSpeakerObjectId"] is not null;
                if (entry["BaseSpeakerAppearanceIndex"] is null
                    ? entry["BaseSpeakerForceColor"] is not null
                    : !Integer(entry, "BaseSpeakerAppearanceIndex", 0, ushort.MaxValue) ||
                      !Integer(entry, "BaseSpeakerForceColor", 0, byte.MaxValue))
                    throw new InvalidDataException("Saved map speech owned layout snapshot is incomplete.");
                if (baseSpeaker
                    ? !Integer(entry, "BaseSpeakerLocationId", 1, 61) ||
                      !Integer(entry, "BaseSpeakerObjectId", 0, ushort.MaxValue) ||
                      !Integer(entry, "ArmyId", 0, 0) || !Integer(entry, "ActionCode", 15, 16)
                    : !Integer(entry, "ArmyId", 1, int.MaxValue))
                    throw new InvalidDataException("Saved map speech speaker identity is contradictory.");
            }
            previous = sequence;
        }
    }

    private static bool Integer(JsonObject owner, string name, int minimum, int maximum) =>
        owner[name] is JsonValue field && field.TryGetValue<int>(out int value) && value >= minimum && value <= maximum;

    private static bool SpatialVoice(JsonNode? node) => node is JsonObject spatial &&
        new[] { "SourceX", "SourceY", "SourceZ" }.All(key => spatial[key] is JsonValue value &&
            value.TryGetValue<float>(out float coordinate) && float.IsFinite(coordinate)) &&
        spatial["Voice"] is JsonObject voice && Integer(voice, "SpeakerId", 0, 6000) &&
        Integer(voice, "VoiceType", 0, byte.MaxValue) && Integer(voice, "VoiceId", 0, int.MaxValue) &&
        Integer(voice, "Language", 0, 1) && Integer(voice, "VariantIndex", 0, int.MaxValue) &&
        voice["SoundId"] is JsonValue sound && sound.TryGetValue<uint>(out _);
}
