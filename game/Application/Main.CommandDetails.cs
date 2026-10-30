using Godot;
using San14.Client.Gameplay;

public partial class Main
{
    private DetailsView? _commandDetailsView;
    private CommandWorkspaceView? _commandDetailsParent;
    private ActionReportView? _commandDetailsReportParent;
    private UnitOrderTargetView? _commandDetailsUnitParent;
    private readonly Stack<DetailsView> _commandDetailsNestedParents = new();
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
        OpenCommandDetails(view, close => ConfigureCurrentForceDetail(view, world, force, close));
    }

    private void OpenCommandCityDetails(int city)
    {
        if (_repository is null || _world is not { } world || _selectedSnapshot is null || !world.Cities.ContainsKey(city)) return;
        var view = new CityDetailsView();
        OpenCommandDetails(view, close => ConfigureCurrentCityDetail(view, world, city, close));
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
        _commandDetailsAdoptSpeechWorld = null;
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
        => OpenNestedCommandDetails(parent, view, configure);

    private void OpenNestedCommandDetails(DetailsView parent, DetailsView view, Action<Action> configure)
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || _commandDetailsView != parent ||
            (_commandDetailsUnitParent is not null && _commandDetailsUnitParent != _unitOrderTarget) ||
            !ReferenceEquals(_session, _commandDetailsSession) || !ReferenceEquals(_world, _commandDetailsWorld) ||
            _residentMap != _commandDetailsMap)
        { view.Free(); return; }
        _commandDetailsNestedParents.Push(parent);
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
        while (_commandDetailsNestedParents.TryPop(out var retained))
        {
            view.Hide(); view.GetParent()?.RemoveChild(view); view.QueueFree();
            _commandDetailsView = retained; _detailsView = retained;
            bool stillCurrent = ReferenceEquals(_session, _commandDetailsSession) &&
                ReferenceEquals(_world, _commandDetailsWorld) && _residentMap == _commandDetailsMap &&
                (_commandDetailsUnitParent is null || _commandDetailsUnitParent == _unitOrderTarget);
            if (resumeParent && !_failed && !_shuttingDown && stillCurrent)
            { retained.ProcessMode = ProcessModeEnum.Inherit; retained.Show(); return; }
            view = retained;
        }
        var parent = _commandDetailsParent; var session = _commandDetailsSession;
        var report = _commandDetailsReportParent;
        var unitParent = _commandDetailsUnitParent;
        var world = _commandDetailsWorld; var map = _commandDetailsMap;
        _commandDetailsView = null; _commandDetailsParent = null; _commandDetailsSession = null;
        _commandDetailsWorld = null; _commandDetailsMap = null;
        _commandDetailsAdoptSpeechWorld = null;
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
