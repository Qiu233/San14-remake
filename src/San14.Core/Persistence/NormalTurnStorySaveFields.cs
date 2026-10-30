using System.Text.Json;
using System.Text.Json.Nodes;

namespace San14.Core;

internal static class NormalTurnStorySaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 167 || state["NativeNormalTurnContinuation"] is not JsonObject turn) return;
        if (turn["StoryPlayback"] is not null || turn["NextAction"] is JsonValue value &&
            value.TryGetValue<int>(out int action) && action is 48 or 49)
            throw new InvalidDataException("Legacy normal Turn contains a future story playback.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (state["NativeNormalTurnContinuation"] is not JsonObject turn) return;
        const string key = "StoryPlayback";
        if (version < 167) { turn[key] = null; return; }
        if (!turn.ContainsKey(key)) throw new InvalidDataException("Saved normal Turn story knowledge is missing.");
        if (turn[key] is not JsonNode saved) return;
        if (saved is not JsonObject playback)
            throw new InvalidDataException("Saved normal Turn story playback is not an object.");
        MigratePlayback(playback, version);
    }

    internal static void MigratePlayback(JsonObject playback, int version)
    {
        MigrateChronicle(playback, version);
        MigrateAudioReturn(playback, version);
        MigrateOutcomes(playback, version);
        NormalTurnStoryStateControlSaveFields.Migrate(playback, version);
        bool entry = Integer(playback["Phase"], 3, 4);
        bool completedExecution = MigrateExecutionCursor(playback, version, out bool fullyCompletedExecution);
        if (playback["Story"] is not JsonObject story ||
            story["Sequence"] is not JsonValue sequence || !sequence.TryGetValue<long>(out long id) || id < 1 ||
            story["Source"] is not JsonObject || story["ContentRevision"] is not JsonValue revision ||
            !revision.TryGetValue<string>(out _) || story["Steps"] is not JsonArray steps || steps.Count == 0 && !completedExecution && !entry ||
            !Integer(story["NextStep"], 0, steps.Count) || story["Scene"] is not JsonObject ||
            playback["IsReturningCamera"] is not JsonValue returning || !returning.TryGetValue<bool>(out _) ||
            playback["RemainingEventIds"] is not JsonArray remaining || remaining.Any(item => !Integer(item, 0, 1023)) ||
            playback["Date"] is not JsonObject date || !Integer(date["Year"], 0, 350) ||
            !Integer(date["Month"], 1, 12) || !Integer(date["Day"], 1, 30) ||
            !playback.ContainsKey("ReturnCamera") || playback["ReturnCamera"] is JsonNode camera && !Camera(camera) ||
            version < 168 && (playback["ServiceChanges"] is not JsonObject service || service["Events"] is not JsonArray ||
            service["Achievements"] is not JsonArray || service["ClockReads"] is not JsonArray ||
            service["Dirty"] is not JsonValue dirty || !dirty.TryGetValue<bool>(out _)))
            throw new InvalidDataException("Saved normal Turn story playback is incomplete or invalid.");
        if (Integer(playback["Phase"], 7, 7) && (!fullyCompletedExecution ||
            !Integer(story["NextStep"], steps.Count, steps.Count) ||
            playback["ChronicleProgress"]?["Completed"] is not JsonValue finished ||
            !finished.TryGetValue<bool>(out bool complete) || !complete))
            throw new InvalidDataException("Saved post-Result Story execution or chronology is incomplete.");
    }

    private static void MigrateChronicle(JsonObject playback, int version)
    {
        const string key = "ChronicleProgress";
        if (version < 220)
        {
            if (playback[key] is not null)
                throw new InvalidDataException("Legacy Story contains future chronology continuation knowledge.");
            //An old active invocation keeps an explicit unavailable owner. Its
            //source-time rows cannot be inferred from the later saved world.
            bool active = !Integer(playback["Phase"], 3, 4) &&
                playback["IsReturningCamera"] is JsonValue returning && returning.TryGetValue<bool>(out bool restoring) && !restoring;
            playback[key] = active ? new JsonObject
            {
                ["Deferred"] = false, ["Completed"] = !Integer(playback["Phase"], 0, 0),
                ["WrittenEntry"] = null, ["SourceTimeUnavailable"] = true
            } : null;
            return;
        }
        if (!playback.ContainsKey(key))
            throw new InvalidDataException("Saved Story chronology continuation knowledge is missing.");
        if (playback[key] is null)
        {
            bool camera = playback["IsReturningCamera"] is JsonValue flag && flag.TryGetValue<bool>(out bool value) && value;
            if (!camera && !Integer(playback["Phase"], 3, 4))
                throw new InvalidDataException("Active Story chronology continuation is missing.");
            return;
        }
        if (playback[key] is not JsonObject progress || !Keys(progress, "Deferred", "Completed", "WrittenEntry", "SourceTimeUnavailable") ||
            progress["Deferred"] is not JsonValue deferred || !deferred.TryGetValue<bool>(out bool delayed) ||
            progress["Completed"] is not JsonValue completed || !completed.TryGetValue<bool>(out bool finished) ||
            progress["SourceTimeUnavailable"] is not JsonValue unavailable || !unavailable.TryGetValue<bool>(out bool unknown) ||
            unknown && (delayed || progress["WrittenEntry"] is not null))
            throw new InvalidDataException("Saved Story chronology continuation is invalid.");
        if (progress["WrittenEntry"] is JsonNode node && (node is not JsonObject entry ||
            !Keys(entry, "Primary", "Slot") || entry["Primary"] is not JsonValue primary ||
            !primary.TryGetValue<bool>(out bool ordinary) ||
            !Integer(entry["Slot"], 0, ordinary ? 199 : 29) || delayed && !finished))
            throw new InvalidDataException("Saved Story chronology table slot is invalid.");
    }

    private static void MigrateAudioReturn(JsonObject playback, int version)
    {
        if (version < 202)
        {
            if (playback["AudioReturn"] is not null || playback["Phase"] is not null && !Integer(playback["Phase"], 0, 2))
                throw new InvalidDataException("Legacy normal Story contains future audio lifecycle knowledge.");
            playback["AudioReturn"] = null;
            return;
        }
        if (!playback.ContainsKey("AudioReturn"))
            throw new InvalidDataException("Saved normal Story audio return knowledge is missing.");
        if (playback["AudioReturn"] is JsonNode value && (value is not JsonObject audio ||
            !Keys(audio, "RequestedMusicIndex", "SpecialMode", "InvocationCamera") ||
            audio["InvocationCamera"] is JsonNode camera && !Camera(camera) ||
            audio["RequestedMusicIndex"] is not null && !Integer(audio["RequestedMusicIndex"], 0, 116) ||
            !Integer(audio["SpecialMode"], int.MinValue, int.MaxValue)))
            throw new InvalidDataException("Saved normal Story audio return values are invalid.");
        if (Integer(playback["Phase"], 3, 3) && playback["AudioReturn"] is not null ||
            (Integer(playback["Phase"], 4, 6) || Integer(playback["Phase"], 7, 7)) && playback["AudioReturn"] is null)
            throw new InvalidDataException("Saved normal Story audio phase is invalid.");
    }

    private static void MigrateOutcomes(JsonObject playback, int version)
    {
        if (version < 201)
        {
            if (playback["Outcomes"] is not null || playback["ResultSummary"] is not null ||
                playback["Phase"] is not null && !Integer(playback["Phase"], 0, 0))
                throw new InvalidDataException("Legacy normal Story contains future result knowledge.");
            playback["Outcomes"] = null;
            playback["Phase"] = 0;
            playback["ResultSummary"] = null;
            return;
        }
        if (!playback.ContainsKey("Outcomes") || !playback.ContainsKey("ResultSummary") ||
            !Integer(playback["Phase"], 0, version < 202 ? 2 : version < 221 ? 6 : 7))
            throw new InvalidDataException("Saved normal Story result knowledge is missing.");
        if (playback["Outcomes"] is JsonNode value && !Outcomes(value))
            throw new InvalidDataException("Saved normal Story result occurrences are invalid.");
        if (playback["ResultSummary"] is JsonNode summary &&
            (summary is not JsonObject text || !Keys(text, "Locale", "Title", "Body") ||
             !Text(text["Locale"], out var locale) || locale is not ("TC" or "SC") ||
             !Text(text["Title"], out _) || !Text(text["Body"], out _)))
            throw new InvalidDataException("Saved normal Story result text is invalid.");
        bool noResult = Integer(playback["Phase"], 0, 0) || Integer(playback["Phase"], 3, 4);
        bool result = Integer(playback["Phase"], 1, 2);
        if (noResult && playback["ResultSummary"] is not null ||
            result && (playback["ResultSummary"] is null || playback["Outcomes"] is null))
            throw new InvalidDataException("Saved normal Story result phase is invalid.");
    }

    private static bool Outcomes(JsonNode node)
    {
        if (node is not JsonObject value || !Keys(value, "RaisedArmyPersonSlots", "AlliancesCreated", "AlliancesExtended",
            "AlliancesCancelled", "DeathPersonSlots", "FreePersonSlots", "CapturedByForce", "JoinedByForce",
            "TroopIncreasedBases", "StatusChanges", "MeritPersonSlots", "ItemsGainedByPerson", "CurrentPlayerCityIncreaseKind")) return false;
        foreach (string field in new[] { "RaisedArmyPersonSlots", "DeathPersonSlots", "FreePersonSlots", "MeritPersonSlots" })
            if (!People(value[field])) return false;
        foreach (string field in new[] { "AlliancesCreated", "AlliancesExtended", "AlliancesCancelled" })
            if (value[field] is not JsonArray pairs || pairs.Any(item => item is not JsonObject pair ||
                !Keys(pair, "FirstForcePhysicalSlot", "SecondForcePhysicalSlot") ||
                !Integer(pair["FirstForcePhysicalSlot"], 0, 51) || !Integer(pair["SecondForcePhysicalSlot"], 0, 51))) return false;
        foreach (string field in new[] { "CapturedByForce", "JoinedByForce" })
            if (value[field] is not JsonArray groups || groups.Any(item => item is not JsonObject group ||
                !Keys(group, "ForcePhysicalSlot", "PersonPhysicalSlots") ||
                !Integer(group["ForcePhysicalSlot"], 0, 51) || !People(group["PersonPhysicalSlots"]))) return false;
        if (value["TroopIncreasedBases"] is not JsonArray bases || bases.Any(item => item is not JsonObject receiver ||
            !Keys(receiver, "IsGate", "PhysicalSlot") || receiver["IsGate"] is not JsonValue flag ||
            !flag.TryGetValue<bool>(out bool gate) || !Integer(receiver["PhysicalSlot"], 0, gate ? 10 : 51))) return false;
        if (value["StatusChanges"] is not JsonArray status || status.Any(item => item is not JsonObject change ||
            !Keys(change, "PersonPhysicalSlot", "Status") || !Integer(change["PersonPhysicalSlot"], 0, 1850) ||
            !Integer(change["Status"], int.MinValue, int.MaxValue))) return false;
        if (value["ItemsGainedByPerson"] is not JsonArray items || items.Any(item => item is not JsonObject group ||
            !Keys(group, "PersonPhysicalSlot", "ItemPhysicalSlots") || !Integer(group["PersonPhysicalSlot"], 0, 1850) ||
            group["ItemPhysicalSlots"] is not JsonArray ids || ids.Any(id => !Integer(id, 0, 201)))) return false;
        return Integer(value["CurrentPlayerCityIncreaseKind"], int.MinValue, int.MaxValue);
    }

    private static bool People(JsonNode? node) => node is JsonArray values && values.All(value => Integer(value, 0, 1850));
    private static bool Keys(JsonObject value, params string[] keys) => value.Count == keys.Length && keys.All(value.ContainsKey);
    private static bool Text(JsonNode? node, out string? value)
    {
        value = null;
        return node is JsonValue text && text.TryGetValue(out value) && value is not null;
    }

    private static bool MigrateExecutionCursor(JsonObject playback, int version, out bool fullyCompleted)
    {
        fullyCompleted = false;
        const string key = "ExecutionCursor";
        if (version < 199)
        {
            if (playback[key] is not null)
                throw new InvalidDataException("Legacy normal Story contains a future execution cursor.");
            playback[key] = null;
            return false;
        }
        if (!playback.ContainsKey(key))
            throw new InvalidDataException("Saved normal Story execution cursor knowledge is missing.");
        if (playback[key] is null) return false;
        if (playback[key] is not JsonObject cursor)
            throw new InvalidDataException("Saved normal Story execution cursor is not an object.");
        const string personList = "PersonListContinuation";
        if (version < 200)
        {
            if (cursor[personList] is not null)
                throw new InvalidDataException("Legacy normal Story contains a future person-list continuation.");
            cursor[personList] = null;
        }
        const string forceRank = "ForceRankContinuation";
        if (version < 203)
        {
            if (cursor[forceRank] is not null)
                throw new InvalidDataException("Legacy normal Story contains a future rank continuation.");
            cursor[forceRank] = null;
        }
        const string singleDeath = "SinglePersonDeathContinuation";
        if (version < 205)
        {
            if (cursor[singleDeath] is not null)
                throw new InvalidDataException("Legacy normal Story contains a future single-person death continuation.");
            cursor[singleDeath] = null;
        }
        const string areas = "ConditionAreaPhysicalSlots";
        if (version < 205)
        {
            if (cursor[areas] is not null)
                throw new InvalidDataException("Legacy normal Story contains future Area selection knowledge.");
            cursor[areas] = null;
        }
        const string selectedCities = "SelectedCityPhysicalSlots";
        if (version < 206)
        {
            if (cursor[selectedCities] is not null)
                throw new InvalidDataException("Legacy normal Story contains future target City selections.");
            cursor[selectedCities] = null;
        }
        if (!cursor.ContainsKey(selectedCities) || cursor[selectedCities] is JsonNode selected &&
            (selected is not JsonArray targets || targets.Count != 5 || targets.Any(target => target is not null &&
                (target is not JsonObject choice || !Keys(choice, "PhysicalSlot") ||
                    choice["PhysicalSlot"] is JsonNode physical && !Integer(physical, 0, 51)))))
            throw new InvalidDataException("Saved normal Story target City selection knowledge is invalid.");
        const string forceList = "ConditionForceListPhysicalSlots";
        if (version < 207)
        {
            if (cursor[forceList] is not null)
                throw new InvalidDataException("Legacy normal Story contains a future Force list.");
            cursor[forceList] = null;
        }
        if (!cursor.ContainsKey(forceList) || cursor[forceList] is JsonNode selectedForces &&
            (selectedForces is not JsonArray forceValues || forceValues.Any(force => !Integer(force, 0, 51))))
            throw new InvalidDataException("Saved normal Story Force list knowledge is invalid.");
        const string moveCities = "MoveCityPhysicalSlots";
        if (version < 208)
        {
            if (cursor[moveCities] is not null)
                throw new InvalidDataException("Legacy normal Story contains future move City selections.");
            cursor[moveCities] = null;
        }
        if (!cursor.ContainsKey(moveCities) || cursor[moveCities] is JsonNode moves &&
            (moves is not JsonArray moveTargets || moveTargets.Count != 2 || moveTargets.Any(target => target is not null &&
                (target is not JsonObject choice || !Keys(choice, "PhysicalSlot") ||
                    choice["PhysicalSlot"] is JsonNode physical && !Integer(physical, 0, 51)))))
            throw new InvalidDataException("Saved normal Story move City selection knowledge is invalid.");
        if (cursor.Count != 20 || !cursor.ContainsKey(areas) || cursor[areas] is JsonNode areaValues &&
            (areaValues is not JsonArray areaRows || areaRows.Any(area => !Integer(area, 0, 500))) ||
            !cursor.ContainsKey(singleDeath) ||
            cursor[singleDeath] is JsonNode death && (death is not JsonObject personDeath ||
                !Keys(personDeath, "PersonPhysicalSlot") || !Integer(personDeath["PersonPhysicalSlot"], 0, 1850)) ||
            !cursor.ContainsKey(forceRank) ||
            cursor[forceRank] is JsonNode rank && !ForceRankFields(rank) || !cursor.ContainsKey(personList) ||
            cursor[personList] is JsonNode pending && (pending is not JsonObject list || list.Count != 2 ||
                !Integer(list["NextPersonIndex"], 0, int.MaxValue) ||
                list["AwaitingDeathCompletion"] is not JsonValue waiting || !waiting.TryGetValue<bool>(out _)) ||
            new[] { "Frames", "SuccessCount", "EvaluatedCount", "RootFold", "MessageContext",
                "CandidatePersonPhysicalSlots", "ActorPersonPhysicalSlots", "DialogueRoles", "PersonListBlocked",
                "ConditionPersonPhysicalSlots", "ConditionCityPhysicalSlots", "ConditionForcePhysicalSlots", "ConditionBlocked" }
                .Any(field => !cursor.ContainsKey(field)) || cursor["Frames"] is not JsonArray frames ||
            frames.Any(frame => frame is not JsonObject row || row.Count != 12 ||
                new[] { "Begin", "End", "All", "Depth", "OrDepth", "Cursor", "Fold", "PendingGroup", "IfPhase",
                    "ThenMarker", "ElseMarker", "SelectedThen" }.Any(field => !row.ContainsKey(field))) ||
            cursor["MessageContext"] is not JsonObject banks || banks.Count != 8 ||
            new[] { "People", "Names", "Cities", "Forces", "Areas", "Items", "Numbers", "Provinces" }.Any(field => !banks.ContainsKey(field)))
            throw new InvalidDataException("Saved normal Story execution cursor fields are incomplete.");
        try
        {
            var value = cursor.Deserialize<CampaignStoryExecutionCursor>()
                ?? throw new InvalidDataException("Saved normal Story execution cursor is null.");
            value.Validate();
            fullyCompleted = value.IsCompleted;
            return value.IsCompleted || value.PersonListContinuation?.AwaitingDeathCompletion == true || value.ForceRankContinuation is not null || value.SinglePersonDeathContinuation is not null;
        }
        catch (JsonException error)
        { throw new InvalidDataException("Saved normal Story execution cursor has an invalid format.", error); }
    }

    private static bool ForceRankFields(JsonNode node) => node is JsonObject rank &&
        Keys(rank, "ForcePhysicalSlot", "Rank", "RequestedKingdomNameId", "RulerPhysicalSlot", "EmperorPhysicalSlot",
            "SelfProclaimed", "ImperialOwnerForceSlot", "Stage", "Source", "StorySequence", "OperationIndex", "SourceOffset",
            "KingdomChoices", "AutomaticKingdomNameId", "KingdomSelectionResolved", "SelectedKingdomNameId", "Messages",
            "NextMessage", "MessagesCompleted") &&
        rank["KingdomChoices"] is JsonArray choices && choices.All(item => item is JsonObject choice &&
            Keys(choice, "Id", "Name", "Description")) &&
        rank["Messages"] is JsonArray messages && messages.All(item => item is JsonObject message &&
            Keys(message, "Text", "Speaker"));

    private static bool Camera(JsonNode node) => node is JsonObject camera &&
        new[] { "FocusX", "FocusY", "FocusZ", "FocusW", "Distance", "Yaw", "Pitch" }
            .All(key => camera[key] is JsonValue value && value.TryGetValue<double>(out double number) && double.IsFinite(number));
    private static bool Integer(JsonNode? node, int min, int max) => node is JsonValue value &&
        value.TryGetValue<int>(out int number) && number >= min && number <= max;
}
