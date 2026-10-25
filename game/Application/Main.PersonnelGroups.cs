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
        internal GameState World { get; } = world;
        internal MapSceneView Map { get; } = map;
        internal CommandSelectionView? Page { get; set; }
        internal CommandSelectionView? Selector { get; set; }
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

    private string PersonnelGroupCaption(int id) =>
        _repository!.LoadMessages(_ui.SourceLocale, MessageKind.Data).Messages.Sections[1].Records[id].Text
        ?? throw new InvalidDataException("Missing personnel group caption.");

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
        owner.DisposeDraft?.Invoke();
        if (_residentMap == owner.Map && GodotObject.IsInstanceValid(owner.Map))
        {
            owner.Map.SetDetailsOpen(false);
            if (!_failed && !_shuttingDown) SetMapFocus(_mapFocused);
        }
    }
}
