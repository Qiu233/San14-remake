using System.Text.Json.Nodes;

namespace San14.Core;

internal static class TalentDiscoverySaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 148)
        {
            if (state["GemForceInventory"] is not null)
                throw new InvalidDataException("Legacy save contains future Gem inventory.");
            state["GemForceInventory"] = null;
        }
        else if (!state.ContainsKey("GemForceInventory"))
            throw new InvalidDataException("Saved Gem inventory knowledge is missing.");
        if (state["GemForceInventory"] is JsonNode saved &&
            (saved is not JsonObject owner || owner["Forces"] is not JsonObject forces || forces.Count != 52 ||
             forces.Any(pair => !int.TryParse(pair.Key, out int id) || id is < 0 or > 51 ||
                pair.Value is not JsonArray counts || counts.Count != 31 || counts.Any(value =>
                    value is not JsonValue count || !count.TryGetValue<ushort>(out _)))))
            throw new InvalidDataException("Saved Gem inventory is incomplete.");
        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) return;
        foreach (var batch in batches.OfType<JsonObject>())
        {
            if (batch["Requests"] is not JsonArray requests) continue;
            foreach (var request in requests.OfType<JsonObject>())
                foreach (string name in new[] { "TalentSceneryDiscovery", "TalentSceneryReward", "TalentForceExperiencePresentation" })
                {
                    if (version < 148)
                    {
                        if (request[name] is not null) throw new InvalidDataException("Legacy save contains future scenery presentation.");
                        request[name] = null;
                    }
                    else if (!request.ContainsKey(name)) throw new InvalidDataException("Saved scenery presentation field is missing.");
                    if (request[name] is not JsonNode node) continue;
                    string[] required = name switch
                    {
                        "TalentSceneryDiscovery" => ["SceneryId", "ForceId", "EventId", "Date", "HexId"],
                        "TalentSceneryReward" => ["SceneryId", "ExecutorOfficerId", "RewardKind", "Amount"],
                        _ => ["Operation", "ForceId", "Amount"]
                    };
                    if (node is not JsonObject fields || required.Any(key => !fields.ContainsKey(key)))
                        throw new InvalidDataException("Saved scenery presentation is incomplete.");
                    if (name == "TalentSceneryReward") MigrateRewardSourceSnapshot(fields, version);
                    if (name == "TalentForceExperiencePresentation")
                    {
                        if (version < 156)
                        {
                            if (fields["PreviousLevel"] is not null)
                                throw new InvalidDataException("Legacy save contains a future force experience level snapshot.");
                            fields["PreviousLevel"] = null;
                        }
                        else if (!fields.ContainsKey("PreviousLevel"))
                            throw new InvalidDataException("Saved force experience previous-level knowledge is missing.");
                    }
                    if (name == "TalentSceneryDiscovery" &&
                        (fields["Date"] is not JsonObject date || new[] { "Year", "Month", "Day", "Hour" }.Any(key => !date.ContainsKey(key))))
                        throw new InvalidDataException("Saved scenery presentation date is incomplete.");
                }
        }
    }

    private static void MigrateRewardSourceSnapshot(JsonObject reward, int version)
    {
        foreach (string name in new[] { "DiscoveryTextAtSource", "ExecutorNameAtSource",
            "ExecutorPortraitAppearanceIndex", "SceneryNameAtSource", "Voice" })
        {
            if (version < 155)
            {
                if (reward[name] is not null)
                    throw new InvalidDataException("Legacy save contains future scenery reward source text.");
                //The old intent cannot reconstruct the already-reached text,
                //speaker or appearance from a later scenario or mutable world.
                reward[name] = null;
            }
            else if (!reward.ContainsKey(name))
                throw new InvalidDataException("Saved scenery reward source knowledge is missing.");
        }
        if (reward["Voice"] is JsonNode voice &&
            (voice is not JsonObject fields || new[] { "SpeakerId", "VoiceType", "VoiceId", "Language",
                "VariantIndex", "SoundId" }.Any(key => !fields.ContainsKey(key))))
            throw new InvalidDataException("Saved scenery reward voice is incomplete.");
    }
}
