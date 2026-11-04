using Godot;

internal partial class DeploymentSetupView
{
    private Node2D? sourceBodyMotion;
    private Node2D? sourceHeaderMotion, sourceResourceMotion, sourceFooterMotion;
    private Control? sourceBodyContent, sourceFooterContent;
    private UiSourceModalTransition? sourceTransition;
    private bool sourceTransitionBusy;
    private Control SourceBodyRoot => sourceBodyContent ?? canvas;
    private Control SourceFooterRoot => sourceFooterContent ?? canvas;

    private void CreateSourceForegroundRoots()
    {
        sourceBodyMotion = new Node2D { Name = "DeploymentBodyMotion" };
        sourceHeaderMotion = new Node2D { Name = "DeploymentHeaderMotion" };
        sourceResourceMotion = new Node2D { Name = "DeploymentResourceMotion" };
        sourceFooterMotion = new Node2D { Name = "DeploymentFooterMotion" };
        canvas.AddChild(sourceBodyMotion); canvas.AddChild(sourceHeaderMotion);
        canvas.AddChild(sourceResourceMotion); canvas.AddChild(sourceFooterMotion);
        sourceBodyContent = new Control { Name = "DeploymentBodyContent", Size = canvas.Size,
            MouseFilter = MouseFilterEnum.Ignore };
        sourceFooterContent = new Control { Name = "DeploymentFooterContent", Size = canvas.Size,
            MouseFilter = MouseFilterEnum.Ignore };
        sourceBodyMotion.AddChild(sourceBodyContent); sourceFooterMotion.AddChild(sourceFooterContent);
    }

    private void ConfigureSourceForeground()
    {
        // These roots exist before any owned pane or sprite is configured.
        // Moving a configured tree would invoke its real exit/disposal path.
        var transition = new UiSourceModalTransition
        { Name = "DeploymentSourceTransition", ProcessMode = ProcessModeEnum.Always };
        AddChild(transition);
        transition.Configure(assets!.SourceLayout(0xF9C5DB14), CommonBackdrop,
            new Dictionary<int, (Node2D Root, Vector2 ContentMount)>
            {
                [1] = (sourceBodyMotion!, Vector2.Zero), [4] = (sourceHeaderMotion!, Vector2.Zero),
                [5] = (sourceResourceMotion!, resourceHeader.Position), [6] = (sourceFooterMotion!, new(0, 904))
            });
        transition.ApplyShownState();
        sourceTransition = transition;
    }

    private bool SourceForegroundAlive => GodotObject.IsInstanceValid(this) &&
        IsInsideTree() && !IsQueuedForDeletion() && !abandoned;

    private async Task EnterSourceForeground()
    {
        if (sourceTransitionBusy || !SourceForegroundAlive) return;
        sourceTransitionBusy = true;
        var previousMode = ProcessMode;
        bool restoreInput = true;
        ProcessMode = ProcessModeEnum.Disabled;
        try { await sourceTransition!.Play(true); }
        catch (Exception error)
        {
            if (SourceForegroundAlive)
            {
                try { sourceTransition!.RestoreShownState(); }
                catch (Exception recovery) { restoreInput = false; ProcessMode = ProcessModeEnum.Disabled; error = new AggregateException(error, recovery); }
                ReportSourceTransitionFailure(error);
            }
        }
        finally
        {
            if (SourceForegroundAlive && restoreInput) { ProcessMode = previousMode; sourceTransitionBusy = false; }
        }
    }

    private void RequestDecision()
    {
        if (!abandoned && selectionChild is null && !sourceTransitionBusy && decide is { } callback)
            _ = ExitSourceForeground(callback);
    }

    private async Task ExitSourceForeground(Action callback)
    {
        if (sourceTransitionBusy || !SourceForegroundAlive) return;
        sourceTransitionBusy = true;
        var previousMode = ProcessMode;
        bool restoreInput = true;
        ProcessMode = ProcessModeEnum.Disabled;
        try
        {
            bool completed = await sourceTransition!.Play(false);
            if (!completed || !SourceForegroundAlive) return;
            // Acceptance rechecks the real child's processing/input context.
            // The transition guard still rejects another navigation here.
            ProcessMode = previousMode;
            // The retained page stays mounted through the actual source
            // endpoint; its owner alone retires it or opens the target state.
            callback();
            if (SourceForegroundAlive && IsVisibleInTree()) sourceTransition.RestoreShownState();
        }
        catch (Exception error)
        {
            if (SourceForegroundAlive)
            {
                try { sourceTransition!.RestoreShownState(); }
                catch (Exception recovery) { restoreInput = false; ProcessMode = ProcessModeEnum.Disabled; error = new AggregateException(error, recovery); }
                ReportSourceTransitionFailure(error);
            }
        }
        finally
        {
            if (SourceForegroundAlive && restoreInput) { ProcessMode = previousMode; sourceTransitionBusy = false; }
        }
    }

    private void ReportSourceTransitionFailure(Exception error)
    {
        if (TransitionFailure is { } report) report(error);
        else GD.PushError(error.ToString());
    }
}
