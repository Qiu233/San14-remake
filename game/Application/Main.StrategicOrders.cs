using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class StrategicOrdersWorkspace(GameplaySession session, GameState world, MapSceneView map,
        StrategicCommandDraft draft, AreaAdministrationOfficerContent cards)
    {
        internal GameplaySession Session { get; } = session;
        internal GameState World { get; } = world;
        internal MapSceneView Map { get; } = map;
        internal StrategicCommandDraft Draft { get; } = draft;
        internal AreaAdministrationOfficerContent Cards { get; } = cards;
        internal StrategicOrdersView View { get; set; } = null!;
        internal CommandSelectionView? Selection { get; set; }
        internal DeploymentCalculatorDialog? Calculator { get; set; }
    }
    private sealed record StrategicOrdersContinuation(GameplaySession Session, MapSceneView Map, int Kind);
    private StrategicOrdersWorkspace? _strategicOrders;
    private StrategicOrdersContinuation? _strategicOrdersContinuation;
    private bool HasStrategicOrdersPage => _strategicOrders is not null;
    private bool IsStrategicOrdersCurrent(StrategicOrdersWorkspace owner) => !_failed && !_shuttingDown &&
        _strategicOrders == owner && _session == owner.Session && ReferenceEquals(owner.World, owner.Session.World) &&
        _residentMap == owner.Map;
    private bool CanEditStrategicOrders(StrategicOrdersWorkspace owner) => IsStrategicOrdersCurrent(owner) &&
        owner.Selection is null && owner.Calculator is null;

    private void OpenStrategicOrders(int kind)
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || HasAdministrationPage || HasMilitaryOrderPage ||
            HasEnvironmentSettings || HasCampaignSavePage || HasMapSystemMenu || HasCommandWorkspace || HasActionReport ||
            HasPersonnelConfirmation || _detailsView is not null || _repository is null ||
            _campaignComposition is not { } composition || _residentMap is not { IsPlayableMapReady: true } map ||
            _session is not { PlayerForceId: int player } session || session.HasPendingOperation ||
            session.World is not { Phase: TurnPhase.Strategy, NativeArmySimulation: not null, CampaignTurnStop: null } world ||
            PlayableOutcome.Evaluate(world, player) != PlayableResult.Ongoing) return;
        var draft = composition.PrepareArmyQueries(world).OpenStrategicCommandDraft(world, player, kind);
        if (draft.UnresolvedOwner is { } missing) { StrategicOrdersUnavailable(missing); return; }
        var owner = new StrategicOrdersWorkspace(session, world, map, draft, new(_repository));
        var view = new StrategicOrdersView(); owner.View = view; _strategicOrders = owner; AddChild(view);
        try
        {
            view.Configure(_repository, _ui, kind, () => CanEditStrategicOrders(owner),
                () => RunUiAction(RequestStrategicOrdersBack),
                () => RunPlayAction(() => SubmitStrategicOrders(owner, false)),
                () => RunPlayAction(() => SubmitStrategicOrders(owner, true)),
                () => RunUiAction(() => OpenStrategicTargetSelection(owner, false)),
                () => RunUiAction(() => OpenStrategicTargetSelection(owner, true)),
                () => RunUiAction(() => OpenStrategicExecutorSelection(owner)),
                () => RunUiAction(() => OpenStrategicMilitaryCalculator(owner)),
                amount => RunUiAction(() => ChangeStrategicOrders(owner, () => draft.SetMilitaryIntelligence(amount))),
                choice => RunUiAction(() => ChangeStrategicOrders(owner, () => draft.SetIndependenceChoice(choice))));
            map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true);
            SetMapFocus(true); HideDeploymentHud(); RefreshStrategicOrders(owner);
        }
        catch { CloseStrategicOrders(); throw; }
    }

    private void RefreshStrategicOrders(StrategicOrdersWorkspace owner)
    {
        if (!IsStrategicOrdersCurrent(owner)) return;
        var preparedAdvice = owner.Draft.PrepareAdvice(new(_presentationRandom.State));
        if (preparedAdvice.Value?.AppRandomSnapshot is { } app)
            _presentationRandom.InstallCommittedState(app.State);
        if (preparedAdvice.UnresolvedInput is { } adviceMissing) GD.PushWarning(adviceMissing);
        var draft = owner.Draft; var budget = draft.Budget; var preview = draft.Preview;
        bool doubleTiger = draft.Kind == 10, tigerWolf = draft.Kind == 11;
        string Number(int? value) => value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? _ui.Get("detail_unknown");
        string Cost(int? value) => value is int cost ? cost == 0 ? "(-0)" : "(^03" + -cost + "^00)" : _ui.Get("detail_unknown");
        var details = draft.TargetDetails;
        var fields = new Dictionary<int, string>
        {
            [doubleTiger ? 113 : 100] = preview.ChancePercent is int chance ? chance + "%" : _ui.Get("detail_unknown"),
            [doubleTiger ? 121 : tigerWolf ? 125 : 108] = Number(budget.GoldBefore),
            [doubleTiger ? 123 : tigerWolf ? 127 : 110] = Number(budget.GoldAfter),
            [doubleTiger ? 124 : tigerWolf ? 128 : 111] = Cost(budget.GoldCost),
            [doubleTiger ? 130 : tigerWolf ? 134 : 117] = Number(preview.TravelDays),
            [doubleTiger ? 137 : tigerWolf ? 141 : 124] = Number(budget.BooksBefore),
            [doubleTiger ? 139 : tigerWolf ? 143 : 126] = Number(budget.BooksAfter),
            [doubleTiger ? 140 : tigerWolf ? 144 : 127] = Cost(budget.BookCost),
            [doubleTiger ? 89 : 78] = "/" + Number(budget.MilitaryBefore)
        };
        if (doubleTiger) fields[31] = details.RelationshipText ?? _ui.Get("detail_unknown");
        else
        {
            fields[18] = draft.Kind == 12 ? Number(details.Loyalty) : tigerWolf
                ? details.GovernorName ?? _ui.Get("detail_unknown") : details.ForceName ?? _ui.Get("detail_unknown");
            fields[25] = draft.Kind == 24 ? details.HomeLocationName ?? _ui.Get("detail_unknown")
                : details.ForceName ?? _ui.Get("detail_unknown");
        }
        AreaAdministrationOfficerValues Card(PersonnelDevelopmentOfficer? officer, string empty) => officer?.OfficerCard is { } card
            ? owner.Cards.Create(card) : new(officer?.Name ?? empty, null, null, null, null);
        owner.View.Apply(new(draft.SelectedTarget?.Name ?? _ui.Get("talent_choose_target"),
            draft.SelectedSecondForce?.Name ?? _ui.Get("talent_choose_target"),
            Card(draft.Executor, _ui.Get("talent_choose_executors")), fields,
            draft.MilitaryIntelligence, draft.MinimumMilitaryIntelligence, draft.MaximumMilitaryIntelligence,
            draft.MilitaryIntelligenceStep, draft.RequiredMilitaryIntelligence,
            draft.Targets.Count > 0, draft.SecondaryForces.Count > 0, draft.Executors.Count > 0,
            draft.MinimumMilitaryIntelligence is not null && draft.MaximumMilitaryIntelligence is not null,
            draft.IndependenceChoice, draft.IndependenceChoices.Select(row => new StrategicOrdersChoice(row.Value, row.Caption, row.Enabled)).ToArray(),
            draft.CanConfirm, draft.CanContinue, preview.AdviceText ?? "",
            Card(draft.AdvisorOfficer, "").Portrait));
        owner.View.ApplyNotice(preview.UnresolvedOwner is null ? preview.Guide ?? "" : _ui.Get("information_partial"));
        owner.View.SetBusy(!CanEditStrategicOrders(owner));
        if ((draft.UnresolvedOwner ?? preview.UnresolvedOwner) is { } missing) GD.PushWarning(missing);
    }

    private void ChangeStrategicOrders(StrategicOrdersWorkspace owner, Func<bool> change)
    {
        if (!CanEditStrategicOrders(owner)) return;
        change(); RefreshStrategicOrders(owner);
    }

    private void OpenStrategicExecutorSelection(StrategicOrdersWorkspace owner)
    {
        if (!CanEditStrategicOrders(owner)) return;
        var rows = owner.Draft.Executors.Select(row => new CommandSelectionRow(row.OfficerId,
            [new(row.Name)], row.Enabled == true, row.DisabledReason ?? row.UnresolvedOwner)).ToArray();
        OpenStrategicSelection(owner, 0x541, rows, owner.Draft.ExecutorId,
            id => owner.Draft.SelectExecutor(id));
    }

    private void OpenStrategicTargetSelection(StrategicOrdersWorkspace owner, bool second)
    {
        if (!CanEditStrategicOrders(owner)) return;
        var rows = (second ? owner.Draft.SecondaryForces : owner.Draft.Targets).Select(row => new CommandSelectionRow(row.Id,
            [new(row.Name)], row.Enabled == true, row.DisabledReason ?? row.UnresolvedOwner)).ToArray();
        int title = second ? 0x636 : owner.Draft.Kind == 10 ? 0x632 : owner.Draft.Kind == 11 ? 0x478
            : owner.Draft.Kind is 12 or 24 ? 0x542 : 0x477;
        OpenStrategicSelection(owner, title, rows, second ? owner.Draft.SecondForceId : owner.Draft.TargetId,
            id => second ? owner.Draft.SelectSecondForce(id) : owner.Draft.SelectTarget(id));
    }

    private void OpenStrategicSelection(StrategicOrdersWorkspace owner, int title,
        IReadOnlyList<CommandSelectionRow> rows, int? selected, Func<int, bool> accept)
    {
        if (!CanEditStrategicOrders(owner)) return;
        var view = new CommandSelectionView(); owner.Selection = view; AddChild(view);
        owner.View.SetBusy(true); owner.View.Hide();
        try
        {
            view.Configure(_repository!, _ui, title, [new("name", PersonnelDataText(title), 1464)], rows,
                selected is int id ? [id] : [], 1, () => IsStrategicOrdersCurrent(owner) && owner.Selection == view,
                result => RunUiAction(() =>
                {
                    if (!IsStrategicOrdersCurrent(owner) || owner.Selection != view) return;
                    owner.Selection = null; view.Abandon(); view.GetParent()?.RemoveChild(view); view.QueueFree();
                    owner.View.Show(); owner.View.SetBusy(false);
                    if (result is { Count: 1 }) accept(result[0]);
                    RefreshStrategicOrders(owner);
                }), CurrentListScrollSpeed, requireSelection: true, singleSelection: true);
        }
        catch
        {
            owner.Selection = null; view.Abandon(); view.GetParent()?.RemoveChild(view); view.QueueFree();
            owner.View.Show(); owner.View.SetBusy(false); throw;
        }
    }

    private void OpenStrategicMilitaryCalculator(StrategicOrdersWorkspace owner)
    {
        if (!CanEditStrategicOrders(owner)) return;
        var options = owner.Draft.QueryMilitaryIntelligenceAmountOptions();
        if (options.UnresolvedOwner is { } missing) { StrategicOrdersUnavailable(missing); return; }
        if (options.Minimum is not int minimum || options.Maximum is not int maximum) return;
        var view = new DeploymentCalculatorDialog(); owner.Calculator = view; AddChild(view); owner.View.SetBusy(true);
        try
        {
            view.Configure(_repository!, _ui, options.Current, minimum, maximum, result => RunUiAction(() =>
            {
                if (!IsStrategicOrdersCurrent(owner) || owner.Calculator != view) return;
                owner.Calculator = null; view.Abandon(); view.GetParent()?.RemoveChild(view); view.QueueFree();
                owner.View.SetBusy(false);
                if (result is int amount) owner.Draft.SetMilitaryIntelligence(amount);
                RefreshStrategicOrders(owner);
            }), panelPosition: new(924, 468), titleMessage: 0x835);
        }
        catch
        { owner.Calculator = null; view.Abandon(); view.GetParent()?.RemoveChild(view); view.QueueFree(); owner.View.SetBusy(false); throw; }
    }

    private void SubmitStrategicOrders(StrategicOrdersWorkspace owner, bool keepOpen)
    {
        if (!CanEditStrategicOrders(owner) || !owner.Draft.CanConfirm || keepOpen && !owner.Draft.CanContinue ||
            owner.Draft.BuildOrder() is not { } order) return;
        owner.View.SetBusy(true);
        if (!ApplyPlayableOrder(order, () =>
        {
            CloseStrategicOrders();
            if (keepOpen) _strategicOrdersContinuation = new(owner.Session, owner.Map, owner.Draft.Kind);
        })) RefreshStrategicOrders(owner);
    }

    private bool TryResumeStrategicOrders()
    {
        if (_strategicOrdersContinuation is not { } next) return false;
        if (_failed || _shuttingDown || IsReturningToTitle || _session != next.Session || _residentMap != next.Map ||
            next.Session.World.Phase != TurnPhase.Strategy)
        { _strategicOrdersContinuation = null; return false; }
        if (HasAdministrationPage || HasMilitaryOrderPage || HasEnvironmentSettings || HasCampaignSavePage || HasMapSystemMenu ||
            HasCommandWorkspace || HasPersonnelConfirmation || _detailsView is not null || _diplomacyNegotiation is not null) return false;
        _strategicOrdersContinuation = null; OpenStrategicOrders(next.Kind); return HasStrategicOrdersPage;
    }

    private void RequestStrategicOrdersBack()
    {
        if (_strategicOrders is not { } owner || !IsStrategicOrdersCurrent(owner)) return;
        if (owner.Calculator is { } calculator) calculator.RequestCancel();
        else if (owner.Selection is { } selector) selector.RequestCancel();
        else CloseStrategicOrders();
    }

    private void CloseStrategicOrders()
    {
        var owner = _strategicOrders; _strategicOrders = null;
        if (owner is null) return;
        if (owner.Selection is { } selection)
        { owner.Selection = null; selection.Abandon(); selection.GetParent()?.RemoveChild(selection); selection.QueueFree(); }
        if (owner.Calculator is { } calculator)
        { owner.Calculator = null; calculator.Abandon(); calculator.GetParent()?.RemoveChild(calculator); calculator.QueueFree(); }
        owner.View.Abandon(); owner.View.GetParent()?.RemoveChild(owner.View); owner.View.QueueFree();
        if (GodotObject.IsInstanceValid(owner.Map) && _residentMap == owner.Map)
        { owner.Map.SetDetailsOpen(false); if (!_failed && !_shuttingDown) SetMapFocus(_mapFocused); }
    }

    private void StrategicOrdersUnavailable(string missing)
    { _playMessage.Text = _ui.Get("information_partial"); GD.PushWarning(missing); }
}
