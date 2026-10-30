using System.Text.Json.Nodes;

namespace San14.Core;

internal static class PersonnelIntelligenceNarrationSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 223) return;
        if (Tasks(state)?.Any(pair => pair.Value is JsonObject task && task.ContainsKey("NarrationProgress")) == true ||
            state["NativePeriodContinuation"]?["Cursor"] is JsonObject cursor && cursor.ContainsKey("AwaitingIntelligenceTaskId"))
            throw Invalid();
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (state["NativePeriodContinuation"]?["Cursor"] is JsonObject cursor && version < 223)
            cursor["AwaitingIntelligenceTaskId"] = null;
        if (version < 223 && Tasks(state) is { } tasks)
        {
            foreach (var pair in tasks)
            {
                if (pair.Value is not JsonObject task || task["Started"] is not JsonValue started ||
                    !started.TryGetValue<bool>(out bool hasStarted)) throw Invalid();
                //Every reached original wait is preceded by Started40. An old
                //started task lacks the local program point and stays unknown.
                task["NarrationProgress"] = hasStarted ? null : new JsonObject { ["Pending"] = null };
            }
        }
        ValidateWorld(state);
    }

    internal static void ValidateWorld(JsonObject state)
    {
        var cursor = state["NativePeriodContinuation"]?["Cursor"] as JsonObject;
        int? waiting = null;
        if (cursor is not null)
        {
            if (!cursor.ContainsKey("AwaitingIntelligenceTaskId")) throw Invalid();
            if (cursor["AwaitingIntelligenceTaskId"] is JsonNode value)
                waiting = Integer(value, 0, int.MaxValue);
        }
        int pendingCount = 0;
        if (Tasks(state) is { } tasks)
        {
            foreach (var pair in tasks)
            {
                if (pair.Value is not JsonObject task || !task.ContainsKey("NarrationProgress")) throw Invalid();
                if (task["NarrationProgress"] is null) continue;
                var progress = Object(task["NarrationProgress"], "Pending");
                if (progress["Pending"] is null) continue;
                pendingCount++;
                var pending = ValidatePending(progress["Pending"]);
                int taskId = Integer(task["Id"], 0, int.MaxValue);
                if (waiting != taskId || cursor is null || Integer(cursor["StageIndex"], 0, byte.MaxValue) != 0 ||
                    Integer(cursor["PersonnelAfterSlot"], 0, int.MaxValue) != Integer(task["DispatchSlot"], 0, int.MaxValue) ||
                    !JsonNode.DeepEquals(cursor["Date"], pending["SourceDate"]) ||
                    task["Started"] is not JsonValue started || !started.TryGetValue<bool>(out bool isStarted) || !isStarted ||
                    task["IsRetiring"] is not JsonValue retiring || !retiring.TryGetValue<bool>(out bool isRetiring) || isRetiring)
                    throw Invalid();
                int kind = Integer(task["Kind"], 0, 1);
                var branch = (PersonnelIntelligenceNarrationBranch)Integer(pending["Branch"], 0, 5);
                if ((kind == (int)PersonnelIntelligenceKind.Inspect) !=
                    (branch is PersonnelIntelligenceNarrationBranch.CancelInspect or
                        PersonnelIntelligenceNarrationBranch.InspectSuccess)) throw Invalid();
                if (pending["AwayOutcome"] is JsonObject outcome &&
                    (outcome["Succeeded"]!.GetValue<bool>() != (branch != PersonnelIntelligenceNarrationBranch.AwayFailure) ||
                     Integer(outcome["Duration"], int.MinValue, int.MaxValue) != Integer(task["DurationOrLevel"], int.MinValue, int.MaxValue)))
                    throw Invalid();
                ValidatePresentation(state, pending, branch);
            }
        }
        if (waiting is not null && pendingCount != 1 || waiting is null && pendingCount != 0) throw Invalid();
    }

    private static void ValidatePresentation(JsonObject state, JsonObject pending,
        PersonnelIntelligenceNarrationBranch branch)
    {
        long sequence = pending["NarrationBatchSequence"]!.GetValue<long>();
        var matches = (state["CampaignEffectRequests"]?["PendingBatches"] as JsonArray)?
            .OfType<JsonObject>().Where(batch => batch["Sequence"] is JsonValue identity &&
                identity.TryGetValue<long>(out long value) && value == sequence).ToArray() ?? [];
        if (matches.Length > 1) throw Invalid();
        int speech = 0, sound = 0;
        foreach (var batch in matches)
        {
            if (Integer(batch["Source"], 0, int.MaxValue) != (int)CampaignEffectSource.IntelligenceCommands ||
                batch["Requests"] is not JsonArray requests) throw Invalid();
            foreach (var request in requests.OfType<JsonObject>())
            {
                if (request["TalentCommandPresentation"] is not JsonObject message) continue;
                int operation = Integer(message["Operation"], 0, int.MaxValue);
                if (operation == (int)TalentCommandPresentationOperation.SoundCue)
                {
                    if (speech != 0 || branch != PersonnelIntelligenceNarrationBranch.AwayTargetNotice ||
                        Integer(message["SoundId"], 0, int.MaxValue) != 6) throw Invalid();
                    sound++;
                }
                else if (operation == (int)TalentCommandPresentationOperation.OfficerText ||
                    operation == (int)TalentCommandPresentationOperation.ConditionalOfficerText)
                {
                    if (message["TextAtSource"] is not JsonValue text || !text.TryGetValue<string>(out string? value) ||
                        !string.Equals(value, pending["MainTextAtSource"]!.GetValue<string>(), StringComparison.Ordinal) ||
                        message["Speaker"] is not JsonObject ||
                        branch == PersonnelIntelligenceNarrationBranch.AwayTargetNotice &&
                            (operation != (int)TalentCommandPresentationOperation.OfficerText ||
                                Integer(message["OfficerId"], 0, int.MaxValue) != 5012)) throw Invalid();
                    speech++;
                }
                else throw Invalid();
            }
        }
        int phase = Integer(pending["Phase"], 0, 1);
        if (phase == (int)PersonnelIntelligenceNarrationPhase.AwaitingNarration
            ? speech != 1 || sound > 1
            : speech != 0 || sound != 0) throw Invalid();
    }

    private static JsonObject ValidatePending(JsonNode? node)
    {
        var pending = Object(node, "Branch", "Phase", "SourceDate", "ActorPhysicalSlot",
            "NarrationBatchSequence", "MainTextAtSource", "AwayOutcome");
        var branch = (PersonnelIntelligenceNarrationBranch)Integer(pending["Branch"], 0, 5);
        _ = Integer(pending["Phase"], 0, 1);
        _ = Integer(pending["ActorPhysicalSlot"], 0, int.MaxValue);
        if (pending["NarrationBatchSequence"] is not JsonValue sequence ||
            !sequence.TryGetValue<long>(out long batch) || batch <= 0 ||
            pending["MainTextAtSource"] is not JsonValue text || !text.TryGetValue<string>(out string? value) || value is null)
            throw Invalid();
        var date = Object(pending["SourceDate"], "Year", "Month", "Day", "Hour");
        //Match the owned native signed-date fields. Reached calendar writers
        //own calendar legality; this wait adds only its actual hour8 boundary.
        _ = Integer(date["Year"], short.MinValue, short.MaxValue);
        _ = Integer(date["Month"], sbyte.MinValue, sbyte.MaxValue);
        _ = Integer(date["Day"], sbyte.MinValue, sbyte.MaxValue);
        if (Integer(date["Hour"], 0, 23) != 8) throw Invalid();
        bool awayOutcome = branch is PersonnelIntelligenceNarrationBranch.AwayFailure or
            PersonnelIntelligenceNarrationBranch.AwaySuccess or
            PersonnelIntelligenceNarrationBranch.AwayTargetNotice;
        if (!awayOutcome)
        {
            if (pending["AwayOutcome"] is not null) throw Invalid();
            return pending;
        }
        var outcome = Object(pending["AwayOutcome"], "OwnForcePhysicalSlot", "TargetForcePhysicalSlot",
            "ActorDistrictPhysicalSlot", "OwnRulerPhysicalSlot", "TargetRulerPhysicalSlot", "Succeeded",
            "Injured", "Gain", "Loss", "Duration");
        foreach (string key in new[] { "OwnForcePhysicalSlot", "TargetForcePhysicalSlot", "ActorDistrictPhysicalSlot" })
            _ = Integer(outcome[key], 0, 51);
        foreach (string key in new[] { "OwnRulerPhysicalSlot", "TargetRulerPhysicalSlot" })
            _ = Integer(outcome[key], 0, int.MaxValue);
        foreach (string key in new[] { "Gain", "Loss", "Duration" })
            _ = Integer(outcome[key], int.MinValue, int.MaxValue);
        foreach (string key in new[] { "Succeeded", "Injured" })
            if (outcome[key] is not JsonValue flag || !flag.TryGetValue<bool>(out _)) throw Invalid();
        return pending;
    }

    private static JsonObject? Tasks(JsonObject state) => state["PersonnelCommands"]?["Intelligences"] as JsonObject;
    private static JsonObject Object(JsonNode? node, params string[] keys)
        => node is JsonObject value && value.Count == keys.Length && keys.All(value.ContainsKey) ? value : throw Invalid();
    private static int Integer(JsonNode? node, int minimum, int maximum)
        => node is JsonValue value && value.TryGetValue<int>(out int number) && number >= minimum && number <= maximum
            ? number : throw Invalid();
    private static InvalidDataException Invalid() => new("Saved intelligence narration continuation is missing or invalid.");
}
