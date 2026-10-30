using Godot;
using San14.Client.Gameplay;

public partial class Main
{
    private CampaignApplicationState? _campaignApplication;
    private Task? _returnToTitleTask;
    private bool IsReturningToTitle => _campaignApplication?.ReturnToTitlePending == true;

    private void AttachCampaignApplication(bool playable)
    {
        if(playable) AttachApplicationProfile(_campaignComposition!,_session!.World);
        _terminalReturn = null;
        _campaignApplication?.Detach(playable ? _campaignComposition?.ApplicationGameplay : null);
        _campaignApplication = playable ? new CampaignApplicationState(_campaignComposition!.ApplicationGameplay) : null;
    }

    private void RequestReturnToTitle()
    {
        if (_failed || _shuttingDown || IsReturningToTitle ||
            _session is not { PlayerForceId: not null } session || _campaignApplication is not { } application) return;
        application.RequestReturnToTitle();
        BeginRequestedReturnToTitle(session, application);
    }

    private void BeginRequestedReturnToTitle(GameplaySession session, CampaignApplicationState application)
    {
        if (!ReferenceEquals(_session, session) || !ReferenceEquals(_campaignApplication, application) ||
            application.ReturnToTitlePending != true || _returnToTitleTask is { IsCompleted: false }) return;
        _terminalReturn = null;
        _returnToTitleTask = ReturnToTitleAsync(session, application);
    }

    private async Task ReturnToTitleAsync(GameplaySession session, CampaignApplicationState application)
    {
        try
        {
            RetireTutorialLessonForReturn();
            ResetMilitaryDeploymentComposition();
            LeaveSceneAudioContext();
            ClosePlayablePeriodReport();
            CloseResidentMap();
            if (_sceneView is { } scene)
            {
                _sceneView = null;
                RetireMapView(scene);
            }
            _browserContent.Hide();
            await Task.WhenAll(_mapRetirements.Select(row => row.Completion));
            if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || _failed || _shuttingDown ||
                !ReferenceEquals(_session, session) || !ReferenceEquals(_campaignApplication, application)) return;

            // Map retirement alone is not campaign removal. Keep the request
            // true until all its owners have retired, then detach this instance.
            application.Detach();
            _campaignApplication = null;
            _session = null;
            _campaignComposition = null;
            _selectedForceId = null;
            _selectedCell = null;
            RunUiAction(() =>
            {
                RefreshScenarios(loadPreview:false);
                SetMapFocus(false);
                OpenTitleMenu();
            });
        }
        catch (Exception error)
        {
            if (GodotObject.IsInstanceValid(this) && IsInsideTree() && !_failed && !_shuttingDown &&
                ReferenceEquals(_session, session) && ReferenceEquals(_campaignApplication, application))
                ShowFailure(error, startup: false);
        }
    }
}
