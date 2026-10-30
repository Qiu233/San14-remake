using San14.Data.Formats;

namespace San14.Data;

/// <summary>The title movie directory: scenario headers, application completion
/// history and fixed movie routes. It never initializes a campaign world.</summary>
public sealed class CollectionMovieCatalog
{
    private readonly GameResourceRepository repository;
    public string Locale { get; }
    public string Title { get; }
    public string SubtitleCaption { get; }
    public string ProgressCaption { get; }
    public string LayoutName => "02_07_03_movie";
    public string CellLayoutName => "02_07_03_offset_movie";
    public bool ShowSubtitlesByDefault => true;
    public IReadOnlyList<CollectionMovieRow> Rows { get; }
    public IReadOnlyList<SourceProvenance> Sources { get; }
    public int TotalCount => Rows.Count;
    public int? UnlockedCount => Rows.Any(row => row.Unlocked is null) ? null : Rows.Count(row => row.Unlocked == true);

    public static CollectionMovieCatalog Load(GameResourceRepository repository, string locale,
        CollectionMovieProgress? progress = null, bool? moviePlaybackEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return repository.LoadCollectionMovieCatalog(locale, progress, moviePlaybackEnabled);
    }

    internal CollectionMovieCatalog(GameResourceRepository repository, string locale,
        IReadOnlyList<S14Entry> scenarios, LoadedMessages messages, CollectionMovieProgress? progress,
        bool? moviePlaybackEnabled)
    {
        this.repository = repository; Locale = locale;
        string Data(int section, int id) => messages.Messages.Sections[section].Records[id].Text
            ?? throw new InvalidDataException($"collection.movie.data.{section}.{id:X}");
        Title = Data(1, 0x7DA); SubtitleCaption = Data(1, 0x9B); ProgressCaption = Data(1, 0x7DC);
        Sources = Array.AsReadOnly(new[] { messages.Source });
        var rows = new List<CollectionMovieRow>();
        Add(1, null, Data(0, 0x1335), true, null, false);
        var ordered = scenarios.Where(s => s.SelectionMetadata is { NativeId: < 199, FilterKind: 0 or 1 } m &&
            (m.ScenarioKindCode != 0 || m.NativeId is 8 or 25 or 63)).ToList();
        ordered.Sort(CompareScenarios);
        foreach (var scenario in ordered)
        {
            int id = scenario.SelectionMetadata!.NativeId;
            int movie = CollectionMovieDefinitions.ScenarioMovies.FirstOrDefault(pair => pair.Scenario == id).Movie;
            if (movie == 49) continue;
            bool? unlocked = progress?.ReadScenarioCompleted(id);
            string name = (id is 8 or 25) && unlocked == false ? Data(1, 0x7DB) : scenario.Title ?? "";
            // The original list retains scenarios with no movie but disables
            // their cell and writes this literal rather than a fabricated key.
            if (movie == 0) { name = "ムービー未作成"; unlocked = false; }
            Add(movie, id, name, unlocked, scenario.SourcePath, scenario.SelectionMetadata.EditionCode == 1);
        }
        bool? ending = false;
        foreach (int id in Enumerable.Range(1, 50).Concat(Enumerable.Range(55, 16)))
        {
            bool? bit = progress?.ReadScenarioCompleted(id);
            if (bit == true) { ending = true; break; }
            if (bit is null) ending = null;
        }
        Add(25, null, Data(0, 0x134D), ending, null, false);
        Rows = rows.AsReadOnly();

        void Add(int movie, int? scenario, string name, bool? unlocked, string? path, bool specialEdition)
        {
            int? badge = (scenario is int id && IsAlternateMap(id), specialEdition) switch
            { (true, true) => 0x2FB, (true, false) => 0x2F8, (false, true) => 0x2FA, _ => null };
            string language = locale == "SC" ? "ch" : "tw";
            string? badgeName = badge switch
            { 0x2F8 => "020_sho_mrk_dlc", 0x2FA => "020_sho_mrk_pk", 0x2FB => "020_sho_mrk_pk_dlc", _ => null };
            CollectionImageRoute? image = unlocked switch
            {
                true when movie is > 0 and < 48 => new($"texture_stl_movie_{(movie == 25 ? 40 : CollectionMovieDefinitions.Movies[movie].ThumbnailId):D3}",
                    $"stl_movie_{(movie == 25 ? 40 : CollectionMovieDefinitions.Movies[movie].ThumbnailId):D3}", 23,
                    movie == 25 ? 40 : CollectionMovieDefinitions.Movies[movie].ThumbnailId),
                false => new("texture_stl_event_001", "stl_event_001", 13, 1),
                _ => null
            };
            rows.Add(new(rows.Count, movie, scenario, name, unlocked, image, path,
                scenario.HasValue && movie is > 0 and < 48)
            {
                MoviePlaybackEnabled = moviePlaybackEnabled,
                BadgeSpriteId = badge,
                BadgeImage = badgeName is null ? null : new($"texture_lang_{language}",
                    $"{language}_{badgeName}", 3, badge!.Value),
                UnresolvedOwner = unlocked is null ? "collection.movie.scenario_completion_history" :
                    moviePlaybackEnabled is null ? "collection.movie.application_playback_capability" : null
            });
        }
    }

    public CollectionMoviePlayback ReadPlayback(int rowId, bool showSubtitles = true)
    {
        if ((uint)rowId >= (uint)Rows.Count) throw new ArgumentOutOfRangeException(nameof(rowId));
        var row = Rows[rowId];
        if (!row.CanPlay) throw new InvalidOperationException(row.UnresolvedOwner ??
            (row.MoviePlaybackEnabled == false ? "Movie playback is disabled by application configuration." : "The movie is locked or has no movie resource."));
        var playback = repository.LoadMoviePlayback(row.MovieId, Locale);
        bool captions = showSubtitles && row.SupportsSubtitles;
        var subtitles = captions ? repository.LoadCollectionMovieSubtitles(row.ScenarioPath!, Locale).Subtitles : [];
        return playback with
        {
            ScenarioId = row.ScenarioId,
            ScenarioPath = row.ScenarioPath,
            ShowSubtitles = captions,
            Subtitles = subtitles
        };
    }

    private static int CompareScenarios(S14Entry a, S14Entry b)
    {
        var x = a.SelectionMetadata!; var y = b.SelectionMetadata!;
        int value = (y.FilterKind == 2).CompareTo(x.FilterKind == 2);
        if (value != 0) return value;
        value = (x.SortSubtypeCode == 2).CompareTo(y.SortSubtypeCode == 2);
        if (value != 0) return value;
        value = (x.ScenarioKindCode == 0).CompareTo(y.ScenarioKindCode == 0);
        if (value != 0) return value;
        value = Nullable.Compare(a.StartYear, b.StartYear);
        if (value != 0) return value;
        value = Nullable.Compare(a.StartMonth, b.StartMonth);
        if (value != 0) return value;
        value = IsAlternateMap(x.NativeId).CompareTo(IsAlternateMap(y.NativeId));
        if (value != 0) return value;
        if (x.SortSubtypeCode == 2 && y.SortSubtypeCode == 2)
        {
            if (x.NativeId == 63 && y.NativeId == 35) return -1;
            if (x.NativeId == 35 && y.NativeId == 63) return 1;
        }
        return x.NativeId.CompareTo(y.NativeId);
    }

    // Original scenario resource selection's param0 map classification.
    private static bool IsAlternateMap(int id)
    {
        if (id is 30 or 31 or 34 or 35 or >= 21 and <= 25) return false;
        return id is >= 9 and <= 50 or >= 150 and < 200;
    }
}

public sealed partial class GameResourceRepository
{
    public CollectionMovieCatalog LoadCollectionMovieCatalog(string locale, CollectionMovieProgress? progress = null,
        bool? moviePlaybackEnabled = true)
    {
        if (locale is not ("TC" or "SC")) throw new ArgumentOutOfRangeException(nameof(locale));
        var paths = EffectiveScenarios.Where(s => s.Locale == locale).Select(s => s.SourcePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var scenarios = ScenarioDocuments.Where(s => paths.Contains(s.SourcePath)).ToArray();
        return new(this, locale, scenarios, LoadMessages(locale, MessageKind.Data), progress, moviePlaybackEnabled);
    }

}
