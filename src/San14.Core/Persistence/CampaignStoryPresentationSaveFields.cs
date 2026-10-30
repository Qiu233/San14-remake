using System.Text.Json.Nodes;

namespace San14.Core;

internal static class CampaignStoryPresentationSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        foreach (var story in Stories(state))
        {
            if (version < 210 && story["Scene"]?["PortraitDarkFlags"] is not null)
                throw new InvalidDataException("Legacy Story contains future portrait layout states.");
            if (version < 208 && Has143Step(story))
                throw new InvalidDataException("Legacy story contains future poem or sound-control semantics.");
            if (version < 201 && Steps(story).Any(step => Kind(step) == "input-wait"))
                throw new InvalidDataException("Legacy story contains future input-wait semantics.");
            if (version < 198) RejectMessageLifecycleFuture(story);
            if (version < 191 && (Steps(story).Any(step => Kind(step) == "audio" &&
                    Integer(step["AudioKind"], 7, int.MaxValue)) ||
                story["Scene"]?["Music"] is JsonObject directMusic && Integer(directMusic["AudioKind"], 7, int.MaxValue)))
                throw new InvalidDataException("Legacy story contains future direct music semantics.");
            if (version < 190 && (Steps(story).Any(step => Kind(step) == "audio" &&
                    Integer(step["AudioKind"], 6, int.MaxValue)) ||
                story["Scene"]?["Music"] is JsonObject music && Integer(music["AudioKind"], 6, int.MaxValue)))
                throw new InvalidDataException("Legacy story contains future music fade semantics.");
            if (version < 182 && Steps(story).Any(step => step["Forces"] is not null || step["Areas"] is not null))
                throw new InvalidDataException("Legacy story contains future Force or Area message parameters.");
            if (version >= 181) continue;
            if (story["Scene"]?["SmallIllustration"] is not null || story["Scene"]?["TutorialIllustration"] is not null ||
                Steps(story).Any(step => Kind(step) is "small-illustration" or "tutorial-illustration" or "hide-characters" ||
                    step["Cities"] is not null || step["SpeechOnly"] is JsonNode speechOnly && !IsFalse(speechOnly) ||
                    step["ResourceKind"] is JsonNode kind && !Integer(kind, 15, 15) ||
                    step["FadeIn"] is JsonNode fade && !IsTrue(fade)) ||
                Texts(story).Any(text => text["SpeechOnly"] is JsonNode speechOnly && !IsFalse(speechOnly)))
                throw new InvalidDataException("Legacy story contains future presentation semantics.");
        }
    }

    internal static void Migrate(JsonObject state, int version)
    {
        foreach (var story in Stories(state))
        {
            MigratePortraitDark(story, legacy: version < 210);
            MigrateMessageLifecycle(story, legacy: version < 198);
            if (story["Scene"] is not JsonObject scene) throw Invalid();
            if (version < 181) scene["SmallIllustration"] = null;
            else if (!scene.ContainsKey("SmallIllustration") ||
                scene["SmallIllustration"] is JsonNode small && !Illustration(small)) throw Invalid();
            if (version < 181) scene["TutorialIllustration"] = null;
            else if (!scene.ContainsKey("TutorialIllustration") ||
                scene["TutorialIllustration"] is JsonNode tutorial && !Illustration(tutorial)) throw Invalid();
            foreach (var step in Steps(story))
            {
                string? kind = Kind(step);
                bool background = ReferenceEquals(step, scene["Background"]) || kind == "background";
                if (background)
                {
                    if (version < 181) { step["ResourceKind"] = 15; step["FadeIn"] = true; }
                    else if (!Integer(step["ResourceKind"], 14, 15) || !Bool(step["FadeIn"])) throw Invalid();
                }
                if (kind is "message" or "narration")
                {
                    if (version < 181) step["Cities"] = null;
                    else if (!step.ContainsKey("Cities") || step["Cities"] is JsonNode cities &&
                        (cities is not JsonArray slots || slots.Count != 5 || slots.Any(id => !Integer(id, 0, ushort.MaxValue)))) throw Invalid();
                }
                if (kind is "message" or "narration")
                    foreach (string key in new[] { "Forces", "Areas" })
                    {
                        if (version < 182) step[key] = null;
                        else if (!step.ContainsKey(key) || step[key] is JsonNode bank &&
                            (bank is not JsonArray parameters || parameters.Count != 5 ||
                             parameters.Any(id => !Integer(id, 0, ushort.MaxValue)))) throw Invalid();
                    }
                if (kind == "message")
                {
                    if (version < 181) step["SpeechOnly"] = false;
                    else if (!Bool(step["SpeechOnly"])) throw Invalid();
                }
                if (kind is "small-illustration" or "tutorial-illustration" && !Illustration(step)) throw Invalid();
                if (kind == "hide-characters" && (!Integer(step["Setting"], int.MinValue, int.MaxValue) ||
                    step["Characters"] is not JsonArray characters || characters.Count > 7 || characters.Any(id => !Integer(id, 1, ushort.MaxValue)))) throw Invalid();
            }
            foreach (var text in Texts(story))
            {
                if (version < 181) text["SpeechOnly"] = false;
                else if (!Bool(text["SpeechOnly"])) throw Invalid();
            }
        }
    }

    // Operation Story metadata is independent of the compressed world payloads.
    // Session10 introduces the World198 message lifecycle; Session11 adds
    // the actual input-wait step also available in the World201 owners.
    internal static void MigrateOperationMetadata(JsonObject metadata, int sessionVersion)
    {
        if (metadata["Story"] is not JsonObject story) return;
        if (sessionVersion < 14 && story["Scene"]?["PortraitDarkFlags"] is not null)
            throw new InvalidDataException("Legacy operation Story contains future portrait layout states.");
        MigratePortraitDark(story, legacy: sessionVersion < 14);
        if (sessionVersion < 12 && Has143Step(story))
            throw new InvalidDataException("Legacy operation story contains future poem or sound-control semantics.");
        if (sessionVersion < 11 && Steps(story).Any(step => Kind(step) == "input-wait"))
            throw new InvalidDataException("Legacy operation story contains future input-wait semantics.");
        if (sessionVersion < 10) RejectMessageLifecycleFuture(story);
        MigrateMessageLifecycle(story, legacy: sessionVersion < 10);
    }

    internal static void ValidateWorld(JsonObject state)
    {
        foreach (var story in Stories(state)) ValidateMessageLifecycle(story);
    }

    private static void RejectMessageLifecycleFuture(JsonObject story)
    {
        if (story["Scene"] is JsonObject scene && scene.ContainsKey("RetainedSpeakerName") ||
            Steps(story).Any(step => Kind(step) is "hide-message" or "wait" ||
            Kind(step) == "message" && step.ContainsKey("WithoutPortrait")))
            throw new InvalidDataException("Legacy story contains future message lifecycle semantics.");
    }

    private static void MigrateMessageLifecycle(JsonObject story, bool legacy)
    {
        if (legacy)
        {
            if (story["Scene"] is not JsonObject scene) throw Invalid();
            scene["RetainedSpeakerName"] = null;
            foreach (var step in Steps(story))
                if (Kind(step) == "message") step["WithoutPortrait"] = false;
        }
        ValidateMessageLifecycle(story);
    }

    private static void MigratePortraitDark(JsonObject story, bool legacy)
    {
        if (story["Scene"] is not JsonObject scene) throw Invalid();
        if (legacy) scene["PortraitDarkFlags"] = null;
    }

    private static void ValidateMessageLifecycle(JsonObject story)
    {
        if (story["Scene"] is not JsonObject scene || !scene.ContainsKey("PortraitDarkFlags") ||
            scene["PortraitDarkFlags"] is JsonNode flags &&
                (flags is not JsonArray dark || dark.Count != 5 || dark.Any(value => value is not null && !Bool(value))) ||
            !scene.ContainsKey("RetainedSpeakerName") ||
            scene["RetainedSpeakerName"] is JsonNode name &&
            (name is not JsonValue value || !value.TryGetValue<string>(out _))) throw Invalid();
        foreach (var step in Steps(story))
            switch (Kind(step))
            {
                case "message":
                    if (!Bool(step["WithoutPortrait"])) throw Invalid();
                    break;
                case "poem-event":
                    if (step.Count != 2 || !Integer(step["RawId"], 1, 20)) throw Invalid();
                    break;
                case "input-wait":
                case "hide-message":
                    if (step.Count != 1 || !step.ContainsKey("Kind")) throw Invalid();
                    break;
                case "wait":
                    if (step.Count != 2 || !Integer(step["Milliseconds"], int.MinValue, int.MaxValue)) throw Invalid();
                    break;
            }
    }

    private static bool Has143Step(JsonObject story) => Steps(story).Any(New143Step) ||
        story["Scene"]?["Music"] is JsonObject music && Integer(music["AudioKind"], 8, int.MaxValue);
    private static bool New143Step(JsonObject step) => Kind(step) == "poem-event" ||
        Kind(step) == "audio" && Integer(step["AudioKind"], 8, int.MaxValue);
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
        if (story["Scene"]?["Background"] is JsonObject background) yield return background;
        if (story["Scene"]?["Text"] is JsonObject text) yield return text;
    }
    private static IEnumerable<JsonObject> Texts(JsonObject story)
    {
        if (story["PreparedStep"]?["Text"] is JsonObject current) yield return current;
        if (story["Scene"]?["PreparedText"] is JsonObject previous) yield return previous;
        if (story["TextLog"] is JsonArray log)
            foreach (var text in log.OfType<JsonObject>()) yield return text;
    }
    private static string? Kind(JsonObject step) => step["Kind"] is JsonValue kind && kind.TryGetValue<string>(out string? value) ? value : null;
    private static bool Illustration(JsonNode node) => node is JsonObject illustration &&
        Integer(illustration["IllustrationId"], int.MinValue, int.MaxValue) && Integer(illustration["Setting"], int.MinValue, int.MaxValue);
    private static bool Bool(JsonNode? node) => node is JsonValue value && value.TryGetValue<bool>(out _);
    private static bool IsTrue(JsonNode? node) => node is JsonValue value && value.TryGetValue<bool>(out bool flag) && flag;
    private static bool IsFalse(JsonNode? node) => node is JsonValue value && value.TryGetValue<bool>(out bool flag) && !flag;
    private static bool Integer(JsonNode? node, int min, int max) => node is JsonValue value && value.TryGetValue<int>(out int number) && number >= min && number <= max;
    private static InvalidDataException Invalid() => new("Saved story presentation semantics are missing or invalid.");
}
