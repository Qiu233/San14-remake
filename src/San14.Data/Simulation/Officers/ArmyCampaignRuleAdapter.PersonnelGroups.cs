using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPersonnelGroupEditExecution
{
    public AreaAdministrationQuery<PersonnelGroupEditDraft> OpenPersonnelGroupEditDraft(GameState state, int forceId)
    {
        try { return AreaAdministrationQuery<PersonnelGroupEditDraft>.Known(new(CreatePersonnelGroupPage(state, forceId))); }
        catch (InvalidDataException error) { return AreaAdministrationQuery<PersonnelGroupEditDraft>.Unknown(error.Message); }
    }
    public AreaAdministrationQuery<PersonnelGroupMoveDraft> OpenPersonnelGroupMoveDraft(GameState state, int forceId)
    {
        try { return AreaAdministrationQuery<PersonnelGroupMoveDraft>.Known(new(CreatePersonnelGroupPage(state, forceId))); }
        catch (InvalidDataException error) { return AreaAdministrationQuery<PersonnelGroupMoveDraft>.Unknown(error.Message); }
    }

    //The cache belongs to the actual application instance configured by the
    //composition. A query never seeds or publishes it from a saved World.
    internal void RequirePersonnelGroupApplicationCurrent()
    {
        if (applicationGameplay is null) throw PersonnelGroupMovementRules.Missing("application_destinations");
    }
    internal void PublishPersonnelGroupMoveDestinationsCurrent(IReadOnlyDictionary<int, int?> changes)
    {
        RequirePersonnelGroupApplicationCurrent();
        foreach (var entry in changes)
            if (entry.Key is < 0 or > 9 || entry.Value is < 1 or > 61)
                throw PersonnelGroupMovementRules.Missing("application_destination_shape");
        foreach (var entry in changes)
            applicationGameplay!.PersonnelGroupMoveDestinations.SetDestination(entry.Key, entry.Value);
    }

    private PersonnelGroupPageSource CreatePersonnelGroupPage(GameState state, int forceId)
    {
        RequirePersonnelGroupApplicationCurrent();
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        PersonReturnCommands.BindCurrent(state, current);
        var rules = CapturePersonnelGroupMovementRules(state, current, forceId);
        var cards = new AreaAdministrationOfficerCardSource(state, current, _definitions, _scenario,
            Seed.Language, _baseCapacityInputs, _repository);
        var cardCache = new Dictionary<int, AreaAdministrationQuery<AreaAdministrationOfficerCard>>();
        var people = new Dictionary<int, PersonnelGroupOfficer>();
        var physical = new Dictionary<int, int>();
        int K(int? value, string name) => value ?? throw PersonnelGroupMovementRules.Missing(name);
        var candidates = new List<int>();
        //234850 / CDistrictPersonIterator(param0): registration order,
        //actual primary District E0 and status1..4. Current selected members
        //are separately merged by73B3D0, including those no longer in it.
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots()
            ?? throw PersonnelGroupMovementRules.Missing("edit_Person_registry"))
        {
            int person = K(entry, "edit_Person_pointer");
            int district = current.ReadPersonDistrictSlot(person)
                ?? throw PersonnelGroupMovementRules.Missing("edit_PersonE0");
            if (district != rules.DistrictId) continue;
            byte status = current.ReadPersonStatus(person)
                ?? throw PersonnelGroupMovementRules.Missing("edit_PersonE6");
            if (status is < 1 or > 4) continue;
            int id = current.ReadPersonIdentity(person)
                ?? throw PersonnelGroupMovementRules.Missing("edit_Person10");
            candidates.Add(id); AddPerson(id, person);
        }
        var members = Enumerable.Range(0, 10).Select(group => rules.ReadGroupMembers(group).ToArray()).ToArray();
        foreach (int id in members.SelectMany(row => row).Distinct())
            AddPerson(id, current.ResolveOfficerSource(id) ?? throw PersonnelGroupMovementRules.Missing("member_pointer"));
        var locations = new List<PersonnelGroupDestination>();
        foreach (int id in rules.DestinationLocationIds)
        {
            string name; int? hex;
            if (id <= 51)
            { var city = state.Cities.GetValueOrDefault(id) ?? throw PersonnelGroupMovementRules.Missing("destination_City"); name = city.Name; hex = city.HexId; }
            else
            { var gate = state.Gates.GetValueOrDefault(id) ?? throw PersonnelGroupMovementRules.Missing("destination_Gate"); name = gate.Name; hex = gate.HexId; }
            locations.Add(new(id, name, hex));
        }
        var destinations = Enumerable.Range(0, 10).Select(group =>
            applicationGameplay!.PersonnelGroupMoveDestinations.GetDestination(group)).ToArray();
        //A cached destination may have changed allegiance since last use.
        //Its source name remains a real Base name even when not selectable.
        string LocationName(int id) => id <= 51
            ? state.Cities.GetValueOrDefault(id)?.Name ?? throw PersonnelGroupMovementRules.Missing("cached_City")
            : state.Gates.GetValueOrDefault(id)?.Name ?? throw PersonnelGroupMovementRules.Missing("cached_Gate");
        string Message(int root, int? location = null) => FormatReportMessage(state, current, root,
            new(Base0: location is int at ? checked((ushort)at) : null),
            ReportMessageOrigin.PersonnelAdministration, null).Text;
        return new(rules, members, candidates, people, physical, locations, destinations,
            ReadHistoryDataText(1, 0x73a), ReadHistoryDataText(1, 0x73e), ReadHistoryDataText(1, 0x73f),
            Message(0x5c0d), Message(0x5c0f), Message(0x5c0e), ReadHistoryDataText(1, 0x545), Message(0x578c),
            ReadHistoryDataText(1, 0x73d), ReadHistoryDataText(1, 0x551), ReadHistoryDataText(1, 0x353),
            LocationName, Message, ReadCard, CommandBookAvailability.Reserved(state, rules.DistrictId));

        void AddPerson(int id, int at)
        {
            if (people.ContainsKey(id)) return;
            var officer = state.Officers.GetValueOrDefault(id)
                ?? throw PersonnelGroupMovementRules.Missing("Person_presentation");
            people.Add(id, new(id, officer.Name, officer.CurrentNameSortKey));
            //69D790 reads Person10, independently of the lookup key.
            physical.Add(id, current.ReadPersonIdentity(at)
                ?? throw PersonnelGroupMovementRules.Missing("Person10_sort"));
        }
        AreaAdministrationQuery<AreaAdministrationOfficerCard> ReadCard(int id)
        {
            if (cardCache.TryGetValue(id, out var cached)) return cached;
            AreaAdministrationQuery<AreaAdministrationOfficerCard> result;
            try
            {
                if (!people.ContainsKey(id)) throw PersonnelGroupMovementRules.Missing("card_candidate");
                int at = current.ResolveOfficerSource(id) ?? throw PersonnelGroupMovementRules.Missing("card_pointer");
                result = AreaAdministrationQuery<AreaAdministrationOfficerCard>.Known(cards.ReadCity(state.PeriodNumber, at, true, 0));
            }
            catch (InvalidDataException error) { result = AreaAdministrationQuery<AreaAdministrationOfficerCard>.Unknown(error.Message); }
            return cardCache[id] = result;
        }
    }

    CommandResult IPersonnelGroupEditExecution.Edit(GameState state, ForceState force,
        SetPersonnelGroupsOrder order, CommandContext context)
    {
        try
        {
            if (state.Phase != TurnPhase.Strategy || context.DelegatedDistrictId is not null || force.Id != order.ForceId)
                return CommandResult.Reject("Personnel group editing requires direct strategy control.", "personnel.groups.edit_scope");
            var page = CreatePersonnelGroupPage(state, force.Id);
            if (order.Destinations is not { Count: 10 })
                throw PersonnelGroupMovementRules.Missing("edit_destination_snapshot");
            //Only primary-District candidates and previously saved members
            //can survive the selector. Editing does not create assignments.
            for (int group = 0; group < 10; group++)
            {
                var allowed = page.EditCandidates.Concat(page.Members[group]).ToHashSet();
                if (order.Groups.Groups[group].Any(id => !allowed.Contains(id)))
                    return CommandResult.Reject("A personnel group member is not a current editing candidate.", "personnel.groups.edit_candidate");
            }
            var accepted = state with { PersonnelGroups = order.Groups };
            PublishPersonnelGroupMoveDestinationsCurrent(Enumerable.Range(0, 10)
                .ToDictionary(group => group, group => order.Destinations[group]));
            return CommandResult.Accept(accepted);
        }
        catch (InvalidDataException error)
        { return CommandResult.RuleUnavailable("personnel.groups.edit_rules", error.Message); }
    }
}

internal sealed class PersonnelGroupPageSource(
    PersonnelGroupMovementRules rules, IReadOnlyList<IReadOnlyList<int>> members, IReadOnlyList<int> editCandidates,
    IReadOnlyDictionary<int, PersonnelGroupOfficer> people, IReadOnlyDictionary<int, int> physical,
    IReadOnlyList<PersonnelGroupDestination> destinations, IReadOnlyList<int?> destinationSnapshot,
    string editTitle, string moveTitle, string selectorTitle, string editGuide, string moveGuide,
    string selectorGuide, string destinationTitle, string destinationGuide,
    string groupFormat, string membersFormat, string emptyText, Func<int, string> locationName,
    Func<int, int?, string> message, Func<int, AreaAdministrationQuery<AreaAdministrationOfficerCard>> readCard,
    long reservedBooks)
{
    internal PersonnelGroupMovementRules Rules => rules;
    internal int ForceId => rules.ForceId;
    internal IReadOnlyList<IReadOnlyList<int>> Members => members;
    internal IReadOnlyList<int> EditCandidates => editCandidates;
    internal IReadOnlyList<int?> DestinationsSnapshot => destinationSnapshot;
    internal IReadOnlyList<PersonnelGroupDestination> Destinations => destinations;
    internal long ReservedBooks => reservedBooks;
    internal string EditTitle => editTitle;
    internal string MoveTitle => moveTitle;
    internal string SelectorTitle => selectorTitle;
    internal string EditGuide => editGuide;
    internal string MoveGuide => moveGuide;
    internal string SelectorGuide => selectorGuide;
    internal string DestinationTitle => destinationTitle;
    internal string DestinationGuide => destinationGuide;
    internal PersonnelGroupOfficer Officer(int id) => people[id];
    internal int Physical(int id) => physical[id];
    internal AreaAdministrationQuery<AreaAdministrationOfficerCard> QueryOfficerCard(int id) => readCard(id);
    internal IReadOnlyList<PersonnelGroupRow> Rows(IEnumerable<IEnumerable<int>> groups, IReadOnlyList<int?> targets, bool moving)
        => groups.Select((ids, group) =>
        {
            var rows = ids.Select(Officer).ToArray();
            string summary = rows.Length == 0 ? emptyText : rows.Length == 1 ? rows[0].Name :
                membersFormat.Replace("%s", rows[0].Name).Replace("%d", (rows.Length - 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
            return new PersonnelGroupRow(group, groupFormat.Replace("%d", (group + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                rows, summary, targets[group], rows.Length > 0 && targets[group] is int at ? locationName(at) : emptyText,
                !moving || rules.IsGroupSelectable(group));
        }).ToArray();
    internal AreaAdministrationQuery<string> AvailabilityWarning(IReadOnlyList<int> selected)
    {
        try { return AreaAdministrationQuery<string>.Known(selected.SelectMany(rules.ReadGroupMembers)
            .Any(id => !rules.CanMoveOfficer(id)) ? message(0x5c14, null) : ""); }
        catch (InvalidDataException error) { return AreaAdministrationQuery<string>.Unknown(error.Message); }
    }
    internal AreaAdministrationQuery<string> DestinationConfirmation(int location)
    {
        if (!rules.DestinationLocationIds.Contains(location)) return AreaAdministrationQuery<string>.Unknown("personnel.groups.destination_candidate");
        try { return AreaAdministrationQuery<string>.Known(message(0x5c16, location)); }
        catch (InvalidDataException error) { return AreaAdministrationQuery<string>.Unknown(error.Message); }
    }
}
