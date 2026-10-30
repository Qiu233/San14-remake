using Godot;

public partial class Main
{
    private CollectionMenuView? _collectionMenu;
    private MusicSelectionView? _collectionMusic;
    private CollectionCatalogView? _collectionCatalog;
    private bool HasCollectionMenu => _collectionMenu is not null;
    private bool HasCollectionChild => _collectionMusic is not null || _collectionCatalog is not null ||
        _collectionItems is not null || _collectionOfficers is not null || _collectionMovies is not null ||
        _collectionEvents is not null;

    private void OpenCollectionMenu()
    {
        if (_failed || _shuttingDown || _repository is null || _titleMenu is not { } title ||
            HasCollectionMenu || HasTitleEditMenu || HasEnvironmentSettings || HasCampaignSavePage || _titleConfirmation is not null) return;
        var view = new CollectionMenuView(); _collectionMenu = view; AddChild(view); title.Hide();
        try
        {
            view.Configure(_repository, _ui, [true, true, true, true, true, true, true],
                () => !_failed && !_shuttingDown && _collectionMenu == view && !HasCollectionChild,
                () => RunUiAction(() => CloseCollectionMenu()),
                command => RunUiAction(() =>
                {
                    if (_collectionMenu != view || HasCollectionChild) return;
                    if (command == CollectionMenuCommand.Music) OpenCollectionMusic();
                    else if (command == CollectionMenuCommand.Officers) OpenCollectionOfficers();
                    else if (command == CollectionMenuCommand.Items) OpenCollectionItems();
                    else if (command == CollectionMenuCommand.Movies) OpenCollectionMovies();
                    else if (command == CollectionMenuCommand.Events) OpenCollectionEvents();
                    else if (command == CollectionMenuCommand.Poems) OpenCollectionCatalog(CollectionCatalogKind.Poems);
                    else if (command == CollectionMenuCommand.Scenery) OpenCollectionCatalog(CollectionCatalogKind.Scenery);
                }));
        }
        catch { CloseCollectionMenu(); throw; }
    }

    private void CloseCollectionMenu(bool resumeTitle = true)
    {
        CloseCollectionMusic(restore: resumeTitle);
        CloseCollectionEvents(restoreMenu: false);
        CloseCollectionMovies(restoreMenu: false);
        CloseCollectionOfficers(restoreMenu: false);
        CloseCollectionItems(restoreMenu: false);
        CloseCollectionCatalog(restoreMenu: false);
        var view = _collectionMenu; _collectionMenu = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        if (resumeTitle) ResumeTitleMenu();
    }

    private void RequestCollectionBack()
    {
        if (RequestCollectionEventPlaybackBack()) return;
        if (_collectionEvents is { } events) events.RequestBack();
        else if (_collectionMovies is { } movies) movies.RequestBack();
        else if (_collectionOfficers is { } officers) officers.RequestBack();
        else if (_collectionItems is { } items) items.RequestBack();
        else if (_collectionCatalog is { } catalog) catalog.RequestBack();
        else if (_collectionMusic is { } music) music.RequestBack();
        else _collectionMenu?.RequestBack();
    }
}
