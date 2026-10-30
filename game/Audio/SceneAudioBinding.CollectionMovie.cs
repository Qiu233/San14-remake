using Godot;

internal sealed partial class SceneAudioBinding
{
    private bool _collectionMovieActive;
    private AudioPlaybackService.MapMusicPauseHandle? _collectionMovieMusic;

    internal void BeginCollectionMovie()
    {
        if (_collectionMovieActive) return;
        _collectionMovieActive = true;
        StopCollectionMovieOrdinarySounds();
        _collectionMovieMusic = _musicSession.BeginCollectionMovieMusicPause(1000);
        MusicChanged?.Invoke();
    }

    internal void EndCollectionMovie(bool restore)
    {
        if (!_collectionMovieActive) return;
        _collectionMovieActive = false;
        StopCollectionMovieOrdinarySounds();
        var owner = _collectionMovieMusic;
        _collectionMovieMusic = null;
        if (owner is not null && GodotObject.IsInstanceValid(_musicSession) && _musicSession.IsInsideTree())
            _musicSession.EndCollectionMovieMusicPause(owner, restore, 1000);
        MusicChanged?.Invoke();
    }

    private void StopCollectionMovieOrdinarySounds()
    {
        //407160 acts on the shared ordinary SE slots, never Voice or BGM.
        if (GodotObject.IsInstanceValid(_session) && _session.IsInsideTree()) _session.StopStorySounds();
        if (GodotObject.IsInstanceValid(_musicSession) && _musicSession.IsInsideTree()) _musicSession.StopStorySounds();
    }
}
