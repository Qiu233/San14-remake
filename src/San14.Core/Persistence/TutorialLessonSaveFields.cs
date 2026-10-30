using System.Text.Json.Nodes;

namespace San14.Core;

internal static class TutorialLessonSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 181 && state["TutorialLessonProgress"] is not null)
            throw new InvalidDataException("Legacy world contains future tutorial lesson progress.");
        if (version < 182 && state["TutorialLessonProgress"]?["MessageContext"] is JsonObject context &&
            (context["Forces"] is not null || context["Areas"] is not null))
            throw new InvalidDataException("Legacy lesson contains future Force or Area message parameters.");
        if (version < 183 && state["TutorialLessonProgress"]?["HomeCommandElapsedMilliseconds"] is not null)
            throw new InvalidDataException("Legacy lesson contains future Home intelligence command timing.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        const string key = "TutorialLessonProgress";
        if (version < 181) { state[key] = null; return; }
        if (!state.ContainsKey(key)) throw Invalid();
        if (state[key] is not JsonNode node) return;
        if (node is not JsonObject progress) throw Invalid();
        if (version < 183) progress["HomeCommandElapsedMilliseconds"] = null;
        //The completed first-lesson format had no commander-Army restriction.
        //Its missing field means no installed restriction, never an inferred slot.
        if (!progress.ContainsKey("RestrictedArmyPhysicalSlot") && Integer(progress["CourseIndex"], 0, 0))
            progress["RestrictedArmyPhysicalSlot"] = null;
        // Earlier completed courses do not install the later focus/wave controllers.
        if (Integer(progress["CourseIndex"], 0, 1))
        {
            if (!progress.ContainsKey("AreaSupportElapsedMilliseconds")) progress["AreaSupportElapsedMilliseconds"] = null;
            if (!progress.ContainsKey("FourthAttackReinforcementCount")) progress["FourthAttackReinforcementCount"] = 0;
            if (!progress.ContainsKey("FourthAttackRefreshRequested")) progress["FourthAttackRefreshRequested"] = false;
        }
        if (!progress.ContainsKey("TerritoryTransferCount") && Integer(progress["CourseIndex"], 0, 1))
            progress["TerritoryTransferCount"] = null;
        if (!progress.ContainsKey("RestrictedArmyPhysicalSlots") && Integer(progress["CourseIndex"], 0, 1))
            progress["RestrictedArmyPhysicalSlots"] = null;
        if (!progress.ContainsKey("FourthConstructionCommanderId") && Integer(progress["CourseIndex"], 0, 1))
            progress["FourthConstructionCommanderId"] = null;
        if (progress.Count != 22 ||
            !new[] { "CourseIndex", "Stage", "ParagraphPhases", "CompletedParagraphMask", "Sequence", "Story",
                "EnabledCommandMask", "ActiveParagraphIndex", "CompletionCourseIndex", "InstructionIndex", "MessageContext",
                "CompletionNotices", "CompletionNoticeIndex", "CorrectionText", "RestrictedArmyPhysicalSlot", "AreaSupportElapsedMilliseconds",
                "FourthAttackReinforcementCount", "FourthAttackRefreshRequested", "TerritoryTransferCount", "RestrictedArmyPhysicalSlots", "FourthConstructionCommanderId", "HomeCommandElapsedMilliseconds" }.All(progress.ContainsKey)) throw Invalid();
        if (progress["CourseIndex"] is not JsonValue courseValue || !courseValue.TryGetValue<int>(out int courseIndex) ||
            courseIndex is < 0 or > 7) throw Invalid();
        int paragraphCount = TutorialLessonProgress.ParagraphCount(courseIndex);
        if (!Integer(progress["Stage"], 0, int.MaxValue) ||
            progress["ParagraphPhases"] is not JsonArray phases || phases.Count != paragraphCount || phases.Any(phase => !Integer(phase, 0, int.MaxValue)) ||
            progress["CompletedParagraphMask"] is not JsonValue mask || !mask.TryGetValue<ulong>(out ulong completed) || completed >= 1UL << paragraphCount ||
            progress["Sequence"] is not JsonValue sequence || !sequence.TryGetValue<long>(out long id) || id < 0 ||
            !Integer(progress["EnabledCommandMask"], 0, ushort.MaxValue) ||
            !Integer(progress["InstructionIndex"], 0, int.MaxValue) ||
            !Integer(progress["CompletionNoticeIndex"], 0, int.MaxValue) ||
            progress["RestrictedArmyPhysicalSlot"] is JsonNode restricted && !Integer(restricted, 0, 500) ||
            progress["ActiveParagraphIndex"] is JsonNode active && !Integer(active, 0, paragraphCount - 1) ||
            progress["CompletionCourseIndex"] is JsonNode course && !Integer(course, 0, 7)) throw Invalid();
        if (progress["HomeCommandElapsedMilliseconds"] is JsonNode homeElapsed &&
            (homeElapsed is not JsonValue homeElapsedValue || !homeElapsedValue.TryGetValue<uint>(out _)) ||
            progress["AreaSupportElapsedMilliseconds"] is JsonNode elapsed &&
            (elapsed is not JsonValue elapsedValue || !elapsedValue.TryGetValue<uint>(out _)) ||
            progress["FourthConstructionCommanderId"] is JsonNode commander && !Integer(commander, 0, 6000) ||
            progress["TerritoryTransferCount"] is JsonNode transfers && !Integer(transfers, 0, int.MaxValue) ||
            !Integer(progress["FourthAttackReinforcementCount"], 0, 5) ||
            progress["FourthAttackRefreshRequested"] is not JsonValue refresh || !refresh.TryGetValue<bool>(out _)) throw Invalid();
        if (progress["RestrictedArmyPhysicalSlots"] is JsonNode restrictions &&
            (restrictions is not JsonArray slotsArray || slotsArray.Count > 501 || slotsArray.Any(slot => !Integer(slot, 0, 500)))) throw Invalid();
        if (progress["Story"] is JsonNode story)
        {
            if (story is not JsonObject request || request.Count != 9 ||
                !new[] { "Sequence", "Source", "ContentRevision", "Steps", "NextStep", "Scene", "PreparedStep", "TextLog", "SelectionContext" }.All(request.ContainsKey)) throw Invalid();
            CampaignStoryPreparationSaveFields.ValidatePreparedStory(request);
        }
        if (version < 182 && progress["MessageContext"] is JsonObject previousContext)
        { previousContext["Forces"] = null; previousContext["Areas"] = null; }
        if (progress["MessageContext"] is JsonNode context &&
            (context is not JsonObject slots || slots.Count != 8 || !CampaignStoryMessageBankSaveFields.BanksValid(slots) ||
             slots["People"] is not JsonArray people || people.Count != 5 || people.Any(id => id is not null && !Integer(id, 0, ushort.MaxValue)) ||
             !slots.ContainsKey("Names") || slots["Names"] is JsonNode names && (names is not JsonArray nameSlots || nameSlots.Count != 5 || nameSlots.Any(name => name is not null && (name is not JsonValue value || !value.TryGetValue<string>(out _)))) ||
             !slots.ContainsKey("Cities") || slots["Cities"] is JsonNode cities && (cities is not JsonArray citySlots || citySlots.Count != 5 || citySlots.Any(id => !Integer(id, 0, ushort.MaxValue))) ||
             !Bank(slots, "Forces") || !Bank(slots, "Areas"))) throw Invalid();
        if (progress["CompletionNotices"] is JsonNode notices &&
            (notices is not JsonArray entries || entries.Any(entry => entry is not JsonObject notice || notice.Count != 2 ||
                notice["TextAtSource"] is not JsonValue text || !text.TryGetValue<string>(out _) || !notice.ContainsKey("SoundId") ||
                notice["SoundId"] is JsonNode sound && !Integer(sound, 5, 5)))) throw Invalid();
        if (progress["CorrectionText"] is JsonNode correction && !CampaignStoryPreparationSaveFields.Text(correction)) throw Invalid();
    }

    private static bool Bank(JsonObject slots, string key) => slots.ContainsKey(key) &&
        (slots[key] is null || slots[key] is JsonArray bank && bank.Count == 5 && bank.All(id => Integer(id, 0, ushort.MaxValue)));
    private static bool Integer(JsonNode? node, int min, int max) => node is JsonValue value && value.TryGetValue<int>(out int number) && number >= min && number <= max;
    private static InvalidDataException Invalid() => new("Saved tutorial lesson progress is missing or invalid.");
}
