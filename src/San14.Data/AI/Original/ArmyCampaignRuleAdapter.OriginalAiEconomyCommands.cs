using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal OriginalAiCommandPreparation CreateOriginalAiMerchantCurrent(GameState state, int cityId,
        int sourceMode, int goldLimit, int foodLimit, IReadOnlyList<int> officerPriority)
    {
        if (cityId is < 1 or > 51 || !state.Cities.TryGetValue(cityId, out var city)) return new(false, null);
        var source = CreateCityMerchantSource(state, cityId);
        var mode = sourceMode == 0 ? CityMerchantMode.Buy : CityMerchantMode.Sell;
        var selection = new CityMerchantAutomaticSelection(goldLimit, foodLimit, officerPriority.ToArray());
        var plan = ReadOriginalAiMerchantSelection(state, source, mode, selection);
        if (plan is null) return new(false, null);
        if (plan.OfficerIds.Distinct().Count() != plan.OfficerIds.Length)
            throw OriginalAiEconomyMissing("Merchant_duplicate_executor_contract");
        int forceId = city.ForceId ?? throw OriginalAiEconomyMissing("Merchant_City_Force");
        return new(true, new CityMerchantOrder(forceId, cityId, plan.OfficerIds, mode, plan.Amount,
            AutomaticSelection: selection));
    }

    private sealed record OriginalAiMerchantSelection(int[] OfficerIds, int Amount, int ExecutorWeight);

    /// <summary>02D800 selection, shared by preparation and formal submission.
    /// The source accumulates every considered eligible officer's weight even
    /// when a buy-capacity plateau removes the newly appended officer.</summary>
    private static OriginalAiMerchantSelection? ReadOriginalAiMerchantSelection(GameState state,
        CityMerchantSource source, CityMerchantMode mode, CityMerchantAutomaticSelection selection)
    {
        if (!source.CanOpen() || selection.OfficerPriority is not { Count: > 0 }) return null;
        var selected = new List<int>();
        int totalWeight = 0;
        if (mode == CityMerchantMode.Buy)
        {
            if (selection.FoodLimit >= 0) return null;
            int price = state.Cities[source.CityId].MerchantFoodPerTenGold
                ?? throw OriginalAiEconomyMissing("Merchant_price");
            if (price == 0) throw OriginalAiEconomyMissing("Merchant_price_divisor");
            int target = Math.Min(unchecked(selection.GoldLimit / price * 10), unchecked(-selection.FoodLimit));
            int previous = 0, chosen = 0;
            foreach (int id in selection.OfficerPriority)
            {
                if (!source.IsCandidate(id)) continue;
                selected.Add(id);
                int maximum = Maximum();
                int goldEquivalent = unchecked(maximum / price * 10);
                totalWeight = unchecked(totalWeight + source.Weight(state, source.Candidate(id)));
                if (goldEquivalent == previous) selected.RemoveAt(selected.Count - 1);
                if (target <= goldEquivalent) { chosen = target; break; }
                previous = chosen = goldEquivalent;
                if (selected.Count >= CityMerchantSource.MaximumOfficers) break;
            }
            int amount = unchecked(price * chosen) / 10;
            return selected.Count != 0 && amount > 0
                ? new(selected.ToArray(), amount, totalWeight) : null;
        }
        if (selection.GoldLimit >= 0) return null;
        int sellPrice = unchecked((state.Cities[source.CityId].MerchantFoodPerTenGold
            ?? throw OriginalAiEconomyMissing("Merchant_sell_price")) * CityMerchantSource.SellPricePercent) / 100;
        int requested = Math.Min(unchecked(-selection.GoldLimit), unchecked(sellPrice * selection.FoodLimit) / 10);
        int final = 0;
        foreach (int id in selection.OfficerPriority)
        {
            if (!source.IsCandidate(id)) continue;
            selected.Add(id);
            int maximum = Maximum();
            totalWeight = unchecked(totalWeight + source.Weight(state, source.Candidate(id)));
            final = Math.Min(requested, maximum);
            if (requested <= maximum || selected.Count >= CityMerchantSource.MaximumOfficers) break;
        }
        return selected.Count != 0 && final > 0 ? new(selected.ToArray(), final, totalWeight) : null;

        int Maximum()
        {
            var page = source.Page(selected, mode, 0);
            if (page.UnresolvedInput is { } missing) throw OriginalAiEconomyMissing(missing);
            return page.MaxAmount ?? throw OriginalAiEconomyMissing("Merchant_capacity");
        }
    }

    internal OriginalAiCommandPreparation CreateOriginalAiForeignTradeCurrent(GameState state,
        SourceWorldContext current, OriginalAiActor actor, int countryId, int[] executors, int gold, int food, int troops)
    {
        if (countryId is < 1 or > 4) return new(false, null);
        // Validate the real normal country resource rather than a guessed Force/City identity.
        _ = ReadForeignTradeCountry(countryId);
        var actorForce = ReadOriginalAiMyselfForce(current, actor);
        if (actorForce is not { } force || !ReadOriginalAiForceValidity(current, force.Id)) return new(false, null);
        var capital = new DiplomacyCommandSource(state, current).Capital(force.Id);
        var treasury = BaseCurrencyAdditionSource.CurrencyCity(capital, current);
        // 053DB0 uses the actual capital's virtual90/virtualA0 and direct Troops3C.
        // Equality fails here even though the downstream generic order allows exact funds.
        if (gold >= OriginalAiEconomyNeed(current.ReadBaseResource(treasury, BaseResourceKind.Gold), "Trade_Gold") ||
            food >= OriginalAiEconomyNeed(current.ReadBaseResource(treasury, BaseResourceKind.Supplies), "Trade_Food") ||
            troops >= OriginalAiEconomyNeed(current.ReadBaseResource(capital, BaseResourceKind.Troops), "Trade_Troops"))
            return new(false, null);
        // 1EA9B0 is void: its packet validator1EFBA0 can deliberately decline
        // creation after the outer node's stock checks have passed.
        if (executors.Length == 0 || gold == 0 && food == 0 && troops == 0) return new(true, null);
        foreach (int identity in executors)
        {
            int physical = OriginalAiEconomyNeed(current.ResolveOfficerSource(identity), "Trade_executor_pointer");
            if (PersonCurrentBusySource.Read(current, physical, OriginalAiEconomyMissing)) return new(true, null);
            var linked = current.ReadPersonLinkedEntity(physical) ?? throw OriginalAiEconomyMissing("Trade_executor_association");
            if (OriginalAiEconomyNeed(current.ReadLinkedEntityNativeValidity(linked), "Trade_executor_association_validity"))
                return new(true, null);
        }
        if (actor.AgentKind == OriginalAiAgentKind.District)
        {
            var primary = ForcePrimaryDistrictSource.Resolve(current,
                current.ReadForceTableReference(force.Id) ?? throw OriginalAiEconomyMissing("Trade_Force_pointer"));
            int primaryId = OriginalAiEconomyNeed(primary.PhysicalSlot, "Trade_primary_District");
            if (actor.DistrictId != primaryId)
                throw OriginalAiEconomyMissing("Trade_nonprimary_delegation_contract");
        }
        return new(true, new DispatchForeignTradeOrder(force.Id, countryId, executors, gold, food, troops, null));
    }

    private static T OriginalAiEconomyNeed<T>(T? value, string field) where T : struct =>
        value ?? throw OriginalAiEconomyMissing(field);
    private static InvalidDataException OriginalAiEconomyMissing(string field) => new("original_ai.economy." + field);
}
