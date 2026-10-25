using Godot;
using San14.Client.Gameplay;

public partial class Main
{
    private DetailsView? _commandDetailsView;
    private CommandWorkspaceView? _commandDetailsParent;
    private ActionReportView? _commandDetailsReportParent;
    private UnitOrderTargetView? _commandDetailsUnitParent;
    private ArmyDetailsListView? _commandDetailsArmyListParent;
    private GameplaySession? _commandDetailsSession;
    private San14.Core.GameState? _commandDetailsWorld;
    private MapSceneView? _commandDetailsMap;
    private ProcessModeEnum _commandDetailsBrowserMode;

    private void OpenCommandOfficerDetails(int officer)
    {
        if (_repository is null || _world is not { } world || _selectedSnapshot is null || !world.Officers.ContainsKey(officer)) return;
        var view = new OfficerDetailsView();
        OpenCommandDetails(view, close => ConfigureCurrentOfficerDetail(view, world, officer, close,
            () => _commandDetailsView == view && _detailsView == view));
    }

    private void OpenCommandForceDetails(int force)
    {
        if (_repository is null || _world is not { } world || _selectedSnapshot is null || !world.Forces.ContainsKey(force)) return;
        var view = new ForceDetailsView();
        OpenCommandDetails(view, close => view.Configure(_repository,
            CampaignDetailViews.Force(world, _selectedSnapshot, force, _ui,
                _campaignComposition?.ForceDetailNameplate(world, force),
                _campaignComposition?.PrepareArmyQueries(world).ReadForceDetailMiniMap(world, force)), _ui, close));
    }

    private void OpenCommandCityDetails(int city)
    {
        if (_repository is null || _world is not { } world || _selectedSnapshot is null || !world.Cities.ContainsKey(city)) return;
        var view = new CityDetailsView();
        OpenCommandDetails(view, close => view.Configure(_repository,
            CampaignDetailViews.City(world, _selectedSnapshot, city, _ui, CurrentCityCapacity(city),
                CurrentCityGeography(city), CurrentCityForce(city), CurrentCityMiniMap(city)), _ui, close));
    }

    private void OpenCommandDetails(DetailsView view, Action<Action> configure, UnitOrderTargetView? unitParent = null)
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || _detailsView is not null ||
            HasMapSystemMenu || HasCampaignSavePage || HasAdministrationPage ||
            (HasMilitaryOrderPage && (unitParent is null || unitParent != _unitOrderTarget || _unitOrderConfirmation is not null)) ||
            HasPersonnelConfirmation || _periodReportView is not null ||
            _session is not { PlayerForceId: not null } session)
        { view.Free(); return; }
        _commandDetailsView = view; _detailsView = view;
        _commandDetailsParent = _commandWorkspace; _commandDetailsSession = session; _commandDetailsWorld = session.World;
        _commandDetailsReportParent = _actionReportView;
        _commandDetailsUnitParent = unitParent;
        _commandDetailsMap = _residentMap; _commandDetailsBrowserMode = _browserContent.ProcessMode;
        AddChild(view);
        try
        {
            if (unitParent is not null)
            { unitParent.Hide(); unitParent.ProcessMode = ProcessModeEnum.Disabled; _residentMap?.SetDetailsOpen(true, keepMapVisible: true); }
            else if (_commandDetailsParent is { } parent) { parent.Hide(); parent.ProcessMode = ProcessModeEnum.Disabled; }
            else if (_commandDetailsReportParent is { } report) { report.Hide(); report.ProcessMode = ProcessModeEnum.Disabled; }
            else _residentMap?.SetDetailsOpen(true, keepMapVisible: true);
            _browserContent.ProcessMode = ProcessModeEnum.Disabled;
            configure(() => { if (_commandDetailsView == view) CloseCommandDetails(); });
            view.RegisterHeader(_sourceHeaders);
        }
        catch (Exception error)
        {
            CloseCommandDetails();
            ReportPlayFailure(error);
        }
    }

    private void OpenArmyListMemberDetails(ArmyDetailsListView parent, ArmyDetailsView view, Action<Action> configure)
    {
        if (_failed || _shuttingDown || IsReturningToTitle || _commandDetailsView != parent ||
            _commandDetailsArmyListParent is not null || _commandDetailsUnitParent != _unitOrderTarget ||
            !ReferenceEquals(_session, _commandDetailsSession) || !ReferenceEquals(_world, _commandDetailsWorld) ||
            _residentMap != _commandDetailsMap)
        { view.Free(); return; }
        _commandDetailsArmyListParent = parent;
        _commandDetailsView = view; _detailsView = view;
        parent.Hide(); parent.ProcessMode = ProcessModeEnum.Disabled;
        AddChild(view);
        try
        {
            configure(() => { if (_commandDetailsView == view) CloseCommandDetails(); });
            view.RegisterHeader(_sourceHeaders);
        }
        catch (Exception error) { CloseCommandDetails(); ReportPlayFailure(error); }
    }

    private void CloseCommandDetails(bool resumeParent = true)
    {
        if (_commandDetailsView is not { } view) return;
        if (_commandDetailsArmyListParent is { } list)
        {
            _commandDetailsArmyListParent = null;
            view.Hide(); view.GetParent()?.RemoveChild(view); view.QueueFree();
            _commandDetailsView = list; _detailsView = list;
            bool stillCurrent = ReferenceEquals(_session, _commandDetailsSession) &&
                ReferenceEquals(_world, _commandDetailsWorld) && _residentMap == _commandDetailsMap &&
                _commandDetailsUnitParent == _unitOrderTarget;
            if (resumeParent && !_failed && !_shuttingDown && stillCurrent)
            { list.ProcessMode = ProcessModeEnum.Inherit; list.Show(); return; }
            view = list;
        }
        var parent = _commandDetailsParent; var session = _commandDetailsSession;
        var report = _commandDetailsReportParent;
        var unitParent = _commandDetailsUnitParent;
        var world = _commandDetailsWorld; var map = _commandDetailsMap;
        _commandDetailsView = null; _commandDetailsParent = null; _commandDetailsSession = null;
        _commandDetailsWorld = null; _commandDetailsMap = null;
        _commandDetailsReportParent = null;
        _commandDetailsUnitParent = null;
        if (_detailsView == view) _detailsView = null;
        view.Hide();
        if (view.GetParent() is Node owner) owner.RemoveChild(view);
        view.QueueFree();
        _browserContent.ProcessMode = _commandDetailsBrowserMode;
        if (!resumeParent || _failed || _shuttingDown) return;
        bool current = ReferenceEquals(_session, session) && ReferenceEquals(_world, world) && _residentMap == map;
        if (unitParent is not null && _unitOrderTarget == unitParent)
        {
            if (current)
            { unitParent.ProcessMode = ProcessModeEnum.Inherit; unitParent.Show(); map?.SetDetailsOpen(false, keepMapVisible: true); }
            else CloseUnitRetarget();
        }
        else if (parent is not null && _commandWorkspace == parent)
        {
            if (current) { parent.ProcessMode = ProcessModeEnum.Inherit; parent.Show(); }
            else CloseCommandWorkspace();
        }
        else if (report is not null && _actionReportView == report && current)
        { report.ProcessMode = ProcessModeEnum.Inherit; report.Show(); }
        else if (current && map is not null && GodotObject.IsInstanceValid(map) && map.IsInsideTree()) map.SetDetailsOpen(false);
    }
}
