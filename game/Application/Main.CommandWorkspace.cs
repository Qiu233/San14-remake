using Godot;
using San14.Client.Gameplay;

public partial class Main
{
    private CommandWorkspaceView? _commandWorkspace;
    private GameplaySession? _commandWorkspaceSession;
    private MapSceneView? _commandWorkspaceMap;
    private bool _commandBrowserVisible;
    private ProcessModeEnum _commandBrowserProcess;
    private Button _playCommands = null!;
    private bool HasCommandWorkspace => _commandWorkspace is not null;

    private void OpenCommandWorkspace(PlayableCommandDeck.Page page)
    {
        if (!DeveloperWorkspaceEnabled || _failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || HasCommandWorkspace ||
            HasMapSystemMenu || HasCampaignSavePage || HasAdministrationPage || HasMilitaryOrderPage ||
            _detailsView is not null || _periodReportView is not null || HasActionReport || HasPersonnelConfirmation || _repository is null ||
            _session is not { PlayerForceId: not null } session) return;
        RefreshPlayControls();
        _playCommandDeck.Select(page);
        var map = _residentMap;
        var view = new CommandWorkspaceView();
        _commandWorkspace = view; _commandWorkspaceSession = session; _commandWorkspaceMap = map;
        _commandBrowserVisible = _browserContent.Visible;
        _commandBrowserProcess = _browserContent.ProcessMode;
        AddChild(view);
        try
        {
            map?.CloseCityCommandMenu(); map?.SetDetailsOpen(true, keepMapVisible: true);
            view.Configure(_repository, _ui, _playCommandDeck,
                [(Control)_playCity.GetParent(), _playTarget, _playStatus, _playMessage], CloseCommandWorkspace);
            _browserContent.Hide(); _browserContent.ProcessMode = ProcessModeEnum.Disabled;
            SetMapFocus(_mapFocused);
        }
        catch { CloseCommandWorkspace(); throw; }
    }

    private void CloseCommandWorkspace()
    {
        ClosePersonnelConfirmation(resumeParent: false);
        CloseCommandDetails(resumeParent: false);
        if (_commandWorkspace is not { } view) return;
        _commandWorkspace = null; _commandWorkspaceSession = null;
        var map = _commandWorkspaceMap; _commandWorkspaceMap = null;
        view.Abandon(); view.QueueFree();
        _browserContent.Visible = _commandBrowserVisible;
        _browserContent.ProcessMode = _commandBrowserProcess;
        if (!_failed && !_shuttingDown && map is not null && GodotObject.IsInstanceValid(map) &&
            map.IsInsideTree() && _residentMap == map) map.SetDetailsOpen(false);
        if (!_failed && !_shuttingDown) SetMapFocus(_mapFocused);
    }
}
