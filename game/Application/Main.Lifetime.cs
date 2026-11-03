using Godot;
using San14.Client.Gameplay;

public partial class Main
{
    private Window? _lifetimeWindow;
    private bool _shuttingDown;
    private Task? _shutdownTask;
    private readonly List<(MapSceneView View, Task Completion)> _mapRetirements = [];
    private readonly List<Node> _retiredInterfaceChildren = [];
    private readonly List<CampaignApplicationState> _retiredCampaignApplications = [];

    private void ConnectWindowLifetime()
    {
        GetTree().AutoAcceptQuit = false;
        _lifetimeWindow = GetWindow();
        _lifetimeWindow.CloseRequested += WindowCloseRequested;
        _lifetimeWindow.FocusEntered += WindowAudioFocusEntered;
        _lifetimeWindow.FocusExited += WindowAudioFocusExited;
    }

    private void DisconnectWindowLifetime()
    {
        if (_lifetimeWindow is { } window && GodotObject.IsInstanceValid(window))
        {
            window.CloseRequested -= WindowCloseRequested;
            window.FocusEntered -= WindowAudioFocusEntered;
            window.FocusExited -= WindowAudioFocusExited;
        }
        _lifetimeWindow = null;
    }

    private void WindowAudioFocusEntered() => _uiAudioFeedback?.SetWindowFocused(true);
    private void WindowAudioFocusExited() => _uiAudioFeedback?.SetWindowFocused(false);

    private void RetireMapView(MapSceneView view)
    {
        // Keep the parent tree and its borrowed resources alive while an
        // initializer or rendering request finishes retiring.
        view.Hide();
        view.ProcessMode = ProcessModeEnum.Disabled;
        view.SetProcessInput(false);
        view.SetProcessUnhandledInput(false);
        Task completion = CloseAndFreeMapAsync(view);
        _mapRetirements.RemoveAll(row => row.Completion.IsCompletedSuccessfully);
        _mapRetirements.Add((view, completion));
        _ = ObserveMapRetirementAsync(completion);
    }

    private static async Task CloseAndFreeMapAsync(MapSceneView view)
    {
        await view.CloseNativeMapAsync();
        if (GodotObject.IsInstanceValid(view))
        {
            if (view.GetParent() is Node parent) parent.RemoveChild(view);
            view.QueueFree();
        }
    }

    private void RetireFailedInterface()
    {
        // Return borrowed command controls while their original parents live,
        // then release the browser model/subscriptions before child disposal.
        CloseCommandWorkspace();
        RetireMilitaryDeploymentComposition();
        foreach (Node child in GetChildren())
        {
            if (child is CanvasItem canvas) canvas.Hide();
            child.ProcessMode = ProcessModeEnum.Disabled;
            _retiredInterfaceChildren.Add(child);
        }
        _ = FreeFailedInterfaceAsync();
    }

    private async Task FreeFailedInterfaceAsync()
    {
        try
        {
            await Task.WhenAll(_mapRetirements.Select(row => row.Completion));
            FreeRetiredInterface();
        }
        catch (Exception error)
        {
            // Keep hidden parents mounted so a later close can retry their owners.
            GD.PushError($"Previous interface still owns map resources: {error}");
        }
    }

    private void FreeRetiredInterface()
    {
        foreach (var application in _retiredCampaignApplications) application.Detach();
        _retiredCampaignApplications.Clear();
        foreach (Node child in _retiredInterfaceChildren)
            if (GodotObject.IsInstanceValid(child)) child.QueueFree();
        _retiredInterfaceChildren.Clear();
    }

    private void RetryMapRetirements()
    {
        _mapRetirements.RemoveAll(row => row.Completion.IsCompletedSuccessfully);
        for (int index = 0; index < _mapRetirements.Count; ++index)
        {
            var row = _mapRetirements[index];
            if (!row.Completion.IsCompleted) continue;
            Task retry = CloseAndFreeMapAsync(row.View);
            _mapRetirements[index] = (row.View, retry);
            _ = ObserveMapRetirementAsync(retry);
        }
    }

    private static async Task ObserveMapRetirementAsync(Task retirement)
    {
        try { await retirement; }
        catch (Exception error) { GD.PushError($"Map resource retirement failed: {error}"); }
    }

    private void WindowCloseRequested()
    {
        if (_shutdownTask is { IsCompleted: false }) return;
        _shutdownTask = ShutdownAsync();
    }

    private async Task ShutdownAsync()
    {
        _shuttingDown = true;
        _residentMap?.BeginMapRetirement();
        _sceneView?.BeginMapRetirement();
        var tree = GetTree();
        try
        {
            _periodAdvanceCancellation?.Cancel();
            CancelNewGamePreparation(restoreParent: false);
            CloseStartupLoading();
            CloseCommandWorkspace();
            CloseScenarioSettings(resumeParent:false);
            CloseEnvironmentSettings(resumeParent:false);
            CloseTitleMenu();
            CloseNewGameSetup();
            CloseScenarioSelection();
            _type6Host?.Stop();
            if (_periodAdvanceTask is { } advance)
            {
                try { await advance; }
                catch (Exception error) { GD.PushWarning($"Period advancement ended with an error during shutdown: {error}"); }
            }
            // Follow the existing return-to-title owner order. Report pages
            // and standalone browser details also hold application UI leases.
            RetireTutorialLessonForReturn();
            ResetMilitaryDeploymentComposition();
            RetireMilitaryDeploymentComposition();
            LeaveSceneAudioContext();
            ClosePlayablePeriodReport();
            var details = _detailsView;
            _detailsView = null;
            if (details is not null && GodotObject.IsInstanceValid(details))
            {
                details.Hide();
                if (details.GetParent() is Node parent) parent.RemoveChild(details);
                details.QueueFree();
            }
            RetryMapRetirements();
            CloseResidentMap();
            if (_sceneView is { } scene)
            {
                _sceneView = null;
                RetireMapView(scene);
            }
            await Task.WhenAll(_mapRetirements.Select(row => row.Completion));
            await RetireApplicationNativeMapDeviceAsync();
            await RetireApplicationMapStaticMediaAsync();
            FreeRetiredInterface();
            await RetireApplicationUiResourcesAsync();
            tree.Quit();
        }
        catch (Exception error)
        {
            // The next window-close request retries these same retained owners.
            // Keep UI work stopped while their resource lifetime remains open.
            GD.PushError($"Application shutdown could not retire map resources. Close the window again to retry: {error}");
        }
    }
}
