using Godot;

internal partial class DeploymentPersonSelectionView
{
    private Node2D? _sourceBodyMotion;
    private Node2D? _sourceHeaderMotion, _sourceFooterMotion;
    private Control? _sourceBodyContent, _sourceFooterContent;
    private UiSourceModalTransition? _foregroundTransition;
    private bool _foregroundTransitionBusy;
    internal Action<Exception>? TransitionFailure { get; set; }
    private Control SourceBodyRoot => _sourceBodyContent ?? _canvas;
    private Control SourceFooterRoot => _sourceFooterContent ?? _canvas;

    private void CreateForegroundRoots()
    {
        _sourceBodyMotion = new Node2D { Name = "PersonSelectionBodyMotion" };
        _sourceHeaderMotion = new Node2D { Name = "PersonSelectionHeaderMotion" };
        _sourceFooterMotion = new Node2D { Name = "PersonSelectionFooterMotion" };
        _canvas.AddChild(_sourceBodyMotion); _canvas.AddChild(_sourceHeaderMotion); _canvas.AddChild(_sourceFooterMotion);
        _sourceBodyContent = new Control { Name = "PersonSelectionBodyContent", Size = _canvas.Size,
            MouseFilter = MouseFilterEnum.Ignore };
        _sourceFooterContent = new Control { Name = "PersonSelectionFooterContent", Size = _canvas.Size,
            MouseFilter = MouseFilterEnum.Ignore };
        _sourceBodyMotion.AddChild(_sourceBodyContent); _sourceFooterMotion.AddChild(_sourceFooterContent);
    }

    private void ConfigureForegroundMotion()
    {
        var transition = new UiSourceModalTransition
        { Name = "PersonSelectionSourceTransition", ProcessMode = ProcessModeEnum.Always };
        AddChild(transition);
        // This selector borrows Make's already-visible common background.
        // Its three existing foregrounds follow its own wrapper. The original
        // narration group7 remains unregistered until its real text is restored.
        transition.Configure(_assets!.SourceLayout(0xF9C5DB14), null,
            new Dictionary<int, (Node2D Root, Vector2 ContentMount)>
            {
                [1] = (_sourceBodyMotion!, Vector2.Zero), [4] = (_sourceHeaderMotion!, Vector2.Zero),
                [6] = (_sourceFooterMotion!, new(0, 904))
            });
        transition.ApplyShownState();
        _foregroundTransition = transition;
    }

    private bool ForegroundOwnerAlive => GodotObject.IsInstanceValid(this) &&
        IsInsideTree() && !IsQueuedForDeletion() && _close is not null;

    private async Task EnterForeground()
    {
        if (_foregroundTransitionBusy || !ForegroundOwnerAlive) return;
        _foregroundTransitionBusy = true;
        var previousMode = ProcessMode;
        bool restoreInput = true;
        ProcessMode = ProcessModeEnum.Disabled;
        try { await _foregroundTransition!.Play(true, includeBackground: false); }
        catch (Exception error)
        {
            if (ForegroundOwnerAlive)
            {
                try { _foregroundTransition!.RestoreShownState(); }
                catch (Exception recovery) { restoreInput = false; ProcessMode = ProcessModeEnum.Disabled; error = new AggregateException(error, recovery); }
                ReportForegroundFailure(error);
            }
        }
        finally
        {
            if (ForegroundOwnerAlive && restoreInput) { ProcessMode = previousMode; _foregroundTransitionBusy = false; }
        }
    }

    private async Task CloseAfterForegroundExit(IReadOnlyList<int>? result)
    {
        var callback = _close;
        if (callback is null || !ForegroundOwnerAlive || _foregroundTransitionBusy) return;
        _foregroundTransitionBusy = true;
        var previousMode = ProcessMode;
        bool callbackStarted = false;
        bool restoreInput = true;
        ProcessMode = ProcessModeEnum.Disabled;
        try
        {
            bool completed = await _foregroundTransition!.Play(false, includeBackground: false);
            if (!completed || !ForegroundOwnerAlive || _close != callback) return;
            // Native862C50 hides first, then event100 restores the retained
            // Make page and accepts or discards these saved selection results.
            Hide(); _close = null; RefreshAdmission(); callbackStarted = true; callback(result);
        }
        catch (Exception error)
        {
            if (ForegroundOwnerAlive)
            {
                try { _foregroundTransition!.RestoreShownState(); }
                catch (Exception recovery) { restoreInput = false; ProcessMode = ProcessModeEnum.Disabled; error = new AggregateException(error, recovery); }
            }
            if (callbackStarted || ForegroundOwnerAlive) ReportForegroundFailure(error);
        }
        finally
        {
            if (ForegroundOwnerAlive && restoreInput) { ProcessMode = previousMode; _foregroundTransitionBusy = false; }
        }
    }

    private void ReportForegroundFailure(Exception error)
    {
        if (TransitionFailure is { } report) report(error);
        else GD.PushError(error.ToString());
    }
}
