using System.Text.Json.Nodes;

namespace San14.Core;

internal static class DuelCaptureAnnouncementSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 218) return;
        if (Announcements(state).Any(request => request["TextAtSource"] is not null || request["Speaker"] is not null))
            throw new InvalidDataException("Legacy duel capture announcement contains future source-time text or speaker knowledge.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        RejectLegacyFields(state, version);
        if (version <= 217)
            foreach (var request in Announcements(state))
            {
                //No source-time formatting or actor was saved. Current Person
                //data cannot reconstruct those operands or the consumed draw.
                request["TextAtSource"] = null;
                request["Speaker"] = null;
            }
        ValidateWorld(state);
    }

    internal static void ValidateWorld(JsonObject state)
    {
        foreach (var request in Announcements(state))
        {
            if (!request.ContainsKey("TextAtSource") || !request.ContainsKey("Speaker")) throw Invalid();
            if (request["Speaker"] is not null and not JsonObject) throw Invalid();
            if (request["TextAtSource"] is null)
            {
                if (request["Speaker"] is not null) throw Invalid();
                continue;
            }
            if (request["TextAtSource"] is not JsonValue text || !text.TryGetValue<string>(out string? value) ||
                value is null || value.Contains('\0')) throw Invalid();
            //Basic root/id/sound and the complete speaker context remain with
            //the existing domain validator. Only new knowledge shape is owned here.
            if (request["MessageRoot"] is JsonValue root && root.TryGetValue<int>(out int code) &&
                (code == 0x2A8C && request["Speaker"] is null || code == 0x34AA && request["Speaker"] is not null))
                throw Invalid();
        }
    }

    private static IEnumerable<JsonObject> Announcements(JsonObject state)
    {
        if (state["CampaignEffectRequests"] is not JsonObject owner || owner["PendingBatches"] is not JsonArray batches)
            yield break;
        foreach (var batch in batches.OfType<JsonObject>())
            if (batch["Requests"] is JsonArray requests)
                foreach (var request in requests.OfType<JsonObject>())
                {
                    if (request["DuelCaptureAnnouncement"] is null) continue;
                    if (request["DuelCaptureAnnouncement"] is not JsonObject announcement) throw Invalid();
                    yield return announcement;
                }
    }

    private static InvalidDataException Invalid() =>
        new("Saved duel capture source-time presentation knowledge is missing or invalid.");
}
