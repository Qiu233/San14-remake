using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The normal item command's eleven person-list tabs share one
/// single selection; visited tabs retain their own actual sort and scroll.</summary>
internal partial class PersonnelItemPersonSelectionView : Control
{
    private readonly Dictionary<int, CommandSelectionView> pages = [];
    private readonly Dictionary<int, Dictionary<string, bool>> nextSort = [];
    private IReadOnlyList<int>? rowOrder;
    private IReadOnlyList<int> selected = [];
    private Func<bool>? current;
    private Action<IReadOnlyList<int>?>? closed;
    private int activePage;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        AdministrationPageChrome.Resources resources, IReadOnlyList<int> initial,
        Func<bool> current, Action<IReadOnlyList<int>?> closed, Func<int> scrollSpeed,
        Func<int, AreaAdministrationQuery<IReadOnlyList<CommandSelectionRow>>> query,
        Func<int, string, bool, AreaAdministrationQuery<IReadOnlyList<CommandSelectionRow>>> sort, string guide)
    {
        this.current = current; this.closed = closed; selected = initial.ToArray();
        Name = "PersonnelItemTargetSelection"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing item person selector caption.");
        string[] captions = PersonnelItemSelectionColumns.PersonCaptions.Select(Message).ToArray();
        SwitchPage(0);

        void SwitchPage(int page)
        {
            if (this.closed is null || !(this.current?.Invoke() ?? false)) return;
            if (pages.TryGetValue(activePage, out var previous)) selected = previous.SelectedIds;
            if (!pages.TryGetValue(page, out var next))
            {
                var result = query(page);
                if (!result.Available)
                {
                    previous?.ApplyNotice(text.Get("information_partial"));
                    if (result.UnresolvedInput is { } reason) GD.PushWarning(reason);
                    return;
                }
                if (!nextSort.TryGetValue(page, out var directions)) nextSort.Add(page, directions = []);
                var columns = PersonnelItemSelectionColumns.PersonPages[page]
                    .Select(id => PersonnelItemSelectionColumns.People[id])
                    .Select(column => new CommandSelectionColumn(column.Id == 0 ? "name" : column.Id.ToString(),
                        Message(column.Caption), column.Width, column.Align == 0x21,
                        column.Align, column.LineArrange, column.InitialAscending)).ToArray();
                next = new CommandSelectionView(); AddChild(next);
                try
                {
                    next.Configure(repository, text, 0x32E, columns, result.Value!, selected, 1,
                        () => IsVisibleInTree() && this.closed is not null && (this.current?.Invoke() ?? false),
                        value => { Hide(); this.closed?.Invoke(value); }, scrollSpeed, guide,
                        retainOnAccept: true, requireSelection: true, singleSelection: true,
                        retainOnReturn: true, sourceSort: (key, ascending) =>
                        {
                            var sorted = sort(page, key, ascending);
                            if (!sorted.Available)
                            { next!.ApplyNotice(text.Get("information_partial")); if (sorted.UnresolvedInput is { } reason) GD.PushWarning(reason); return null; }
                            return sorted.Value;
                        }, groupCaptions: captions, selectedGroup: page,
                        groupSelected: SwitchPage, nextSortDirections: directions, resources: resources);
                    if (rowOrder is not null) next.ApplyRowOrder(rowOrder);
                    next.SortOrderChanged = ids =>
                    {
                        rowOrder = ids;
                        foreach (var other in pages.Values) if (other != next) other.ApplyRowOrder(ids);
                    };
                    next.Name = "PersonnelItemTargetPage" + page; pages.Add(page, next);
                }
                catch { next.Abandon(); RemoveChild(next); next.QueueFree(); throw; }
            }
            else next.ReplaceSelection(selected);
            if (previous != next) previous?.Hide();
            activePage = page; next.Show();
        }
    }

    internal void RequestCancel() { if (pages.TryGetValue(activePage, out var page)) page.RequestCancel(); }
    internal void Abandon()
    {
        current = null; closed = null;
        foreach (var page in pages.Values) page.Abandon();
        Hide();
    }
}
