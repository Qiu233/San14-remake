using System.Text.Json;
using System.Text.Json.Nodes;

namespace San14.Core;

internal static class EditedEventPlaybackSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 190 && state["EditedEventPlayback"] is not null)
            throw new InvalidDataException("Legacy world contains a future manual edited-event playback.");
        if (version < 191 && state["EditedEventPlayback"] is JsonObject playback &&
            (playback["Summary"] is not null || Integer(playback["Phase"], 2, 2)))
            throw new InvalidDataException("Legacy world contains a future edited-event result page.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        const string key = "EditedEventPlayback";
        if (version < 190) { state[key] = null; return; }
        if (!state.ContainsKey(key)) throw Invalid();
        if (state[key] is not JsonNode node) return;
        if (node is not JsonObject playback) throw Invalid();
        if (version < 191)
        {
            if (!Keys(playback, "Slot", "Source", "Story", "Phase")) throw Invalid();
            playback["Summary"] = null;
        }
        if (version < 222)
        {
            if (!Keys(playback, "Slot", "Source", "Story", "Phase", "Summary") || !Integer(playback["Phase"], 0, 2))
                throw Invalid();
            //The source-time City transitions cannot be recovered from a
            //later saved World, including an old already prepared Summary.
            playback["StateControl"] = JsonSerializer.SerializeToNode(CampaignStoryStateControlProgress.Unavailable);
        }
        if (!Keys(playback, "Slot", "Source", "Story", "Phase", "Summary", "StateControl") ||
            !Integer(playback["Slot"], 0, 99) || !Integer(playback["Phase"], 0, 3) ||
            playback["Source"] is not JsonObject source || !Keys(source, "ResourceUri", "ResourceSha256", "DirectoryIndex", "EventId") ||
            source["ResourceUri"]?.GetValue<string>() != "san14://user/edited-events" ||
            source["ResourceSha256"] is not JsonValue revision || !revision.TryGetValue<string>(out string? hash) ||
            hash is not { Length: 64 } || hash.Any(c => !Uri.IsHexDigit(c)) ||
            !Integer(source["DirectoryIndex"], 0, 99) || !Integer(source["EventId"], 0, 99) ||
            playback["Story"] is not JsonObject story || !Keys(story, "Sequence", "Source", "ContentRevision",
                "Steps", "NextStep", "Scene", "PreparedStep", "TextLog", "SelectionContext") ||
            story["Sequence"] is not JsonValue sequence || !sequence.TryGetValue<long>(out long id) || id < 1 ||
            story["ContentRevision"] is not JsonValue content || !content.TryGetValue<string>(out string? text) ||
            string.IsNullOrWhiteSpace(text) || story["Steps"] is not JsonArray steps ||
            !Integer(story["NextStep"], 0, steps.Count) || story["Scene"] is not JsonObject)
            throw Invalid();
        if (playback["Summary"] is JsonNode summaryNode &&
            (summaryNode is not JsonObject summary || !Keys(summary, "Locale", "Title", "Body", "Hint") ||
                !Text(summary["Locale"]) || summary["Locale"]!.GetValue<string>() is not ("TC" or "SC") ||
                !Text(summary["Title"]) || !Text(summary["Body"]) || !Text(summary["Hint"]))) throw Invalid();
        ValidateStateControl(playback["StateControl"]);
        if (Integer(playback["Phase"], 3, 3) && state["EditedEventResultCursor"] is not null) throw Invalid();
        CampaignStoryPreparationSaveFields.ValidatePreparedStory(story);
        // Typed validation after deserialization checks Source equality,
        // actual phase/cursor and the exclusive Strategy lifecycle.
    }

    private static void ValidateStateControl(JsonNode? node)
    {
        if (node is not JsonObject progress ||
            !Keys(progress, "Pairs", "HasChanges", "NextOperation", "PendingNotice", "AwaitingPresentationBatch") ||
            !Integer(progress["NextOperation"], 0, 69) ||
            progress["HasChanges"] is JsonNode changed && (changed is not JsonValue value || !value.TryGetValue<bool>(out _)) ||
            progress["Pairs"] is JsonNode pairs && (pairs is not JsonArray rows || rows.Count != 23 || rows.Any(pair =>
                pair is not JsonObject row || !Keys(row, "GainedForcePhysicalSlot", "LostForcePhysicalSlot") ||
                !Force(row["GainedForcePhysicalSlot"]) || !Force(row["LostForcePhysicalSlot"]))) ||
            progress["AwaitingPresentationBatch"] is JsonNode batch &&
                (batch is not JsonValue number || !number.TryGetValue<long>(out long sequence) || sequence < 1) ||
            progress["PendingNotice"] is JsonNode pending && (pending is not JsonObject notice ||
                !Keys(notice, "Province", "Force", "Gained", "TextAtSource") ||
                !Integer(notice["Province"], 0, 22) || !Integer(notice["Force"], 0, 51) ||
                notice["Gained"] is not JsonValue gained || !gained.TryGetValue<bool>(out _) ||
                notice["TextAtSource"] is not JsonValue text || !text.TryGetValue<string>(out string? message) || message is null))
            throw Invalid();
        try
        {
            (progress.Deserialize<CampaignStoryStateControlProgress>() ?? throw Invalid()).Validate();
        }
        catch (JsonException error) { throw new InvalidDataException("Invalid edited-event province-control format.", error); }
    }

    private static bool Force(JsonNode? node) => node is null || Integer(node, 0, 51);
    private static bool Keys(JsonObject value, params string[] keys) =>
        value.Count == keys.Length && keys.All(value.ContainsKey);
    private static bool Integer(JsonNode? node, int minimum, int maximum) => node is JsonValue value &&
        value.TryGetValue<int>(out int number) && number >= minimum && number <= maximum;
    private static bool Text(JsonNode? node) => node is JsonValue value &&
        value.TryGetValue<string>(out string? text) && text is not null && !text.Contains('\0');
    private static InvalidDataException Invalid() => new("Saved manual edited-event playback is missing or invalid.");
}
