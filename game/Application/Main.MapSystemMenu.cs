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
    private CancellationTokenSource? _mapSystemPreparation;
    private bool _mapSystemPresentationActive, _mapSystemClosing;
    private bool HasMapSystemMenu => _mapSystemMenu is not null;
    private Button _playSystemMenu = null!;

    private void AdoptMapSystemSpeechWorld(GameplaySession session, GameState previous, GameState replacement)
    {
        // Only the synchronous speech transaction may move these retained UI
        // owners. A gameplay commit or replaced session must still retire them.
        if (_mapSystemMenu is null || _mapSystemSession != session ||
            _mapSystemMap != _residentMap || !ReferenceEquals(_mapSystemWorld, previous)) return;
        _mapSystemWorld = replacement;
        if (_campaignSaveView is not null && _campaignSaveSession == session && _campaignSaveMap == _residentMap &&
            ReferenceEquals(_campaignSaveWorld, previous)) _campaignSaveWorld = replacement;
        if (HasCurrentWorldEditors && _worldEditSession == session && _worldEditMap == _residentMap &&
            ReferenceEquals(_worldEditWorld, previous)) _worldEditWorld = replacement;
    }

    private async void OpenMapSystemMenu()
    {
        if (_failed || _shuttingDown || IsReturningToTitle || HasMapSystemMenu || HasCampaignSavePage || HasAdministrationPage || HasMilitaryOrderPage || HasCommandWorkspace ||
            _detailsView is not null || _periodReportView is not null || HasActionReport || HasPersonnelConfirmation || _repository is null ||
            _session is not { PlayerForceId: not null } session) return;
        // File/settings/application operations do not require a completed GPU
        // map. Keep the actual optional map owner for suspension and retirement.
        var map=_residentMap;
        var world = session.World;
        var view = new MapSystemMenuView();
        _mapSystemMenu = view; _mapSystemSession = session; _mapSystemWorld = world; _mapSystemMap = map;
        var preparation = new CancellationTokenSource();
        _mapSystemPreparation = preparation;
        var token = preparation.Token;
        string locale = _ui.SourceLocale;
        view.BeginPreparation(); AddChild(view);
        MapSystemMenuPreparedResources? prepared = null;
        MapSystemResourceWorkspace? resourceOwner = null;
        try
        {
            // Only CPU resources run on this exclusive worker repository.
            // The pending real page owns admission immediately; no fake-ready
            // map or foreground is published while it waits.
            while (true)
            {
                resourceOwner = EnsureMapSystemResourcePreparation(map, checkGeneration: true);
                try
                {
                    prepared = await resourceOwner.Preparation.WaitAsync(token);
                    if (token.IsCancellationRequested || resourceOwner.Retired || !Current() || _ui.SourceLocale != locale)
                    { if (_mapSystemMenu == view) CloseMapSystemMenu(); return; }
                    // The finite file-generation check may wait on the file
                    // system. Keep even this cache-hit work off the UI thread.
                    bool generationCurrent = await Task.Run(prepared.IsCurrentGeneration, token);
                    if (token.IsCancellationRequested || resourceOwner.Retired || !Current() || _ui.SourceLocale != locale)
                    { if (_mapSystemMenu == view) CloseMapSystemMenu(); return; }
                    if (!generationCurrent)
                    {
                        RetireMapSystemResourcePreparation(resourceOwner);
                        prepared = null;
                        continue;
                    }
                    break;
                }
                catch (San14.Data.ResourceReadGenerationChangedException) when (!token.IsCancellationRequested && Current())
                {
                    // A genuine resource update invalidates the whole closure.
                    // Re-prepare on its real worker; never publish mixed inputs.
                    RetireMapSystemResourcePreparation(resourceOwner);
                }
            }
            if (token.IsCancellationRequested || resourceOwner.Retired || !Current() || _ui.SourceLocale != locale)
            { if (_mapSystemMenu == view) CloseMapSystemMenu(); return; }
            _mapSystemPresentationActive = true;
            map?.CloseCityCommandMenu(); map?.SetDetailsOpen(true, keepMapVisible: true); SetMapFocus(_mapFocused);
            // Admission queries can fail. Complete them while the local
            // preparation still owns its resources, before Configure receives
            // their ownership or creates any UI borrowers.
            bool scenarioSettingsSupported = _campaignComposition?.PrepareArmyQueries(_mapSystemWorld!).CanViewCurrentScenarioSettings(_mapSystemWorld!) == true;
            bool currentWorldEditSupported = _campaignComposition?.PrepareArmyQueries(_mapSystemWorld!)
                .QueryCurrentWorldEditAdmission(_mapSystemWorld!, session.HasPendingOperation).Value?.Allowed == true;
            var ready = prepared;
            var releaseResources = BorrowMapSystemResources(resourceOwner, view, ready);
            prepared = null; // Configure accepts this page's lease before creating any UI borrowers.
            view.Configure(_repository, _ui, 0x0473, RequestCloseMapSystemMenu, command =>
            {
                if (!Current() || HasCampaignSavePage || HasEnvironmentSettings || HasScenarioSettings || HasCurrentWorldEditors || _mapSystemConfirmation is not null) return;
                if (command == MapSystemCommand.Exit) RequestLeave(returnToTitle: false);
                else if (command == MapSystemCommand.ReturnToTitle) RequestLeave(returnToTitle: true);
                else if (command == MapSystemCommand.Environment) RunUiAction(OpenEnvironmentSettings);
                else if (command == MapSystemCommand.ScenarioSettings) RunUiAction(OpenScenarioSettings);
                else if (command == MapSystemCommand.Edit) RunUiAction(OpenCurrentWorldEditors);
                else if (command == MapSystemCommand.Manual)
                {
                    // The original opens its external manual and keeps Config.
                    // A failed host launch does not retire the campaign/menu.
                    if (UiSourceOnlineManual.ForLocale(_ui.SourceLocale) is { } url)
                    {
                        Error error = OS.ShellOpen(url);
                        if (error != Error.Ok) GD.PushWarning($"Could not open the original online manual: {error}.");
                    }
                }
                else
                {
                    OpenCampaignSaves(command == MapSystemCommand.Save);
                    if (HasCampaignSavePage) view.SuspendContent();
                }
            }, scenarioSettingsSupported: scenarioSettingsSupported,
                currentWorldEditSupported: currentWorldEditSupported,
                prepared: ready, releasePrepared: releaseResources);
            if (await view.PlayTransition(true) && Current()) view.SetBusy(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (_mapSystemMenu == view)
            {
                CloseMapSystemMenu();
                RunUiAction(() => System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw());
            }
        }
        finally
        {
            if (_mapSystemPreparation == preparation) _mapSystemPreparation = null;
            preparation.Dispose();
        }

        bool Current() => !_failed && !_shuttingDown && _mapSystemMenu == view && _residentMap == map &&
            ReferenceEquals(_session, session) && ReferenceEquals(session.World, _mapSystemWorld) && ReferenceEquals(_world, _mapSystemWorld);

        void RequestLeave(bool returnToTitle)
        {
            view.SetBusy(true); view.Hide();
            var question = new DeploymentTargetConfirmation(); _mapSystemConfirmation = question; AddChild(question);
            try
            {
                question.ConfigureGameMessage(_repository!, _ui, returnToTitle ? 0x5F01 : 0x2727, confirmed =>
                {
                    if (!Current() || _mapSystemConfirmation != question) return;
                    _mapSystemConfirmation = null; question.Abandon(); question.QueueFree();
                    if (confirmed)
                    {
                        if (returnToTitle) RequestReturnToTitle();
                        else WindowCloseRequested();
                    }
                    else { view.SetBusy(false); view.Show(); }
                });
                question.Name = returnToTitle ? "MapSystemReturnToTitleConfirmation" : "MapSystemExitConfirmation";
            }
            catch
            {
                _mapSystemConfirmation = null; question.Abandon(); question.QueueFree();
                view.SetBusy(false); view.Show(); throw;
            }
        }
    }

    private async void RequestCloseMapSystemMenu()
    {
        if (_mapSystemMenu is not { } view || _mapSystemClosing) return;
        if (!view.IsPrepared || view.IsTransitioning) { CloseMapSystemMenu(); return; }
        _mapSystemClosing = true; view.SetBusy(true);
        try
        {
            // Source exit owns both foreground and medium-out. The retained
            // background remains visible until those actual clocks complete.
            if (await view.PlayTransition(false) && _mapSystemMenu == view) CloseMapSystemMenu();
        }
        catch (Exception error)
        {
            if (_mapSystemMenu == view)
            { CloseMapSystemMenu(); RunUiAction(() => System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw()); }
        }
    }

    private void CloseMapSystemMenu()
    {
        if (_mapSystemMenu is null && _mapSystemMap is null) return;
        _mapSystemPreparation?.Cancel(); _mapSystemPreparation = null;
        _mapSystemClosing = false;
        bool presentationActive = _mapSystemPresentationActive; _mapSystemPresentationActive = false;
        CloseCurrentWorldEditors(resumeParent: false);
        CloseScenarioSettings(resumeParent: false);
        CloseEnvironmentSettings(resumeParent: false);
        var question = _mapSystemConfirmation; _mapSystemConfirmation = null;
        if (question is not null) { question.Abandon(); question.QueueFree(); }
        CloseCampaignSaves();
        var map = _mapSystemMap; _mapSystemMap = null;
        _mapSystemSession = null; _mapSystemWorld = null;
        var view = _mapSystemMenu; _mapSystemMenu = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        if (_mapSystemResources is { Map: null } resources) RetireMapSystemResourcePreparation(resources);
        if (presentationActive && map is not null && GodotObject.IsInstanceValid(map) && _residentMap == map) map.SetDetailsOpen(false);
        if (!_failed && !_shuttingDown) SetMapFocus(_mapFocused);
    }

    private bool ResumeMapSystemMenu()
    {
        var view = _mapSystemMenu;
        if (HasCurrentWorldEditors || HasEnvironmentSettings || HasScenarioSettings || view is null || _session is not { } session ||
            !ReferenceEquals(_mapSystemSession, session) || !ReferenceEquals(_mapSystemWorld, session.World) ||
            !ReferenceEquals(_world, session.World) || _residentMap != _mapSystemMap)
        {
            // A replaced campaign cannot retain the old parent's borrowed background.
            view?.Hide(); return false;
        }
        view.ResumeContent(); return true;
    }
}
