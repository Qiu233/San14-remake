using System.Text.Json.Nodes;

namespace San14.Core;

internal static class CampaignEndingSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 181 && (state["EndingProgress"] is not null ||
            state["CampaignTurnStop"]?["Reason"] is JsonValue reason && reason.TryGetValue<int>(out int kind) &&
            kind == (int)CampaignTurnStopReason.EndingFromEvent900))
            throw new InvalidDataException("Legacy world contains a future Ending continuation.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        const string key = "EndingProgress";
        if (version < 181) { state[key] = null; return; }
        if (!state.ContainsKey(key)) throw Invalid();
        if (state[key] is not JsonNode node) return;
        if (node is not JsonObject progress || !Keys(progress, "FromEvent900", "Stage", "Substep", "NextSequence",
                "DeclinedAbdication", "AbdicationAccepted", "ChosenKingdomId", "Pending", "Story", "Completed") ||
            !Bool(progress["FromEvent900"]) || !Bool(progress["DeclinedAbdication"]) || !Bool(progress["Completed"]) ||
            progress["AbdicationAccepted"] is JsonNode answer && !Bool(answer) ||
            !Integer(progress["Stage"], 0, 15) || !Integer(progress["Substep"], 0, 4) ||
            progress["NextSequence"] is not JsonValue seq || !seq.TryGetValue<long>(out long next) || next < 1 ||
            progress["ChosenKingdomId"] is JsonNode selected && !Integer(selected, 0, int.MaxValue)) throw Invalid();
        if (progress["Pending"] is JsonNode pending && !Presentation(pending)) throw Invalid();
        if (progress["Story"] is JsonNode story)
        {
            if (story is not JsonObject request || !Keys(request, "Sequence", "Source", "ContentRevision", "Steps", "NextStep",
                "Scene", "PreparedStep", "TextLog", "SelectionContext")) throw Invalid();
            CampaignStoryPreparationSaveFields.ValidatePreparedStory(request);
        }
    }

    private static bool Presentation(JsonNode node) => node is JsonObject pending &&
        Keys(pending, "Sequence", "Kind", "TextAtSource", "ForceId", "RulerOfficerId", "Date", "Speaker", "KingdomChoices", "MovieId", "Notices") &&
        pending["KingdomChoices"] is JsonArray choices && choices.All(choice => choice is JsonObject value && Keys(value, "Id", "Name", "Description")) &&
        pending["Notices"] is JsonArray notices && notices.All(notice => notice is JsonObject value && Keys(value, "TextAtSource", "SoundId"));
    private static bool Keys(JsonObject value, params string[] keys) => value.Count == keys.Length && keys.All(value.ContainsKey);
    private static bool Bool(JsonNode? node) => node is JsonValue value && value.TryGetValue<bool>(out _);
    private static bool Integer(JsonNode? node, int min, int max) => node is JsonValue value && value.TryGetValue<int>(out int n) && n >= min && n <= max;
    private static InvalidDataException Invalid() => new("Saved Ending progress is missing or invalid.");
}
