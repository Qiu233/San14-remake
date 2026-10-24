using System.Collections.Immutable;

namespace San14.Core;

public sealed record CampaignActionReportCategory(byte Category, ImmutableArray<CampaignHistoryRecord> Records,
    ImmutableArray<string> StrategistPages, bool ListCompleted, bool StrategistCompleted, bool UsesEmptyFallback = false);
public sealed record CampaignActionReportContent(ImmutableArray<CampaignActionReportCategory> Categories,
    int? StrategistPersonId, bool HistoryScanAndSortCompleted, bool? StrategistPortraitVisible = null,
    ushort? StrategistPortraitAppearance = null);
/// <summary>Persisted presentation input for one reporting period. Advice is
/// generated on actual category selection, never on redraw or eager prefetch.</summary>
public sealed record PreparedCampaignActionReport(int PeriodNumber, CampaignActionReportContent Content,
    bool Completed, string? Missing)
{
    public byte SelectedCategory { get; init; } = 1;
}
public sealed record CampaignActionReportPreparationResult(GameState State, PreparedCampaignActionReport Report);

public interface ICampaignActionReportOperation
{
    GameState State { get; }
    CampaignActionReportContent Content { get; }
    byte SelectedCategory { get; }
    bool Completed { get; }
    string? Missing { get; }
    void Prepare();
}
public interface ICampaignActionReportSource
{
    ICampaignActionReportOperation BeginActionReport(GameState state);
    ICampaignActionReportOperation BeginActionReportCategory(GameState state,
        PreparedCampaignActionReport report, byte category);
}
public sealed partial record GameState
{
    public PreparedCampaignActionReport? PreparedActionReport { get; init; }
}

public sealed partial class GameSimulation
{
    public CampaignActionReportPreparationResult PrepareActionReport(ICampaignActionReportSource source,
        ICampaignAppRandomBinding? appRandom = null) => RunActionReport(source, null, appRandom);

    public CampaignActionReportPreparationResult SelectActionReportCategory(ICampaignActionReportSource source,
        byte category, ICampaignAppRandomBinding? appRandom = null) => RunActionReport(source, category, appRandom);

    private CampaignActionReportPreparationResult RunActionReport(ICampaignActionReportSource source,
        byte? category, ICampaignAppRandomBinding? appRandom)
    {
        ArgumentNullException.ThrowIfNull(source);
        RequireNoProgress();
        if (State.Phase != TurnPhase.Reporting) throw new InvalidOperationException("Action reports require the reporting phase.");
        if (category > 6) throw new ArgumentOutOfRangeException(nameof(category));
        var retained = State.PreparedActionReport;
        if (retained is not null && retained.PeriodNumber != State.PeriodNumber)
            throw new InvalidDataException("The prepared action report belongs to another period.");
        if (retained is not null && (category is null || category == retained.SelectedCategory)) return new(State, retained);
        if (category.HasValue && retained is null) throw new InvalidOperationException("Action report lists have not been prepared.");
        //The saved value is a private snapshot, never an alternative live App.
        //Without a live binding the formatter reaches its real missing draw.
        var previousSnapshot = State.CampaignAppRandom32;
        var input = State with { CampaignAppRandom32 = appRandom is null ? null : new(appRandom.ReadCurrentState()) };
        var operation = category is byte selected ? source.BeginActionReportCategory(input, retained!, selected) : source.BeginActionReport(input);
        try { operation.Prepare(); }
        finally
        {
            var prepared = new PreparedCampaignActionReport(State.PeriodNumber, operation.Content,
                operation.Completed, operation.Missing) { SelectedCategory = operation.SelectedCategory };
            var reached = operation.State;
            if (appRandom is null) reached = reached with { CampaignAppRandom32 = previousSnapshot };
            State = reached with { PreparedActionReport = prepared };
            if (appRandom is not null && reached.CampaignAppRandom32 is { } committed)
                appRandom.InstallCommittedState(committed.State);
        }
        return new(State, State.PreparedActionReport!);
    }
}
