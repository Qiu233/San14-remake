using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Selection knowledge enters through this bounded save-format
/// boundary. Story stepping never rescans or validates these immutable slots.</summary>
internal static class CampaignStorySelectionSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 192) return;
        foreach (var story in Stories(state))
            if (story.ContainsKey("SelectionContext")) throw Future();
    }
    internal static void Migrate(JsonObject state, int version)
    {
        foreach (var story in Stories(state))
        {
            if (version < 192) story["SelectionContext"] = null;
            ValidateStory(story);
        }
    }
    internal static void ValidateWorld(JsonObject state)
    {
        foreach (var story in Stories(state)) ValidateStory(story);
    }
    //CampaignProgressOperation stores its pending Story in Session metadata,
    //outside every independently versioned compressed world payload.
    internal static void MigrateOperationMetadata(JsonObject metadata, int sessionVersion)
    {
        if (metadata["Story"] is not JsonObject story) return;
        if (sessionVersion < 7)
        {
            if (story.ContainsKey("SelectionContext")) throw Future();
            story["SelectionContext"] = null;
        }
        ValidateStory(story);
    }
    internal static void ValidateRequest(CampaignStoryRequest story)
    {
        if (story.SelectionContext is not { } context) return;
        if (context.ActorIds.IsDefault || context.ActorIds.Length != 5 || context.ActorIds.Any(id => id is < 0 or > 6000) ||
            context.TargetForcePhysicalSlots.IsDefault || context.TargetForcePhysicalSlots.Length != 4 ||
            context.TargetForcePhysicalSlots.Any(id => id is < 0 or > 51)) throw Invalid();
        if (context.DialogueRoles is { } roles && (roles.ForcePhysicalSlot is < 0 or > 51 ||
            roles.PersonIds.IsDefault || roles.PersonIds.Length != 6 || roles.PersonIds.Any(id => id is < 0 or > 6000))) throw Invalid();
    }
    private static void ValidateStory(JsonObject story)
    {
        if (!story.ContainsKey("SelectionContext")) throw Invalid();
        if (story["SelectionContext"] is not JsonNode node) return;
        if (node is not JsonObject context || !Keys(context, "ActorIds", "TargetForcePhysicalSlots", "DialogueRoles") ||
            !Slots(context["ActorIds"], 5, 6000) || !Slots(context["TargetForcePhysicalSlots"], 4, 51)) throw Invalid();
        if (context["DialogueRoles"] is JsonNode dialogue && (dialogue is not JsonObject roles ||
            !Keys(roles, "ForcePhysicalSlot", "PersonIds") || !NullableInteger(roles["ForcePhysicalSlot"], 51) ||
            !Slots(roles["PersonIds"], 6, 6000))) throw Invalid();
    }
    private static IEnumerable<JsonObject> Stories(JsonObject state)
    {
        if (state["NativeNormalTurnContinuation"]?["StoryPlayback"]?["Story"] is JsonObject normal) yield return normal;
        if (state["EndingProgress"]?["Story"] is JsonObject ending) yield return ending;
        if (state["TutorialLessonProgress"]?["Story"] is JsonObject tutorial) yield return tutorial;
        if (state["EditedEventPlayback"]?["Story"] is JsonObject edited) yield return edited;
        if (state["CampaignEventCommandPlayback"]?["Invocation"]?["Story"] is JsonObject command) yield return command;
    }
    private static bool Slots(JsonNode? node, int count, int max) => node is JsonArray values && values.Count == count &&
        values.All(value => NullableInteger(value, max));
    private static bool NullableInteger(JsonNode? node, int max) => node is null || node is JsonValue value &&
        value.TryGetValue<int>(out int number) && number >= 0 && number <= max;
    private static bool Keys(JsonObject value, params string[] keys) => value.Count == keys.Length && keys.All(value.ContainsKey);
    private static InvalidDataException Future() => new("Legacy story contains future invocation selection knowledge.");
    private static InvalidDataException Invalid() => new("Saved story invocation selections are missing or invalid.");
}
