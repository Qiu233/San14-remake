using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private IReadOnlyList<ForeignTradeCountryDefinition>? foreignTradeCountries;
    internal ForeignTradeCountryDefinition ReadForeignTradeCountry(int country)
    {
        if (country is < 0 or > 4) throw ForeignTradeCommandMissing("country_index");
        return (foreignTradeCountries ??= _repository.LoadForeignTradeCountries(
            _scenario.FixedDataSource.SourcePath))[country];
    }
    internal ForeignTradeCountryDefinition ReadForeignTradeCountry(GameState state, int country) =>
        ReadForeignTradeCountry(country) with { ReturnDays = country == 0 ? 0 :
            ForeignTradeParameter(state, $"return-gold-{country}-cost") };

    //231720: country duration + actual Base-to-Base journey, then the actor's
    //trait114, land advantage5, and current ordinary/advanced settings.
    internal int ReadForeignTradeTravelDaysCurrent(GameState state, SourceWorldContext current,
        int person, int country, Dictionary<(int, int), int>? baseDistances = null)
    {
        var definition = ReadForeignTradeCountry(country);
        int home = ForeignTradeNeed(current.ReadPersonFirstLocation(person), "travel_PersonE2");
        var pair = (Math.Min(home, definition.HostCityId), Math.Max(home, definition.HostCityId));
        if (baseDistances is null || !baseDistances.TryGetValue(pair, out int distance))
        {
            distance = ReadForeignTradeBaseDistanceCurrent(state, current, home, definition.HostCityId);
            if (baseDistances is not null) baseDistances[pair] = distance;
        }
        int days = ForeignTradeParameter(state, $"return-gold-{country}-cost") + distance;
        if (ForeignTradeNeed(TacticRemakeCurrentTraits.CommanderHasEffect(current, person, 114), "travel_trait114_presence"))
            days -= ForeignTradeNeed(TacticRemakeCurrentTraits.CommanderEffectSum(current, person, 114), "travel_trait114_value");
        var force = current.ReadPersonForce(person) ?? throw ForeignTradeCommandMissing("travel_Force");
        int advantage = PersonReturnPathSource.ReadForceAdvantage(state, current, force, 5).Value
            ?? throw ForeignTradeCommandMissing("travel_advantage5");
        if (advantage != 0) days -= ForeignTradeParameter(state, "return-state-advantage-reduction");
        int selector = ForeignTradeNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "travel_settings_selector");
        if (selector != 0)
        {
            int mode = ForeignTradeNeed(BattleRemakeCurrentSettings.ReadInt32(state, 0xFC), "travel_settings_FC");
            if (mode == 0) days -= ForeignTradeParameter(state, "return-low-difficulty-reduction");
        }
        else
        {
            int percent = ForeignTradeNeed(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 13), "travel_advanced13");
            days = unchecked(percent * days) / 100;
        }
        return days;
    }

    private static int ReadForeignTradeBaseDistanceCurrent(GameState state, SourceWorldContext current, int first, int second)
    {
        if (first == second || first is < 1 or > 61 || second is < 1 or > 61) return 0;
        int lower = Math.Min(first, second), upper = Math.Max(first, second);
        var source = BaseLookup.Find(lower, current); var target = BaseLookup.Find(upper, current);
        if (!ForeignTradeNeed(current.ReadBaseNativeValidity(source), "travel_lower_Base_validity") ||
            !ForeignTradeNeed(current.ReadBaseNativeValidity(target), "travel_upper_Base_validity")) return 0;
        var force = current.ReadForceTableReference(ForeignTradeNeed(current.ReadBaseForceSlot(source), "travel_lower_Base_Force"))
            ?? throw ForeignTradeCommandMissing("travel_lower_Force_pointer");
        var start = current.ReadHexTableReference(current.ReadBaseCurrentHexIndex(source))
            ?? throw ForeignTradeCommandMissing("travel_start_Hex");
        var end = current.ReadHexTableReference(current.ReadBaseCurrentHexIndex(target))
            ?? throw ForeignTradeCommandMissing("travel_end_Hex");
        var list = current.CreateTemporaryHexList() ?? throw ForeignTradeCommandMissing("travel_list");
        try
        {
            var path = PersonReturnPathSource.Fill(state, current, start, end, force, list);
            return path.Days ?? throw ForeignTradeCommandMissing(path.UnresolvedOwner ?? "travel_path");
        }
        finally { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
    }

    internal int ReadForeignTradeTaskRemainingDaysCurrent(GameState state, int officerId)
    {
        var owner = state.PersonnelCommands ?? throw ForeignTradeCommandMissing("remaining_command_owner");
        var caravans = owner.Caravans ?? throw ForeignTradeCommandMissing("remaining_caravan_owner");
        var task = owner.DispatchOrder().Where(entry => entry.Kind == PersonnelCommandKind.Caravan)
            .Select(entry => caravans[entry.Id]).FirstOrDefault(task => task.MemberIds.Contains(officerId))
            ?? throw ForeignTradeCommandMissing("remaining_associated_task");
        var date = state.NativeMapSeason?.ThirdDate ?? throw ForeignTradeCommandMissing("remaining_date");
        int total = unchecked(PersonReturnCommands.CalendarOrdinal(task.DueDate) - PersonReturnCommands.CalendarOrdinal(task.StartDate));
        int elapsed = unchecked(PersonReturnCommands.CalendarOrdinal(date) - PersonReturnCommands.CalendarOrdinal(task.StartDate));
        return Math.Max(0, unchecked(total - elapsed));
    }

    private static T ForeignTradeNeed<T>(T? value, string name) where T : struct =>
        value ?? throw ForeignTradeCommandMissing(name);
    private static InvalidDataException ForeignTradeCommandMissing(string name) => new("foreign-trade.command." + name);
}
