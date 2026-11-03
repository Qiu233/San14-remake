using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private IncomeReportNumericView? _incomeReportView;
    private GameplaySession? _incomeReportSession;
    private CampaignApplicationState? _incomeReportApplication;
    private MapSceneView? _incomeReportMap;
    private Action? _incomeReportCompleted;
    private MapInformationListView? _incomeReportDetails;
    private CityDetailsView? _incomeReportCity;
    private readonly Dictionary<int, IncomeReportNumbers> _incomeReportNumbers = [];
    private bool HasIncomeReport => _incomeReportView is not null;

    private bool OpenIncomeReport(Action completed)
    {
        ArgumentNullException.ThrowIfNull(completed);
        if (_repository is null || _session is not { PlayerForceId: not null } session ||
            session.World.Phase != TurnPhase.Reporting || _campaignComposition is not { } composition ||
            _campaignApplication is not { } application) return false;
        if (HasIncomeReport) throw new InvalidOperationException("An income report already owns this report stage.");
        var rules = composition.PrepareArmyQueries(session.World);
        var source = rules.ReadIncomeReport(session.World, _tutorialLesson?.Lesson.World ?? session.World);
        var graph = source.HasDistrictResourceHistory ? rules.ReadIncomeReportGraph(source) : null;
        _incomeReportSession = session; _incomeReportApplication = application;
        _incomeReportMap = _residentMap; _incomeReportCompleted = completed;
        AdministrationPageChrome.Resources resources = null!;
        var view = new IncomeReportNumericView { Name = "CampaignIncomeReport" };
        _incomeReportView = view;
        try
        {
            AddChild(view);
            resources = TryBorrowApplicationUiResources(view);
            RetirePeriodReportView();
            EnsureReportBackground(session, application, _incomeReportMap, view);
            view.Configure(_repository, resources.Assets, resources.SpriteShader, resources.Font,
                _ui.SourceLocale, source.Current, source.Force, Numbers, Advice, null, false,
                showDetails: district => OpenIncomeReportDetails(view, rules, source, district),
                showGraph: graph is null ? null : _ => view.ToggleGraph(), () => IncomeReportInputAdmitted(view),
                () => RunPlayAction(() => CompleteIncomeReport(view)),
                error => { CloseIncomeReport(); ReportPlayFailure(error); }, _sourceHeaders, graph);
            if (graph is null) view.SetMeta("income_history_unresolved_owner", "income-report.current.graph_history");
            _incomeReportMap?.SetDetailsOpen(true, keepMapVisible: true);
            SetMeta("play_report_overlay_visible", true);
            return true;
        }
        catch { CloseIncomeReport(); throw; }

        IncomeReportNumbers Numbers(DistrictReference district)
        {
            int physical = district.PhysicalSlot ?? throw new InvalidDataException("income-report.District_pointer");
            if (_incomeReportNumbers.TryGetValue(physical, out var known)) return known;
            var value = rules.ReadIncomeReportNumbers(source, district);
            var numbers = new IncomeReportNumbers(value.Gold, value.GoldIncome, value.GoldExpense,
                value.Food, value.FoodIncome, value.FoodExpense, value.Soldiers, value.SoldierChange,
                value.ActionPoints, value.ActionPointIncome, value.ShowActionPointIncome, value.HighlightGoldExpense);
            _incomeReportNumbers.Add(physical, numbers);
            return numbers;
        }
        string Advice(DistrictReference district)
        {
            //Only a real tab change reaches this formatter. It draws from the
            //same authoritative application stream used by other report text.
            var advice = rules.PrepareIncomeReportAdvice(source, district, _presentationRandom.Next);
            LoadedStandingPortraitSources? layers = advice.Portrait is { } portrait
                ? portrait.LoadSources(resources.Assets.Portraits.RequireSource)
                : null;
            view.ApplyPreparedStrategistPortrait(layers);
            return advice.Text;
        }
    }

    private bool IncomeReportInputAdmitted(IncomeReportNumericView view) =>
        _incomeReportDetails is null && IncomeReportContextCurrent(view);

    private bool IncomeReportContextCurrent(IncomeReportNumericView view) =>
        !_failed && !_shuttingDown && !_advancingPeriod && !IsReturningToTitle && !TutorialOverlayOwnsInput &&
        _incomeReportView == view && ReferenceEquals(_session, _incomeReportSession) &&
        ReferenceEquals(_campaignApplication, _incomeReportApplication) && _world?.Phase == TurnPhase.Reporting &&
        _detailsView is null && !HasMapSystemMenu && !HasCampaignSavePage && !HasAdministrationPage && !HasMilitaryOrderPage;

    private void RequestIncomeReportBack()
    {
        if (_incomeReportCity is { } city) city.RequestClose();
        else if (_incomeReportDetails is { } details) details.RequestBack();
        else _incomeReportView?.ProcessSourceInput(cancelRequested: true);
    }

    private void OpenIncomeReportDetails(IncomeReportNumericView parent, ArmyCampaignRuleAdapter rules,
        CampaignIncomeReportPublication source, DistrictReference district)
    {
        if (!IncomeReportInputAdmitted(parent) || _repository is null) return;
        var publication = rules.ReadIncomeReportDetails(source, district);
        var list = new MapInformationListView { Name = "IncomeReportCityDetails" };
        _incomeReportDetails = list; AddChild(list);
        try
        {
            var columns = publication.Columns.Select(column => new MapInformationListColumn(
                column.ColumnId.ToString(System.Globalization.CultureInfo.InvariantCulture), column.Caption,
                column.Width, column.CanSort, column.InitialSortAscending == true)).ToArray();
            MapInformationListRow[] Rows() => publication.Rows.Select(row => new MapInformationListRow(row.CityIndex,
                row.Cells.Select(cell => cell.Text ?? _ui.Get("detail_unknown")).ToArray(), row.CanInspect == true)).ToArray();
            IReadOnlyList<MapInformationListRow>? Sort(string key, bool ascending)
            {
                if (!int.TryParse(key, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out int column)) return null;
                var sorted = rules.SortIncomeReportDetails(publication, column, ascending);
                if (!sorted.Available)
                {
                    GD.PushWarning(sorted.UnresolvedInput);
                    list.SetMeta("sort_missing_" + column, sorted.UnresolvedInput ?? "income-report.details.sort_key");
                    return null;
                }
                publication = sorted.Value!;
                return Rows();
            }
            list.Configure(_repository, _ui, publication.Title, columns, Rows(),
                () => _incomeReportDetails == list && _incomeReportCity is null && IncomeReportContextCurrent(parent),
                () => RunPlayAction(CloseIncomeReportDetails),
                city => RunPlayAction(() => OpenIncomeReportCity(parent, list, rules, source, city)), CurrentListScrollSpeed,
                sort: Sort, tabCaptions: [publication.TabCaption], completeColumnProfile: true, normalColumnLayout: true,
                narrowTable: true, retainSortDirections: true);
            list.RegisterHeader(_sourceHeaders);
            foreach (var column in publication.Columns)
                if (column.SortUnresolvedOwner is { } missing) list.SetMeta("sort_missing_" + column.ColumnId, missing);
            parent.Hide();
        }
        catch { CloseIncomeReportDetails(); throw; }
    }

    private void OpenIncomeReportCity(IncomeReportNumericView parent, MapInformationListView list,
        ArmyCampaignRuleAdapter rules, CampaignIncomeReportPublication source, int city)
    {
        if (_incomeReportDetails != list || _incomeReportCity is not null || !IncomeReportContextCurrent(parent) ||
            _repository is null || _incomeReportSession is not { } session || _selectedSnapshot is null) return;
        var view = new CityDetailsView(); _incomeReportCity = view; AddChild(view);
        try
        {
            ConfigureCurrentCityDetail(view, session.World, city, () => RunPlayAction(CloseIncomeReportCity),
                preparedRules: rules, report: source);
            view.RegisterHeader(_sourceHeaders); list.Hide();
        }
        catch { CloseIncomeReportCity(); throw; }
    }

    private void CloseIncomeReportCity()
    {
        var city = _incomeReportCity; _incomeReportCity = null;
        if (city is not null && GodotObject.IsInstanceValid(city))
        { city.Hide(); city.GetParent()?.RemoveChild(city); city.QueueFree(); }
        if (_incomeReportDetails is { } list && GodotObject.IsInstanceValid(list)) list.Show();
    }

    private void CloseIncomeReportDetails()
    {
        CloseIncomeReportCity();
        var list = _incomeReportDetails; _incomeReportDetails = null;
        if (list is not null && GodotObject.IsInstanceValid(list))
        { list.Abandon(); list.GetParent()?.RemoveChild(list); list.QueueFree(); }
        if (_incomeReportView is { } parent && GodotObject.IsInstanceValid(parent)) parent.Show();
    }

    private void CompleteIncomeReport(IncomeReportNumericView view)
    {
        if (_incomeReportView != view || !ReferenceEquals(_session, _incomeReportSession)) return;
        var completed = _incomeReportCompleted;
        CloseIncomeReport();
        //682DD0's actual confirm closes this dialog and sets tutorial event16.
        //Retirement/cancellation never calls this completion path.
        ObserveTutorialIncomeReportAcknowledged();
        completed?.Invoke();
    }

    private void CloseIncomeReport()
    {
        var view = _incomeReportView; _incomeReportView = null;
        CloseIncomeReportDetails();
        _incomeReportCompleted = null;
        if (!_failed && !_shuttingDown && ReferenceEquals(_session, _incomeReportSession) &&
            ReferenceEquals(_campaignApplication, _incomeReportApplication) &&
            _incomeReportMap is { } map && _residentMap == map && GodotObject.IsInstanceValid(map) && map.IsInsideTree())
            map.SetDetailsOpen(false);
        _incomeReportSession = null; _incomeReportApplication = null; _incomeReportMap = null;
        if (view is not null && GodotObject.IsInstanceValid(view))
        {
            view.Abandon();
            if (view.GetParent() is Node parent) parent.RemoveChild(view);
            if (!view.IsQueuedForDeletion()) view.QueueFree();
        }
        _incomeReportNumbers.Clear();
        if (!HasActionReport) CloseReportBackground();
    }
}
