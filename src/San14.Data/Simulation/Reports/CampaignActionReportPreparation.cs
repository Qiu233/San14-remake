using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public ICampaignActionReportSource CreateActionReportSource(ActionReportApplicationInputs application)
        => new CampaignActionReportSource(this, application);

    private sealed class CampaignActionReportSource(ArmyCampaignRuleAdapter package,
        ActionReportApplicationInputs application) : ICampaignActionReportSource
    {
        public ICampaignActionReportOperation BeginActionReport(GameState state)
            => new CampaignActionReportOperation(package, application, state, null, 1);
        public ICampaignActionReportOperation BeginActionReportCategory(GameState state,
            PreparedCampaignActionReport report, byte category)
            => new CampaignActionReportOperation(package, application, state, report, category);
    }

    /// <summary>One initial preparation or one actual tab selection. It owns
    /// every reached history/Area/random prefix and stable local display input.</summary>
    private sealed class CampaignActionReportOperation : ICampaignActionReportOperation
    {
        private static readonly byte[] order = [1, 2, 3, 4, 5, 6, 0];
        private readonly ArmyCampaignRuleAdapter package;
        private readonly ActionReportApplicationInputs application;
        private readonly PreparedCampaignActionReport? retained;
        private readonly ActionReportLocalRecords local = new();
        private readonly Dictionary<byte, ActionReportPreparedList> lists = [];
        private readonly Dictionary<byte, ActionReportStrategistSnapshot> advice = [];
        private ActionReportPreparationOperation? preparation;
        private SourceWorldContext? current;
        private WorldIndexContext? pool;
        private bool entered;
        public GameState State { get; private set; }
        public byte SelectedCategory { get; }
        public bool Completed { get; private set; }
        public string? Missing { get; private set; }

        internal CampaignActionReportOperation(ArmyCampaignRuleAdapter package, ActionReportApplicationInputs application,
            GameState state, PreparedCampaignActionReport? retained, byte category)
        {
            this.package = package; this.application = application; State = state;
            this.retained = retained; SelectedCategory = category;
        }

        public CampaignActionReportContent Content
        {
            get
            {
                var historical = preparation?.HistorySnapshot;
                var rows = ImmutableArray.CreateBuilder<CampaignActionReportCategory>(7);
                foreach (byte category in order)
                {
                    var old = retained?.Content.Categories.FirstOrDefault(value => value.Category == category);
                    ImmutableArray<CampaignHistoryRecord> records;
                    bool complete, fallback;
                    if (lists.TryGetValue(category, out var ready))
                    { records = ready.Records; complete = true; fallback = ready.UsesEmptyFallback; }
                    else if (old is not null)
                    { records = old.Records; complete = old.ListCompleted; fallback = old.UsesEmptyFallback; }
                    else
                    {
                        var prefix = historical?.Buckets.FirstOrDefault(value => value.Category == category)?.Records
                            .Select(value => value.Record) ?? [];
                        records = prefix.Concat(local.Records.Where(value => value.Category == category))
                            .Concat(local.ArmySupplyAggregate is { } aggregate && aggregate.Category == category ? new[] { aggregate } : [])
                            .ToImmutableArray();
                        complete = false; fallback = false;
                    }
                    bool hasAdvice = advice.TryGetValue(category, out var reached);
                    var pages = hasAdvice ? reached!.Pages.Select(value => value.Text).ToImmutableArray() : old?.StrategistPages ?? [];
                    rows.Add(new(category, records, pages, complete,
                        hasAdvice ? reached!.Completed : old?.StrategistCompleted ?? false, fallback));
                }
                advice.TryGetValue(SelectedCategory, out var selected);
                return new(rows.MoveToImmutable(), selected?.Speaker?.PersonId ?? retained?.Content.StrategistPersonId,
                    historical?.HistoryScanAndSortCompleted ?? retained?.Content.HistoryScanAndSortCompleted ?? false,
                    selected?.PortraitVisible ?? retained?.Content.StrategistPortraitVisible,
                    selected?.PortraitAppearance ?? retained?.Content.StrategistPortraitAppearance);
            }
        }

        public void Prepare()
        {
            if (entered) throw new InvalidOperationException("The action report operation was already entered.");
            entered = true;
            try
            {
                if (retained is null)
                {
                    preparation = package.BeginActionReportPreparation(State, application);
                    current = preparation.Current; pool = preparation.Pool;
                    try
                    {
                        preparation.ConsumeHistory();
                        preparation.PrepareSyntheticRecords(local);
                    }
                    finally { State = preparation.State; }
                    //Only the fully returned eight-stage operation can reach
                    //actual complete-list filtering and the original5007 fallback.
                    if (!preparation.SyntheticPreparationCompleted) throw Incomplete("synthetic_preparation");
                    var history = preparation.HistorySnapshot;
                    foreach (byte category in order)
                    {
                        var records = history.Buckets.Single(value => value.Category == category).Records.Select(value => value.Record)
                            .Concat(local.Records.Where(value => value.Category == category))
                            .Concat(local.ArmySupplyAggregate is { } aggregate && aggregate.Category == category ? new[] { aggregate } : [])
                            .ToArray();
                        lists[category] = preparation.PrepareKnownList(category, records);
                    }
                }
                else
                {
                    var selected = retained.Content.Categories.FirstOrDefault(value => value.Category == SelectedCategory)
                        ?? throw Incomplete("selected_category");
                    if (!selected.ListCompleted) throw Incomplete("selected_list");
                    lists[SelectedCategory] = new(SelectedCategory, selected.Records, selected.UsesEmptyFallback);
                    current = package.CaptureReportContentCurrent(State);
                    pool = current.ReadOwnedCurrentListPool() ?? throw Incomplete("current_pool");
                }
                var operation = package.BeginActionReportStrategistPages(State, current!, pool!, lists[SelectedCategory]);
                try { operation.Produce(); }
                finally { State = operation.State; advice[SelectedCategory] = operation.Snapshot; }
                Completed = true;
            }
            catch (InvalidDataException error) { Missing = error.Message; }
        }
        private static InvalidDataException Incomplete(string field) => new("action-report.content." + field);
    }
}
