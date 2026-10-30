using San14.Core;

namespace San14.Data;

public sealed record IncomeReportNumericTotals(int Gold, int GoldIncome, int GoldExpense,
    int Food, int FoodIncome, int FoodExpense, int Soldiers, int SoldierChange,
    int ActionPoints, int ActionPointIncome, bool ShowActionPointIncome, bool HighlightGoldExpense);
public sealed record IncomeReportDistrictAdvice(string Text, DeploymentNarrationPortraitRoute? Portrait);

/// <summary>One report entry's actual current world and registry ownership.
/// Drawing its rows does not prepare messages or draw application randoms.</summary>
public sealed class CampaignIncomeReportPublication
{
    public SourceWorldContext Current { get; }
    public ForceReference Force { get; }
    public bool HasDistrictResourceHistory => World.PlayerDistrictResourceHistory is not null;
    internal GameState World { get; }
    internal GameState TutorialControl { get; }
    internal CampaignIncomeReportPublication(GameState world, GameState tutorialControl,
        SourceWorldContext current, ForceReference force)
    { World = world; TutorialControl = tutorialControl; Current = current; Force = force; }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    public CampaignIncomeReportPublication ReadIncomeReport(GameState state, GameState tutorialControl)
    {
        var current = CaptureReportContentCurrent(state);
        BaseReturnCaptivesInputs.BindCurrent(state, current);
        var force = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw IncomeMissing("player_Force");
        return new(state, tutorialControl, current, force);
    }

    /// <summary>67F970: current District foothold aggregation, independently
    /// from predicted monthly income and the provisional settlement UI.</summary>
    public IncomeReportNumericTotals ReadIncomeReportNumbers(CampaignIncomeReportPublication report,
        DistrictReference district)
    {
        var state = report.World; var current = report.Current;
        int gold = 0, food = 0, soldiers = 0, goldIncome = 0, foodIncome = 0, recruited = 0,
            goldExpense = 0, foodExpense = 0;
        foreach (var target in IncomeDistrictBases(current, district))
        {
            soldiers = unchecked(soldiers + IncomeNeed(current.ReadBaseResource(target, BaseResourceKind.Troops), "Base3C"));
            if (IncomeNeed(current.ReadBaseObjectLink(target), "Base4E") >= 52) continue;
            gold = unchecked(gold + IncomeNeed(current.ReadBaseResource(target, BaseResourceKind.Gold), "Base34"));
            food = unchecked(food + IncomeNeed(current.ReadBaseResource(target, BaseResourceKind.Supplies), "Base38"));
            int cityId = IncomeNeed(target.PhysicalSlot, "City_pointer");
            var city = target.Kind == BaseKind.City ? state.Cities.GetValueOrDefault(cityId) : null;
            var earned = city?.TurnIncomeTotals ?? throw IncomeMissing("City_D0_D4_D8");
            //The normal 2BC080 writers own these exact signed D0/D4/D8
            //accumulators. Never reconstruct them from inventory differences.
            goldIncome = unchecked(goldIncome + earned.Gold);
            foodIncome = unchecked(foodIncome + earned.Food);
            recruited = unchecked(recruited + earned.RecruitedTroops);
            goldExpense = unchecked(goldExpense + IncomeNeed(current.ReadBaseReturnField50(target), "Base50"));
            foodExpense = unchecked(foodExpense + IncomeNeed(city?.PeriodFoodExpenditure, "Base54"));
        }
        int physical = IncomeNeed(district.PhysicalSlot, "District_pointer");
        int points = DistrictInt32(current, physical, 0x24), pointIncome = 0;
        bool rulerDistrict = IncomeRulerDistrict(report, district);
        if (rulerDistrict)
        {
            int raw = IncomeNeed(current.ReadDistrictForceId(physical), "District10");
            int force = raw < 52 ? raw : 0;
            var owner = state.NativeArmySimulation?.Forces.GetValueOrDefault(force)
                ?? throw IncomeMissing("Force_runtime");
            goldExpense = unchecked(goldExpense + IncomeNeed(owner.AssociationGoldExpense, "Force1AC"));
            pointIncome = IncomeNeed(owner.AssociationIntelligenceGain, "Force1A8");
        }
        bool showPointIncome = rulerDistrict &&
            IncomeNeed(TacticRemakeCurrentBattleControl.ReadSelectedLessonIndex(report.TutorialControl),
                "tutorial_selected_lesson") == -1;
        int day = IncomeNeed(state.NativeMapSeason?.ThirdDate.Day, "World37_day");
        bool highlight = false;
        if (day < 11)
        {
            var advantage = PersonReturnPathSource.ReadForceAdvantage(state, current, report.Force, 12);
            highlight = IncomeNeed(advantage.Value, advantage.UnresolvedOwner ?? "player_land_advantage12") != 0;
        }
        return new(gold, goldIncome, goldExpense, food, foodIncome, foodExpense, soldiers,
            recruited, points, pointIncome, showPointIncome, highlight);
    }

    /// <summary>6823E0, called once on an actual district selection. The
    /// caller supplies the existing application random owner.</summary>
    public IncomeReportDistrictAdvice PrepareIncomeReportAdvice(CampaignIncomeReportPublication report,
        DistrictReference district, Func<int, int> drawApp32)
    {
        var state = report.World; var current = report.Current;
        int physical = IncomeNeed(district.PhysicalSlot, "advice_District_pointer");
        var lookup = current.ReadPersonLookupReference(IncomeNeed(current.ReadDistrictLeader(physical), "District12"))
            ?? throw IncomeMissing("leader_lookup");
        int leader = IncomeNeed(lookup.PhysicalSlot, "leader_pointer");
        bool ruler = IncomeNeed(current.ReadPersonNativeValidity(leader), "leader_validity") &&
            IncomeNeed(current.ReadPersonStatus(leader), "leader_E6") == 1;
        int speaker = leader;
        if (ruler)
        {
            var pool = current.ReadOwnedCurrentListPool() ?? throw IncomeMissing("current_pool");
            var selection = new PersonDeathHistorySelection(state, current, pool,
                new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
                    SimulationRuleIdentity.ForLocale(Seed.Language)), _governorInputs,
                (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect));
            speaker = IncomeNeed(selection.Select(district).PhysicalSlot, "advice_speaker");
        }
        ushort person0 = IncomeNeed(current.ReadPersonIdentity(speaker), "speaker10");
        var portraitLookup = current.ReadPersonLookupReference(person0)
            ?? throw IncomeMissing("portrait_lookup");
        bool visible = portraitLookup.PhysicalSlot is int portraitPerson &&
            IncomeNeed(current.ReadPersonNativeValidity(portraitPerson), "portrait_validity");
        DeploymentNarrationPortraitRoute? portrait = null;
        if (visible)
            portrait = ReadNarrationPortrait(state, current, _definitions, person0,
                IncomeNeed(current.ReadPersonPortraitAppearance(portraitLookup.PhysicalSlot!.Value), "portrait_A6"));
        int rawForce = IncomeNeed(current.ReadDistrictForceId(physical), "advice_District10");
        var forceReference = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0)
            ?? throw IncomeMissing("advice_Force_pointer");
        ushort person1 = IncomeNeed(current.ReadMoraleWaveRuler22EAC0(forceReference), "advice_ruler");
        int root = 0x4FF2; ushort? city0 = null; int[]? integers = null;
        if (IncomeNeed(state.NativeMapSeason?.ThirdDate.Day, "World37_day") == 1)
        {
            var shortages = new List<BaseReference>();
            //224300 is the fixed City pointer iterator, not a dictionary scan.
            for (int slot = 0; slot < 52; slot++)
            {
                var city = current.ReadCityTableReference(slot) ?? throw IncomeMissing("City_table");
                if (physical is < 1 or > 51 ||
                    IncomeNeed(current.ReadCityDistrictReference(city)?.PhysicalSlot, "City_District") != physical) continue;
                int id = IncomeNeed(city.PhysicalSlot, "shortage_City_pointer");
                var owner = state.Cities.GetValueOrDefault(id) ?? throw IncomeMissing("shortage_City_owner");
                var totals = owner.TurnIncomeTotals ?? throw IncomeMissing("shortage_D0_D4");
                if (totals.Gold < IncomeNeed(current.ReadBaseReturnField50(city), "shortage_Base50") ||
                    totals.Food < IncomeNeed(owner.PeriodFoodExpenditure, "shortage_Base54")) shortages.Add(city);
            }
            if (shortages.Count == 1)
            { root = 0x4FF9; city0 = IncomeNeed(current.ReadBaseObjectLink(shortages[0]), "shortage_Base4E"); }
            else if (shortages.Count > 1) { root = 0x5000; integers = [shortages.Count]; }
        }
        var operands = new HistoryMessageOperands(Person0: person0, Person1: person1, Base0: city0, Integers: integers);
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language));
        var people = new DeploymentNarrationCurrentPeople(state, current, abilities, person0, person1,
            panelReader: (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect),
            appearanceRepository: _repository, appearanceScenario: _scenario, appearanceLanguage: Seed.Language);
        var context = new ReportCurrent(this, () => state, current, people, operands);
        var vm = new NarrationResourceMessages(_repository.LoadMessages(Seed.Language,
            San14.Data.Formats.MessageKind.Game).Messages);
        return new(vm.ResolveIncomeReportAdvice(context, drawApp32, root), portrait);
    }

    private static IEnumerable<BaseReference> IncomeDistrictBases(SourceWorldContext current, DistrictReference district)
    {
        int key = IncomeNeed(district.PhysicalSlot, "District_iterator");
        foreach (var item in current.ReadRegisteredBases() ?? throw IncomeMissing("Base80_registry"))
        {
            if (key is < 1 or > 51) continue;
            var target = item ?? throw IncomeMissing("Base80_pointer");
            if (IncomeNeed(current.ReadBaseDistrictReference(target)?.PhysicalSlot, "Base_District") == key) yield return target;
        }
    }

    private static bool IncomeRulerDistrict(CampaignIncomeReportPublication report, DistrictReference district)
    {
        var current = report.Current;
        if (!IncomeNeed(current.ReadForceNativeValidity(report.Force), "player_validity")) return false;
        int physical = IncomeNeed(district.PhysicalSlot, "ruler_District_pointer");
        if (IncomeNeed(current.ReadDistrictForceId(physical), "ruler_District10") !=
            IncomeNeed(current.ReadForcePhysicalSlot(report.Force), "player_ordinal")) return false;
        var leader = current.ReadPersonLookupReference(IncomeNeed(current.ReadDistrictLeader(physical), "ruler_District12"))
            ?? throw IncomeMissing("ruler_lookup");
        return leader.PhysicalSlot is int at && IncomeNeed(current.ReadPersonNativeValidity(at), "ruler_validity") &&
            IncomeNeed(current.ReadPersonStatus(at), "ruler_E6") == 1;
    }

    private static int DistrictInt32(SourceWorldContext current, int physical, int offset)
    {
        uint value = 0;
        for (int i = 0; i < 4; i++) value |= (uint)IncomeNeed(current.ReadDistrictRawByte(physical, offset + i), "District24") << (i * 8);
        return unchecked((int)value);
    }
    private static T IncomeNeed<T>(T? value, string field) where T : struct => value ?? throw IncomeMissing(field);
    private static InvalidDataException IncomeMissing(string field) => new("income-report.current." + field);
}
