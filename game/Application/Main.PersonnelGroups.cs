using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class PersonnelGroupsWorkspace(GameplaySession session, GameState world, MapSceneView map)
    {
        internal GameplaySession Session { get; } = session;
        internal GameState World { get; set; } = world;
        internal MapSceneView Map { get; } = map;
        internal CommandSelectionView? Page { get; set; }
        internal CommandSelectionView? Selector { get; set; }
        internal PersonnelGroupMembersView? Members { get; set; }
        internal DeploymentTargetConfirmation? Question { get; set; }
        internal Action? Back { get; set; }
        internal Action? DisposeDraft { get; set; }
    }

    private PersonnelGroupsWorkspace? _personnelGroups;
    private bool HasPersonnelGroupsPage => _personnelGroups is not null;
    private bool IsPersonnelGroupsCurrent(PersonnelGroupsWorkspace owner) =>
        ReferenceEquals(_personnelGroups, owner) && ReferenceEquals(_session, owner.Session) &&
        ReferenceEquals(_world, owner.World) && ReferenceEquals(owner.Session.World, owner.World) &&
        ReferenceEquals(_residentMap, owner.Map) && !_failed && !_shuttingDown && !_advancingPeriod;

    private void AdoptPersonnelGroupsSpeechWorld(GameplaySession session, GameState previous, GameState replacement)
    {
        // The same workspace owns the hidden list, members, destination and confirmation.
        if (_personnelGroups is not { Page: not null } owner || owner.Session != session ||
            owner.Map != _residentMap || !ReferenceEquals(owner.World, previous)) return;
        owner.World = replacement;
    }

    private string PersonnelGroupCaption(int id) =>
        _repository!.LoadMessages(_ui.SourceLocale, MessageKind.Data).Messages.Sections[1].Records[id].Text
        ?? throw new InvalidDataException("Missing personnel group caption.");

    private void OpenPersonnelGroups(bool editing)
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || HasAdministrationPage ||
            HasMilitaryOrderPage || HasCampaignSavePage || HasMapSystemMenu || HasCommandWorkspace || HasActionReport ||
            HasPersonnelConfirmation || _detailsView is not null || _repository is null ||
            _campaignComposition is not { } composition || _residentMap is not { IsPlayableMapReady: true } map ||
            _session is not { PlayerForceId: int player } session || session.HasPendingOperation ||
            session.World is not { Phase: TurnPhase.Strategy } world ||
            PlayableOutcome.Evaluate(world, player) != PlayableResult.Ongoing) return;
        var rules = composition.PrepareArmyQueries(world);
        PersonnelGroupEditDraft? edit = null;
        PersonnelGroupMoveDraft? move = null;
        if (editing)
        {
            var result = rules.OpenPersonnelGroupEditDraft(world, player);
            if (!result.Available) { PersonnelGroupsUnavailable(result.UnresolvedInput); return; }
            edit = result.Value!;
        }
        else
        {
            var result = rules.OpenPersonnelGroupMoveDraft(world, player);
            if (!result.Available) { PersonnelGroupsUnavailable(result.UnresolvedInput); return; }
            move = result.Value!;
        }
        var owner = new PersonnelGroupsWorkspace(session, world, map)
        { DisposeDraft = () => { edit?.Dispose(); move?.Dispose(); } };
        _personnelGroups = owner;
        map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true);
        SetMapFocus(true); HideDeploymentHud();
        try
        {
            if (edit is not null) OpenPersonnelGroupEditor(owner, edit);
            else OpenPersonnelGroupMover(owner, move!);
        }
        catch { ClosePersonnelGroups(); throw; }
    }

    private static CommandSelectionRow[] PersonnelGroupRows(IReadOnlyList<PersonnelGroupRow> rows) =>
        rows.Select(row => new CommandSelectionRow(row.GroupIndex,
            [new(row.Caption, row.GroupIndex), new(row.MemberCaption, SortKey: row.Members.FirstOrDefault()?.NameSortKey),
             new(row.DestinationName)], row.Selectable)).ToArray();

    private void OpenPersonnelGroupEditor(PersonnelGroupsWorkspace owner, PersonnelGroupEditDraft draft)
    {
        ShowMain();
        void ShowMain()
        {
            RetirePersonnelGroupList(owner.Page);
            owner.Page = ShowPersonnelGroupList(owner, 0x73A, PersonnelGroupColumns(), PersonnelGroupRows(draft.Rows),
                [], 1, draft.Guide, selected =>
                {
                    if (selected is { Count: 1 }) { ChooseMembers(selected[0]); return; }
                    var order = draft.BuildOrder();
                    if (order.Available && ApplyPlayableOrder(order.Value!, ClosePersonnelGroups)) return;
                    owner.Page!.Show();
                    owner.Page.ApplyNotice(order.Available ? _playMessage.Text : PersonnelGroupsUnavailable(order.UnresolvedInput));
                }, rowActivated: true, returnOnly: true, retainOnReturn: true);
            owner.Page.Name = "PersonnelGroupEdit";
            owner.Back = owner.Page.RequestCancel;
        }
        void ChooseMembers(int group)
        {
            var initial = QueryPage(0);
            if (!initial.Available)
            {
                owner.Page!.Show(); owner.Page.ApplyNotice(PersonnelGroupsUnavailable(initial.UnresolvedInput)); return;
            }
            var view = new PersonnelGroupMembersView(); owner.Members = view; AddChild(view);
            try
            {
                view.Configure(_repository!, _ui, draft.Rows[group].Members.Select(person => person.OfficerId).ToArray(),
                    () => IsPersonnelGroupsCurrent(owner) && owner.Members == view,
                    selected => RunUiAction(() =>
                    {
                        if (!IsPersonnelGroupsCurrent(owner) || owner.Members != view) return;
                        if (selected is not null)
                        {
                            var result = draft.ReplaceMembers(group, selected);
                            if (!result.Available)
                            { view.Show(); view.ApplyNotice(PersonnelGroupsUnavailable(result.UnresolvedInput)); return; }
                        }
                        view.Abandon(); RemoveChild(view); view.QueueFree(); owner.Members = null;
                        if (selected is null) owner.Page!.Show(); else ShowMain();
                    }), CurrentListScrollSpeed, draft.SelectorGuide, page => page == 0 ? initial : QueryPage(page));
            }
            catch { view.Abandon(); RemoveChild(view); view.QueueFree(); owner.Members = null; owner.Page!.Show(); throw; }

            AreaAdministrationQuery<IReadOnlyList<CommandSelectionRow>> QueryPage(int page)
            {
                var result = draft.QueryPersonPage(group, page);
                return !result.Available ? new(null, result.UnresolvedInput!)
                    : new(result.Value!.Rows.Select(row =>
                        new CommandSelectionRow(row.OfficerId, row.Cells.Select(PersonnelGroupCell).ToArray(), true)).ToArray(), null);
            }
        }
    }

    private CommandSelectionCell PersonnelGroupCell(PersonnelGroupPersonCell cell) =>
        new(cell.UnresolvedOwner is not null ? _ui.Get("detail_unknown")
            : cell.Text ?? (cell.NotApplicable ? PersonnelGroupCaption(0x353)
                : cell.Number?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? ""),
            cell.SortKey is not null ? null : cell.SortNumber ?? cell.Number, cell.SortKey,
            SortGroup: cell.SortGroup, SortUnavailable: cell.SortUnresolvedOwner is not null, LocationOrder: cell.LocationOrder,
            SourceFormattedText: cell.UnresolvedOwner is null ? cell.SourceFormattedText : null);

    private void OpenPersonnelGroupMover(PersonnelGroupsWorkspace owner, PersonnelGroupMoveDraft draft)
    {
        owner.Page = ShowPersonnelGroupList(owner, 0x73E, PersonnelGroupColumns(), PersonnelGroupRows(draft.Rows),
            draft.SelectedGroupIndices, 10, draft.Guide, selected =>
            {
                if (selected is null) { ClosePersonnelGroups(); return; }
                var selection = draft.SelectGroups(selected);
                if (!selection.Available || !draft.CanSelectDestination)
                { owner.Page!.Show(); owner.Page.ApplyNotice(PersonnelGroupsUnavailable(selection.UnresolvedInput)); return; }
                var warning = draft.QueryAvailabilityWarning();
                if (!warning.Available)
                { owner.Page!.Show(); owner.Page.ApplyNotice(PersonnelGroupsUnavailable(warning.UnresolvedInput)); return; }
                if (string.IsNullOrEmpty(warning.Value)) ChooseDestination();
                else AskPersonnelGroups(owner, warning.Value, accepted =>
                { if (accepted) ChooseDestination(); else owner.Page!.Show(); });
            });
        owner.Page.Name = "PersonnelGroupMove";
        owner.Back = owner.Page.RequestCancel;

        void ChooseDestination()
        {
            var page = draft.QueryDestinationPage();
            if (!page.Available)
            { owner.Page!.Show(); owner.Page.ApplyNotice(PersonnelGroupsUnavailable(page.UnresolvedInput)); return; }
            var preselection = draft.QueryPreselectedLocation();
            if (!preselection.Available)
            { owner.Page!.Show(); owner.Page.ApplyNotice(PersonnelGroupsUnavailable(preselection.UnresolvedInput)); return; }
            var columns = PersonnelGroupListColumns.Destination.Select(column => new CommandSelectionColumn(
                column.Id == 0 ? "destination" : column.Id.ToString(), PersonnelGroupCaption(column.CaptionMessage),
                column.Width, column.Align == 0x21, column.Align, column.LineArrange, column.InitialAscending)).ToArray();
            owner.Selector = ShowPersonnelGroupList(owner, 0x545,
                columns, page.Value!.Rows.Select(location => new CommandSelectionRow(location.LocationId,
                    location.Cells.Select(PersonnelGroupCell).ToArray(), true)).ToArray(),
                preselection.Value is int preselected ? [preselected] : [], 1, draft.DestinationGuide,
                selected =>
                {
                    if (selected is null)
                    {
                        RetirePersonnelGroupList(owner.Selector); owner.Selector = null; owner.Page!.Show(); return;
                    }
                    int location = selected.Single();
                    var message = draft.QueryDestinationConfirmation(location);
                    if (!message.Available)
                    { owner.Selector!.Show(); owner.Selector.ApplyNotice(PersonnelGroupsUnavailable(message.UnresolvedInput)); return; }
                    AskPersonnelGroups(owner, message.Value!, accepted =>
                    {
                        if (!accepted) { owner.Selector!.Show(); return; }
                        var assignment = draft.AssignDestination(location);
                        var order = draft.BuildOrder();
                        if (assignment.Available && order.Available && ApplyPlayableOrder(order.Value!, ClosePersonnelGroups)) return;
                        owner.Selector!.Show();
                        owner.Selector.ApplyNotice(!assignment.Available ? PersonnelGroupsUnavailable(assignment.UnresolvedInput)
                            : !order.Available ? PersonnelGroupsUnavailable(order.UnresolvedInput) : _playMessage.Text);
                    });
                }, rowActivated: true, returnOnly: true, retainOnReturn: true);
            owner.Selector.Name = "PersonnelGroupDestination";
        }
    }

    private void AskPersonnelGroups(PersonnelGroupsWorkspace owner, string message, Action<bool> completed)
    {
        var question = new DeploymentTargetConfirmation(); owner.Question = question; AddChild(question);
        try
        {
            question.ConfigureWrappedMessage(_repository!, _ui, message, accepted => RunPlayAction(() =>
            {
                if (!IsPersonnelGroupsCurrent(owner) || owner.Question != question) return;
                owner.Question = null; question.Abandon(); RemoveChild(question); question.QueueFree();
                completed(accepted);
            }));
        }
        catch { owner.Question = null; question.Abandon(); RemoveChild(question); question.QueueFree(); throw; }
    }

    private string PersonnelGroupsUnavailable(string? reason)
    {
        string text = _ui.Get("information_partial"); _playMessage.Text = text;
        if (reason is not null) GD.PushWarning(reason);
        return text;
    }

    private CommandSelectionView ShowPersonnelGroupList(PersonnelGroupsWorkspace owner, int title,
        IReadOnlyList<CommandSelectionColumn> columns, IReadOnlyList<CommandSelectionRow> rows,
        IReadOnlyList<int> initial, int maximum, string guide, Action<IReadOnlyList<int>?> completed,
        bool rowActivated = false, bool returnOnly = false, bool retainOnReturn = false,
        bool requireSelection = true)
    {
        var view = new CommandSelectionView();
        AddChild(view);
        try
        {
            view.Configure(_repository!, _ui, title, columns, rows, initial, maximum,
                () => IsPersonnelGroupsCurrent(owner) && owner.Question is null,
                selected => RunUiAction(() =>
                {
                    if (IsPersonnelGroupsCurrent(owner)) completed(selected);
                }), CurrentListScrollSpeed, guide, retainOnAccept: true,
                requireSelection: requireSelection, singleSelection: rowActivated, activateRows: rowActivated,
                returnOnly: returnOnly, retainOnReturn: retainOnReturn);
            return view;
        }
        catch { view.Abandon(); RemoveChild(view); view.QueueFree(); throw; }
    }

    private CommandSelectionColumn[] PersonnelGroupColumns() =>
        [new("group", PersonnelGroupCaption(0x73C), 132),
         new("name", PersonnelGroupCaption(0x211), 300),
         new("destination", PersonnelGroupCaption(0x322), 160)];

    private void RequestPersonnelGroupsBack()
    {
        if (_personnelGroups is not { } owner || !IsPersonnelGroupsCurrent(owner)) return;
        if (owner.Question is { } question) { question.RequestCancel(); return; }
        if (owner.Members is { } members) { members.RequestCancel(); return; }
        if (owner.Selector is { } selector) { selector.RequestCancel(); return; }
        owner.Back?.Invoke();
    }

    private static void RetirePersonnelGroupList(CommandSelectionView? view)
    {
        if (view is null) return;
        view.Abandon(); view.GetParent()?.RemoveChild(view); view.QueueFree();
    }

    private void ClosePersonnelGroups()
    {
        var owner = _personnelGroups; _personnelGroups = null;
        if (owner is null) return;
        if (owner.Question is { } question)
        { question.Abandon(); question.GetParent()?.RemoveChild(question); question.QueueFree(); }
        RetirePersonnelGroupList(owner.Selector); RetirePersonnelGroupList(owner.Page);
        if (owner.Members is { } members)
        { members.Abandon(); members.GetParent()?.RemoveChild(members); members.QueueFree(); }
        owner.DisposeDraft?.Invoke();
        if (_residentMap == owner.Map && GodotObject.IsInstanceValid(owner.Map))
        {
            owner.Map.SetDetailsOpen(false);
            if (!_failed && !_shuttingDown) SetMapFocus(_mapFocused);
        }
    }
}
