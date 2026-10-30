namespace San14.Data;

/// <summary>Application scenario-completion history, independent of campaign saves.
/// A missing bit is unknown; an explicitly new profile has no completed scenarios.</summary>
public sealed class CollectionMovieProgress
{
    private readonly bool?[] completed;
    public CollectionMovieProgress(IReadOnlyList<bool?> completedScenarios)
    {
        ArgumentNullException.ThrowIfNull(completedScenarios);
        if (completedScenarios.Count != 200) throw new ArgumentException("Movie progress requires 200 scenario bits.", nameof(completedScenarios));
        completed = completedScenarios.ToArray();
    }
    public static CollectionMovieProgress CreateNew() => new(Enumerable.Repeat<bool?>(false, 200).ToArray());
    public bool? ReadScenarioCompleted(int scenarioId) => (uint)scenarioId < 200
        ? completed[scenarioId] : throw new ArgumentOutOfRangeException(nameof(scenarioId));
}

public sealed record CollectionMovieRow(int RowId, int MovieId, int? ScenarioId,
    string Name, bool? Unlocked, CollectionImageRoute? Thumbnail,
    string? ScenarioPath, bool SupportsSubtitles)
{
    public string? UnresolvedOwner { get; init; }
    public bool? MoviePlaybackEnabled { get; init; }
    public int? BadgeSpriteId { get; init; }
    public CollectionImageRoute? BadgeImage { get; init; }
    public bool CanPlay => MoviePlaybackEnabled == true && Unlocked == true && MovieId is > 0 and < 48;
}

public sealed record CollectionMovieSubtitle(int Index, int StartMs, int EndMs,
    string AlreadyExpandedText);

public sealed record LoadedCollectionMovieSubtitles(IReadOnlyList<CollectionMovieSubtitle> Subtitles,
    SourceProvenance Source);

/// <summary>Normal WMV and optional resource-backed scenario captions. No
/// campaign is created or modified to play the title's static movie.</summary>
public sealed record CollectionMoviePlayback(int MovieId, string ResourceUri,
    string PhysicalPath, bool IsOverride, int? ScenarioId,
    string? ScenarioPath, bool ShowSubtitles, IReadOnlyList<CollectionMovieSubtitle> Subtitles,
    int RestoreScenarioId = 8)
{
    public string SubtitleLayoutName => "cmn_movie";
    public int SubtitleNodeIndex => 2;
    public int SubtitleTextPreset => 0x47;
    public string SubtitleEnterClip => "in";
    public string SubtitleHoldClip => "loop";
    public string SubtitleExitClip => "out";
}
