using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>The owned special-person capture notice, never a reconstruction
/// from a final Person state or an already released legacy announcement.</summary>
internal static class HiredArmyCaptureSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 225 || state["ArmyDefeatContinuation"] is not JsonObject value) return;
        if (value.ContainsKey("HiredCapture") || IsNoticePoint(value))
            throw new InvalidDataException("Legacy army defeat contains future special capture notice progress.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (state["ArmyDefeatContinuation"] is not JsonNode saved) return;
        if (saved is not JsonObject capture) throw Invalid();
        //The old resume points keep their own proven prefix. Null does not
        //invent a notice phase or turn a legacy queued result into a wait.
        if (version < 225) capture["HiredCapture"] = null;
        if (!capture.ContainsKey("HiredCapture")) throw Invalid();
        if (capture["HiredCapture"] is null)
        {
            if (IsNoticePoint(capture)) throw Invalid();
            return;
        }
        if (!IsNoticePoint(capture) || capture["HiredCapture"] is not JsonObject progress ||
            !Integer(progress["Phase"], (int)HiredArmyCapturePhase.AwaitingAnnouncement,
                (int)HiredArmyCapturePhase.ReadyToRelease) ||
            progress["AnnouncementBatchSequence"] is not JsonValue sequenceValue ||
            !sequenceValue.TryGetValue<long>(out long sequence) || sequence <= 0 ||
            capture["PendingCapturedRulerTreatment"] is not null || capture["ChosenDisposition"] is not null ||
            !Boolean(capture["ReclaimDefeatedCommander"], false) ||
            state["CampaignEffectRequests"] is not JsonObject effects ||
            effects["LastBatchSequence"] is not JsonValue lastValue ||
            !lastValue.TryGetValue<long>(out long last) || sequence > last ||
            effects["PendingBatches"] is not JsonArray batches) throw Invalid();

        var matching = batches.OfType<JsonObject>().Where(batch => batch["Sequence"] is JsonValue value &&
            value.TryGetValue<long>(out long found) && found == sequence).Take(2).ToArray();
        if (Integer(progress["Phase"], (int)HiredArmyCapturePhase.ReadyToRelease,
                (int)HiredArmyCapturePhase.ReadyToRelease))
        {
            if (matching.Length != 0) throw Invalid();
            return;
        }
        //Only the producer's single 34AA payload owns this wait. Earlier
        //source requests keep their ordinary consumers and FIFO positions.
        if (matching.Length != 1 || !Integer(matching[0]["Source"],
                (int)CampaignEffectSource.ArmyBattleDefeat, (int)CampaignEffectSource.ArmyBattleDefeat) ||
            matching[0]["Requests"] is not JsonArray { Count: 1 } requests ||
            requests[0]?["DuelCaptureAnnouncement"] is not JsonObject notice ||
            !Integer(notice["MessageRoot"], 0x34AA, 0x34AA) ||
            !(Integer(notice["SoundId"], 6, 6) || Integer(notice["SoundId"], 8, 8)) ||
            notice["TextAtSource"] is not JsonValue text || !text.TryGetValue<string>(out string? content) ||
            content is null || notice["Speaker"] is not null) throw Invalid();
    }

    private static bool IsNoticePoint(JsonObject value) => Integer(value["ResumePoint"],
        (int)ArmyDefeatResumePoint.AfterSpecialCaptureAnnouncement, (int)ArmyDefeatResumePoint.AfterSpecialCaptureAnnouncement);
    private static bool Integer(JsonNode? node, int min, int max) => node is JsonValue value &&
        value.TryGetValue<int>(out int number) && number >= min && number <= max;
    private static bool Boolean(JsonNode? node, bool expected) => node is JsonValue value &&
        value.TryGetValue<bool>(out bool actual) && actual == expected;
    private static InvalidDataException Invalid() =>
        new("Saved special army capture notice has missing or invalid source-time progress.");
}
