namespace San14.Core;

public sealed record AreaDevelopmentValues(int? Agriculture, int? Commerce, int? Barracks);

public sealed partial record AreaState
{
    // Null is unresolved current progress. It is never filled from a UI level or
    // from an absent legacy snapshot while loading a save.
    public AreaDevelopmentValues? Development { get; init; }
    public int? Security { get; init; }
}

/// <summary>Source identity remains independent from the engine's event key.</summary>
public sealed record OriginalEventSourceReference(string ResourceUri, string ResourceSha256,
    int DirectoryIndex, int EventId);

public sealed record PlayableEventExecutionStamp(string Revision, int LastPeriod,
    long ExecutionCount);

public sealed record PlayableEventRuntimeState(int LastSettledPeriod,
    Dictionary<string, string> SourceRevisions,
    Dictionary<string, PlayableEventExecutionStamp> Executions,
    Dictionary<string, int> Flags, Dictionary<string, long> Counters)
{
    public static PlayableEventRuntimeState Empty(int currentPeriod) =>
        new(currentPeriod, new(StringComparer.Ordinal), new(StringComparer.Ordinal),
            new(StringComparer.Ordinal), new(StringComparer.Ordinal));
}

public sealed record PlayableEventReportEntry(string Kind, string EventKey,
    string Revision, int Period, int? ForceId = null, int? TargetId = null,
    int Amount = 0, string? MessageKey = null, string? RuntimeKey = null,
    OriginalEventSourceReference? Source = null, int? SourceOperationIndex = null);

public sealed partial record GameState
{
    public PlayableEventRuntimeState? EventRuntime { get; init; }
    public PlayableEventReportEntry[]? EventReports { get; init; }
}

public static class PlayableEventStateValidation
{
    public static void Validate(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (AreaState area in state.Areas.Values)
        {
            if (area.Security is < 0 or > 100)
                throw new InvalidDataException($"Area {area.Id} current security is invalid.");
            if (area.Development is { } values &&
                (values.Agriculture is < 0 or > 1000 || values.Commerce is < 0 or > 1000 ||
                 values.Barracks is < 0 or > 1000))
                throw new InvalidDataException($"Area {area.Id} current development is invalid.");
        }
        if (state.EventRuntime is { } runtime)
        {
            if (runtime.LastSettledPeriod < 0 || runtime.LastSettledPeriod > state.PeriodNumber ||
                runtime.SourceRevisions is null || runtime.Executions is null ||
                runtime.Flags is null || runtime.Counters is null)
                throw new InvalidDataException("Event runtime is incomplete or has an invalid period.");
            foreach ((string key, string revision) in runtime.SourceRevisions)
                if (!ValidKey(key) || !ValidKey(revision))
                    throw new InvalidDataException("Event source revision is invalid.");
            foreach ((string key, PlayableEventExecutionStamp stamp) in runtime.Executions)
                if (!ValidKey(key) || stamp is null || !ValidKey(stamp.Revision) ||
                    stamp.LastPeriod < 0 || stamp.LastPeriod > runtime.LastSettledPeriod ||
                    stamp.ExecutionCount < 1 ||
                    !runtime.SourceRevisions.TryGetValue(key, out string? revision) || revision != stamp.Revision)
                    throw new InvalidDataException("Event execution stamp is invalid.");
            if (runtime.Flags.Keys.Any(key => !ValidKey(key)) || runtime.Counters.Keys.Any(key => !ValidKey(key)))
                throw new InvalidDataException("Event flag or counter key is invalid.");
        }
        foreach (PlayableEventReportEntry report in state.EventReports ?? [])
        {
            if (report is null || !ValidKey(report.Kind) || !ValidKey(report.EventKey) ||
                !ValidKey(report.Revision) || report.Period < 0 || report.Period > state.PeriodNumber ||
                (report.ForceId is int force && !state.Forces.ContainsKey(force)) ||
                report.TargetId < 0 || report.SourceOperationIndex < 0 ||
                (report.MessageKey is not null && !ValidKey(report.MessageKey)) ||
                (report.RuntimeKey is not null && !ValidKey(report.RuntimeKey)))
                throw new InvalidDataException("Event report is invalid.");
            ValidateSource(report.Source);
        }
    }

    public static void ValidateSource(OriginalEventSourceReference? source)
    {
        if (source is null) return;
        if (!ValidKey(source.ResourceUri) || !Uri.TryCreate(source.ResourceUri, UriKind.Absolute, out _) ||
            source.ResourceSha256 is not { Length: 64 } ||
            source.ResourceSha256.Any(c => !Uri.IsHexDigit(c)) || source.DirectoryIndex < 0 || source.EventId < 0)
            throw new InvalidDataException("Event source reference is invalid.");
    }

    internal static bool ValidKey(string? key) => !string.IsNullOrWhiteSpace(key) && key.Length <= 1024;
}
