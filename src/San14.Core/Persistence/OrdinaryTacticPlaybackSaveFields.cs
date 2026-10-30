using System.Text.Json.Nodes;

namespace San14.Core;

internal static class OrdinaryTacticPlaybackSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 181 && (state["ManualTacticIntervention"]?["OrdinaryPlaybacks"] is not null ||
            Integer(state["NativePeriodContinuation"]?["Cursor"]?["StageIndex"], 6, 6)))
            throw new InvalidDataException("Legacy world contains a future ordinary tactic playback continuation.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (state["ManualTacticIntervention"] is not JsonObject manager)
        {
            if (Integer(state["NativePeriodContinuation"]?["Cursor"]?["StageIndex"], 6, 6)) throw Invalid();
            return;
        }
        const string key = "OrdinaryPlaybacks";
        if (version < 181) { manager[key] = new JsonArray(); return; }
        if (manager[key] is not JsonArray playbacks || manager["Effects"] is not JsonArray effects ||
            playbacks.Count > effects.Count) throw Invalid();
        for (int index = 0; index < playbacks.Count; index++)
            if (playbacks[index] is not JsonObject playback || playback.Count != 2 ||
                !Integer(playback["Sequence"], 1, long.MaxValue) ||
                playback["ResultsPublished"] is not JsonValue published || !published.TryGetValue<bool>(out _) ||
                !JsonNode.DeepEquals(playback["Sequence"], effects[index]?["Sequence"]) ||
                !Integer(effects[index]?["Kind"], (int)TacticPresentationKind.OrdinaryEffects,
                    (int)TacticPresentationKind.OrdinaryEffects)) throw Invalid();
    }

    private static bool Integer(JsonNode? node, long minimum, long maximum) => node is JsonValue value &&
        value.TryGetValue<long>(out long number) && number >= minimum && number <= maximum;
    private static InvalidDataException Invalid() => new("Saved ordinary tactic playbacks are missing or inconsistent with their manager order.");
}
