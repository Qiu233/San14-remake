using System.Text.Json.Nodes;

namespace San14.Core;

internal static class CampaignTurnStopSaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 163)
        {
            if (state["CampaignTurnStop"] is not null)
                throw new InvalidDataException("Legacy save contains a future normal Turn stop request.");
            state["CampaignTurnStop"] = null;
            return;
        }
        if (!state.ContainsKey("CampaignTurnStop"))
            throw new InvalidDataException("Saved normal Turn stop knowledge is missing.");
        if (state["CampaignTurnStop"] is not JsonNode saved) return;
        if (saved is not JsonObject stop || stop["Reason"] is not JsonValue reasonValue ||
            !reasonValue.TryGetValue<int>(out int reason) || !Enum.IsDefined((CampaignTurnStopReason)reason) ||
            !stop.ContainsKey("RemainingOrdinaryForceId") || stop["RemainingOrdinaryForceId"] is JsonNode remaining &&
            (remaining is not JsonValue remainingValue || !remainingValue.TryGetValue<int>(out int force) || force is < 0 or > 51) ||
            !stop.ContainsKey("EndingRequested") || stop["EndingRequested"] is JsonNode ending &&
            (ending is not JsonValue endingValue || !endingValue.TryGetValue<bool>(out _)))
            throw new InvalidDataException("Saved normal Turn stop request is invalid.");
    }
}
