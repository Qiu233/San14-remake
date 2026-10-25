using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private const string HireAssociationPrefix = "simulation.personnel.hire-association-";
    // File-backed TC constants extracted offline. Saved typed parameters take
    // precedence; this table also supports saves made before this consumer.
    private static readonly IReadOnlyDictionary<string, int> HireAssociationRules = new Dictionary<string, int>
    {
        ["gold-fee"] = 200, ["policy-fee-percent"] = 1, ["trait-fee-percent"] = 1,
        ["minimum-adviser-intelligence"] = 60,
        ["small-force-adviser-percent"] = 100, ["medium-force-adviser-percent"] = 90,
        ["large-force-adviser-percent"] = 80, ["other-force-adviser-percent"] = 70,
        ["busy-activity-factor"] = 1, ["idle-activity-factor"] = 2,
        ["territory-factor"] = 50, ["territory-offset"] = 1,
        ["intelligence-ceiling"] = 120, ["gain-divisor"] = 1000,
        ["policy-gain-percent"] = 1,
        ["easy-gain-percent"] = 70, ["normal-gain-percent"] = 100, ["hard-gain-percent"] = 120,
        ["threatened-gain-percent"] = 130, ["ordinary-gain-percent"] = 100,
        ["highest-rank-budget-capacity"] = 200000, ["high-rank-budget-capacity"] = 150000,
        ["middle-rank-budget-capacity"] = 100000, ["other-rank-budget-capacity"] = 50000
    };

    internal static GameState InstallFreshPersonnelHireAssociationParameters(GameState state)
    {
        var native = state.NativeArmySimulation ?? throw HireMissing("association.parameter_owner");
        if (native.Parameters.Language != "TC") return state;
        var values = new Dictionary<string, int>(native.Parameters.Int32Values);
        foreach (var pair in HireAssociationRules) values.TryAdd(HireAssociationPrefix + pair.Key, pair.Value);
        return state with { NativeArmySimulation = native with
            { Parameters = native.Parameters with { Int32Values = values } } };
    }

    //26BB30(filterForce=0), reached by34CC40 only at the ruler's Home.
    //This is a forecast: neither the treasury nor the three assignments are
    //changed. A slot costs gold only when its actual intelligence gain is positive.
    internal int ReadHireCapitalAssociationGoldExpenseCurrent(GameState state,
        SourceWorldContext current, ForceReference force)
    {
        int own = HireNeed(current.ReadForcePhysicalSlot(force), "association.Force_ordinal");
        ushort rulerId = own == 0 ? (ushort)5014 :
            HireNeed(current.ReadForceRecord(HireNeed(force.PhysicalSlot, "association.Force_pointer"))?.RawRuler10,
                "association.ruler10");
        int ruler = HireAssociationPerson(current, rulerId);
        var capital = BaseLookup.Find(HireNeed(current.ReadPersonFirstLocation(ruler), "association.rulerE2"), current);
        int goldLeft = HireNeed(current.ReadBaseResource(capital, BaseResourceKind.Gold), "association.capital_Gold");
        var primary = ForcePrimaryDistrictSource.Resolve(current, force);
        int predicted = ReadDiplomacyDistrictBudget(current, HireNeed(primary.PhysicalSlot, "association.primary_District"));
        int expense = 0;
        for (int slot = 0; slot < 3; slot++)
        {
            var district = ForcePrimaryDistrictSource.Resolve(current, force);
            if (ReadHireAssociationBudgetCapacity(state, current, district) <= predicted) break;
            int rawForce = HireNeed(current.ReadForceAssociationTargetSlot(force, slot), "association.target19C");
            var other = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0)
                ?? throw HireMissing("association.target_Force_pointer");
            int person = HireAssociationPerson(current,
                HireNeed(current.ReadForceAssociationPersonId(force, slot), "association.actor196"));
            if (!HireNeed(current.ReadForceNativeValidity(other), "association.target_validity") ||
                !HireNeed(current.ReadPersonNativeValidity(person), "association.actor_validity")) continue;
            int fee = ReadHireAssociationFee(state, current, force, person);
            if (goldLeft < fee) break;
            int gain = ReadHireAssociationGain(state, current, force, other, person);
            if (gain <= 0) continue;
            predicted = unchecked(predicted + gain);
            goldLeft = unchecked(goldLeft - fee);
            expense = unchecked(expense + fee);
        }
        return expense;
    }

    private int ReadHireAssociationFee(GameState state, SourceWorldContext current, ForceReference force, int actor)
    {
        int basis = HireAssociationParameter(state, "gold-fee");
        int policy = ReadHireAssociationPolicy(state, current, force, 38);
        float discount = (float)unchecked(HireAssociationParameter(state, "policy-fee-percent") * policy) * basis / 100f;
        int reduction = discount > 0f ? discount >= 1f ? HireAssociationTruncate(discount) : 1 : 0;
        int trait = HireNeed(TacticRemakeCurrentTraits.PersonContribution(current, actor, 119), "association.actor_trait119");
        float adjustment = (float)unchecked(HireAssociationParameter(state, "trait-fee-percent") * trait) *
            unchecked(basis - reduction) / 100f;
        int signed = adjustment >= 0f ? adjustment <= 0f ? 0 : adjustment < 1f ? 1 : HireAssociationTruncate(adjustment)
            : adjustment > -1f ? -1 : HireAssociationTruncate(adjustment);
        return Math.Max(0, unchecked(basis - signed - reduction));
    }

    private int ReadHireAssociationGain(GameState state, SourceWorldContext current,
        ForceReference own, ForceReference other, int actor)
    {
        if (!HireNeed(current.ReadForceNativeValidity(other), "association.gain_target_validity") ||
            !HireNeed(current.ReadPersonNativeValidity(actor), "association.gain_actor_validity") ||
            !HireNeed(current.ReadForceNativeValidity(own), "association.gain_own_validity")) return 0;
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language));
        int Intelligence(int physical) => HireNeed(abilities.ResolveDimension(state,
            HireNeed(current.ReadPersonIdentity(physical), "association.ability_Person10"), 2,
            (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect)), "association.current_intelligence");
        int actorIntelligence = Intelligence(actor);
        int otherId = HireNeed(current.ReadForcePhysicalSlot(other), "association.target_ordinal");
        float territory = ReadHireAssociationTerritory(current, otherId);
        int adviser = HireAssociationPerson(current,
            HireNeed(current.ReadAssignmentForceAdvisor52(otherId), "association.target_adviser52"));
        int minimum = HireAssociationParameter(state, "minimum-adviser-intelligence");
        int adviserIntelligence = HireNeed(current.ReadPersonNativeValidity(adviser), "association.adviser_validity")
            ? Intelligence(adviser) : minimum;
        int cities = HireNeed(PersonArmyCaptureSource.ReadOwnedCityCount(current, other), "association.target_City_count");
        int factor = HireAssociationParameter(state, cities is >= 1 and <= 5 ? "small-force-adviser-percent" :
            cities is >= 6 and <= 10 ? "medium-force-adviser-percent" :
            cities is >= 11 and <= 15 ? "large-force-adviser-percent" : "other-force-adviser-percent");
        int defense = unchecked(factor * adviserIntelligence) / 100;
        defense = defense < minimum ? minimum : Math.Min(defense, 120);
        bool busy = (HireNeed(current.ReadArmyReturnPersonFlags196(actor), "association.actor15E") & 1) != 0 ||
            ReadHireAssociationActorBusy(current, actor);
        int activity = HireAssociationParameter(state, busy ? "busy-activity-factor" : "idle-activity-factor");
        float root = MathF.Sqrt(MathF.Sqrt(territory));
        float weight = root * HireAssociationParameter(state, "territory-factor") +
            HireAssociationParameter(state, "territory-offset");
        int product = unchecked((HireAssociationParameter(state, "intelligence-ceiling") - defense) * activity * actorIntelligence);
        float raw = weight * product / HireAssociationParameter(state, "gain-divisor");
        int basis = Math.Max(0, HireAssociationTruncate(raw));
        int policy = ReadHireAssociationPolicy(state, current, own, 25);
        int policyAdd = policy > 0 ? Math.Max(1, unchecked(HireAssociationParameter(state, "policy-gain-percent") * policy * basis) / 100) : policy;
        int ownId = HireNeed(current.ReadForcePhysicalSlot(own), "association.own_ordinal");
        int panel = HireNeed(_baseCapacityInputs.ReadPanelModifier(state, ownId, 44), "association.Force_panel44");
        int gain = unchecked(unchecked(panel * basis) / 100 + policyAdd + basis);
        int selector = HireNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "association.settings_E58");
        int percent;
        if (selector != 0)
        {
            int difficulty = HireNeed(BattleRemakeCurrentSettings.ReadInt32(state, 0x12C), "association.settings_difficulty12C");
            percent = HireAssociationParameter(state, difficulty == 0 ? "easy-gain-percent" :
                difficulty == 2 ? "hard-gain-percent" : "normal-gain-percent");
        }
        else
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw HireMissing("association.player_Force_pointer");
            int playerId = HireNeed(current.ReadForcePhysicalSlot(player), "association.player_ordinal");
            percent = HireNeed(BattleRemakeCurrentSettings.ReadAdvancedParameter(state,
                playerId != 0 && playerId == ownId ? 22 : 23), "association.settings_advanced_gain_percent");
        }
        gain = unchecked(percent * gain) / 100;
        int threat = HireAssociationParameter(state, ReadHireAssociationThreat(state, current, otherId)
            ? "threatened-gain-percent" : "ordinary-gain-percent");
        gain = unchecked(threat * gain) / 100;
        int districtSlot = HireNeed(current.ReadPersonDistrictSlot(actor), "association.actorE0");
        var actorDistrict = current.ReadDistrictTableReference(districtSlot < 52 ? districtSlot : 0)
            ?? throw HireMissing("association.actor_District_pointer");
        int remaining = unchecked(ReadHireAssociationBudgetCapacity(state, current, actorDistrict) -
            ReadDiplomacyDistrictBudget(current, HireNeed(actorDistrict.PhysicalSlot, "association.actor_District")));
        return Math.Min(gain, remaining);
    }

    private static int ReadHireAssociationBudgetCapacity(GameState state, SourceWorldContext current, DistrictReference district)
    {
        int rawForce = HireNeed(current.ReadDistrictForceSlot(district), "association.capacity_District10");
        var force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0)
            ?? throw HireMissing("association.capacity_Force_pointer");
        int rank = HireNeed(current.ReadMoraleWaveForceRank47(HireNeed(force.PhysicalSlot, "association.capacity_Force")),
            "association.capacity_rank47");
        return HireAssociationParameter(state, rank switch
        {
            1 or 2 or 3 or 10 or 11 or 12 => "highest-rank-budget-capacity",
            4 or 5 => "high-rank-budget-capacity", 6 or 7 => "middle-rank-budget-capacity",
            _ => "other-rank-budget-capacity"
        });
    }

    //22FCA0's cached value is only the quotient of the current Area registry.
    //Computing that derived value directly avoids publishing the native date cache.
    private static float ReadHireAssociationTerritory(SourceWorldContext current, int force)
    {
        int owned = 0, total = 0;
        foreach (var entry in current.ReadRegisteredAreaReferences() ?? throw HireMissing("association.Area_registry"))
        {
            var area = entry ?? throw HireMissing("association.Area_pointer");
            if (!HireNeed(current.ReadAreaNativeValidity(area), "association.Area_validity")) continue;
            if (HireNeed(current.ReadAreaCurrentForceSlot(area), "association.Area_Hex_Force") == force) owned++;
            total++;
        }
        return total > 0 ? (float)owned / total : 0f;
    }

    private static bool ReadHireAssociationActorBusy(SourceWorldContext current, int actor)
    {
        byte status = HireNeed(current.ReadPersonStatus(actor), "association.actorE6");
        if (status is >= 1 and <= 4 &&
            PersonPositionRepair.DecodeFirstLocation(HireNeed(current.ReadPersonFirstLocation(actor), "association.actorE2")) !=
            PersonPositionRepair.DecodeSecondLocation(HireNeed(current.ReadPersonSecondLocation(actor), "association.actorE4"))) return true;
        var linked = current.ReadPersonLinkedEntity(actor) ?? throw HireMissing("association.actor_command_pointer");
        if (!HireNeed(current.ReadLinkedEntityNativeValidity(linked), "association.actor_command_validity")) return false;
        var manager = current.ReadOwnedPersonCommandManager() ?? throw HireMissing("association.personnel_manager");
        int id = HireNeed(linked.Identity, "association.actor_command_identity");
        if (manager.Disappearances?.ContainsKey(id) == true) return true; // Actual v98=16.
        if (manager.Returns.ContainsKey(id) || manager.Talents?.ContainsKey(id) == true ||
            manager.Diplomacies?.ContainsKey(id) == true) return false;
        throw HireMissing("association.actor_command_virtual98");
    }

    //232660: the first threatened owned Base, then non-excluded registered
    //Armies with bit0 of26. This is not a troop-count or Force-validity test.
    private static bool ReadHireAssociationThreat(GameState state, SourceWorldContext current, int force)
    {
        foreach (var entry in current.ReadRegisteredBases() ?? throw HireMissing("association.Base_registry"))
        {
            var target = entry ?? throw HireMissing("association.Base_pointer");
            if (force is < 1 or > 51 || HireNeed(current.ReadBaseForceSlot(target), "association.Base_Force") != force) continue;
            if (!HireNeed(current.ReadBaseNativeValidity(target), "association.Base_validity")) continue;
            int location = HireNeed(current.ReadBaseObjectLink(target), "association.Base4E");
            bool? nearby = target.Kind == BaseKind.City
                ? target.PhysicalSlot == 0 ? state.UnassignedCityHasNearbyEnemyArmy : state.Cities.GetValueOrDefault(location)?.HasNearbyEnemyArmy
                : target.PhysicalSlot == 0 ? state.UnassignedGateHasNearbyEnemyArmy : state.Gates.GetValueOrDefault(location)?.HasNearbyEnemyArmy;
            if (HireNeed(nearby, "association.Base67_nearby_enemy")) return true;
        }
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army) ?? throw HireMissing("association.Army_registry"))
        {
            if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical }) throw HireMissing("association.Army_pointer");
            var army = new ArmyUnitReference(physical);
            if (force is < 1 or > 51 || current.ReadArmyUnitRawForceSlot(army) != force) continue;
            if (HireNeed(current.ReadOwnedArmyExclusion233560(entry), "association.Army_excluded")) continue;
            if (!HireNeed(current.ReadArmyUnitNativeValidity(army), "association.Army_validity")) continue;
            var arena = state.ReadArmyLifecycle() ?? throw HireMissing("association.Army_lifecycle");
            var receiver = arena.Receivers.Find(arena.Slots[physical].ReceiverId)
                ?? throw HireMissing("association.Army_receiver");
            var image = receiver.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : receiver.PendingImage;
            if ((HireNeed(image?.NativeTacticRuntime?.Flags26, "association.Army26") & 1) != 0) return true;
        }
        return false;
    }

    private static int HireAssociationPerson(SourceWorldContext current, int id) =>
        HireNeed((id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot,
            "association.Person_pointer");

    private static int ReadHireAssociationPolicy(GameState state, SourceWorldContext current, ForceReference reference, int effect)
    {
        int id = HireNeed(current.ReadForcePhysicalSlot(reference), "association.policy_Force");
        var force = state.Forces.GetValueOrDefault(id) ?? throw HireMissing("association.policy_Force_owner");
        return HireNeed(BattleRemakeCurrentForcePolicies.ReadForceEffect(state, force, effect, current), "association.policy_effect");
    }

    private static int HireAssociationParameter(GameState state, string name)
    {
        var bank = state.NativeArmySimulation?.Parameters ?? throw HireMissing("association.parameter_owner");
        string key = HireAssociationPrefix + name;
        if (bank.Language != "TC" || bank.RuleVersion != SimulationRuleIdentity.ForLocale("TC") || bank.Float32Values.ContainsKey(key))
            throw HireMissing(key);
        return bank.Int32Values.TryGetValue(key, out int value) ? value :
            HireAssociationRules.TryGetValue(name, out int initial) ? initial : throw HireMissing(key);
    }

    private static int HireAssociationTruncate(float value) => !float.IsFinite(value) || value < int.MinValue || value >= 2147483648f
        ? int.MinValue : (int)value;
}
