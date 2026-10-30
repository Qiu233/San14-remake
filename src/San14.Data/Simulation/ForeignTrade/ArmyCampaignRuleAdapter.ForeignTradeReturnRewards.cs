using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //8057B0 and81CE60. The caller owns one candidate and retains the returned
    //publication on the task until its actual report is acknowledged. Reopening
    //that report must not reenter this settlement producer.
    internal (GameState State, PersonnelCaravanReport Report) SettleForeignTradeReturnCurrent(
        GameState state, SourceWorldContext current, PersonnelCaravanTask task)
    {
        if (task.PendingReport is { } settled) return (state, settled);
        if (task.MemberIds.Length == 0) throw ForeignTradeReturnMissing("member_head");
        int head = ForeignTradeReturnNeed(current.ResolveOfficerSource(task.MemberIds[0]), "head_Person");
        var force = current.ReadPersonForce(head) ?? throw ForeignTradeReturnMissing("head_Force");
        int forceId = ForeignTradeReturnNeed(force.PhysicalSlot, "Force_pointer");
        int rulerId = ForeignTradeReturnNeed(current.ReadForceRecord(forceId)?.RawRuler10, "Force_ruler10");
        int ruler = ForeignTradeReturnNeed((current.ReadPersonLookupReference(rulerId <= 6000 ? rulerId : 0)
            ?? throw ForeignTradeReturnMissing("ruler_lookup")).PhysicalSlot, "ruler_Person");
        var receiver = BaseLookup.Find(ForeignTradeReturnNeed(current.ReadPersonFirstLocation(ruler), "ruler_BaseE2"), current);
        var country = ReadForeignTradeCountry(task.CountryId);
        int ordinaryGold = ForeignTradeOrdinaryReturnGold(state, current, task, forceId);
        int gold = 0, itemId = 0, gemId = 0, gemCount = 0, weaponId = 0;
        bool successful = task.ReturnDisposition != 2;
        bool acquired = false;
        var working = state;
        if (successful)
        {
            switch (task.RewardStage)
            {
                case 1:
                {
                    int item = country.FirstItemId;
                    var pointer = current.ReadRegistryTableReference(RegistryKind.Item, item < 202 ? item : 0)
                        ?? throw ForeignTradeReturnMissing("reward_Item_pointer");
                    if (TalentForceIsPlayerCurrent(working, current, force) &&
                        ForeignTradeReturnNeed(current.ReadRegistryItemRawField50(pointer), "reward_Item50") == 4)
                    {
                        working = SearchAcquireItem(working, current, item < 202 ? item : 0, checked((ushort)rulerId));
                        itemId = item < 202 ? item : 0;
                    }
                    else
                    {
                        int intelligence = ForeignTradeReturnAbilitySum(working, current, task, 2);
                        var sampler = ReadArmyDestructionSampler(working, current)
                            ?? throw ForeignTradeReturnMissing("gold_sampler_World450_45C");
                        int random = TacticEffectSourceSamplerMath.Sample(ForeignTradeParameter(working, "item-fallback-random-bound"),
                            1, intelligence, rulerId, task.CountryId, 0, sampler);
                        gold = unchecked(random + unchecked(ForeignTradeParameter(working, "item-fallback-ability-scale") * intelligence)
                            + ForeignTradeParameter(working, "item-fallback-base"));
                        working = DAddDiplomacyResource(working, current, receiver, BaseResourceKind.Gold, gold);
                    }
                    acquired = true;
                    break;
                }
                case 2:
                    working = ForeignTradeReturnWeapon(working, forceId, task.CountryId, 1);
                    weaponId = country.FirstWeaponId;
                    acquired = true;
                    break;
                case 3:
                    working = ForeignTradeReturnWeapon(working, forceId, task.CountryId, 2);
                    weaponId = country.SecondWeaponId;
                    acquired = true;
                    break;
                case 4:
                {
                    int physical = country.GemId is >= 0 and <= 30 ? country.GemId : 0;
                    var gem = current.ReadSkillGemReference(physical) ?? throw ForeignTradeReturnMissing("reward_Gem_pointer");
                    int before = ForeignTradeReturnNeed(TalentGemAcquisitionSource.Read(working.GemForceInventory, force, gem), "reward_Gem_count");
                    if (physical == 0 || before < 100)
                    {
                        int charm = ForeignTradeReturnAbilitySum(working, current, task, 4);
                        int requested = Math.Max(ForeignTradeParameter(working, "gem-return-minimum"),
                            charm / ForeignTradeParameter(working, "gem-return-charisma-divisor"));
                        gemCount = Math.Min(100 - before, requested);
                        gemId = physical;
                        if (physical != 0)
                        {
                            var inventory = working.GemForceInventory ?? throw ForeignTradeReturnMissing("reward_Gem_inventory");
                            var counts = inventory.Forces.GetValueOrDefault(forceId) ?? throw ForeignTradeReturnMissing("reward_Gem_Force");
                            if (counts.Length != 31) throw ForeignTradeReturnMissing("reward_31_Gem_slots");
                            var after = (ushort[])counts.Clone();
                            after[physical] = unchecked((ushort)Math.Clamp(unchecked(before + gemCount), 0, 100));
                            working = working with { GemForceInventory = inventory with { Forces = new(inventory.Forces) { [forceId] = after } } };
                        }
                        acquired = true;
                    }
                    break;
                }
            }
            working = DAddDiplomacyResource(working, current, receiver, BaseResourceKind.Gold, ordinaryGold);
            if (acquired) working = ForeignTradeReturnSpendFriendship(working, forceId, task.CountryId);
        }
        working = ApplyForeignTradeMemberRewardsCurrent(working, current, task, 18);
        return (working, new(successful, gold, itemId, gemId, gemCount, weaponId, ordinaryGold));
    }

    private int ForeignTradeOrdinaryReturnGold(GameState state, SourceWorldContext current,
        PersonnelCaravanTask task, int forceId)
    {
        int intelligence = ForeignTradeReturnAbilitySum(state, current, task, 2);
        int charm = ForeignTradeReturnAbilitySum(state, current, task, 4);
        int country = Math.Clamp(task.CountryId, 0, 4);
        int costCountry = task.CountryId is 2 or 3 or 4 ? task.CountryId : 1;
        int baseCost = ForeignTradeParameter(state, $"return-gold-{costCountry}-cost");
        int factor = ForeignTradeCurrentSource.ReadFriendship(state, forceId, country)
            / ForeignTradeParameter(state, "return-gold-friendship-divisor");
        int abilities = unchecked(charm + intelligence) / ForeignTradeParameter(state, "return-gold-ability-divisor");
        int cost = unchecked((baseCost / ForeignTradeParameter(state, "return-gold-cost-divisor"))
            * ForeignTradeParameter(state, "return-gold-cost-scale"));
        return unchecked(unchecked(unchecked(factor * abilities) + cost) / 10 * 10);
    }

    private int ForeignTradeReturnAbilitySum(GameState state, SourceWorldContext current, PersonnelCaravanTask task, int dimension)
    {
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        int sum = 0;
        foreach (int id in task.MemberIds)
        {
            int physical = ForeignTradeReturnNeed(current.ResolveOfficerSource(id), "ability_Person");
            if (!ForeignTradeReturnNeed(current.ReadPersonNativeValidity(physical), "ability_Person_validity")) continue;
            int value = ForeignTradeReturnNeed(abilities.ResolveDimension(state, id, dimension,
                (owner, effect) => _baseCapacityInputs.ReadPanelModifier(state, owner, effect)), "effective_ability");
            sum = unchecked(sum + value);
        }
        return sum;
    }

    private static GameState ForeignTradeReturnWeapon(GameState state, int forceId, int countryId, int kind)
    {
        //241500: four countries each own two bits in the existing Force193.
        if (countryId is < 1 or > 4 || kind is < 1 or > 2) return state;
        byte flags = ForeignTradeCurrentSource.ReadFlags(state, forceId);
        int bit = (countryId - 1) * 2 + kind - 1;
        return ForeignTradeCurrentSource.WriteFlags(state, forceId, unchecked((byte)(flags | (1 << bit))));
    }

    private static GameState ForeignTradeReturnSpendFriendship(GameState state, int forceId, int countryId)
    {
        var trade = ForeignTradeCurrentSource.ReadProgress(state, forceId);
        int country = Math.Clamp(countryId, 0, 4);
        int level = trade.AcquiredTechnologyLevels[country];
        int floor = level switch
        {
            0 => ForeignTradeParameter(state, "unacquired-friendship-floor"),
            1 => ForeignTradeParameter(state, "first-friendship-floor"),
            2 => ForeignTradeParameter(state, "second-friendship-floor"),
            3 => ForeignTradeParameter(state, "third-friendship-floor"),
            4 => ForeignTradeParameter(state, "other-friendship-floor"),
            _ => 0
        };
        int reduced = unchecked(trade.FriendshipPoints[country] - ForeignTradeParameter(state, "reward-friendship-spend"));
        int requested = reduced < floor ? floor : Math.Min(reduced, 1000);
        return ForeignTradeCurrentSource.WriteFriendship(state, forceId, country, requested);
    }

    internal GameState ApplyForeignTradeMemberRewardsCurrent(GameState state, SourceWorldContext current,
        PersonnelCaravanTask task, int kind)
    {
        if (kind is not (17 or 18)) throw ForeignTradeReturnMissing("member_reward_kind");
        var working = state;
        string result = task.ReturnDisposition != 2 ? "success" : "failure";
        int changedCount = 0, best = 0, bestPolitics = 0;
        foreach (int id in task.MemberIds)
        {
            int physical = ForeignTradeReturnNeed(current.ResolveOfficerSource(id), "growth_Person");
            if (!ForeignTradeReturnNeed(current.ReadPersonNativeValidity(physical), "growth_Person_validity")) continue;
            var force = current.ReadPersonForce(physical) ?? throw ForeignTradeReturnMissing("growth_Force");
            int forceId = ForeignTradeReturnNeed(force.PhysicalSlot, "growth_Force_pointer");
            int home = ForeignTradeReturnNeed(current.ReadPersonFirstLocation(physical), "growth_BaseE2");
            if (home is >= 1 and <= 61 && forceId is >= 46 and <= 50) continue;
            bool band = false;
            for (int i = 0; i < 5; i++) if (unchecked((uint)(id - (4111 + i * 20))) <= 9) { band = true; break; }
            if (band && home is >= 1 and <= 61 && forceId is not (>= 46 and <= 50)) continue;
            if (task.ReturnDisposition is < 1 or > 6) continue;
            bool changed = false;
            foreach (int dimension in new[] { 3, 2, 1, 0, 4 })
            {
                var growth = _growthSource.TryApplyWithinArmyOperation(working, current, _growthQueries,
                    id, dimension, ForeignTradeParameter(working, kind == 17 ? $"outward-growth-{dimension}" :
                        $"return-{result}-growth-{dimension}"), 1);
                if (growth.Outcome == PersonGrowthOutcome.CurrentInputsUnresolved)
                    throw ForeignTradeReturnMissing("member_2BA120_growth");
                working = growth.State;
                changed |= growth.Receipt is { } receipt && receipt.BeforeBaseAbility != receipt.AfterBaseAbility;
            }
            int merit = kind == 17 ? unchecked(ForeignTradeParameter(working, "outward-merit-day-scale") *
                ReadForeignTradeOutwardMeritDays(working, task.CountryId)) : ForeignTradeParameter(working, $"return-{result}-merit");
            working = ApplyTalentPendingMeritCurrent(working, current, physical, unchecked(merit * 10));
            if (!changed) continue;
            changedCount++;
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
            int politics = ForeignTradeReturnNeed(abilities.ResolveDimension(working, id, 3,
                (owner, effect) => _baseCapacityInputs.ReadPanelModifier(working, owner, effect)), "growth_best_politics");
            if (politics > bestPolitics) { best = id; bestPolitics = politics; }
        }
        if (changedCount == 0 || best == 0) return working;
        int bestPhysical = ForeignTradeReturnNeed(current.ResolveOfficerSource(best), "growth_best_Person");
        var bestForce = current.ReadPersonForce(bestPhysical) ?? throw ForeignTradeReturnMissing("growth_best_Force");
        if (!TalentForceIsPlayerCurrent(working, current, bestForce)) return working;
        var person = working.Officers.GetValueOrDefault(best) ?? throw ForeignTradeReturnMissing("growth_best_owner");
        var ownerGrowth = person.Growth ?? throw ForeignTradeReturnMissing("growth_best_channels");
        var notices = ownerGrowth.Notifications ?? throw ForeignTradeReturnMissing("growth_best_notices");
        var tagged = notices.Select((notice, index) => new OfficerAbilityGrowthNotice(notice.AbilityIndex,
            notice.AfterAbility, notice.BeforeAbility, index == 0 ? 0 : 1, kind, changedCount == 1 ? 1 : 0)).ToArray();
        return working with { Officers = new(working.Officers) { [best] = person with
            { Growth = new(ownerGrowth.Experience, tagged) } } };
    }

    private static int ReadForeignTradeOutwardMeritDays(GameState state, int country)
    {
        if (country is < 1 or > 4) throw ForeignTradeReturnMissing("outward_merit_country");
        int days = ForeignTradeParameter(state, $"return-gold-{country}-cost");
        int selector = ForeignTradeReturnNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "outward_merit_E58");
        if (selector != 0)
        {
            int mode = ForeignTradeReturnNeed(BattleRemakeCurrentSettings.ReadInt32(state, 0xFC), "outward_merit_FC");
            if (mode == 0) days = unchecked(days - ForeignTradeParameter(state, "return-low-difficulty-reduction"));
        }
        else
        {
            int percent = ForeignTradeReturnNeed(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 13), "outward_merit_advanced13");
            days = unchecked(percent * days) / 100;
        }
        return days;
    }

    private static T ForeignTradeReturnNeed<T>(T? value, string field) where T : struct =>
        value ?? throw ForeignTradeReturnMissing(field);
    private static InvalidDataException ForeignTradeReturnMissing(string field) => new("foreign-trade.return." + field);
}
