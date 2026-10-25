using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Version166 additions to normal Turn decisions and ordered presentation requests.</summary>
internal static class NormalTurnCoalitionSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 166) return;
        if (state["NativeArmySimulation"]?["Forces"] is JsonObject forces &&
            forces.Any(pair => pair.Value?["ForeignTradeProgress"] is not null))
            throw new InvalidDataException("Legacy force contains future foreign trade progress.");
        if (state["NativeNormalTurnContinuation"] is JsonObject turn &&
            (turn["PendingCoalitionInvitation"] is not null || !Integer(turn["NextAction"], 16, 16)))
            throw new InvalidDataException("Legacy normal Turn contains a future coalition decision.");
        foreach (var (source, request) in Requests(state))
        {
            if (request["MonthlyDisasterConsequence"] is not null ||
                Integer(request["MonthlyMaintenance"]?["Operation"], (int)MonthlyMaintenanceOperation.EmperorSceneEnter, int.MaxValue) ||
                Integer(request["ForceCoalitionVisual"]?["SceneKind"], 53, 53) ||
                Integer(request["ForceCoalitionVisual"]?["Operation"], (int)ForceCoalitionVisualOperation.CityProvinceAdvantageIndicatorsRefresh, int.MaxValue) ||
                source == (int)CampaignEffectSource.MonthlyMaintenance &&
                (Integer(request["TalentCommandPresentation"]?["SoundId"], 16, 16) ||
                 Integer(request["TalentCommandPresentation"]?["SoundId"], 22, 23)))
                throw new InvalidDataException("Legacy normal Turn contains a future presentation request.");
        }
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (state["NativeArmySimulation"]?["Forces"] is JsonObject forces)
            foreach (var pair in forces)
            {
                if (pair.Value is not JsonObject force) throw new InvalidDataException("Saved native force is invalid.");
                if (version < 166) { force["ForeignTradeProgress"] = null; continue; }
                if (!force.ContainsKey("ForeignTradeProgress") || force["ForeignTradeProgress"] is JsonNode savedProgress &&
                    (savedProgress is not JsonObject progress ||
                     progress["FriendshipPoints"] is not JsonArray points || points.Count != 5 || points.Any(value => !Integer(value, 0, ushort.MaxValue)) ||
                     progress["AcquiredTechnologyLevels"] is not JsonArray levels || levels.Count != 5 || levels.Any(value => !Integer(value, 0, byte.MaxValue))))
                    throw new InvalidDataException("Saved foreign trade progress is incomplete or invalid.");
            }
        if (state["NativeNormalTurnContinuation"] is JsonObject turn)
        {
            if (version < 166) turn["PendingCoalitionInvitation"] = null;
            else if (!turn.ContainsKey("PendingCoalitionInvitation") ||
                turn["PendingCoalitionInvitation"] is JsonNode node &&
                (node is not JsonObject offer || !Integer(offer["TargetForceId"], 0, 51) ||
                 !Integer(offer["PlayerForceId"], 0, 51) || !Integer(offer["PlayerRulerId"], 0, 6000) ||
                 !Integer(offer["SpeakerOfficerId"], 0, 6000) ||
                 offer["OrderedMemberForceIds"] is not JsonArray members || members.Any(id => !Integer(id, 0, 51)) ||
                 !Text(offer, "QuestionTextAtSource", false) || !NullableInteger(offer, "CameraHexId", 0, 48400)))
                throw new InvalidDataException("Saved coalition invitation is incomplete or invalid.");
        }
        foreach (var (_, request) in Requests(state))
        {
            if (version < 166) { request["MonthlyDisasterConsequence"] = null; continue; }
            if (!request.ContainsKey("MonthlyDisasterConsequence"))
                throw new InvalidDataException("Saved monthly disaster consequence knowledge is missing.");
            if (request["MonthlyDisasterConsequence"] is not JsonNode saved) continue;
            if (saved is not JsonObject consequence ||
                !Integer(consequence["Operation"], 0, (int)MonthlyDisasterConsequenceOperation.CameraRestore) ||
                !Integer(consequence["DisasterKind"], 0, 1) || !Integer(consequence["CityId"], 1, 51) ||
                !Integer(consequence["HexId"], 0, 48399) || !Text(consequence, "TextAtSource", true) ||
                !NullableInteger(consequence, "Amount", int.MinValue, int.MaxValue) ||
                !NullableInteger(consequence, "ArmyReceiverId", 1, 500) ||
                !NullableInteger(consequence, "BaseLocationId", 1, 61))
                throw new InvalidDataException("Saved monthly disaster consequence is incomplete or invalid.");
        }
    }

    private static IEnumerable<(int? Source, JsonObject Request)> Requests(JsonObject state)
    {
        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) yield break;
        foreach (var batch in batches.OfType<JsonObject>())
        {
            int? source = batch["Source"] is JsonValue value && value.TryGetValue<int>(out int number) ? number : null;
            if (batch["Requests"] is JsonArray requests)
                foreach (var request in requests.OfType<JsonObject>()) yield return (source, request);
        }
    }

    private static bool NullableInteger(JsonObject owner, string key, int min, int max) =>
        owner.ContainsKey(key) && (owner[key] is null || Integer(owner[key], min, max));
    private static bool Integer(JsonNode? field, int min, int max) => field is JsonValue value &&
        value.TryGetValue<int>(out int number) && number >= min && number <= max;
    private static bool Text(JsonObject owner, string key, bool nullable) => owner.ContainsKey(key) &&
        (nullable && owner[key] is null || owner[key] is JsonValue value && value.TryGetValue<string>(out _));
}
