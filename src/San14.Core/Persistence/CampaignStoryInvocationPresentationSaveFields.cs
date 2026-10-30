using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>World196 and Session9 preserve the invocation's Item layer,
/// formatter-only results and actual owned effect slots at save boundaries.</summary>
internal static class CampaignStoryInvocationPresentationSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 196) return;
        foreach (var story in Stories(state)) RejectFuture(story);
    }

    internal static void Migrate(JsonObject state, int version)
    {
        foreach (var story in Stories(state)) MigrateStory(story, legacy: version < 196);
    }

    internal static void ValidateWorld(JsonObject state)
    {
        foreach (var story in Stories(state)) ValidateStory(story);
    }

    // The operation's Story is outside the independently versioned world
    // blocks. Session9 introduces these same semantics in its metadata.
    internal static void MigrateOperationMetadata(JsonObject metadata, int sessionVersion)
    {
        if (metadata["Story"] is not JsonObject story) return;
        if (sessionVersion < 9) RejectFuture(story);
        MigrateStory(story, legacy: sessionVersion < 9);
    }

    private static void RejectFuture(JsonObject story)
    {
        if (story["Scene"] is JsonObject scene &&
                (scene.ContainsKey("ItemIllustration") || scene.ContainsKey("OwnedEffects")) ||
            Steps(story).Any(step => Kind(step) is "item-illustration" or
                "reset-standing-registration" or "format-only-message" or "owned-effect") ||
            Texts(story).Any(text => text.ContainsKey("FormatOnly")))
            throw new InvalidDataException("Legacy story contains future invocation presentation state.");
    }

    private static void MigrateStory(JsonObject story, bool legacy)
    {
        if (story["Scene"] is not JsonObject scene) throw Invalid();
        if (legacy)
        {
            // Absence of an old owned-slot snapshot is unknown, not a claim
            // that the original presentation context held no effects.
            scene["ItemIllustration"] = null;
            scene["OwnedEffects"] = null;
            foreach (var text in Texts(story)) text["FormatOnly"] = false;
        }
        ValidateStory(story);
    }

    private static void ValidateStory(JsonObject story)
    {
        if (story["Scene"] is not JsonObject scene ||
            !scene.ContainsKey("ItemIllustration") || !scene.ContainsKey("OwnedEffects") ||
            scene["ItemIllustration"] is JsonNode item &&
                (item is not JsonObject illustration || !ItemIllustration(illustration, step: false)) ||
            scene["OwnedEffects"] is JsonNode owned && !OwnedEffects(owned)) throw Invalid();

        foreach (var step in Steps(story))
            switch (Kind(step))
            {
                case "item-illustration":
                    if (!ItemIllustration(step, step: true)) throw Invalid();
                    break;
                case "reset-standing-registration":
                    if (!Keys(step, "Kind")) throw Invalid();
                    break;
                case "format-only-message":
                    if (!FormatOnlyMessage(step)) throw Invalid();
                    break;
                case "owned-effect":
                    if (!Keys(step, "Kind", "Operation", "EffectId", "SelectorMode") ||
                        !Integer(step["Operation"], 0, 1) ||
                        (step["Operation"]!.GetValue<int>() == 0
                            ? !Integer(step["EffectId"], 118, 129) || !Integer(step["SelectorMode"], 1, 2)
                            : !Integer(step["EffectId"], 0, 0) || !Integer(step["SelectorMode"], 0, 0))) throw Invalid();
                    break;
            }
        foreach (var text in Texts(story))
            if (text["FormatOnly"] is not JsonValue format || !format.TryGetValue<bool>(out _)) throw Invalid();
    }

    private static bool ItemIllustration(JsonObject value, bool step) =>
        (step ? Keys(value, "Kind", "IllustrationId", "Setting") : Keys(value, "IllustrationId", "Setting")) &&
        (value["IllustrationId"] is null || Integer(value["IllustrationId"], 0, 456)) &&
        Integer(value["Setting"], int.MinValue, int.MaxValue);

    private static bool FormatOnlyMessage(JsonObject step) =>
        Keys(step, "Kind", "Speaker", "MessageId", "People", "Names", "Cities", "Forces", "Areas", "Items", "Numbers", "Provinces") &&
        Integer(step["Speaker"], 0, ushort.MaxValue) && Integer(step["MessageId"], int.MinValue, int.MaxValue) &&
        step["People"] is JsonArray people && people.Count == 5 &&
            people.All(person => person is null || Integer(person, 0, ushort.MaxValue)) &&
        (step["Names"] is null || step["Names"] is JsonArray names && names.Count == 5 &&
            names.All(name => name is null || name is JsonValue text && text.TryGetValue<string>(out string? value) && value is not null)) &&
        Bank(step["Cities"], ushort.MaxValue) && Bank(step["Forces"], ushort.MaxValue) &&
        Bank(step["Areas"], ushort.MaxValue) && CampaignStoryMessageBankSaveFields.BanksValid(step);

    private static bool Bank(JsonNode? node, int max) => node is null ||
        node is JsonArray bank && bank.Count == 5 && bank.All(value => Integer(value, 0, max));

    private static bool OwnedEffects(JsonNode node)
    {
        if (node is not JsonArray slots || slots.Count > 3) return false;
        var seen = new HashSet<int>();
        foreach (var entry in slots)
            if (entry is not JsonObject slot || !Keys(slot, "Slot", "EffectId", "SelectorMode") ||
                !Integer(slot["Slot"], 0, 2) || !seen.Add(slot["Slot"]!.GetValue<int>()) ||
                !Integer(slot["EffectId"], 118, 129) || !Integer(slot["SelectorMode"], 1, 2)) return false;
        return true;
    }

    private static IEnumerable<JsonObject> Stories(JsonObject state)
    {
        if (state["NativeNormalTurnContinuation"]?["StoryPlayback"]?["Story"] is JsonObject normal) yield return normal;
        if (state["EndingProgress"]?["Story"] is JsonObject ending) yield return ending;
        if (state["TutorialLessonProgress"]?["Story"] is JsonObject tutorial) yield return tutorial;
        if (state["EditedEventPlayback"]?["Story"] is JsonObject edited) yield return edited;
        if (state["CampaignEventCommandPlayback"]?["Invocation"]?["Story"] is JsonObject command) yield return command;
    }

    private static IEnumerable<JsonObject> Steps(JsonObject story)
    {
        if (story["Steps"] is JsonArray steps)
            foreach (var step in steps.OfType<JsonObject>()) yield return step;
        if (story["Scene"]?["Text"] is JsonObject text) yield return text;
    }

    private static IEnumerable<JsonObject> Texts(JsonObject story)
    {
        if (story["PreparedStep"]?["Text"] is JsonObject current) yield return current;
        if (story["Scene"]?["PreparedText"] is JsonObject previous) yield return previous;
        if (story["TextLog"] is JsonArray log)
            foreach (var text in log.OfType<JsonObject>()) yield return text;
    }

    private static string? Kind(JsonObject step) => step["Kind"] is JsonValue kind &&
        kind.TryGetValue<string>(out string? value) ? value : null;
    private static bool Keys(JsonObject value, params string[] keys) => value.Count == keys.Length && keys.All(value.ContainsKey);
    private static bool Integer(JsonNode? node, int min, int max) => node is JsonValue value &&
        value.TryGetValue<int>(out int number) && number >= min && number <= max;
    private static InvalidDataException Invalid() => new("Saved invocation story presentation is missing or invalid.");
}
