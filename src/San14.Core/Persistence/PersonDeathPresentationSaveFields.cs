using System.Text.Json.Nodes;

namespace San14.Core;

internal static class PersonDeathPresentationSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 174 && Reports(state).Any(report => report["Voice"] is not null || report["Speaker"] is not null))
            throw new InvalidDataException("Legacy death report contains future source-time presentation.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        foreach (var report in Reports(state))
        {
            if (version < 174)
            {
                report["Voice"] = null;
                report["Speaker"] = null;
                continue;
            }
            if (!report.ContainsKey("Voice") || report["Voice"] is JsonNode voice && !Voice(voice) ||
                !report.ContainsKey("Speaker") || report["Speaker"] is JsonNode speaker && !Speaker(speaker))
                throw new InvalidDataException("Saved death-report source presentation is missing or invalid.");
        }
    }

    private static IEnumerable<JsonObject> Reports(JsonObject state)
    {
        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) yield break;
        foreach (var batch in batches.OfType<JsonObject>())
            if (batch["Requests"] is JsonArray requests)
                foreach (var request in requests.OfType<JsonObject>())
                    if (request["PersonDeathReport"] is JsonObject report) yield return report;
    }
    private static bool Voice(JsonNode node) => node is JsonObject voice && Integer(voice, "SpeakerId", 0, 6000) &&
        Integer(voice, "VoiceType", 0, byte.MaxValue) && Integer(voice, "VoiceId", 0, int.MaxValue) &&
        Integer(voice, "Language", 0, 1) && Integer(voice, "VariantIndex", 0, int.MaxValue) &&
        voice["SoundId"] is JsonValue sound && sound.TryGetValue<uint>(out _);
    private static bool Speaker(JsonNode node) => node is JsonObject speaker && Integer(speaker, "OfficerId", 0, 6000) &&
        speaker["NameAtSource"] is JsonValue name && name.TryGetValue<string>(out _) && speaker.ContainsKey("PortraitAppearanceIndex") &&
        (speaker["PortraitAppearanceIndex"] is null || Integer(speaker, "PortraitAppearanceIndex", 0, ushort.MaxValue));
    private static bool Integer(JsonObject owner, string key, int minimum, int maximum) => owner[key] is JsonValue value &&
        value.TryGetValue<int>(out int number) && number >= minimum && number <= maximum;
}
