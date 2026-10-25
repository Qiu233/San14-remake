using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class RulerDecisionWorkspace(GameplaySession session, NormalPeriodOperation pending, MapSceneView map)
    {
        internal readonly GameplaySession Session = session;
        internal readonly NormalPeriodOperation Pending = pending;
        internal readonly MapSceneView Map = map;
        internal Control? View;
        internal Action? Abandon;
        internal DeploymentNarrationView? Warning;
    }
    private RulerDecisionWorkspace? _rulerDecision;
    private bool HasRulerDecision => _rulerDecision is not null;
    private bool IsRulerDecisionCurrent => _rulerDecision is { } owner &&
        ReferenceEquals(_session, owner.Session) && ReferenceEquals(owner.Session.PendingNormalPeriod, owner.Pending) &&
        ReferenceEquals(_residentMap, owner.Map);

    private bool TryOpenRulerDecision(GameplaySession session, NormalPeriodOperation pending,
        CampaignComposition composition, MapSceneView map, int player)
    {
        if (pending.PendingRulerSuccession is null && pending.PendingCapturedRulerTreatment is null) return false;
        var owner = new RulerDecisionWorkspace(session, pending, map); _rulerDecision = owner;
        try
        {
            RetirePeriodReportView(); CloseActionReport(); map.CloseCityCommandMenu();
            map.SetDetailsOpen(true, keepMapVisible: true); SetMapFocus(false); HideDeploymentHud();
            //An execution may suspend inside death processing for a successor.
            //That nested question precedes the remaining defeat settlement.
            if (pending.PendingRulerSuccession is { } successor)
            {
                if (successor.ForceId != player) throw new InvalidDataException("Ruler succession belongs to another force.");
                OpenRulerSuccessorSelector(owner, successor, Submit);
            }
            else if (pending.PendingCapturedRulerTreatment is { } prisoner)
            {
                if (prisoner.CapturingForceId != player) throw new InvalidDataException("Prisoner treatment belongs to another force.");
                var view = new CapturedRulerTreatmentView(); owner.View = view; owner.Abandon = view.Abandon;
                AddChild(view);
                view.Configure(_repository!, _ui, ReadCurrentOfficerDetail(pending.Candidate, prisoner.OfficerId),
                    prisoner, () => IsRulerDecisionCurrent && !_advancingPeriod,
                    choice => RunUiAction(() => Submit(new ConfirmCapturedRulerTreatmentOrder(player, prisoner.OfficerId, choice))));
            }
            return true;
        }
        catch { CloseRulerDecision(); throw; }

        void Submit(GameOrder choice)
        {
            if (!IsRulerDecisionCurrent || _advancingPeriod || _failed || _shuttingDown) return;
            CloseRulerDecision();
            _advancingPeriod = true;
            _periodAdvanceTask = AdvancePlayableAsync(session, composition, campaignChoice: choice);
        }
    }

    private void OpenRulerSuccessorSelector(RulerDecisionWorkspace owner, RulerSuccessionRequest request,
        Action<GameOrder> submit)
    {
        var query = _campaignComposition!.PrepareArmyQueries(owner.Pending.Candidate)
            .QueryRulerSuccessionPersonPage(owner.Pending.Candidate, request.ForceId);
        var page = query.Value ?? throw new InvalidDataException(query.UnresolvedInput);
        var columns = page.Columns.Select(column => new CommandSelectionColumn(column.ColumnId.ToString(),
            column.Caption, column.Width, Numeric: column.ColumnId is >= 8 and <= 12)).ToArray();
        var rows = page.Rows.Select(row => new CommandSelectionRow(row.OfficerId,
            row.Cells.Select(PersonnelGroupCell).ToArray(), true)).ToArray();
        var selector = new CommandSelectionView(); owner.View = selector; owner.Abandon = selector.Abandon;
        AddChild(selector);
        selector.Configure(_repository!, _ui, 0x4AB, columns, rows, [], 1,
            () => IsRulerDecisionCurrent && !_advancingPeriod && owner.Warning is null,
            selected => RunUiAction(() =>
            {
                if (!IsRulerDecisionCurrent || _advancingPeriod) return;
                if (selected is { Count: 1 })
                { submit(new ConfirmRulerSuccessionOrder(request.ForceId, selected[0])); return; }
                //The source warns and reopens on Cancel/zero selection. No
                //fallback successor is selected, and the pending model is kept.
                var warning = new DeploymentNarrationView { Name = "RulerSuccessionSelectionRequired", ZIndex = 97,
                    KeyboardInput = HandleGameplayNarrationKeyboard };
                owner.Warning = warning; AddChild(warning);
                warning.ConfigureAnnouncement(_repository!, _ui, page.EmptyChoiceWarning, () => RunUiAction(() =>
                {
                    if (!IsRulerDecisionCurrent || owner.Warning != warning) return;
                    owner.Warning = null; warning.Hide(); RemoveChild(warning); warning.QueueFree(); selector.Show();
                }), () => _preferences.AutomaticMessageAdvance,
                    () => CanAdvanceNarration && IsRulerDecisionCurrent && owner.Warning == warning);
            }), CurrentListScrollSpeed, page.Guide, retainOnAccept: true,
            singleSelection: true, retainOnReturn: true);
        selector.Name = "RulerSuccession"; selector.ZIndex = 96;
    }

    private void CloseRulerDecision()
    {
        var owner = _rulerDecision; _rulerDecision = null;
        if (owner is null) return;
        owner.Abandon?.Invoke();
        foreach (var view in new Control?[] { owner.Warning, owner.View })
            if (view is not null) { view.Hide(); view.GetParent()?.RemoveChild(view); view.QueueFree(); }
        if (GodotObject.IsInstanceValid(owner.Map) && ReferenceEquals(_residentMap, owner.Map)) owner.Map.SetDetailsOpen(false);
    }
}
