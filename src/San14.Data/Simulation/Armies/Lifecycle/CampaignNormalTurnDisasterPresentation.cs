using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The numerical cut queues the actual scene boundary; only its presentation
    //host can consume it. In particular, publishing numbers is not an ACK.
    internal GameState AppendNormalTurnDisasterEventCurrent(GameState state, int? eventKind)
    {
        if (eventKind is not (null or 14 or 15 or 16))
            throw new InvalidDataException("normal.turn.disaster.scene_kind");
        var pending = state.CampaignEffectRequests
            ?? throw new InvalidDataException("normal.turn.disaster.presentation_owner");
        long revision = state.NativeClock?.UpdateOrdinal
            ?? throw new InvalidDataException("normal.turn.disaster.presentation_revision");
        var request = new MonthlyMaintenanceRequest(eventKind.HasValue
            ? MonthlyMaintenanceOperation.DisasterSceneEnter : MonthlyMaintenanceOperation.DisasterSceneExit,
            eventKind, null, null);
        return state with { CampaignEffectRequests = pending.Append(CampaignEffectSource.MonthlyMaintenance,
            revision, [new(null, null) { MonthlyMaintenance = request }]) };
    }
}
