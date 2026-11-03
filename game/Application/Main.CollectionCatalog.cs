using San14.Data;

public partial class Main
{
    private void OpenCollectionCatalog(CollectionCatalogKind kind)
    {
        if (_failed || _shuttingDown || _repository is null ||
            _collectionMenu is not { } parent || HasCollectionChild) return;
        var view = new CollectionCatalogView(); _collectionCatalog = view; AddChild(view); parent.Hide();
        try
        {
            var prepared = TryBorrowApplicationPreparedResources(view);
            view.Configure(_repository, _ui, prepared.TitleCatalog, kind,
                () => !_failed && !_shuttingDown && _collectionMenu == parent &&
                    _collectionCatalog == view && _titleMenu is not null,
                () => RunUiAction(() => CloseCollectionCatalog(restoreMenu: true)), CurrentListScrollSpeed, RunUiAction,
                () => _sceneAudioBinding?.BeginPoemEvent(_ui.SourceLocale, _preferences.MusicSelection),
                restore => _sceneAudioBinding?.EndPoemEvent(restore && !_failed && !_shuttingDown),
                () => _sceneAudioBinding?.PlayPoemAppearanceSoundAsync() ?? Task.CompletedTask,
                resources: prepared.CreateChromeResources(), preparedPoemEvents: prepared.PoemEvents);
        }
        catch { CloseCollectionCatalog(restoreMenu: true); throw; }
    }

    private void CloseCollectionCatalog(bool restoreMenu)
    {
        var view = _collectionCatalog; _collectionCatalog = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        if (restoreMenu && !_failed && !_shuttingDown) _collectionMenu?.Show();
    }
}
