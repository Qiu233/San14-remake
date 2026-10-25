using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal sealed partial class MonthlyCampaignMaintenanceSource
    {
        //2704F0's relationship downgrade and27BCD0 share the actual
        //2CD1C0(people,1,1,0) operation. Its caller retains the live list.
        private void RecallSpecialPersonnel(HexListHandle people)
        {
            var effects = new List<CampaignEffectRequest>();
            state = package.ApplySpecialPersonnelRecallCurrent(state, current, pool, people,
                conditional: true, requireValidOriginalForce: true, suppressPrimary: false,
                date: state.NativeMapSeason?.ThirdDate ?? throw Missing("special_recall_date"), effects: effects);
            if (effects.Count == 0) return;
            if (borrowedEffects is not null) borrowedEffects.AddRange(effects);
            else state = state with
            {
                CampaignEffectRequests = (state.CampaignEffectRequests ?? throw Missing("special_recall_effect_owner"))
                    .Append(CampaignEffectSource.MonthlyMaintenance,
                        state.NativeClock?.UpdateOrdinal ?? throw Missing("special_recall_revision"), effects)
            };
        }
    }
}
