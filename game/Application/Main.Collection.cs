using Godot;

public partial class Main
{
    private CollectionMenuView? _collectionMenu;
    private MusicSelectionView? _collectionMusic;
    private bool HasCollectionMenu => _collectionMenu is not null;

    private void OpenCollectionMenu()
    {
        if (_failed || _shuttingDown || _repository is null || _titleMenu is not { } title ||
            HasCollectionMenu || HasTitleEditMenu || HasEnvironmentSettings || HasCampaignSavePage || _titleConfirmation is not null) return;
        var view = new CollectionMenuView(); _collectionMenu = view; AddChild(view); title.Hide();
        try
        {
            view.Configure(_repository, _ui, [true, false, false, false, false, false, false],
                () => !_failed && !_shuttingDown && _collectionMenu == view && _collectionMusic is null,
                () => RunUiAction(() => CloseCollectionMenu()),
                command => RunUiAction(() =>
                {
                    if (_collectionMenu != view || _collectionMusic is not null) return;
                    if (command == CollectionMenuCommand.Music) OpenCollectionMusic();
                }));
        }
        catch { CloseCollectionMenu(); throw; }
    }

    private void CloseCollectionMenu(bool resumeTitle = true)
    {
        CloseCollectionMusic(restore: resumeTitle);
        var view = _collectionMenu; _collectionMenu = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        if (resumeTitle) ResumeTitleMenu();
    }

    private void RequestCollectionBack()
    {
        if (_collectionMusic is { } music) music.RequestBack();
        else _collectionMenu?.RequestBack();
    }
}
