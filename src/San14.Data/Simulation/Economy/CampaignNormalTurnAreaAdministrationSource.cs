using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>2955A0(Base,0,&scratch): actual upkeep, development,
    /// security and rewards, borrowing the enclosing Turn's world and pool.</summary>
    internal GameState ApplyNormalTurnAreaAdministrationCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, BaseReference city,
        List<CampaignEffectRequest> effects)
    {
        _ = pool;
        int location = AdministrationK(current.ReadBaseObjectLink(city), "City4E");
        if (location > 51 || AdministrationK(current.ReadBaseForceSlot(city), "City_B0") == 0) return state;
        _ = state.NativeMapSeason?.ThirdDate ?? throw AdministrationMissing("World34_date");
        int developmentTrait = 0, securityTrait = 0;
        foreach (int person in AdministrationResidents(current, location))
        {
            if (PersonCurrentBusySource.Read(current, person, AdministrationMissing)) continue;
            developmentTrait = Math.Max(developmentTrait, AdministrationK(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x2D), "resident_trait45"));
            securityTrait = Math.Max(securityTrait, AdministrationK(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x4A), "resident_trait74"));
        }
        var temporary = current.CreateTemporaryHexList() ?? throw AdministrationMissing("Area_temporary");
        var selected = new List<(AreaReference Area, HexListNodeReference Node)>();
        uint levelSum = 0;
        try
        {
            int cityId = AdministrationK(city.PhysicalSlot, "City_identity");
            if (cityId is >= 1 and <= 51)
                foreach (var item in current.ReadRegisteredAreaReferences() ?? throw AdministrationMissing("Area_managerB0"))
                {
                    var area = item ?? throw AdministrationMissing("Area_pointer");
                    if (AdministrationK(current.ReadAreaCitySlot(area), "Area35") != cityId ||
                        AdministrationK(current.ReadAreaCurrentForceSlot(area), "Area_Force") != AdministrationK(current.ReadBaseForceSlot(city), "City_Force_repeated")) continue;
                    int id = AdministrationK(area.PhysicalSlot, "Area_identity");
                    if (AdministrationK(current.ReadAssignmentArea36(id), "Area36") == 0) continue;
                    byte focus = AdministrationK(current.ReadAssignmentArea72(id), "Area72");
                    bool develops = focus is >= 1 and <= 3 && AdministrationLevel(state, id, focus) < 5;
                    if (AdministrationK(state.Areas.GetValueOrDefault(id)?.Security, "Area_security") >= 100 && !develops) continue;
                    var node = current.AllocateHexListNode(temporary);
                    if (node.Identity is not int cursor) continue;
                    current.WriteCurrentListPayload(cursor, area);
                    selected.Add((area, node));
                }
            for (int index = 1; index < selected.Count; index++)
            {
                var item = selected[index]; int before = index;
                while (before > 0 && AreaBefore(item.Area, selected[before - 1].Area))
                { selected[before] = selected[before - 1]; before--; }
                selected[before] = item;
            }
            if (temporary.Identity.HasValue) current.ReorderCurrentList(temporary, selected.Select(item => item.Node).ToArray());
            var force = AdministrationForce(current, AdministrationK(current.ReadBaseForceSlot(city), "policy_Force"));
            int developmentPolicy = ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, force, 0x1D, true).Value;
            int securityPolicy = ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current,
                AdministrationForce(current, AdministrationK(current.ReadBaseForceSlot(city), "security_policy_Force")), 0x1E, true).Value;
            foreach (var item in selected)
            {
                int area = AdministrationK(item.Area.PhysicalSlot, "selected_Area");
                int person = PersonAreaAppointmentSource.Resolve(current, AdministrationK(current.ReadAssignmentArea36(area), "selected_Area36"));
                int rawHex = AdministrationK(current.ReadAreaHexIndex(item.Area), "selected_Area3A");
                var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0) ?? throw AdministrationMissing("selected_Hex");
                int feePolicy = ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current,
                    AdministrationForce(current, AdministrationK(current.ReadHexForceSlot(hex), "selected_Hex14")), 0x26, true).Value;
                int fee = AreaAdministrationUpkeep.Calculate(AreaAdministrationUpkeep.ReadCurrent(state), feePolicy,
                    AdministrationK(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x27), "selected_trait39"));
                if (AdministrationK(current.ReadBaseResource(city, BaseResourceKind.Gold), "City_gold") < fee) break;
                if (!AdministrationK(current.ReadPersonNativeValidity(person), "assigned_Person_validity"))
                    throw AdministrationMissing("assigned_Person_invalid_scalar_fallback");
                byte focus = AdministrationK(current.ReadAssignmentArea72(area), "selected_Area72");
                bool scalarAway = AdministrationScalarAway(state, current, person);
                var preview = new AreaAdministrationPreviewSource(state, current, _scenario, Seed.Language, _baseCapacityInputs, true);
                int gain = AdministrationValue(preview.AutoDevelopment(area, person, focus, Tasks,
                    developmentTrait, developmentPolicy, candidateCity: false, awayAtSource: scalarAway));
                if (focus is >= 1 and <= 3)
                {
                    var field = focus == 1 ? CeAreaDevelopmentField.Commerce : focus == 2 ? CeAreaDevelopmentField.Agriculture : CeAreaDevelopmentField.Barracks;
                    var writer = new OwnedAreaDevelopmentContext(state, current, _normalAftermathInputs);
                    int progress = AdministrationK(writer.ReadAreaProgress(item.Area, field), "development_progress");
                    _normalAftermathInputs.Development.WriteCurrentProgress(item.Area, field,
                        Math.Clamp(unchecked(progress + gain), 0, 1000), 1, writer);
                    state = writer.State;
                    levelSum = unchecked(levelSum + (uint)AdministrationLevel(state, area, focus));
                }
                if (gain > 0) state = ApplyAdministrationPersonDevelopmentRewardCurrent(state, current, person,
                    focus == 2 ? 1 : focus == 3 ? 2 : 0);
                preview = new AreaAdministrationPreviewSource(state, current, _scenario, Seed.Language, _baseCapacityInputs, true);
                int securityGain = AdministrationValue(preview.Security(area, person, false, Tasks, securityTrait, securityPolicy,
                    awayAtSource: AdministrationScalarAway(state, current, person)));
                if (AdministrationK(current.ReadAreaCurrentForceSlot(item.Area), "security_Area_Force") is not (>= 46 and <= 50))
                {
                    var owner = state.Areas[area];
                    int security = Math.Clamp(unchecked(AdministrationK(owner.Security, "security_before") + securityGain), 0, 100);
                    state = state with { Areas = new(state.Areas) { [area] = owner with { Security = security } } };
                }
                if (securityGain > 0 && AdministrationPersonRewardEligible(current, person))
                {
                    bool away = AdministrationPersonAway(current, person);
                    state = ApplyAdministrationGrowthCurrent(state, current, person, 0, away ? 2 : 10);
                    state = ApplyTalentPendingMeritCurrent(state, current, person, 0);
                }
                state = AddNormalTurnBaseResourceCurrent(state, current, city, BaseResourceKind.Gold, unchecked(-fee), recordExpense: true);
            }
            //1EDFA0 seeks an actual command25 receiver. Existing command
            //owners represent Return/Absence/Talent/Diplomacy/Strategy tasks;
            //none of those kinds is a defensive administration receiver.
            RequireNoAdministrationCounterIntelligenceCommand(current);
            var finalForce = AdministrationForce(current, AdministrationK(current.ReadBaseForceSlot(city), "tail_Force"));
            if (AdministrationK(current.ReadForceNativeValidity(finalForce), "tail_Force_validity") && levelSum != 0)
            {
                int id = AdministrationK(finalForce.PhysicalSlot, "tail_Force_identity");
                var native = state.NativeArmySimulation ?? throw AdministrationMissing("tail_native_owner");
                var owner = native.Forces.GetValueOrDefault(id) ?? throw AdministrationMissing("tail_Force_owner");
                uint best = AdministrationK(owner.BestAdministrationLevelSum, "Force_best_administration_level_sum");
                if (best < levelSum)
                {
                    state = state with { NativeArmySimulation = native with { Forces = new(native.Forces)
                        { [id] = owner with { BestAdministrationLevelSum = levelSum } } } };
                    int ruler = AdministrationK(current.ReadForceRecord(id)?.RawRuler10, "experience_ruler10");
                    int physical = AdministrationK((ruler <= 6000 ? current.ReadPersonLookupReference(ruler) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "experience_ruler_pointer");
                    int trait = AdministrationK(current.ReadPersonNativeValidity(physical), "experience_ruler_validity")
                        ? AdministrationK(TacticRemakeCurrentTraits.PersonContribution(current, physical, 0x4C), "experience_trait76") : 0;
                    int advantage = AdministrationK(PersonReturnPathSource.ReadForceAdvantage(state, current, finalForce, 6).Value, "experience_advantage6");
                    //TC immutable141A49090=50,141A490B4=1; shared effect6
                    //and Force12 flag8 coefficients use their saved owners.
                    int amount = unchecked(50 + unchecked(advantage * AdministrationArmyParameter(state,
                        SimulationParameterKeys.ArmyDestruction.ForceExperienceLandAdvantageMultiplier) * 50) / 100 + unchecked(trait * 50) / 100);
                    if ((AdministrationK(owner.Flags12, "experience_Force12") & 8) != 0)
                        amount = unchecked(amount + unchecked(AdministrationArmyParameter(state,
                            SimulationParameterKeys.ArmyDestruction.ForceExperienceFlagBonusPercent) * 50) / 100);
                    state = ApplyAdministrationForceExperienceCurrent(state, current, finalForce, amount, effects);
                }
            }
        }
        finally { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); }
        var published = state;
        current.ConditionAreaSecurityReader = id => published.Areas.GetValueOrDefault(id)?.Security is int value && value is >= 0 and <= 255 ? (byte)value : null;
        return state;

        int Tasks(int person) => (current.ReadAssignmentPersonAreaVector(person) ?? throw AdministrationMissing("Person_assignment_vector")).ActiveCount1E8;
        bool AreaBefore(AreaReference first, AreaReference second)
        {
            bool a = IsOccupationCityArea(current, first), b = IsOccupationCityArea(current, second);
            return a != b ? a : AdministrationK(first.PhysicalSlot, "sort_Area") < AdministrationK(second.PhysicalSlot, "sort_other_Area");
        }
    }

    private GameState ApplyAdministrationPersonDevelopmentRewardCurrent(GameState state, SourceWorldContext current, int person, int kind)
    {
        if (!AdministrationPersonRewardEligible(current, person)) return state;
        bool away = AdministrationPersonAway(current, person);
        //2B94D0 kind0/1/2: fixed XP and task-scaled merit, not a fraction
        //of the development increment. NormalTurn is outside UI override3348E0.
        int dimension = kind == 0 ? 2 : kind == 1 ? 3 : 1;
        int growth = kind == 0 ? away ? 10 : 50 : kind == 1 ? away ? 5 : 25 : away ? 1 : 5;
        int task = 100;
        if (AdministrationK(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x7E), "reward_trait126") < 1)
            task = (current.ReadAssignmentPersonAreaVector(person) ?? throw AdministrationMissing("reward_task_vector")).ActiveCount1E8
                switch { 0 or 1 => 100, 2 => 64, 3 => 44, _ => throw AdministrationMissing("reward_task_fallback_global") };
        state = ApplyAdministrationGrowthCurrent(state, current, person, dimension, growth);
        return ApplyTalentPendingMeritCurrent(state, current, person, unchecked((away ? 24 : 48) * task * 10) / 100);
    }

    private GameState ApplyAdministrationGrowthCurrent(GameState state, SourceWorldContext current, int person, int dimension, int amount)
    {
        var result = _growthSource.TryApplyWithinArmyOperation(state, current, _growthQueries,
            AdministrationK(current.ReadPersonIdentity(person), "growth_Person10"), dimension, amount, 0);
        return result.Outcome == PersonGrowthOutcome.CurrentInputsUnresolved ? throw AdministrationMissing("growth_2BA120") : result.State;
    }

    private static bool AdministrationPersonRewardEligible(SourceWorldContext current, int person)
    {
        if (!AdministrationK(current.ReadPersonNativeValidity(person), "reward_Person_validity")) return false;
        int home = AdministrationK(current.ReadPersonFirstLocation(person), "reward_PersonE2");
        if (home is < 1 or > 61) return true;
        int force = AdministrationK(current.ReadPersonForce(person)?.PhysicalSlot, "reward_Person_Force");
        if (force is >= 46 and <= 50) return false;
        int id = AdministrationK(current.ReadPersonIdentity(person), "reward_Person_identity");
        for (int row = 0; row < 5; row++) if (id >= 4111 + row * 20 && id <= 4120 + row * 20) return false;
        return true;
    }

    private static bool AdministrationPersonAway(SourceWorldContext current, int person)
        => (AdministrationK(OfficerBehaviorSourceAdapter.EncodeFlags(current.ReadPersonBehavior(person)), "reward_Person15E") & 1) != 0 ||
            PersonCurrentBusySource.Read(current, person, AdministrationMissing);

    private static bool AdministrationScalarAway(GameState state, SourceWorldContext current, int person)
    {
        if ((AdministrationK(OfficerBehaviorSourceAdapter.EncodeFlags(current.ReadPersonBehavior(person)), "scalar_Person15E") & 1) != 0) return true;
        if (AdministrationK(PersonArmyLocationSource.ReadCommanderArmyValidity(state, current, person), "scalar_commander_Army_validity")) return true;
        var linked = current.ReadPersonLinkedEntity(person) ?? throw AdministrationMissing("scalar_associated22AC00");
        return AdministrationK(current.ReadLinkedEntityNativeValidity(linked), "scalar_associated_validity");
    }

    private static IEnumerable<int> AdministrationResidents(SourceWorldContext current, int location)
    {
        if (location is < 1 or > 61) yield break;
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw AdministrationMissing("resident_Person_registry"))
        {
            int person = AdministrationK(entry, "resident_Person_pointer");
            if (AdministrationK(current.ReadPersonFirstLocation(person), "resident_PersonE2") == location &&
                AdministrationK(current.ReadPersonStatus(person), "resident_PersonE6") is >= 1 and <= 4) yield return person;
        }
    }

    private static void RequireNoAdministrationCounterIntelligenceCommand(SourceWorldContext current)
    {
        var manager = current.ReadOwnedPersonCommandManager() ?? throw AdministrationMissing("counterintelligence_command_manager");
        if (manager.Disappearances is null || manager.Talents is null || manager.Diplomacies is null || manager.Strategies is null)
            throw AdministrationMissing("counterintelligence_command_kinds");
        if (manager.Strategies.Values.Any(task => task.Kind is not (10 or 11 or 12 or 13 or 14 or 20 or 24)))
            throw AdministrationMissing("counterintelligence_unmapped_command25");
    }

    private static int AdministrationLevel(GameState state, int area, int focus)
    {
        var values = state.Areas.GetValueOrDefault(area)?.Development ?? throw AdministrationMissing("Area_development");
        int progress = AdministrationK(focus == 1 ? values.Commerce : focus == 2 ? values.Agriculture : values.Barracks, "Area_progress");
        return CeAreaIncomeMath.Level(checked((ushort)progress));
    }
    private static ForceReference AdministrationForce(SourceWorldContext current, int id) => current.ReadForceTableReference(id <= 51 ? id : 0) ?? throw AdministrationMissing("Force_pointer");
    private static int AdministrationValue(AreaAdministrationQuery<int?> value) => value.Value ?? throw AdministrationMissing(value.UnresolvedInput ?? "numeric_quote");
    private static int AdministrationArmyParameter(GameState state, string key)
        => state.NativeArmySimulation?.Parameters is { } parameters && !parameters.Float32Values.ContainsKey(key) &&
            parameters.Int32Values.TryGetValue(key, out int value) ? value : throw AdministrationMissing(key);
    private static T AdministrationK<T>(T? value, string field) where T : struct => value ?? throw AdministrationMissing(field);
    private static InvalidDataException AdministrationMissing(string field) => new("normal.turn.area_administration." + field);
}
