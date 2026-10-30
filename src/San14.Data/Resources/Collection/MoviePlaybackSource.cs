namespace San14.Data;

/// <summary>
/// Shared movie routes for normal game presentations and the title collection.
/// Resolving a resource does not unlock a collection entry or establish the
/// application's movie setting, decoder availability or presentation completion.
/// </summary>
public static class MoviePlaybackSource
{
    /// <summary>
    /// The original application movie-play gate. Three mandatory presentations
    /// bypass the environment setting; all other movies retain its actual value,
    /// including unknown. File existence and system decoding are separate concerns.
    /// </summary>
    public static bool? IsPlaybackEnabled(int movieId, bool? environmentMoviePlaybackEnabled)
    {
        ValidateMovieId(movieId);
        return movieId is 26 or 28 or 46 ? true : environmentMoviePlaybackEnabled;
    }

    /// <summary>
    /// Reads a movie selected by its actual business caller, without subtitles or
    /// collection-progress gates. The caller applies its own presentation gate.
    /// </summary>
    public static CollectionMoviePlayback ReadPlayback(GameResourceRepository repository, int movieId, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ValidateMovieId(movieId);
        if (locale is not ("TC" or "SC")) throw new ArgumentOutOfRangeException(nameof(locale));
        var definition = CollectionMovieDefinitions.Movies[movieId];
        string directory = movieId == 45 ? "0003_OS" : locale == "SC" && definition.Simplified
            ? "0003_SC" : definition.Localized ? "0003_TC" : "0003";
        string relative = (definition.Dlc ? "DLC/" : "") + directory + "/" + definition.FileStem + ".wmv";
        var route = repository.ResolveMovieFile(relative);
        return new(movieId, route.ResourceUri, route.PhysicalPath, route.IsOverride,
            null, null, false, []);
    }

    private static void ValidateMovieId(int movieId)
    {
        if (movieId <= 0 || movieId >= CollectionMovieDefinitions.Movies.Length)
            throw new ArgumentOutOfRangeException(nameof(movieId));
    }
}

public sealed partial class GameResourceRepository
{
    public CollectionMoviePlayback LoadMoviePlayback(int movieId, string locale) =>
        MoviePlaybackSource.ReadPlayback(this, movieId, locale);

    internal (string ResourceUri, string PhysicalPath, bool IsOverride) ResolveMovieFile(string relative)
    {
        var id = GameResourceId.File(relative);
        string? replacement = _overrides.Find(id);
        string path = replacement ?? SourcePath(relative);
        if (!File.Exists(path)) throw new FileNotFoundException($"Original movie resource is missing: {id}", path);
        return (id.ToString(), path, replacement is not null);
    }
}
