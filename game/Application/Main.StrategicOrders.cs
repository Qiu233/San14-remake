using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private enum StrategicSelectionPurpose { None, Target, SecondForce, Executor }
    private sealed class StrategicOrdersWorkspace(GameplaySession session, GameState world, MapSceneView map,
        StrategicCommandDraft draft, AreaAdministrationOfficerContent cards)
    {
        internal GameplaySession Session { get; } = session;
        internal GameState World { get; set; } = world;
        internal MapSceneView Map { get; } = map;
        internal StrategicCommandDraft Draft { get; } = draft;
        internal AreaAdministrationOfficerContent Cards { get; } = cards;
        internal StrategicOrdersView View { get; set; } = null!;
        internal StrategicSelectionView? Selection { get; set; }
        internal StrategicSelectionPurpose SelectionPurpose { get; set; }
        internal long SelectionAcceptedSequence { get; set; }
        internal StrategicSelectionPurpose LastAcceptedPurpose { get; set; }
        internal int? LastAcceptedId { get; set; }
        internal Action? Returned { get; init; }
        internal int? InitialTargetId { get; init; }
        internal DeploymentCalculatorDialog? Calculator { get; set; }
    }
    private sealed record StrategicOrdersContinuation(GameplaySession Session, MapSceneView Map, int Kind,
        int? InitialTargetId, Action? Returned);
    private StrategicOrdersWorkspace? _strategicOrders;
    private StrategicOrdersContinuation? _strategicOrdersContinuation;
    private Func<int,bool>? StrategicTargetAdmission;
    private Func<int,bool>? StrategicExecutorAdmission;
    private bool StrategicSelectionReturnAllowed=true;
    private bool StrategicSelectionAllowed(StrategicSelectionPurpose purpose,int id) =>
        purpose==StrategicSelectionPurpose.Target ? StrategicTargetAdmission?.Invoke(id)??true :
        purpose==StrategicSelectionPurpose.Executor ? StrategicExecutorAdmission?.Invoke(id)??true : true;
    private void RefreshStrategicSelectionAdmission()
    {
        if(_strategicOrders is not {} owner)return;
        owner.Selection?.ApplySelectionAdmission(id=>StrategicSelectionAllowed(owner.SelectionPurpose,id),
            StrategicSelectionReturnAllowed);
        owner.View.SetReturnAllowed(StrategicSelectionReturnAllowed);
    }
    private bool HasStrategicOrdersPage => _strategicOrders is not null;
    private bool IsStrategicOrdersCurrent(StrategicOrdersWorkspace owner) => !_failed && !_shuttingDown &&
        _strategicOrders == owner && _session == owner.Session && ReferenceEquals(owner.World, owner.Session.World) &&
        _residentMap == owner.Map;
    private void AdoptStrategicOrdersSpeechWorld(GameplaySession session, GameState previous, GameState replacement)
    {
        // The common speech dispatcher admits the committed presentation-only
        // successor. Keep the draft, cached advice and any selector/calculator.
        if (_strategicOrders is not { } owner || owner.Session != session || _residentMap != owner.Map ||
            !ReferenceEquals(owner.World, previous)) return;
        owner.World = replacement;
    }

    private bool CanEditStrategicOrders(StrategicOrdersWorkspace owner) => IsStrategicOrdersCurrent(owner) &&
        owner.Selection is null && owner.Calculator is null;

    private void OpenStrategicOrders(int kind, int? initialTargetId = null, Action? returned = null)
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
        if (initialTargetId is int initial && !draft.SelectTarget(initial))
        { StrategicOrdersUnavailable("strategic.page.initial_target"); return; }
        var owner = new StrategicOrdersWorkspace(session, world, map, draft, new())
            { Returned = returned, InitialTargetId = initialTargetId };
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
                choice => RunUiAction(() => ChangeStrategicOrders(owner, () => draft.SetIndependenceChoice(choice))), resources: TryBorrowApplicationUiResources(view));
            map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true);
            SetMapFocus(true); HideDeploymentHud(); RefreshStrategicOrders(owner);
            if(kind==20)ObserveTutorialInformation(37);
            BindCurrentTutorialStrategic();
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
        if(!CanEditStrategicOrders(owner))return;
        var prepared=owner.Draft.ReadExecutorSelection();
        if(!prepared.Available||prepared.Value is not {} publication)
        {StrategicOrdersUnavailable(prepared.UnresolvedInput??"strategic.selection.executors");return;}
        OpenStrategicSelection(owner,StrategicSelectionPurpose.Executor,publication,owner.Draft.SelectExecutor);
    }

    private void OpenStrategicTargetSelection(StrategicOrdersWorkspace owner,bool second)
    {
        if(!CanEditStrategicOrders(owner))return;
        var prepared=owner.Draft.ReadTargetSelection(second);
        if(!prepared.Available||prepared.Value is not {} publication)
        {StrategicOrdersUnavailable(prepared.UnresolvedInput??"strategic.selection.targets");return;}
        OpenStrategicSelection(owner,second?StrategicSelectionPurpose.SecondForce:StrategicSelectionPurpose.Target,
            publication,id=>second?owner.Draft.SelectSecondForce(id):owner.Draft.SelectTarget(id));
    }

    private void OpenStrategicSelection(StrategicOrdersWorkspace owner,StrategicSelectionPurpose purpose,
        StrategicSelectionPage publication,Func<int,bool> accept)
    {
        if(!CanEditStrategicOrders(owner)||publication.Eligibility.Count==0)return;
        var view=new StrategicSelectionView();owner.Selection=view;AddChild(view);
        owner.SelectionPurpose=purpose;owner.View.SetBusy(true);owner.View.Hide();
        bool Current()=>IsStrategicOrdersCurrent(owner)&&owner.Selection==view;
        void BindMode(bool list)
        {
            if(!Current())return;
            owner.Map.EndStrategicTargetPick(view);owner.Map.SetDetailsOpen(list,keepMapVisible:true);
            if(!list)owner.Map.BeginStrategicTargetPick(view,owner.World,
                publication.MapSelection??throw new InvalidDataException("strategic.selection.map_publication"),
                ()=>Current()&&view.IsMapMode,view.CanChooseTarget,id=>RunUiAction(()=>{if(Current())view.PickMapTarget(id);}));
            HideDeploymentHud();
        }
        view.TreeExiting+=()=>{if(GodotObject.IsInstanceValid(owner.Map))owner.Map.EndStrategicTargetPick(view);};
        try
        {
            IntelligenceSelectionView.MapMode? mapMode=publication.MapSelection is {} mapSource?
                new(mapSource.TitleMessageId,list=>RunUiAction(()=>BindMode(list)),owner.Map.SourceRightCancelReleased,
                    InitialMap:mapSource.InitialMap):null;
            view.Configure(_repository!,_ui,publication,Current,chosen=>RunUiAction(()=>
            {
                if(!Current()||chosen is int blocked&&!StrategicSelectionAllowed(purpose,blocked)||
                    chosen is null&&!StrategicSelectionReturnAllowed)return;
                owner.Map.EndStrategicTargetPick(view);owner.Map.SetDetailsOpen(true,keepMapVisible:true);
                owner.Selection=null;owner.SelectionPurpose=StrategicSelectionPurpose.None;
                view.Abandon();view.GetParent()?.RemoveChild(view);view.QueueFree();owner.View.Show();owner.View.SetBusy(false);
                if(chosen is int accepted&&accept(accepted))
                {
                    owner.LastAcceptedPurpose=purpose;owner.LastAcceptedId=accepted;
                    owner.SelectionAcceptedSequence=checked(owner.SelectionAcceptedSequence+1);
                }
                RefreshStrategicOrders(owner);
            }),CurrentListScrollSpeed,mapMode);
            BindCurrentTutorialStrategic();RefreshStrategicSelectionAdmission();
            if(mapMode is not null)BindMode(!view.IsMapMode);
        }
        catch
        {
            owner.Map.EndStrategicTargetPick(view);owner.Map.SetDetailsOpen(true,keepMapVisible:true);
            owner.Selection=null;owner.SelectionPurpose=StrategicSelectionPurpose.None;
            view.Abandon();view.GetParent()?.RemoveChild(view);view.QueueFree();owner.View.Show();owner.View.SetBusy(false);throw;
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
            CloseStrategicOrders(notifyReturned: !keepOpen);
            if (keepOpen) _strategicOrdersContinuation = new(owner.Session, owner.Map, owner.Draft.Kind,
                owner.InitialTargetId, owner.Returned);
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
        _strategicOrdersContinuation = null; OpenStrategicOrders(next.Kind, next.InitialTargetId, next.Returned);
        if (!HasStrategicOrdersPage) next.Returned?.Invoke();
        return HasStrategicOrdersPage;
    }

    private void RequestStrategicOrdersBack()
    {
        if (_strategicOrders is not { } owner || !IsStrategicOrdersCurrent(owner)) return;
        if(!StrategicSelectionReturnAllowed)return;
        if (owner.Calculator is { } calculator) calculator.RequestCancel();
        else if (owner.Selection is { } selector) selector.RequestCancel();
        else CloseStrategicOrders(notifyReturned: true);
    }

    private void CloseStrategicOrders(bool notifyReturned = false)
    {
        StrategicTargetAdmission=StrategicExecutorAdmission=null;StrategicSelectionReturnAllowed=true;
        var owner = _strategicOrders; _strategicOrders = null;
        if (owner is null) return;
        if (owner.Selection is { } selection)
        { if(GodotObject.IsInstanceValid(owner.Map))owner.Map.EndStrategicTargetPick(selection);owner.Selection = null; selection.Abandon(); selection.GetParent()?.RemoveChild(selection); selection.QueueFree(); }
        if (owner.Calculator is { } calculator)
        { owner.Calculator = null; calculator.Abandon(); calculator.GetParent()?.RemoveChild(calculator); calculator.QueueFree(); }
        owner.View.Abandon(); owner.View.GetParent()?.RemoveChild(owner.View); owner.View.QueueFree();
        if (GodotObject.IsInstanceValid(owner.Map) && _residentMap == owner.Map)
        { owner.Map.SetDetailsOpen(false); if (!_failed && !_shuttingDown) SetMapFocus(_mapFocused); }
        if (notifyReturned) owner.Returned?.Invoke();
    }

    private void StrategicOrdersUnavailable(string missing)
    { _playMessage.Text = _ui.Get("information_partial"); GD.PushWarning(missing); }
}
