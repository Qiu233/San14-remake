using San14.Core;

namespace San14.Data;

internal sealed partial class ProposalLifecycleSource
{
    internal int ReadProposalCityFoodExpense(BaseReference city)
    {
        var quote = Package.ReadCityCaptureFoodQuoteCurrent(State, Current, city);
        int value = quote.Value ?? throw Missing(quote.UnresolvedOwner ?? "City_food_quote");
        State = quote.State ?? State;
        return value;
    }

    internal bool IsProposalCityResourceNeeded(BaseReference city, bool gold)
    {
        int foodExpense = ReadProposalCityFoodExpense(city);
        if (gold)
        {
            int expense = Package.ReadProposalCityGoldExpense(State, Current, city);
            int stock = K(Current.ReadBaseResource(city, BaseResourceKind.Gold), "levy_City_gold");
            if (stock <= unchecked(expense * Rule("exploit-expense-multiplier", 3))) return true;
            var predicted = Package.ReadProposalCityIncomeQuote(State, Current, city, AdvanceQuoteDate());
            return predicted.Gold < unchecked(expense * Rule("exploit-predicted-percent", 110)) / 100;
        }
        else
        {
            int stock = K(Current.ReadBaseResource(city, BaseResourceKind.Supplies), "levy_City_food");
            if (stock <= unchecked(foodExpense * Rule("requisition-expense-multiplier", 3))) return true;
            var predicted = Package.ReadProposalCityIncomeQuote(State, Current, city, AdvanceQuoteDate());
            if (predicted.Food >= foodExpense) return false;
            return unchecked(foodExpense * Rule("requisition-future-expense-scale", 24)) / 10 - predicted.Food >
                unchecked(stock * Rule("requisition-stock-percent-needed", 30)) / 100;
        }
    }

    private NativeMapSeasonDate AdvanceQuoteDate()
    {
        var date = State.NativeMapSeason?.ThirdDate ?? throw Missing("prediction_date");
        int hour = date.Hour + 10, day = date.Day, month = date.Month, year = date.Year;
        //31D250 advances the native calendar in hours. The quote does not
        //advance the actual campaign date or consume a simulation tick.
        if (hour >= 24) { hour -= 24; day++; }
        if (day > 30) { day = 1; month++; }
        if (month > 12) { month = 1; year++; }
        return new(checked((short)year), checked((sbyte)month), checked((sbyte)day), checked((sbyte)hour));
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal int ReadProposalCityGoldExpense(GameState state, SourceWorldContext current, BaseReference city,
        bool includeMonthlyPayrollAndRecruitment = true)
    {
        if (city.Kind != BaseKind.City ||
            ProposalLifecycleSource.K(current.ReadBaseObjectLink(city), "expense_Base_link") > 51) return 0;
        int raw = ProposalLifecycleSource.K(current.ReadBaseForceSlot(city), "expense_Force");
        var force = current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw ProposalLifecycleSource.Missing("expense_Force_pointer");
        if (!ProposalLifecycleSource.K(current.ReadForceNativeValidity(force), "expense_Force_validity")) return 0;
        var date = state.NativeMapSeason?.ThirdDate ?? throw ProposalLifecycleSource.Missing("expense_date");
        var areas = current.ReadRegisteredAreaReferences() ?? throw ProposalLifecycleSource.Missing("expense_Area_registry");
        var preview = new AreaAdministrationPreviewSource(state, current, _scenario, Seed.Language, _baseCapacityInputs, true);
        //2DC710 selects its payroll/recruitment portion explicitly. Ordinary
        //hiring callers retain their actual calendar gate through the null flag.
        int value = ReadHireCityGoldExpense(state, current, city, date, areas, preview,
            includeMonthlyPayrollAndRecruitment);
        //The hiring force aggregate adds its independent34CC40 capital
        //association bill. That bill is outside2DC710's City quote.
        int id = ProposalLifecycleSource.K(force.PhysicalSlot, "expense_Force_receiver");
        int ruler = ProposalLifecycleSource.K(current.ReadForceRecord(id)?.RawRuler10, "expense_ruler");
        int person = current.ResolveOfficerSource(ruler) ?? throw ProposalLifecycleSource.Missing("expense_ruler_pointer");
        if (ProposalLifecycleSource.K(current.ReadPersonFirstLocation(person), "expense_ruler_home") ==
            ProposalLifecycleSource.K(current.ReadBaseObjectLink(city), "expense_City_link"))
            value = unchecked(value - ReadHireCapitalAssociationGoldExpenseCurrent(state, current, force));
        return value;
    }

    internal (int Gold, int Food) ReadProposalCityIncomeQuote(GameState state, SourceWorldContext current,
        BaseReference city, NativeMapSeasonDate date)
    {
        if (ProposalLifecycleSource.K(current.ReadBaseObjectLink(city), "prediction_City_link") >= 52) return (0, 0);
        int owner = ProposalLifecycleSource.K(current.ReadBaseForceSlot(city), "prediction_Force");
        var force = current.ReadForceTableReference(owner <= 51 ? owner : 0) ?? throw ProposalLifecycleSource.Missing("prediction_Force_pointer");
        if (!ProposalLifecycleSource.K(current.ReadForceNativeValidity(force), "prediction_Force_validity")) return (0, 0);
        var preview = new AreaAdministrationPreviewSource(state, current, _scenario, Seed.Language, _baseCapacityInputs, true);
        var abilities = CreateArmyDestructionAbilities(current);
        int gold = 0, food = 0;
        //2BC080 quote1 computes the requested monthly Area scalars and the
        //landmark multiplier. Resident extras and resource posting are mode0.
        foreach (var item in current.ReadRegisteredAreaReferences() ?? throw ProposalLifecycleSource.Missing("prediction_Area_registry"))
        {
            var area = item ?? throw ProposalLifecycleSource.Missing("prediction_Area_pointer");
            if (ProposalLifecycleSource.K(current.ReadAreaCitySlot(area), "prediction_Area_city") != city.PhysicalSlot ||
                ProposalLifecycleSource.K(current.ReadAreaCurrentForceSlot(area), "prediction_Area_force") != owner) continue;
            int id = ProposalLifecycleSource.K(area.PhysicalSlot, "prediction_Area_receiver");
            var goldQuote = preview.Gold(id, physical => ProposalLifecycleSource.K(
                current.ReadAssignmentPersonAreaVector(physical)?.ActiveCount1E8, "prediction_Person_tasks"));
            if (!goldQuote.Available) throw ProposalLifecycleSource.Missing(goldQuote.UnresolvedInput ?? "prediction_Area_gold");
            gold = unchecked(gold + ProposalLifecycleSource.K(goldQuote.Value, "prediction_Area_gold_value"));
            food = unchecked(food + CityFoodIncomeSource.ReadArea(state, current, area, city, force, date.Month, abilities));
        }
        if (owner != 51 && owner is not (>= 46 and <= 50))
        {
            int goldPercent = ReadHireGoldLandmarkPercent(state, current, city);
            int foodPercent = ReadHireGoldLandmarkPercent(state, current, city, 2);
            if (goldPercent > 0 && gold > 0) gold = HireEconomyTruncate(goldPercent / 100f * (float)gold + (float)gold);
            if (foodPercent > 0 && food > 0) food = HireEconomyTruncate(foodPercent / 100f * (float)food + (float)food);
        }
        return (gold, food);
    }

    internal GameState ApplyProposalCityFoodWarning(GameState state, SourceWorldContext current, BaseReference city)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw ProposalLifecycleSource.Missing("warning_player");
        int playerId = ProposalLifecycleSource.K(player.PhysicalSlot, "warning_player_receiver");
        if (playerId == 0) return state;
        int ownerId = ProposalLifecycleSource.K(current.ReadBaseForceSlot(city), "warning_City_force");
        if (ownerId != playerId || !ProposalLifecycleSource.K(current.ReadForceNativeValidity(player), "warning_player_validity")) return state;
        current.CurrentPlayerForce = player;
        int food = ProposalLifecycleSource.K(current.ReadBaseResource(city, BaseResourceKind.Supplies), "warning_City_food");
        var quote = ReadCityCaptureFoodQuoteCurrent(state, current, city);
        int expense = quote.Value ?? throw ProposalLifecycleSource.Missing(quote.UnresolvedOwner ?? "warning_expense");
        state = quote.State ?? state;
        bool warning = false;
        if (unchecked(food - unchecked(expense * 3)) < 1)
        {
            var prediction = CityCaptureFoodWarningSource.TryReadPredictedFoodIncome(state, current, city,
                abilities: CreateArmyDestructionAbilities(current), sceneries: _landmarkDefinitions);
            int income = prediction.Value ?? throw ProposalLifecycleSource.Missing(prediction.UnresolvedOwner ?? "warning_income");
            warning = unchecked(food + income - unchecked(expense * 3)) < 1;
        }
        int id = ProposalLifecycleSource.K(city.PhysicalSlot, "warning_City_receiver");
        var owner = state.NativeBaseCaptureCurrent ?? throw ProposalLifecycleSource.Missing("warning_C9_owner");
        var before = owner.Cities.GetValueOrDefault(id) ?? throw ProposalLifecycleSource.Missing("warning_City_owner");
        byte bits = ProposalLifecycleSource.K(before.C9, "warning_C9");
        byte after = warning ? (byte)(bits | 2) : (byte)(bits & 0xFD);
        current.SetCaptureCityByteC9(id, after);
        return state with { NativeBaseCaptureCurrent = owner with
            { Cities = new(owner.Cities) { [id] = before with { C9 = after } } } };
    }
}
