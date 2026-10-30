using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private CurrentWorldEditMenuView? _worldEditMenu;
    private DeploymentTargetConfirmation? _worldEditQuestion;
    private GameplaySession? _worldEditSession;
    private GameState? _worldEditWorld;
    private MapSceneView? _worldEditMap;
    private bool HasCurrentWorldEditors => _worldEditMenu is not null || _worldEditQuestion is not null;

    private bool IsCurrentWorldEditCurrent => !_failed && !_shuttingDown && !IsReturningToTitle &&
        _worldEditSession is { } session && ReferenceEquals(_session, session) &&
        ReferenceEquals(session.World, _worldEditWorld) && ReferenceEquals(_world, _worldEditWorld) &&
        _mapSystemMenu is not null && _mapSystemSession == session && _residentMap == _worldEditMap;

    private ArmyCampaignRuleAdapter CurrentWorldEditRules =>
        (_campaignComposition ?? throw new InvalidOperationException("The current editing campaign is missing."))
            .PrepareArmyQueries(_worldEditWorld ?? throw new InvalidOperationException("The current editing world is missing."));

    private void OpenCurrentWorldEditors()
    {
        if (_repository is null || HasCurrentWorldEditors || _mapSystemMenu is not { } parent ||
            _mapSystemSession is not { PlayerForceId: not null } session || _campaignComposition is not { } composition ||
            !ReferenceEquals(_session, session) || !ReferenceEquals(_mapSystemWorld, session.World) ||
            _residentMap != _mapSystemMap) return;
        var query = composition.PrepareArmyQueries(session.World).QueryCurrentWorldEditAdmission(session.World, session.HasPendingOperation);
        if (!query.Available || query.Value is not { Allowed: true } admission)
        {
            _playMessage.Text = _ui.Get("detail_unknown");
            if (!query.Available) GD.PushWarning(query.UnresolvedInput ?? "current-world-edit.admission");
            return;
        }
        _worldEditSession = session; _worldEditWorld = session.World; _worldEditMap = _mapSystemMap;
        parent.SetBusy(true); parent.Hide();
        if (admission.RequiresConfirmation)
            AskCurrentWorldEditQuestion(0x5F2C, confirmed =>
            { if (confirmed) ShowMenu(); else CloseCurrentWorldEditors(); });
        else ShowMenu();

        void ShowMenu()
        {
            if (!IsCurrentWorldEditCurrent || _worldEditMenu is not null) return;
            var view = new CurrentWorldEditMenuView(); _worldEditMenu = view; AddChild(view);
            try
            {
                view.Configure(_repository!, _ui, () => IsCurrentWorldEditCurrent && _worldEditMenu == view && _worldEditQuestion is null,
                    () => RunUiAction(CloseCurrentWorldEditors), entry => RunUiAction(() =>
                    {
                        if (!IsCurrentWorldEditCurrent || _worldEditMenu != view || _worldEditQuestion is not null) return;
                        if (entry is CurrentWorldEditEntry.Item or CurrentWorldEditEntry.Items)
                            OpenCurrentWorldItems(entry == CurrentWorldEditEntry.Items);
                        else OpenCurrentWorldEntitySelection(entry switch
                        {
                            CurrentWorldEditEntry.Force => CurrentWorldEditKind.Force,
                            CurrentWorldEditEntry.City => CurrentWorldEditKind.City,
                            _ => CurrentWorldEditKind.Area
                        });
                    }));
            }
            catch { CloseCurrentWorldEditors(); throw; }
        }
    }

    private void AskCurrentWorldEditQuestion(int message, Action<bool> completed)
    {
        if (_repository is null || !IsCurrentWorldEditCurrent || _worldEditQuestion is not null) return;
        var view = new DeploymentTargetConfirmation(); _worldEditQuestion = view; AddChild(view);
        try
        {
            view.ConfigureGameMessage(_repository, _ui, message, confirmed => RunUiAction(() =>
            {
                if (!IsCurrentWorldEditCurrent || _worldEditQuestion != view) return;
                _worldEditQuestion = null; view.Abandon(); view.QueueFree(); completed(confirmed);
            }));
            view.Name = "CurrentWorldEditConfirmation";
        }
        catch { _worldEditQuestion = null; view.Abandon(); view.QueueFree(); throw; }
    }

    private bool SubmitCurrentWorldEdit(CurrentWorldEditOrder order, Action accepted)
    {
        if (!IsCurrentWorldEditCurrent || _worldEditQuestion is not null) return false;
        return ApplyPlayableOrder(order, () =>
        {
            // Normal command application owns the replacement. Retain the
            // same editing host against that accepted world before map refresh.
            _worldEditWorld = _worldEditSession!.World;
            _mapSystemWorld = _worldEditWorld;
            accepted();
        });
    }

    private void ResumeCurrentWorldEditMenu()
    {
        if (IsCurrentWorldEditCurrent && _worldEditMenu is { } menu)
        { menu.SetBusy(false); menu.Show(); }
    }
    private void CloseCurrentWorldEditors() => CloseCurrentWorldEditors(resumeParent: true);
    private void CloseCurrentWorldEditors(bool resumeParent)
    {
        var question = _worldEditQuestion; _worldEditQuestion = null;
        if (question is not null) { question.Abandon(); question.QueueFree(); }
        CloseCurrentWorldItems(resumeParent: false);
        CloseCurrentWorldEntitySelection(resumeParent: false);
        var menu = _worldEditMenu; _worldEditMenu = null;
        if (menu is not null) { menu.Abandon(); menu.QueueFree(); }
        _worldEditSession = null; _worldEditWorld = null; _worldEditMap = null;
        if (resumeParent && _mapSystemMenu is { } parent && ResumeMapSystemMenu()) parent.SetBusy(false);
    }

    private void CancelCurrentWorldEditor()
    {
        if (_worldEditQuestion is { } question) question.RequestCancel();
        else if (CancelCurrentWorldItemChild()) { }
        else if (CancelCurrentWorldEntityChild()) { }
        else _worldEditMenu?.RequestBack();
    }
}
