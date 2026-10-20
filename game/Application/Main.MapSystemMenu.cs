using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private MapSystemMenuView? _mapSystemMenu;
    private DeploymentTargetConfirmation? _mapSystemConfirmation;
    private GameplaySession? _mapSystemSession;
    private GameState? _mapSystemWorld;
    private MapSceneView? _mapSystemMap;
    private bool HasMapSystemMenu => _mapSystemMenu is not null;

    private void OpenMapSystemMenu()
    {
        if (_failed || _shuttingDown || HasMapSystemMenu || HasCampaignSavePage || HasAdministrationPage || HasDeploymentPage ||
            _detailsView is not null || _periodReportView is not null || !_mapFocused || _repository is null ||
            _residentMap is not { IsPlayableMapReady: true } map || _session is not { PlayerForceId: not null } session) return;
        var world = session.World;
        var view = new MapSystemMenuView();
        _mapSystemMenu = view; _mapSystemSession = session; _mapSystemWorld = world; _mapSystemMap = map;
        AddChild(view);
        try
        {
            map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true); SetMapFocus(true);
            // CConfigDlg's actual caller supplies top-left0/0. Its control
            // transform replaces the authored root translation.
            view.Configure(_repository, _ui, 0x0473, Vector2.Zero, CloseMapSystemMenu, command =>
            {
                if (!Current() || HasCampaignSavePage || _mapSystemConfirmation is not null) return;
                if (command == MapSystemCommand.Exit) RequestExit();
                else
                {
                    OpenCampaignSaves(command == MapSystemCommand.Save);
                    if (HasCampaignSavePage) view.Hide();
                }
            });
        }
        catch { CloseMapSystemMenu(); throw; }

        bool Current() => !_failed && !_shuttingDown && _mapSystemMenu == view && _residentMap == map &&
            ReferenceEquals(_session, session) && ReferenceEquals(session.World, world) && ReferenceEquals(_world, world);

        void RequestExit()
        {
            view.SetBusy(true); view.Hide();
            var question = new DeploymentTargetConfirmation(); _mapSystemConfirmation = question; AddChild(question);
            try
            {
                question.ConfigureGameMessage(_repository!, _ui, 0x2727, confirmed =>
                {
                    if (!Current() || _mapSystemConfirmation != question) return;
                    _mapSystemConfirmation = null; question.Abandon(); question.QueueFree();
                    if (confirmed) WindowCloseRequested();
                    else { view.SetBusy(false); view.Show(); }
                });
                question.Name = "MapSystemExitConfirmation";
            }
            catch
            {
                _mapSystemConfirmation = null; question.Abandon(); question.QueueFree();
                view.SetBusy(false); view.Show(); throw;
            }
        }
    }

    private void CloseMapSystemMenu()
    {
        if (_mapSystemMenu is null && _mapSystemMap is null) return;
        var question = _mapSystemConfirmation; _mapSystemConfirmation = null;
        if (question is not null) { question.Abandon(); question.QueueFree(); }
        CloseCampaignSaves();
        var map = _mapSystemMap; _mapSystemMap = null;
        _mapSystemSession = null; _mapSystemWorld = null;
        var view = _mapSystemMenu; _mapSystemMenu = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        if (map is not null && GodotObject.IsInstanceValid(map) && _residentMap == map) map.SetDetailsOpen(false);
        if (!_failed && !_shuttingDown) SetMapFocus(_mapFocused);
    }

    private bool ResumeMapSystemMenu()
    {
        if (_mapSystemMenu is not { } view || _session is not { } session ||
            !ReferenceEquals(_mapSystemSession, session) || !ReferenceEquals(_mapSystemWorld, session.World) ||
            !ReferenceEquals(_world, session.World) || _residentMap != _mapSystemMap) return false;
        view.Show(); return true;
    }
}
