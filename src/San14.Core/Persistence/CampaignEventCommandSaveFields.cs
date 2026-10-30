using System.Text.Json.Nodes;

namespace San14.Core;

internal static class CampaignEventCommandSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 204 && state["CampaignEventCommandPlayback"] is not null)
            throw new InvalidDataException("Legacy world contains a future ordinary event command.");
    }
    internal static void Migrate(JsonObject state, int version)
    {
        const string key = "CampaignEventCommandPlayback";
        if (version < 204) { RejectLegacyFields(state, version); state[key] = null; return; }
        if (!state.ContainsKey(key)) throw new InvalidDataException("Saved ordinary event command knowledge is missing.");
        if (state[key] is null) return;
        if (state[key] is not JsonObject value || value.Count != 2 ||
            value["Flags"] is not JsonValue flags || !flags.TryGetValue<int>(out int mode) || mode is not (2 or 3) ||
            value["Invocation"] is not JsonObject invocation)
            throw new InvalidDataException("Saved ordinary event command is incomplete.");
        NormalTurnStorySaveFields.MigratePlayback(invocation, version);
        if (invocation["Story"] is not JsonObject story)
            throw new InvalidDataException("Saved ordinary event command Story is missing.");
        CampaignStoryPreparationSaveFields.ValidatePreparedStory(story);
    }
}
