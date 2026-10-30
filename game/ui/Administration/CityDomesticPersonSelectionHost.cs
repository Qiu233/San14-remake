using Godot;
using San14.Core;
using San14.Data;

/// <summary>Owns one draft's modal surfaces and retained per-role list state.
/// Queries use the same publication; closing only releases the input surface.</summary>
internal sealed class CityDomesticPersonSelectionHost(Node parent, GameResourceRepository repository,
    PreviewText text, GameState world, CityDomesticDraft draft, Func<bool> current,
    Action<string> unavailable, int scrollSpeed,Func<int>? currentScrollSpeed=null) : IDisposable
{
    private readonly Dictionary<CityDomesticDuty, CityDomesticPersonListState> lists = [];
    private CityDomesticPersonSelectionView? active;
    private bool disposed;
    private sealed record Page(long Revision, IReadOnlyList<CityDomesticPersonEntry> Entries,
        AreaAdministrationQuery<int?> Preselection);

    internal Action Open(CityDomesticPersonSelection selection, UiSourceCommonBlur commonBackdrop, Action<CityDomesticCandidate?> complete)
    {
        if (disposed || !current() || active is not null)
            throw new InvalidOperationException("City person selector is outside its current page lifecycle.");
        if (!lists.TryGetValue(selection.Duty, out var state))
            lists.Add(selection.Duty, state = new());
        var order = state.Candidates is null ? Enumerable.Range(0, selection.Candidates.Count).ToArray() : state.Order.ToArray();
        var query = QueryPage(selection, order, state.ActivePage);
        if (!query.Available || !query.Value!.Preselection.Available)
        {
            unavailable(query.UnresolvedInput ?? query.Value!.Preselection.UnresolvedInput!);
            complete(null); return () => { };
        }
        var page = query.Value!;
        if (disposed || !current() || draft.Revision != page.Revision)
        {
            unavailable("city-domestic.person.selector_stale");
            complete(null); return () => { };
        }
        var view = new CityDomesticPersonSelectionView(); active = view;
        bool closed = false;
        void Close()
        {
            if (closed) return; closed = true;
            if (ReferenceEquals(active, view)) active = null;
            if (GodotObject.IsInstanceValid(view))
            { view.Abandon(); if (!view.IsQueuedForDeletion()) view.QueueFree(); }
        }
        try
        {
            parent.AddChild(view);
            bool Current() => !disposed && current() && ReferenceEquals(active, view) && draft.Revision == page.Revision;
            view.Configure(repository, text, selection.Duty, CityDomesticPersonColumns.For(selection.Duty, state.ActivePage), page.Entries,
                state, page.Preselection.Value, currentScrollSpeed?.Invoke()??scrollSpeed,
                (pageIndex, rows, column, ascending) =>
                {
                    if (!Current()) return new(null, "city-domestic.person.selector_stale");
                    var sorted = draft.QueryPersonOrder(selection, rows, CityDomesticPersonListContent.SortColumn(column),
                        ascending ? CityDomesticPersonSortDirection.Natural : CityDomesticPersonSortDirection.Reversed);
                    return Current() ? sorted : new(null, "city-domestic.person.selector_stale");
                },
                chosen =>
                {
                    if (Current()) complete(chosen);
                }, unavailable, (pageIndex, rows) =>
                {
                    if (!Current()) return new(null, "city-domestic.person.selector_stale");
                    var next = QueryPage(selection, rows, pageIndex);
                    if (!Current()) return new(null, "city-domestic.person.selector_stale");
                    return next.Available && next.Value!.Revision == page.Revision
                        ? new(next.Value.Entries, null)
                        : new(null, next.UnresolvedInput ?? "city-domestic.person.selector_stale");
                },currentScrollSpeed,commonBackdrop,Current);
            return Close;
        }
        catch { Close(); throw; }
    }

    private AreaAdministrationQuery<Page> QueryPage(CityDomesticPersonSelection selection,
        IReadOnlyList<int> order, int pageIndex)
    {
        try
        {
            if (pageIndex == 0)
            {
                var query = draft.QueryPersonBasicPage(selection, order);
                return query.Available ? new(new(query.Value!.Revision,
                    CityDomesticPersonListContent.Create(repository, text, world, query.Value), query.Value.PreselectedOccurrence), null)
                    : new(null, query.UnresolvedInput);
            }
            return pageIndex switch
            {
                1 => Wrap(draft.QueryPersonAffiliationPage(selection, order), p => CityDomesticPersonListContent.Create(repository, text, world, p)),
                2 => Wrap(draft.QueryPersonAbilityPage(selection, order), p => CityDomesticPersonListContent.Create(repository, text, world, p)),
                3 => Wrap(draft.QueryPersonMeritPage(selection, order), p => CityDomesticPersonListContent.Create(repository, text, world, p)),
                4 => Wrap(draft.QueryPersonTaskPage(selection, order), p => CityDomesticPersonListContent.Create(repository, text, world, p)),
                5 => Wrap(draft.QueryPersonPersonalPage(selection, order), p => CityDomesticPersonListContent.Create(repository, text, world, p)),
                _ => throw new ArgumentOutOfRangeException(nameof(pageIndex))
            };
        }
        catch (InvalidDataException error) { return new(null, error.Message); }

        static AreaAdministrationQuery<Page> Wrap<TRow>(AreaAdministrationQuery<CityDomesticPersonPage<TRow>> query,
            Func<CityDomesticPersonPage<TRow>, IReadOnlyList<CityDomesticPersonEntry>> format) => query.Available
                ? new(new(query.Value!.Revision, format(query.Value), query.Value.PreselectedOccurrence), null)
                : new(null, query.UnresolvedInput);
    }

    internal bool TryCancel()
    {
        if (disposed || active is not { } view) return false;
        view.RequestCancel(); return true;
    }

    public void Dispose()
    {
        if (disposed) return; disposed = true;
        var view = active; active = null;
        if (view is not null && GodotObject.IsInstanceValid(view))
        { view.Abandon(); if (!view.IsQueuedForDeletion()) view.QueueFree(); }
        lists.Clear();
    }
}
