using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed record PersonnelGroupOfficer(int OfficerId, string Name, string? NameSortKey);
public sealed record PersonnelGroupRow(int GroupIndex, string Caption,
    IReadOnlyList<PersonnelGroupOfficer> Members, string MemberCaption,
    int? DestinationLocationId, string DestinationName, bool Selectable);
public sealed record PersonnelGroupDestination(int LocationId, string Name, int? HexId);

/// <summary>Editing Return commits the ten working groups. Cancelling the
/// member selector simply does not call ReplaceMembers.</summary>
public sealed class PersonnelGroupEditDraft : IDisposable
{
    private readonly PersonnelGroupPageSource source;
    private readonly List<int>[] groups;
    private readonly int?[] destinations;
    private bool closed;
    internal PersonnelGroupEditDraft(PersonnelGroupPageSource source)
    {
        this.source = source; groups = source.Members.Select(row => row.ToList()).ToArray();
        destinations = source.DestinationsSnapshot.ToArray(); Refresh();
    }
    public int ForceId => source.ForceId;
    public string Title => source.EditTitle;
    public string SelectorTitle => source.SelectorTitle;
    public string SelectorGuide => source.SelectorGuide;
    public string Guide => source.EditGuide;
    public IReadOnlyList<PersonnelGroupRow> Rows { get; private set; } = [];
    public AreaAdministrationQuery<IReadOnlyList<PersonnelGroupOfficer>> QueryCandidates(int groupIndex)
    {
        if (closed || groupIndex is < 0 or > 9)
            return AreaAdministrationQuery<IReadOnlyList<PersonnelGroupOfficer>>.Unknown("personnel.groups.edit_selection");
        return AreaAdministrationQuery<IReadOnlyList<PersonnelGroupOfficer>>.Known(
            source.EditCandidates.Concat(groups[groupIndex]).Distinct().Select(source.Officer).ToArray());
    }
    public AreaAdministrationDraftResult ReplaceMembers(int groupIndex, IReadOnlyList<int> ids)
    {
        var candidates = QueryCandidates(groupIndex);
        if (!candidates.Available || ids.Count > 50 || ids.Distinct().Count() != ids.Count ||
            ids.Any(id => candidates.Value!.All(row => row.OfficerId != id)))
            return new(false, candidates.UnresolvedInput ?? "personnel.groups.edit_members");
        var ordered = ids.OrderBy(source.Physical).ToList();
        bool changed = !groups[groupIndex].SequenceEqual(ordered);
        groups[groupIndex] = ordered;
        //775900 removes selected identities from the other groups, without
        //clearing those groups' independent destination words.
        var selected = ordered.ToHashSet();
        for (int group = 0; group < 10; group++)
            if (group != groupIndex) groups[group].RemoveAll(selected.Contains);
        if (changed) destinations[groupIndex] = null;
        Refresh(); return new(true);
    }
    public AreaAdministrationQuery<AreaAdministrationOfficerCard> QueryOfficerCard(int officerId)
        => source.QueryOfficerCard(officerId);
    public AreaAdministrationQuery<PersonnelGroupPersonPage> QueryPersonPage(int groupIndex, int pageIndex)
    {
        var candidates = QueryCandidates(groupIndex);
        if (!candidates.Available) return AreaAdministrationQuery<PersonnelGroupPersonPage>.Unknown(candidates.UnresolvedInput!);
        var membership = new Dictionary<int, int>();
        for (int group = 0; group < 10; group++) foreach (int id in groups[group]) membership[id] = group + 1;
        return source.QueryPersonPage(candidates.Value!.Select(row => row.OfficerId).ToArray(), pageIndex, membership);
    }
    public AreaAdministrationQuery<SetPersonnelGroupsOrder> BuildOrder()
        => closed ? AreaAdministrationQuery<SetPersonnelGroupsOrder>.Unknown("personnel.groups.closed")
        : AreaAdministrationQuery<SetPersonnelGroupsOrder>.Known(new(ForceId,
            new(groups.Select(row => row.ToImmutableArray()).ToImmutableArray()))
            { Destinations = destinations.ToArray() });
    private void Refresh() => Rows = source.Rows(groups, destinations, false);
    public void Dispose() => closed = true;
}

/// <summary>Only the current selection is sent to71BE00. The application
/// cache is published by successful execution, never by selection or queries.</summary>
public sealed class PersonnelGroupMoveDraft : IDisposable
{
    private readonly PersonnelGroupPageSource source;
    private readonly int?[] destinations;
    private int[] selected = [];
    private bool closed;
    internal PersonnelGroupMoveDraft(PersonnelGroupPageSource source)
    { this.source = source; destinations = source.DestinationsSnapshot.ToArray(); Refresh(); }
    public int ForceId => source.ForceId;
    public string Title => source.MoveTitle;
    public string Guide => source.MoveGuide;
    public string DestinationTitle => source.DestinationTitle;
    public string DestinationGuide => source.DestinationGuide;
    public IReadOnlyList<PersonnelGroupRow> Rows { get; private set; } = [];
    public IReadOnlyList<PersonnelGroupDestination> Destinations => source.Destinations;
    public IReadOnlyList<int> SelectedGroupIndices => selected;
    public int? PreselectedLocationId => QueryPreselectedLocation().Value;
    public AreaAdministrationQuery<int?> QueryPreselectedLocation() => source.PreselectedDestination(selected);
    public int BooksBefore => source.Rules.AvailableBooks;
    public int BookCost => source.Rules.BookCost;
    public int BooksAfter => Math.Clamp(BooksBefore - BookCost, 0, 200);
    public bool CanSelectDestination => !closed && selected.Length > 0 && Destinations.Count > 0;
    public bool CanConfirm => !closed && selected.Length > 0 && BooksBefore - source.ReservedBooks >= BookCost &&
        selected.All(group => destinations[group] is int destination && source.Rules.DestinationLocationIds.Contains(destination));
    public AreaAdministrationDraftResult SelectGroups(IReadOnlyList<int> groups)
    {
        if (closed || groups.Count > 10 || groups.Distinct().Count() != groups.Count ||
            groups.Any(group => group is < 0 or > 9 || !Rows[group].Selectable))
            return new(false, "personnel.groups.move_selection");
        selected = groups.OrderBy(group => group).ToArray(); return new(true);
    }
    public AreaAdministrationQuery<string> QueryAvailabilityWarning()
        => source.AvailabilityWarning(selected);
    public AreaAdministrationQuery<string> QueryDestinationConfirmation(int locationId)
        => source.DestinationConfirmation(locationId);
    public AreaAdministrationQuery<PersonnelGroupDestinationPage> QueryDestinationPage()
        => closed ? AreaAdministrationQuery<PersonnelGroupDestinationPage>.Unknown("personnel.groups.closed")
            : source.QueryDestinationPage(selected);
    public AreaAdministrationDraftResult AssignDestination(int locationId)
    {
        if (closed || selected.Length == 0 || !source.Rules.DestinationLocationIds.Contains(locationId))
            return new(false, "personnel.groups.move_destination");
        foreach (int group in selected) destinations[group] = locationId;
        Refresh(); return new(true);
    }
    public AreaAdministrationQuery<AreaAdministrationOfficerCard> QueryOfficerCard(int officerId)
        => source.QueryOfficerCard(officerId);
    public AreaAdministrationQuery<PreparePersonnelGroupMoveOrder> BuildOrder() => !CanConfirm
        ? AreaAdministrationQuery<PreparePersonnelGroupMoveOrder>.Unknown("personnel.groups.move_cannot_confirm")
        : AreaAdministrationQuery<PreparePersonnelGroupMoveOrder>.Known(new(ForceId,
            selected.Select(group => new PersonnelGroupMoveSelection(group, destinations[group]!.Value)).ToArray()));
    private void Refresh() => Rows = source.Rows(source.Members, destinations, true);
    public void Dispose() => closed = true;
}
