using System.Text.Json.Nodes;

namespace San14.Core;

internal static class CampaignStoryPreparationSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 168 || Story(state) is not JsonObject story) return;
        if (story["PreparedStep"] is not null || story["TextLog"] is not null || story["Steps"] is JsonArray steps && steps.OfType<JsonObject>().Any(step =>
            Integer(step["AudioKind"], (int)StoryAudioKind.StopActiveSoundSlots, int.MaxValue) ||
            step["People"] is JsonArray people && people.Any(person => person is null) ||
            step["Kind"]?.GetValue<string>() == "narration" && step["Names"] is null) || story["Scene"] is JsonObject scene &&
            (scene["PreparedText"] is not null || scene["VisibleActors"] is not null || scene["HighlightedSlot"] is not null))
            throw new InvalidDataException("Legacy story contains future prepared presentation state.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (Story(state) is not JsonObject story || story["Scene"] is not JsonObject scene) return;
        if (version < 168)
        {
            story["PreparedStep"] = null;
            story["TextLog"] = new JsonArray();
            scene["PreparedText"] = null;
            scene["VisibleActors"] = new JsonObject();
            scene["HighlightedSlot"] = null;
            return;
        }
        ValidatePreparedStory(story);
    }

    internal static void ValidatePreparedStory(JsonObject story)
    {
        if (story["Scene"] is not JsonObject scene)
            throw new InvalidDataException("Saved story scene is missing.");
        if (story["TextLog"] is not JsonArray log || log.Any(entry => entry is null || !Text(entry)) ||
            !story.ContainsKey("PreparedStep") || story["PreparedStep"] is JsonNode prepared &&
            (prepared is not JsonObject step || !Integer(step["Step"], 0, int.MaxValue) ||
             !step.ContainsKey("Text") || step["Text"] is JsonNode text && !Text(text)) ||
            !scene.ContainsKey("PreparedText") || scene["PreparedText"] is JsonNode previous && !Text(previous) ||
            scene["VisibleActors"] is not JsonObject actors || actors.Any(pair => !int.TryParse(pair.Key, out int slot) ||
                slot is < 0 or > 4 || !Speaker(pair.Value)) || !scene.ContainsKey("HighlightedSlot") ||
            scene["HighlightedSlot"] is JsonNode highlighted && !Integer(highlighted, 0, 4))
            throw new InvalidDataException("Saved prepared story presentation is missing or invalid.");
    }

    private static JsonObject? Story(JsonObject state) =>
        state["NativeNormalTurnContinuation"]?["StoryPlayback"]?["Story"] as JsonObject ??
        state["CampaignEventCommandPlayback"]?["Invocation"]?["Story"] as JsonObject;
    internal static bool Text(JsonNode node) => node is JsonObject text && String(text["SpeakerName"]) && String(text["Text"]) &&
        text["SpeechOnly"] is JsonValue speechOnly && speechOnly.TryGetValue<bool>(out _) &&
        text["Pages"] is JsonArray pages && pages.All(String) && text.ContainsKey("Speaker") &&
        (text["Speaker"] is null || Speaker(text["Speaker"])) && text["Voices"] is JsonArray voices && voices.All(Voice);
    private static bool Speaker(JsonNode? node) => node is JsonObject speaker && Integer(speaker["OfficerId"], 0, 6000) &&
        String(speaker["NameAtSource"]) && speaker.ContainsKey("PortraitAppearanceIndex") &&
        (speaker["PortraitAppearanceIndex"] is null || Integer(speaker["PortraitAppearanceIndex"], 0, ushort.MaxValue));
    private static bool Voice(JsonNode? node) => node is JsonObject voice && Integer(voice["SpeakerId"], 0, 6000) &&
        Integer(voice["VoiceType"], 0, byte.MaxValue) && Integer(voice["VoiceId"], 0, int.MaxValue) &&
        Integer(voice["Language"], 0, 1) && Integer(voice["VariantIndex"], 0, int.MaxValue) &&
        voice["SoundId"] is JsonValue sound && sound.TryGetValue<uint>(out _);
    private static bool String(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out _);
    private static bool Integer(JsonNode? node, int min, int max) => node is JsonValue value &&
        value.TryGetValue<int>(out int number) && number >= min && number <= max;
}
