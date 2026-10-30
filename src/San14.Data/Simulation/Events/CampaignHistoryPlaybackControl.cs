using San14.Core;

namespace San14.Data;

/// <summary>The History manager's independent reception-chronicle control.
/// It is neither the report-recording blocker nor the playback camera flag.</summary>
internal static class CampaignHistoryPlaybackControl
{
    internal static GameState Enter(GameState state, int executionMode, int eventId)
    {
        //83C100 writes this field only for actual ordinary mode0. Edited
        //nonzero modes retain their existing manager knowledge, including null.
        if (executionMode != 0) return state;
        var history = state.CampaignHistory ?? throw Missing("entry_manager_owner");
        return state with { CampaignHistory = history with
            { EmperorReceptionChronicleSuppressed = eventId == 311 } };
    }

    internal static GameState Exit(GameState state)
    {
        //All modes clear it AFTER the full3D6F00 contents/business execution,
        //before the result page or return-to-title branch. A suspended leaf
        //therefore keeps the preceding value across real presentation receipts.
        var history = state.CampaignHistory ?? throw Missing("exit_manager_owner");
        return state with { CampaignHistory = history with
            { EmperorReceptionChronicleSuppressed = false } };
    }

    internal static int? ReadEmperorReceptionChronicleSuppression(GameState state) =>
        state.CampaignHistory?.EmperorReceptionChronicleSuppressed is bool suppressed
            ? suppressed ? 1 : 0 : null;

    private static InvalidDataException Missing(string field) => new("history.playback_control." + field);
}
