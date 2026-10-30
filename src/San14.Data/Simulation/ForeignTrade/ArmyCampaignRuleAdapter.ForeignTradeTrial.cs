using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //324650→32D4A0. The adviser tests the offered food against journey cost;
    //gold and the item do not enter this particular prediction.
    internal int ResolveForeignTradeTrialCurrent(GameState state, SourceWorldContext current,
        IReadOnlyList<int> memberIds, int country, int gold, int supplies, int troops, int itemId)
    {
        if (country is < 1 or > 4 || memberIds.Count == 0) return 0;
        int first = ForeignTradeNeed(current.ResolveOfficerSource(memberIds[0]), "trial_head");
        var force = current.ReadPersonForce(first) ?? throw CaravanMissing("trial_force");
        if (!ForeignTradeNeed(current.ReadForceNativeValidity(force), "trial_force_validity")) return 0;
        return ReadForeignTradeRequiredFoodCurrent(state, current, memberIds, country, troops) <= supplies ? 1 : 0;
    }

    //32D4A0's shared amount. Its callers retain their own admission: the
    //normal adviser checks Force validity, and the AI source checks Country
    //validity plus a nonempty actual member list before reaching this query.
    internal int ReadForeignTradeRequiredFoodCurrent(GameState state, SourceWorldContext current,
        IReadOnlyList<int> memberIds, int country, int troops)
    {
        int first = ForeignTradeNeed(current.ResolveOfficerSource(memberIds[0]), "trial_head");
        var force = current.ReadPersonForce(first) ?? throw CaravanMissing("trial_force");
        int abilities = 0; bool trait = false;
        var abilitySource = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        foreach (int id in memberIds)
        {
            int person = ForeignTradeNeed(current.ResolveOfficerSource(id), "trial_person");
            for (int dimension = 0; dimension < 5; dimension++)
                abilities = unchecked(abilities + ForeignTradeNeed(abilitySource.ResolveDimension(state, id, dimension,
                    (owner, effect) => _baseCapacityInputs.ReadPanelModifier(state, owner, effect)), "trial_ability"));
            if (!trait) trait = ForeignTradeNeed(TacticRemakeCurrentTraits.CommanderHasEffect(current, person, 114), "trial_trait114");
        }
        int factor = Math.Max(1, unchecked((troops / ForeignTradeParameter(state, "trial-troop-divisor")) *
            ForeignTradeParameter(state, "trial-troop-scale")) / 10);
        int amount = unchecked(ForeignTradeParameter(state, "trial-food-base") * ReadForeignTradeCountry(state, country).ReturnDays * factor * 2) / 100;
        amount = unchecked(amount * (abilities < ForeignTradeParameter(state, "trial-leadership-threshold")
            ? ForeignTradeParameter(state, "trial-low-leadership-scale") : 10)) / 10;
        amount = unchecked(amount * (trait ? ForeignTradeParameter(state, "trial-trait-percent") : 100)) / 100;
        int advantage = PersonReturnPathSource.ReadForceAdvantage(state, current, force, 5).Value ?? throw CaravanMissing("trial_advantage5");
        amount = unchecked(amount * (advantage != 0 ? ForeignTradeParameter(state, "trial-advantage-percent") : 100)) / 100;
        return amount;
    }
}
