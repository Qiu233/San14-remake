using System.Text.Json.Nodes;

namespace San14.Core;

internal static class CampaignStoryMessageBankSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        foreach (var slots in WorldBanks(state))
        {
            if (version < 195) RejectFuture(slots);
            if (version < 209 && (slots["Provinces"] is not null || PartialNames(slots)))
                throw new InvalidDataException("Legacy story contains a future Province message bank.");
        }
    }
    internal static void Migrate(JsonObject state, int version)
    {
        foreach (var slots in WorldBanks(state))
        {
            if (version < 195) { slots["Items"] = null; slots["Numbers"] = null; }
            if (version < 209) slots["Provinces"] = null;
            if (!BanksValid(slots)) throw Invalid();
        }
    }
    internal static void ValidateWorld(JsonObject state)
    {
        foreach (var slots in WorldBanks(state)) if (!BanksValid(slots)) throw Invalid();
    }
    internal static void MigrateOperationMetadata(JsonObject metadata, int sessionVersion)
    {
        if (metadata["Story"] is not JsonObject story) return;
        foreach (var slots in TextSteps(story))
        {
            if (sessionVersion < 8)
            {
                RejectFuture(slots);
                slots["Items"] = null; slots["Numbers"] = null;
            }
            if (sessionVersion < 13)
            {
                if (slots["Provinces"] is not null || PartialNames(slots))
                    throw new InvalidDataException("Legacy operation Story contains a future Province message bank.");
                slots["Provinces"] = null;
            }
            if (!BanksValid(slots)) throw Invalid();
        }
    }
    internal static bool BanksValid(JsonObject slots) =>
        slots.ContainsKey("Items") && slots.ContainsKey("Numbers") && slots.ContainsKey("Provinces") &&
        (slots["Items"] is null || slots["Items"] is JsonArray items && items.Count == 5 &&
            items.All(item => Integer(item, 0, 201))) &&
        (slots["Numbers"] is null || slots["Numbers"] is JsonArray numbers && numbers.Count == 5 &&
            numbers.All(number => number is null || Integer(number, int.MinValue, int.MaxValue))) &&
        (slots["Provinces"] is null || slots["Provinces"] is JsonArray provinces && provinces.Count == 10 &&
            provinces.All(province => province is null || Integer(province, 0, 22)));
    private static IEnumerable<JsonObject> WorldBanks(JsonObject state)
    {
        if (state["NativeNormalTurnContinuation"]?["StoryPlayback"]?["Story"] is JsonObject normal)
            foreach (var slots in TextSteps(normal)) yield return slots;
        if (state["EndingProgress"]?["Story"] is JsonObject ending)
            foreach (var slots in TextSteps(ending)) yield return slots;
        if (state["TutorialLessonProgress"]?["Story"] is JsonObject tutorial)
            foreach (var slots in TextSteps(tutorial)) yield return slots;
        if (state["EditedEventPlayback"]?["Story"] is JsonObject edited)
            foreach (var slots in TextSteps(edited)) yield return slots;
        if (state["CampaignEventCommandPlayback"]?["Invocation"]?["Story"] is JsonObject command)
            foreach (var slots in TextSteps(command)) yield return slots;
        if (state["TutorialLessonProgress"]?["MessageContext"] is JsonObject context) yield return context;
        if (state["NativeNormalTurnContinuation"]?["StoryPlayback"]?["ExecutionCursor"]?["MessageContext"] is JsonObject normalContext)
            yield return normalContext;
        if (state["CampaignEventCommandPlayback"]?["Invocation"]?["ExecutionCursor"]?["MessageContext"] is JsonObject commandContext)
            yield return commandContext;
    }
    private static IEnumerable<JsonObject> TextSteps(JsonObject story)
    {
        if (story["Steps"] is JsonArray steps)
            foreach (var step in steps.OfType<JsonObject>()) if (IsText(step)) yield return step;
        if (story["Scene"]?["Text"] is JsonObject text && IsText(text)) yield return text;
    }
    private static bool IsText(JsonObject value) => value["Kind"] is JsonValue kind && kind.TryGetValue<string>(out var name) &&
        name is "message" or "narration" or "format-only-message";
    private static void RejectFuture(JsonObject slots)
    {
        if (slots.ContainsKey("Items") || slots.ContainsKey("Numbers"))
            throw new InvalidDataException("Legacy story contains future Item or Number message banks.");
    }
    private static bool PartialNames(JsonObject slots) => slots["Names"] is JsonArray names && names.Any(name => name is null);
    private static bool Integer(JsonNode? node, int min, int max) => node is JsonValue value &&
        value.TryGetValue<int>(out int number) && number >= min && number <= max;
    private static InvalidDataException Invalid() => new("Saved story Item, Number or Province message banks are missing or invalid.");
}
