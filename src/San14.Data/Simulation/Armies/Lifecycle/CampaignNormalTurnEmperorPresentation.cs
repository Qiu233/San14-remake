using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState AppendNormalTurnEmperorNarrationCurrent(GameState state, int? kind)
    {
        if (kind is not (null or 6 or 45 or 55)) throw new InvalidDataException("normal.turn.emperor.scene_kind");
        var pending = state.CampaignEffectRequests ?? throw new InvalidDataException("normal.turn.emperor.pending_owner");
        long revision = state.NativeClock?.UpdateOrdinal ?? throw new InvalidDataException("normal.turn.emperor.revision");
        return state with { CampaignEffectRequests = pending.Append(CampaignEffectSource.MonthlyMaintenance, revision,
            [new(null, null) { MonthlyMaintenance = new(kind.HasValue
                ? MonthlyMaintenanceOperation.EmperorSceneEnter : MonthlyMaintenanceOperation.EmperorSceneExit,
                kind, null, null) }]) };
    }
}
