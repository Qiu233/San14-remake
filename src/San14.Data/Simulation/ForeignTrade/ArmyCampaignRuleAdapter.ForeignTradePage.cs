using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public ForeignTradeDraft CreateForeignTradeDraft(GameState state, int forceId, GameState? tutorialControl = null)
    {
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            _baseCapacityInputs.BindCurrent(state, current);
            var force = current.ReadForceTableReference(forceId) ?? throw ForeignTradeCommandMissing("page_Force");
            if (!ForeignTradeNeed(current.ReadForceNativeValidity(force), "page_Force_validity"))
                throw ForeignTradeCommandMissing("page_invalid_Force");
            int district = ForeignTradeNeed(ForcePrimaryDistrictSource.Resolve(current, force).PhysicalSlot, "page_District");
            var capital = new DiplomacyCommandSource(state, current).Capital(forceId);
            var treasury = BaseCurrencyAdditionSource.CurrencyCity(capital, current);
            var candidates = ReadForeignTradeExecutorsCurrent(state, current, forceId, district);
            var items = ReadForeignTradeItemsCurrent(state, current, forceId);
            var travel = new Dictionary<(int Person, int Country), int>();
            var baseDistances = new Dictionary<(int, int), int>();
            var abilities = SearchAbilities(current);
            GameState cardWorld = state;
            var cards = new AreaAdministrationOfficerCardSource(state, current, _definitions, _scenario,
                Seed.Language, _baseCapacityInputs, _repository);
            return new(forceId, _repository.LoadMessages(Seed.Language, MessageKind.Data)
                .Messages.Sections[1].Records[0x700].Text ?? throw ForeignTradeCommandMissing("page_title"), Read, AdoptPresentationWorld,
                (kind, draft) => PrepareForeignTradeSelectorCurrent(state, current, forceId, draft, kind), Card, Summary, lookup => cards.ReadTraitTooltip(lookup));

            ForeignTradePageSelection Read(int? country, IReadOnlyList<int> selected, int gold, int supplies, int troops, int? item)
            {
                var countries = new List<ForeignTradeCountryChoice>();
                var tasks = state.PersonnelCommands?.Caravans ?? throw ForeignTradeCommandMissing("page_caravan_owner");
                for (int id = 1; id < 5; id++)
                {
                    bool? eligible; string? missing = null;
                    try
                    {
                        eligible = PersonReturnPathSource.ReadForeignTradeEligibility(state, current, force, id,
                            ForeignTradeApplication(tutorialControl)) && !tasks.Values.Any(task =>
                                task.SourceForceId == forceId && task.CountryId == id && !task.IsRetiring);
                    }
                    catch (InvalidDataException error) { eligible = null; missing = error.Message; }
                    countries.Add(new(id, ReadForeignTradeCountry(id).Name, eligible, missing));
                }
                var people = new List<ForeignTradeExecutorChoice>();
                foreach (var candidate in candidates)
                {
                    int? days = null; string? missing = null;
                    if (country is int destination)
                        try
                        {
                            if (!travel.TryGetValue((candidate.Physical, destination), out int duration))
                                travel[(candidate.Physical, destination)] = duration = ReadForeignTradeTravelDaysCurrent(state, current,
                                    candidate.Physical, destination, baseDistances);
                            days = duration;
                        }
                        catch (InvalidDataException error) { missing = error.Message; }
                    people.Add(new(candidate.Id, candidate.Name, candidate.Location,
                        Ability(candidate.Id, 2), Ability(candidate.Id, 3), Ability(candidate.Id, 4), days, true, missing));
                }
                int goldStock = ForeignTradeNeed(current.ReadBaseResource(treasury, BaseResourceKind.Gold), "page_Gold");
                int foodStock = ForeignTradeNeed(current.ReadBaseResource(treasury, BaseResourceKind.Supplies), "page_Food");
                int troopStock = ForeignTradeNeed(current.ReadBaseResource(capital, BaseResourceKind.Troops), "page_Troops");
                int books = state.Districts.GetValueOrDefault(district)?.OrdersRemaining ?? throw ForeignTradeCommandMissing("page_District_books");
                books = checked((int)(books - CommandBookAvailability.Reserved(state, district)));
                int? daysMax = selected.Count == 0 ? null : selected.Select(id => people.FirstOrDefault(row => row.OfficerId == id)?.TravelDays)
                    .Aggregate((int?)0, (maximum, value) => maximum is int a && value is int b ? Math.Max(a, b) : null);
                bool complete = country is int chosen && countries.Any(row => row.Id == chosen && row.Enabled == true) &&
                    selected.Count is > 0 and <= 5 && selected.Distinct().Count() == selected.Count &&
                    selected.All(id => people.Any(row => row.OfficerId == id && row.Enabled == true && row.TravelDays is not null)) &&
                    gold >= 0 && supplies >= 0 && troops >= 0 && gold <= goldStock && supplies <= foodStock && troops <= troopStock &&
                    selected.Count <= books && (gold != 0 || supplies != 0 || troops != 0 || item is not null) &&
                    (item is null || items.Any(row => row.ItemId == item && row.Enabled == true));
                string? unresolved = country is int chosenCountry && countries.FirstOrDefault(row => row.Id == chosenCountry)?.UnresolvedOwner is string countryMissing
                    ? countryMissing : selected.Select(id => people.FirstOrDefault(row => row.OfficerId == id)?.UnresolvedOwner).FirstOrDefault(value => value is not null);
                return new(countries, people, items, goldStock, foodStock, troopStock, selected.Count, books, daysMax, complete, unresolved);
            }
            AreaAdministrationQuery<AreaAdministrationOfficerCard> Card(int id)
            {
                try
                {
                    if (!ReferenceEquals(cardWorld, state))
                    {
                        _baseCapacityInputs.BindCurrent(state, current);
                        cards = new AreaAdministrationOfficerCardSource(state, current, _definitions, _scenario,
                            Seed.Language, _baseCapacityInputs, _repository);
                        cardWorld = state;
                    }
                    int physical = id == 0
                        ? ForeignTradeNeed(current.ReadTransferPersonTableReference(0)?.PhysicalSlot, "card_empty_person")
                        : _definitions.ResolveOfficerSource(id) ?? throw ForeignTradeCommandMissing("card_Person");
                    return AreaAdministrationQuery<AreaAdministrationOfficerCard>.Known(cards.ReadCity(0, physical,
                        id != 0 && ForeignTradeNeed(current.ReadPersonNativeValidity(physical), "card_validity"), 17));
                }
                catch (InvalidDataException error) { return AreaAdministrationQuery<AreaAdministrationOfficerCard>.Unknown(error.Message); }
            }
            ForeignTradeSummary Summary(int? country)
            {
                if (country is null) return new("", "", null);
                try
                {
                    int points = ForeignTradeCurrentSource.ReadFriendship(state, forceId, country.Value);
                    var relations = state.ForeignCountryRelations?.Countries.GetValueOrDefault(country.Value)?.Relations
                        ?? throw ForeignTradeCommandMissing("summary_foreign_relations");
                    var rivals = Enumerable.Range(1, 4).Where(id => id != country &&
                        (relations.TryGetValue(id, out byte value) ? value : throw ForeignTradeCommandMissing("summary_foreign_relation")) == 2)
                        .Select(id => ReadForeignTradeCountry(id).Name);
                    return new(points.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(4), string.Join("?", rivals), null);
                }
                catch (InvalidDataException error) { return new(null, null, error.Message); }
            }
            int? Ability(int id, int dimension)
            {
                try { return abilities.ResolveDimension(state, id, dimension,
                    (owner, effect) => _baseCapacityInputs.ReadPanelModifier(state, owner, effect)); }
                catch (InvalidDataException) { return null; }
            }
        }
        catch (InvalidDataException error) { return new(forceId, error.Message, AdoptPresentationWorld); }

        bool AdoptPresentationWorld(GameState previous, GameState replacement)
        {
            if (ReferenceEquals(previous, replacement) || !ReferenceEquals(state, previous)) return false;
            if (ReferenceEquals(tutorialControl, previous)) tutorialControl = replacement;
            state = replacement;
            return true;
        }
    }

    private ApplicationGameplayContext? ForeignTradeApplication(GameState? tutorialControl = null)
    {
        var application = ReadApplicationGameplay();
        return application is not null && tutorialControl is not null ? application with
            { TutorialAllowsForeignTrade = TacticRemakeCurrentBattleControl.Read(tutorialControl, 55) } : application;
    }

    private sealed record ForeignTradeCandidate(int Id, int Physical, string Name, string Location);
    private IReadOnlyList<ForeignTradeCandidate> ReadForeignTradeExecutorsCurrent(GameState state, SourceWorldContext current,
        int forceId, int district)
    {
        var result = new List<ForeignTradeCandidate>();
        foreach (int person in NormalTurnServingPeople(current, forceId, district, false))
        {
            if (ForeignTradeNeed(OfficerHealthSourceAdapter.ReadByte(current, person), "executor_health") >= 2 ||
                (ForeignTradeNeed(current.ReadArmyReturnPersonFlags196(person), "executor_flags") & 0x4001) != 0 ||
                PersonCurrentBusySource.Read(current, person, ForeignTradeCommandMissing) ||
                ForeignTradeNeed(current.ReadPersonStatus(person), "executor_status") == 1 || DuelAftermathSpecialPerson(current, person)) continue;
            int id = ForeignTradeNeed(current.ReadPersonIdentity(person), "executor_identity");
            var officer = state.Officers.GetValueOrDefault(id) ?? throw ForeignTradeCommandMissing("executor_owner");
            int home = ForeignTradeNeed(current.ReadPersonFirstLocation(person), "executor_home");
            result.Add(new(id, person, officer.Name, state.Cities.GetValueOrDefault(home)?.Name ??
                state.Gates.GetValueOrDefault(home)?.Name ?? ""));
        }
        return result;
    }

    private IReadOnlyList<ForeignTradeItemChoice> ReadForeignTradeItemsCurrent(GameState state, SourceWorldContext current, int force)
    {
        var result = new List<ForeignTradeItemChoice>();
        int ruler = TalentRulerCurrent(current, current.ReadForceTableReference(force) ?? throw ForeignTradeCommandMissing("item_Force"));
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Item) ?? throw ForeignTradeCommandMissing("item_registry"))
        {
            int id = ForeignTradeNeed(entry?.PhysicalSlot, "item_pointer");
            if (!ForeignTradeNeed(current.ReadRegistryObjectNativeValidity(entry!), "item_validity") ||
                ForeignTradeNeed(current.ReadCaptureItemOwner4A(id), "item_owner") != ruler ||
                ForeignTradeNeed(current.ReadRegistryItemRawField50(entry!), "item_status") != 1) continue;
            var item = state.Items?.GetValueOrDefault(id) ?? throw ForeignTradeCommandMissing("item_saved_owner");
            if ((ForeignTradeNeed(item.Lifecycle?.RetainedFlags, "item_flags") & 1) == 0) continue;
            result.Add(new(id, state.ItemDefinitions.GetValueOrDefault(item.DefinitionId)?.Name
                ?? throw ForeignTradeCommandMissing("item_name"), true, null));
        }
        return result;
    }
}
