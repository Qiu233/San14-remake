using Godot;
using San14.Data;

public partial class Main
{
    private string _collectionMusicHint = "";

    private void OpenCollectionMusic()
    {
        if (_repository is null || _collectionMenu is not { } parent || _collectionMusic is not null) return;
        var catalog = MusicEditCatalog.Load(_repository, _ui.SourceLocale);
        var view = new MusicSelectionView(); _collectionMusic = view; AddChild(view); parent.Hide();
        try
        {
            _collectionMusicHint = new AdministrationMessages(_repository, _ui.SourceLocale).Format(0x287D);
            view.ConfigureCollection(_repository, _ui,
                catalog.Candidates.Select(row => new MusicSelectionRow(row.Index, row.Name, row.Number)).ToArray(),
                () => !_failed && !_shuttingDown && _collectionMenu == parent && _collectionMusic == view && _titleMenu is not null,
                () => RunUiAction(() => CloseCollectionMusic(restore: true)),
                index => RunUiAction(() =>
                {
                    if (_collectionMusic != view || _collectionMenu != parent) return;
                    if (_sceneAudioBinding is not { } audio)
                    { view.ApplyNotice(_ui.Get("scene_audio_unavailable")); return; }
                    audio.BeginCollectionMusic(_ui.SourceLocale, _preferences.MusicSelection);
                    audio.PlayCollectionMusic(index);
                    UpdateCollectionMusicStatus();
                }), CurrentListScrollSpeed);
            _sceneAudioBinding?.BeginCollectionMusic(_ui.SourceLocale, _preferences.MusicSelection);
            UpdateCollectionMusicStatus();
        }
        catch { CloseCollectionMusic(restore: true); throw; }
    }

    private void UpdateCollectionMusicStatus()
    {
        if (_collectionMusic is not { } view) return;
        view.ApplyNotice(_sceneAudioBinding is null || _sceneAudioBinding.MapMusicError is not null
            ? _ui.Get("scene_audio_unavailable") : _collectionMusicHint);
    }

    private void CloseCollectionMusic(bool restore)
    {
        var view = _collectionMusic; _collectionMusic = null; _collectionMusicHint = "";
        if (view is not null)
        {
            view.Abandon(); view.QueueFree();
            _sceneAudioBinding?.EndCollectionMusic(restore && !_failed && !_shuttingDown);
        }
        if (restore && !_failed && !_shuttingDown) _collectionMenu?.Show();
    }
}
