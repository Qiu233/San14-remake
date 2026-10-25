using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState AppendNormalTurnPersonnelEventCurrent(GameState state, int? kind)
    {
        if (kind is not (null or 9)) throw new InvalidDataException("normal.turn.personnel.scene_kind");
        var pending = state.CampaignEffectRequests ?? throw new InvalidDataException("normal.turn.personnel.pending_owner");
        long revision = state.NativeClock?.UpdateOrdinal ?? throw new InvalidDataException("normal.turn.personnel.revision");
        return state with { CampaignEffectRequests = pending.Append(CampaignEffectSource.MonthlyMaintenance, revision,
            [new(null, null) { MonthlyMaintenance = new(kind.HasValue
                ? MonthlyMaintenanceOperation.PersonnelSceneEnter : MonthlyMaintenanceOperation.PersonnelSceneExit,
                kind, null, null) }]) };
    }
}
