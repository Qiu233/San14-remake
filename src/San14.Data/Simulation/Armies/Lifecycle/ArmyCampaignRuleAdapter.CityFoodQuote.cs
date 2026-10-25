using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2DC710(0,1): actual food quote, including its reached lifecycle tail.
    //The caller lends its completed Turn grouping owner and current pool.
    //No food stock, shortage, supply flag, planning or date is advanced here.
    private CityCaptureFoodQuote ReadNormalTurnCityFoodQuoteCurrent(GameState state,
        SourceWorldContext current, BaseReference city, ArmyMapGroupingOwner groups)
    {
        if (!_baseCapacityInputs.BindCurrent(state, current)) throw CityFoodQuoteMissing("current_Base_parameters");
        var pool = current.ReadOwnedCurrentListPool() ?? throw CityFoodQuoteMissing("shared_pool");
        var operations = new CityCaptureFoodQuoteOperations(
            (latest, list) => SortArmyFoodExpenseTemporaryCurrent(latest, current, list),
            latest =>
            {
                latest = OwnedArmyLifecycleAuxiliaries.ConsumePendingCurrent(latest, current, pool, groups.ResetReceiver)
                    ?? throw CityFoodQuoteMissing("28E6D0_pending_cleanup");
                var profile = MapArmyGroupingProfile() ?? throw CityFoodQuoteMissing("2FCDA0_profile");
                var recomputed = groups.Recompute(profile, MapArmyGroupingQueries(latest, current));
                if (!recomputed.Applied) throw CityFoodQuoteMissing(recomputed.Missing ?? "2FCDA0_current_groups");
                return latest;
            },
            (latest, target, policies) => ReadCityFoodQuoteAdministrationTailCurrent(latest, current, target, policies));
        return CityCaptureFoodWarningSource.TryReadFoodExpenseCurrent(state, current, city, operations);
    }

    private static InvalidDataException CityFoodQuoteMissing(string field)
        => new("normal-turn.food-quote." + field);
}
