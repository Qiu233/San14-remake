using System.Text.Json.Nodes;

namespace San14.Core;

internal static class TalentSaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if(version<152)state["TalentAutoSearchPreferences"]=null;
        else if(!state.ContainsKey("TalentAutoSearchPreferences"))
            throw new InvalidDataException("Saved automatic search preferences knowledge is missing.");
        if(state["TalentAutoSearchPreferences"] is JsonNode preferences)
        {
            if(preferences is not JsonObject fields)
                throw new InvalidDataException("Saved automatic search preferences are invalid.");
            if(!Read("ExecutorPolicy",3)||!Read("TravelRange",2)||!Read("AllocationPolicy",1))
                throw new InvalidDataException("Saved automatic search preferences are invalid.");
            bool Read(string name,int maximum)=>fields[name] is JsonValue value&&value.TryGetValue<int>(out int number)&&number>=0&&number<=maximum;
        }
        if(state["PreparedTalentOrders"] is JsonObject prepared)
            foreach(var entry in prepared)
            {
                if(entry.Value is not JsonObject fields)
                    throw new InvalidDataException("Saved prepared talent order is incomplete.");
                if(version<152)fields["RecruitMode"]=0;
                else if(fields["RecruitMode"] is not JsonValue value||!value.TryGetValue<int>(out int mode)||
                    mode is not (0 or 10 or 30)||
                    fields["Kind"] is JsonValue kind&&kind.TryGetValue<int>(out int rawKind)&&rawKind==(int)TalentOrderKind.Search&&mode!=0)
                    throw new InvalidDataException("Saved prepared recruitment absence mode is invalid.");
            }
        if (version < 147)
        {
            if (state["TalentGemDiscoveries"] is not null)
                throw new InvalidDataException("Legacy save contains future talent discovery locations.");
            state["TalentGemDiscoveries"] = null;
        }
        else if (!state.ContainsKey("TalentGemDiscoveries"))
            throw new InvalidDataException("Saved talent discovery knowledge is missing.");
        if (state["TalentGemDiscoveries"] is JsonNode discovery &&
            (discovery is not JsonObject discoveryFields || discoveryFields["Gems"] is not JsonObject gems || gems.Count != 31 ||
             gems.Any(pair => pair.Value is not JsonArray sites || sites.Count != 5 || sites.Any(site =>
                site is not JsonObject location || !location.ContainsKey("Area") || !location.ContainsKey("Remaining")))))
            throw new InvalidDataException("Saved talent discovery locations are incomplete.");
        if (state["PersonnelCommands"] is JsonObject owner)
        {
            if (version < 147)
            {
                if (owner["Talents"] is not null)
                    throw new InvalidDataException("Legacy save contains future talent tasks.");
                owner["Talents"] = null;
            }
            else if (!owner.ContainsKey("Talents"))
                throw new InvalidDataException("Saved talent task knowledge is missing.");
            if (owner["Talents"] is JsonNode tasks)
            {
                if (tasks is not JsonObject rows) throw new InvalidDataException("Saved talent task collection is invalid.");
                foreach (var row in rows)
                {
                    if (row.Value is not JsonObject fields || new[] { "Id", "DispatchSlot", "MemberIds", "Kind", "ForceId",
                        "SourceCityId", "SourceDistrictId", "TargetHexId", "TargetOfficerId", "StartHexId", "EndHexId",
                        "StartDate", "DueDate", "SavedSample", "Started", "Cancelled", "TargetOriginalForceId",
                        "TargetOriginalHomeLocationId", "RecruitMode", "NeedsRefresh", "IsRetiring", "IsAssociated" }
                        .Any(key => !fields.ContainsKey(key)))
                        throw new InvalidDataException("Saved talent task fields are incomplete.");
                    foreach (string name in new[] { "StartDate", "DueDate" })
                        if (fields[name] is not JsonObject date || new[] { "Year", "Month", "Day", "Hour" }.Any(key => !date.ContainsKey(key)))
                            throw new InvalidDataException("Saved talent task date is incomplete.");
                    if (version < 151)
                        fields["IsRecruitWait"] = false;
                    else if (fields["IsRecruitWait"] is not JsonValue wait || !wait.TryGetValue<bool>(out _))
                        throw new InvalidDataException("Saved recruitment wait state is missing or invalid.");
                }
            }
        }
        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) return;
        foreach (var node in batches)
        {
            if (node is not JsonObject batch || batch["Requests"] is not JsonArray requests) continue;
            if (version < 147 && batch["Source"] is JsonValue source && source.TryGetValue<int>(out int raw) &&
                raw >= (int)CampaignEffectSource.TalentCommands)
                throw new InvalidDataException("Legacy save contains future talent presentation origin.");
            foreach (var request in requests.OfType<JsonObject>())
            {
                if (version < 147)
                {
                    if (request["TalentCommandPresentation"] is not null)
                        throw new InvalidDataException("Legacy save contains future talent presentation.");
                    request["TalentCommandPresentation"] = null;
                }
                else if (!request.ContainsKey("TalentCommandPresentation"))
                    throw new InvalidDataException("Saved talent presentation field is missing.");
                if (request["TalentCommandPresentation"] is JsonNode payload)
                {
                    if (payload is not JsonObject fields || new[] { "Operation", "SoundId", "OfficerId", "TargetOfficerId",
                        "RecruitSucceeded", "TextAtSource" }.Any(key => !fields.ContainsKey(key)))
                        throw new InvalidDataException("Saved talent presentation is incomplete.");
                    MigrateSourcePresentation(fields, version);
                }
            }
        }
    }

    private static void MigrateSourcePresentation(JsonObject presentation, int version)
    {
        foreach (string name in new[] { "RecruitConversation", "Voice" })
        {
            if (version < 154)
            {
                if (presentation[name] is not null)
                    throw new InvalidDataException("Legacy save contains future talent presentation context.");
                //Old messages retain unknown source-time actors and voice. No
                //conversation text or fresh scenario data can reconstruct them.
                presentation[name] = null;
            }
            else if (!presentation.ContainsKey(name))
                throw new InvalidDataException("Saved talent presentation context knowledge is missing.");
            if (presentation[name] is not JsonNode saved) continue;
            string[] required = name == "RecruitConversation"
                ? ["ExecutorOfficerId", "TargetOfficerId", "ExecutorNameAtSource", "TargetNameAtSource",
                    "ExecutorPortraitAppearanceIndex", "TargetPortraitAppearanceIndex"]
                : ["SpeakerId", "VoiceType", "VoiceId", "Language", "VariantIndex", "SoundId"];
            if (saved is not JsonObject fields || required.Any(key => !fields.ContainsKey(key)))
                throw new InvalidDataException("Saved talent presentation source context is incomplete.");
        }
    }
}
