using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>2E7550(Base,0), including the real2B3FA0 App draw and
    /// governor/resident modifiers. Gate repair pays its mapped City.</summary>
    internal GameState ApplyNormalTurnBaseRepairCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, BaseReference target)
    {
        _ = pool;
        if (!AdministrationK(current.ReadBaseNativeValidity(target), "repair_Base_validity")) return state;
        int fee = AdministrationArmyParameter(state, SimulationParameterKeys.CityFoodExpense.RepairGoldFee);
        if (AdministrationK(current.ReadBaseResource(target, BaseResourceKind.Gold), "repair_Gold") < fee) return state;
        int capacity = BasePostLoad.ReadCapacities(target, current).Field46;
        int wall = BasePostLoad.ReadLinkedField14(target, current);
        if (capacity <= wall) return state;
        int governorId = AdministrationK(current.ReadArmyReturnBaseGovernor2E(target), "repair_Governor2E");
        if (governorId is < 1 or > 6000) return state;
        int location = AdministrationK(current.ReadBaseObjectLink(target), "repair_Base4E");
        bool nearby = AdministrationK(target.Kind == BaseKind.City
            ? target.PhysicalSlot == 0 ? state.UnassignedCityHasNearbyEnemyArmy : state.Cities.GetValueOrDefault(location)?.HasNearbyEnemyArmy
            : target.PhysicalSlot == 0 ? state.UnassignedGateHasNearbyEnemyArmy : state.Gates.GetValueOrDefault(location)?.HasNearbyEnemyArmy,
            "repair_Base67_nearby_enemy");
        if (nearby) return state;
        var footprint = current.ReadBaseHexListHandle(target) ?? throw AdministrationMissing("repair_Base70");
        var node = current.ReadHexListHead(footprint) ?? throw AdministrationMissing("repair_footprint_head");
        while (node.Identity.HasValue)
        {
            var hex = current.ReadHexListNodePayload(node) ?? throw AdministrationMissing("repair_footprint_Hex");
            if (AdministrationK(current.ReadCaptureHexWord16(AdministrationK(hex.PhysicalSlot, "repair_footprint_Hex_id")), "repair_footprint_Hex16") != 0) return state;
            node = current.ReadHexListNodeNext(node) ?? throw AdministrationMissing("repair_footprint_next");
        }
        var obj = current.ReadObjectTableReference(location <= 3000 ? location : 0) ?? throw AdministrationMissing("repair_Object");
        if (!AdministrationK(current.ReadObjectNativeValidity(obj), "repair_Object_validity")) return state;
        int governor = AdministrationK((current.ReadPersonLookupReference(governorId) ?? throw AdministrationMissing("repair_Governor_pointer")).PhysicalSlot, "repair_Governor_identity");
        int politics = 20;
        if (AdministrationK(current.ReadPersonNativeValidity(governor), "repair_Governor_validity"))
        {
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
            politics = Math.Max(20, AdministrationK(abilities.ResolveDimension(state, governorId, 3,
                (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect)), "repair_Governor_politics"));
        }
        var saved = state.CampaignAppRandom32 ?? throw AdministrationMissing("repair_App32");
        var random = new SharedSourceRandom32(saved.State);
        int sampled = random.Next(politics);
        state = state with { CampaignAppRandom32 = new(random.ReadCurrentState()) };
        //TC typed/data.tsv:1A4A9C0=2,1A4A9D8=1,1A4A9E4=1,
        //1A49BA4=1 and nonadvanced repair-mode scales80/100/120.
        int basis = Math.Clamp(unchecked((politics + sampled) * 2), 40, 400);
        var governorForce = current.ReadPersonForce(governor) ?? throw AdministrationMissing("repair_Governor_Force");
        int policy = ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, governorForce, 0x27, true).Value;
        int residentTrait = 0;
        foreach (int person in AdministrationResidents(current, location))
        {
            if (PersonCurrentBusySource.Read(current, person, AdministrationMissing)) continue;
            residentTrait = AdministrationK(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x2B), "repair_resident_trait43");
            if (residentTrait != 0) break;
        }
        int product = unchecked(residentTrait * basis) / 100;
        int traitAmount = residentTrait > 0 ? Math.Max(1, product) : residentTrait < 0 ? Math.Min(-1, product) : 0;
        var force = AdministrationForce(current, AdministrationK(current.ReadBaseForceSlot(target), "repair_Base_Force"));
        int advantage = AdministrationK(PersonReturnPathSource.ReadForceAdvantage(state, current, force, 4).Value, "repair_advantage4");
        int gain = unchecked(basis + unchecked(advantage * basis) / 100 + traitAmount + unchecked(basis * policy) / 100);
        int mode = AdministrationK(BattleRemakeCurrentSettings.ReadInt32(state, 0xC8), "repair_modeC8");
        int selector = AdministrationK(BattleRemakeCurrentSettings.ReadSelectorE58(state), "repair_settings_selector");
        if (selector == 0)
            gain = unchecked(AdministrationK(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 6), "repair_advanced_percent") * gain) / 100;
        else gain = unchecked((mode == 0 ? 80 : mode == 2 ? 120 : 100) * gain) / 100;
        int currentCapacity = BasePostLoad.ReadCapacities(target, current).Field46;
        int before = BasePostLoad.ReadLinkedField14(target, current);
        gain = Math.Min(gain, unchecked(currentCapacity - before));
        int after = Math.Min(unchecked(before + gain), BasePostLoad.ReadCapacities(target, current).Field46);
        BasePostLoad.WriteLinkedField14(target, after, current);
        byte flag = AdministrationK(current.ReadBasePostLoadFlag66(target), "repair_Base66");
        if (flag != 0)
        {
            byte updated = BasePostLoad.ReadFlag66(target, current);
            if (updated != flag) current.SetBasePostLoadFlag66(target, updated);
        }
        ushort stored = AdministrationK(current.ReadObjectRawField14(obj), "repair_stored_Object14");
        if (target.Kind == BaseKind.City)
        {
            var owner = state.Cities.GetValueOrDefault(location) ?? throw AdministrationMissing("repair_City_owner");
            state = state with { Cities = new(state.Cities) { [location] = owner with { Durability = stored, DamageTier = current.ReadBasePostLoadFlag66(target) } } };
        }
        else
        {
            var owner = state.Gates.GetValueOrDefault(location) ?? throw AdministrationMissing("repair_Gate_owner");
            state = state with { Gates = new(state.Gates) { [location] = owner with { Durability = stored, DamageTier = current.ReadBasePostLoadFlag66(target) } } };
        }
        var payer = CurrentGateMapQueries.FindCity(target, current);
        if (AdministrationK(current.ReadBaseNativeValidity(payer), "repair_payer_validity"))
            state = AddNormalTurnBaseResourceCurrent(state, current, payer, BaseResourceKind.Gold, unchecked(-fee), recordExpense: true);
        return state;
    }

    private GameState ApplyAdministrationForceExperienceCurrent(GameState state, SourceWorldContext current,
        ForceReference force, int amount, List<CampaignEffectRequest> effects)
    {
        if (!AdministrationK(current.ReadForceNativeValidity(force), "experience_Force_validity") || amount < 0) return state;
        _force164Inputs.BindCurrent(state, current);
        int id = AdministrationK(force.PhysicalSlot, "experience_Force_identity");
        int before = AdministrationK(current.ReadForce164(force), "experience_Force164");
        int oldLevel = AdministrationK(OwnedForce164Inputs.ReadLevel(state, before), "experience_old_level");
        int after = Math.Clamp(unchecked(before + amount), 0, 9999999);
        int newLevel = AdministrationK(OwnedForce164Inputs.ReadLevel(state, after), "experience_new_level");
        var packet = new ArmyDestructionForce164Packet(current, force, requested: amount, before: before,
            after: after, oldLevel: oldLevel, newLevel: newLevel);
        ArmyDestructionForce164.ApplyContext(packet);
        state = ArmyDestructionForce164.PublishCurrent(state, packet);
        if (TalentForceIsPlayerCurrent(state, current, force))
        {
            int shown = newLevel == 10 ? unchecked(AdministrationK(OwnedForce164Inputs.ReadMaximumThreshold(state), "experience_maximum_threshold") - before) : amount;
            if (amount > 0 && oldLevel != 10 && shown > 0) effects.Add(new(null, null) { TalentForceExperiencePresentation =
                new(TalentForceExperiencePresentationOperation.ExperienceGain, id, shown) { PreviousLevel = oldLevel } });
            if (oldLevel < newLevel && newLevel == 10) effects.Add(new(null, null) { TalentForceExperiencePresentation =
                new(TalentForceExperiencePresentationOperation.LevelTenReached, id, 0) { PreviousLevel = oldLevel } });
        }
        return state;
    }
}
