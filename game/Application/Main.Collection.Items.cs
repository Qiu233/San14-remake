using San14.Data;

public partial class Main
{
    private CollectionItemsView? _collectionItems;

    private void OpenCollectionItems()
    {
        if (_failed || _shuttingDown || _repository is null ||
            _collectionMenu is not { } parent || HasCollectionChild) return;
        var catalog = _repository.LoadCollectionItemCatalog(_ui.SourceLocale);
        var view = new CollectionItemsView(); _collectionItems = view; AddChild(view); parent.Hide();
        try
        {
            view.Configure(_repository, _ui, catalog,
                () => !_failed && !_shuttingDown && _collectionMenu == parent &&
                    _collectionItems == view && _titleMenu is not null,
                () => RunUiAction(() => CloseCollectionItems(restoreMenu: true)),
                CurrentListScrollSpeed, RunUiAction,
                () => _sceneAudioBinding?.BeginPoemEvent(_ui.SourceLocale, _preferences.MusicSelection),
                restore => _sceneAudioBinding?.EndPoemEvent(restore && !_failed && !_shuttingDown),
                () => _sceneAudioBinding?.PlayPoemAppearanceSoundAsync() ?? Task.CompletedTask);
        }
        catch { CloseCollectionItems(restoreMenu: true); throw; }
    }

    private void CloseCollectionItems(bool restoreMenu)
    {
        var view = _collectionItems; _collectionItems = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        if (restoreMenu && !_failed && !_shuttingDown) _collectionMenu?.Show();
    }
}
