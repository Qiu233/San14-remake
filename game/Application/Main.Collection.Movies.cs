using Godot;
using San14.Data;

public partial class Main
{
    private CollectionMoviesView? _collectionMovies;

    private void OpenCollectionMovies()
    {
        if (_failed || _shuttingDown || _repository is null ||
            _collectionMenu is not { } parent || HasCollectionChild) return;
        var profile = GetApplicationProfile().Current;
        var progress = new CollectionMovieProgress(Enumerable.Range(0, 200)
            .Select(profile.ReadScenarioCompleted).ToArray());
        var catalog = _repository.LoadCollectionMovieCatalog(_ui.SourceLocale, progress);
        string library = ResolveMovieNativeLibrary();
        var view = new CollectionMoviesView(); _collectionMovies = view; AddChild(view); parent.Hide();
        try
        {
            view.Configure(_repository, _ui, catalog, library,
                () => !_failed && !_shuttingDown && _collectionMenu == parent && _collectionMovies == view && _titleMenu is not null,
                () => RunUiAction(() => CloseCollectionMovies(true)), CurrentListScrollSpeed, RunUiAction,
                () => _preferences.Audio.Movie / 100f,
                () => _sceneAudioBinding?.BeginCollectionMovie(),
                restore => _sceneAudioBinding?.EndCollectionMovie(restore && !_failed && !_shuttingDown),
                CurrentMoviePlaybackEnabled, resources: TryBorrowApplicationUiResources(view));
        }
        catch { CloseCollectionMovies(true); throw; }
    }

    // MOVIE_PLAY starts enabled in the normal application. This is its host
    // setting, independent of whether a movie file or decoder is available.
    private bool? CurrentMoviePlaybackEnabled() =>
        _preferences.Presentation.MoviesEnabled &&
        ProjectSettings.GetSetting("san14/movie/playback_enabled", true).AsBool();

    private static string ResolveMovieNativeLibrary()
    {
        string defaultLibrary = OS.HasFeature("editor")
            ? "res://../native/San14.Media.Decoder/bin/x64/Release/San14.Media.Decoder.dll"
            : Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath())!, "native", "windows-x64", "San14.Media.Decoder.dll");
        string configured = System.Environment.GetEnvironmentVariable("SAN14_MOVIE_NATIVE_LIBRARY") ??
            ProjectSettings.GetSetting("san14/movie/native_library", defaultLibrary).AsString();
        return Path.GetFullPath(ProjectSettings.GlobalizePath(configured));
    }

    private void CloseCollectionMovies(bool restoreMenu)
    {
        var view = _collectionMovies; _collectionMovies = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        if (restoreMenu && !_failed && !_shuttingDown) _collectionMenu?.Show();
    }
}
