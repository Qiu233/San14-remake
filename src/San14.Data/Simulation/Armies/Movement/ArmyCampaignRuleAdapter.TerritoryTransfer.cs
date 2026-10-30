using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //One current185D00 consumer serves movement, encirclement and site
    //ownership. Its boolean is the native accepted-transfer return, which
    //can be true while a special partner protects the previous Hex owner.
    //The caller prepares its current service/scenery/input lifetime once.
    private (GameState State, bool Accepted) TransferTerritoryHexCurrent(GameState state,
        SourceWorldContext current, HexReference hex, int force, int cityId,
        bool enqueueEncirclement, bool allowType3Override, bool showAppearance,
        NativeMapSeasonDate? date = null, List<CampaignEffectRequest>? borrowedEffects = null)
    {
        var pool = current.ReadOwnedCurrentListPool() ?? throw OccupationMissing("transfer_current_pool");
        return new MonthlyCampaignMaintenanceSource(this, state, current, pool, borrowedEffects: borrowedEffects)
            .TransferTerritoryHexCurrent(hex, force, cityId, enqueueEncirclement,
                allowType3Override, showAppearance, date);
    }

    internal sealed partial class MonthlyCampaignMaintenanceSource
    {
        internal (GameState State, bool Accepted) TransferTerritoryHexCurrent(HexReference hex,
            int force, int cityId, bool enqueueEncirclement, bool allowType3Override,
            bool showAppearance, NativeMapSeasonDate? date)
        {
            borrowedTerritoryDate = date;
            bool accepted = RestoreMonthlyOccupation(hex, force, cityId,
                enqueueEncirclement, allowType3Override, showAppearance);
            return (state, accepted);
        }
    }
}
