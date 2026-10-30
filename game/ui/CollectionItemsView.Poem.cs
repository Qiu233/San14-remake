using San14.Data;

internal partial class CollectionItemsView
{
    private CollectionCatalog? poemCatalog;
    private PoemEventCatalog? poemEventCatalog;
    private CollectionDetailView? poemDetail;
    private PoemEventView? poemEvent;
    private Action? poemEntered;
    private Action<bool>? poemLeft;
    private Func<Task>? poemAppearanceSound;
    internal bool HasPoemEvent => poemEvent is not null;

    private void ShowPoemDetail(int itemId)
    {
        if (!ReadAdmission() || detail is null || poemDetail is not null || !catalog.ReadItem(itemId).CanAppreciatePoem) return;
        var publication = poemCatalog ??= CollectionCatalog.Load(repository, text.SourceLocale);
        var poem = publication.ReadPoem(itemId);
        var page = new CollectionDetailView(); poemDetail = page; AddChild(page); detail.Hide();
        try
        {
            page.Configure(repository, text, publication, poem, null, [new(itemId, poem.Name)],
                () => ReadAdmission() && poemDetail == page,
                () => runUiAction(ClosePoemDetail), _ => { }, scrollSpeed,
                () => runUiAction(() => ShowPoemEvent(itemId)));
        }
        catch { ClosePoemDetail(); throw; }
    }

    private void ClosePoemDetail()
    {
        ClosePoemEvent(restore: !abandoned);
        var page = poemDetail; poemDetail = null;
        if (page is not null) { page.Abandon(); page.QueueFree(); }
        if (!abandoned) detail?.Show();
    }

    private void ShowPoemEvent(int itemId)
    {
        if (!ReadAdmission() || poemDetail is null || poemEvent is not null) return;
        var publication = (poemEventCatalog ??= PoemEventCatalog.Load(repository, text.SourceLocale)).Read(itemId);
        var view = new PoemEventView(); poemEvent = view; AddChild(view); poemDetail.Hide();
        try
        {
            view.Configure(repository, text, publication,
                () => ReadAdmission() && poemEvent == view,
                () => runUiAction(() => ClosePoemEvent(restore: true)),
                error => runUiAction(() => throw error),
                () => poemAppearanceSound?.Invoke() ?? Task.CompletedTask);
            poemEntered?.Invoke();
        }
        catch { ClosePoemEvent(restore: true); throw; }
    }

    private void ClosePoemEvent(bool restore)
    {
        var view = poemEvent; poemEvent = null;
        if (view is null) return;
        view.Abandon(); view.QueueFree();
        if (restore && !abandoned) poemDetail?.Show();
        poemLeft?.Invoke(restore && !abandoned);
    }
}
