using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //6D6F50/6D6D40/6D7150 and their food counterparts pass32C110's
    //cost to324150. This is the preparation selector, not Spy's90% search.
    internal AreaAdministrationQuery<DiplomacyNegotiationAmountSelection> ReadDiplomacyPreparationAmountCurrent(
        GameState state, SourceWorldContext current, int executor, int target, DiplomaticMissionKind kind,
        int months, int? targetCity, bool supplies, int stock)
    {
        try
        {
            var q = new DiplomacyQuery(this, state, current, executor, target,
                DiplomacyCommandSource.NativeKind(kind), months, targetCity);
            var home = new DiplomacyCommandSource(state, current).Capital(q.OwnOrdinal);
            var city = BaseCurrencyAdditionSource.CurrencyCity(home, current);
            var owner = q.BaseForce(city);
            int intelligence = 0;
            int? ruler = null;
            if (DNeed(current.ReadForceNativeValidity(owner), "preset_capital_Force_validity"))
            {
                int person = q.Ruler(owner);
                if (q.Valid(person))
                {
                    ruler = person;
                    intelligence = DNeed(new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
                        SimulationRuleIdentity.ForLocale(Seed.Language)).ResolveDimension(state, q.Id(person), 2,
                            (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect)), "preset_ruler_INT");
                }
            }
            //Current CE profile's324150 constants: INT floor70, estimate
            //spread167%, upper band120%. Sample is the existing pure world
            //sampler, not an App32 draw or a random trial/settlement.
            int spread = unchecked(100 - Math.Max(70, intelligence));
            int sample = 0;
            int bound = unchecked(Math.Max(0, spread) * 2 + 1);
            if (spread != 0 && ruler is int actor && bound >= 2)
                sample = TacticEffectSourceSamplerMath.Sample(bound, 1, q.Id(actor), q.Ordinal(owner), 0, 0,
                    ReadArmyDestructionSampler(state, current) ?? throw DiplomacyNumericMissing("preset_sampler"));
            int estimate = unchecked(unchecked(unchecked((sample - spread) * 167) / 100 + 100) * q.PreparationOfferCost()) / 100;
            int upper = unchecked(120 * estimate) / 100;
            int minimum = supplies ? 10000 : 1000;
            int budget = Math.Max(minimum, unchecked(stock - (supplies ? 10000 : 5000)));
            int low = minimum, middle = 0, high = 0;
            if (minimum < budget)
            {
                int targetAmount = supplies ? unchecked(estimate * 10) : estimate;
                int upperAmount = supplies ? unchecked(upper * 10) : upper;
                if (targetAmount < budget) { middle = targetAmount; high = Math.Min(budget, upperAmount); }
                else middle = budget;
            }
            else low = budget;
            return AreaAdministrationQuery<DiplomacyNegotiationAmountSelection>.Known(
                new(stock >= minimum, minimum, stock, Array.AsReadOnly(new[] { low, middle, high })));
        }
        catch (InvalidDataException missing)
        { return AreaAdministrationQuery<DiplomacyNegotiationAmountSelection>.Unknown(missing.Message); }
    }

    private sealed partial class DiplomacyQuery
    {
        //32C110's actual4/5/9 pure cost branch. No borrowed task is created.
        internal int PreparationOfferCost()
        {
            if (NativeKind == 4) return GoodwillCost();
            if (NativeKind == 5)
            {
                int ownCities = CityCount(Own), otherCities = CityCount(Other);
                if (ownCities < otherCities) otherCities = unchecked(otherCities * 2 - ownCities);
                int amount = Math.Max(Parameter("alliance-cost-base"), unchecked(
                    unchecked((AllianceMonths - Parameter("alliance-months-baseline")) * Parameter("alliance-month-cost")) +
                    unchecked(Parameter("alliance-city-cost") * otherCities) + Parameter("alliance-cost-base")));
                return unchecked(amount + RelationCost(true));
            }
            if (NativeKind == 9)
            {
                int cities = CityCount(Other), victim = CityCount(BaseForce(AttackBase()));
                int difference = cities < victim ? unchecked(victim - cities) : 1;
                return unchecked(unchecked(difference * difference) * Parameter("attack-city-square-cost") +
                    Parameter("attack-cost-base") + RelationCost(false));
            }
            throw DiplomacyNumericMissing("preset_kind");
        }
    }
}
