using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class DoctrineResearchWorkspace(GameplaySession session, GameState world,
        MapSceneView map, DoctrineResearchDraft draft)
    {
        internal GameplaySession Session { get; } = session;
        internal GameState World { get; } = world;
        internal MapSceneView Map { get; } = map;
        internal DoctrineResearchDraft Draft { get; } = draft;
        internal DoctrineResearchView View { get; set; } = null!;
        internal DeploymentTargetConfirmation? Question { get; set; }
        internal MapInformationListView? Effects { get; set; }
    }
    private DoctrineResearchWorkspace? _doctrineResearch;
    private bool HasDoctrineResearchPage => _doctrineResearch is not null;
    private bool IsDoctrineResearchCurrent(DoctrineResearchWorkspace owner) => !_failed && !_shuttingDown &&
        _doctrineResearch == owner && _session == owner.Session && _residentMap == owner.Map &&
        ReferenceEquals(owner.Session.World, owner.World);
    private bool CanEditDoctrineResearch(DoctrineResearchWorkspace owner) => IsDoctrineResearchCurrent(owner) &&
        owner.Question is null && owner.Effects is null;

    private void OpenDoctrineResearch()
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || HasAdministrationPage || HasMilitaryOrderPage ||
            HasEnvironmentSettings || HasCampaignSavePage || HasMapSystemMenu || HasCommandWorkspace || HasActionReport ||
            HasPersonnelConfirmation || _detailsView is not null || _repository is null ||
            _campaignComposition is not { } composition || _residentMap is not { IsPlayableMapReady: true } map ||
            _session is not { PlayerForceId: int player } session || session.HasPendingOperation ||
            session.World is not { Phase: TurnPhase.Strategy } world || PlayableOutcome.Evaluate(world, player) != PlayableResult.Ongoing) return;
        var rules = composition.PrepareArmyQueries(world);
        //This normal strategy page is the live user-strategy host; no other
        //mode or suspended transaction borrows its controller availability.
        bool hostAvailable = ReferenceEquals(_world, world) && _residentMap == map && _session == session && !_advancingPeriod;
        var draft = rules.OpenDoctrineResearchDraft(world, player, hostAvailable);
        if (draft.UnresolvedOwner is { } missing) { DoctrineResearchUnavailable(missing); return; }
        var owner = new DoctrineResearchWorkspace(session, world, map, draft);
        _doctrineResearch = owner;
        var view = new DoctrineResearchView(); owner.View = view; AddChild(view);
        try
        {
            LoadedSprite[] layers = [];
            if (draft.RulerId is int ruler)
            {
                var actor = rules.QueryPersonnelPresentationSpeaker(world, ruler);
                if (actor.Value?.StandingPortrait is { } standing)
                {
                    var sprites = new LocalizedUiSpriteSource(_repository);
                    layers = new[] { standing.SpriteB, standing.SpriteA, standing.SpriteC }
                        .Select(name => sprites.LoadTexturePackSprite(standing.ResourceName, name)).ToArray();
                }
                else if ((actor.Value?.PortraitUnresolvedInput ?? actor.UnresolvedInput) is { } portraitMissing)
                    GD.PushWarning(portraitMissing);
            }
            view.Configure(_repository, _ui, () => CanEditDoctrineResearch(owner),
                () => RunUiAction(() => RequestDoctrineResearchBack()),
                () => RunPlayAction(() => SubmitDoctrineResearch(owner)),
                id => RunUiAction(() => { if (CanEditDoctrineResearch(owner) && draft.SelectPanel(id)) RefreshDoctrineResearch(owner); }),
                () => RunUiAction(() =>
                {
                    if (CanEditDoctrineResearch(owner) && draft.SelectedPanelId is int id && draft.Research(id)) RefreshDoctrineResearch(owner);
                    else if (draft.LastError is { } error) DoctrineResearchUnavailable(error);
                }),
                () => RunUiAction(() => RedefineDoctrineResearch(owner)),
                () => RunUiAction(() =>
                {
                    if (CanEditDoctrineResearch(owner) && draft.AutoResearch()) RefreshDoctrineResearch(owner);
                    else if (draft.LastError is { } error) DoctrineResearchUnavailable(error);
                }),
                () => RunUiAction(() => OpenDoctrineResearchEffects(owner)), layers);
            map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true);
            SetMapFocus(true); HideDeploymentHud(); RefreshDoctrineResearch(owner);
        }
        catch { CloseDoctrineResearch(); throw; }
    }

    private void RefreshDoctrineResearch(DoctrineResearchWorkspace owner)
    {
        if (!IsDoctrineResearchCurrent(owner)) return;
        var draft = owner.Draft;
        var hint = draft.SelectedPanelId is null ? draft.QueryGuide() : draft.QuerySelectedGuide();
        if (hint.UnresolvedInput is { } missing) GD.PushWarning(missing);
        string unknown = _ui.Get("detail_unknown");
        var identity = new Dictionary<int, string>
        {
            [6] = draft.ForceName ?? unknown,
            [10] = draft.RulerDoctrineId is int doctrine ? owner.World.Doctrines.GetValueOrDefault(doctrine)?.Name ?? unknown : unknown,
            [14] = draft.TitleName ?? unknown,
            [18] = draft.ResearchCount?.ToString() ?? unknown,
            [20] = draft.ResearchCapacity?.ToString() ?? unknown
        };
        owner.View.Apply(new(draft.Rows, draft.ReservePoints, draft.Budget, draft.SelectedPanel, identity,
            draft.CanConfirm, draft.CanRedefine, draft.CanAutoResearch,
            PersonnelDataText(draft.SelectedActionCaptionId), hint.Value ?? _ui.Get("information_partial")));
    }

    private void SubmitDoctrineResearch(DoctrineResearchWorkspace owner)
    {
        if (!CanEditDoctrineResearch(owner) || !owner.Draft.CanConfirm) return;
        owner.View.SetBusy(true);
        if (!ApplyPlayableOrder(owner.Draft.BuildOrder(), CloseDoctrineResearch))
        { owner.View.SetBusy(false); RefreshDoctrineResearch(owner); }
    }

    private void RedefineDoctrineResearch(DoctrineResearchWorkspace owner)
    {
        if (!CanEditDoctrineResearch(owner) || !owner.Draft.CanRedefine) return;
        void Apply()
        {
            if (owner.Draft.Redefine()) RefreshDoctrineResearch(owner);
            else if (owner.Draft.LastError is { } error) DoctrineResearchUnavailable(error);
        }
        if (owner.Draft.Redefined) Apply();
        else AskDoctrineResearch(owner, owner.Draft.QueryRedefineConfirmation(), Apply);
    }

    private void RequestDoctrineResearchBack(bool escape = false)
    {
        if (_doctrineResearch is not { } owner || !IsDoctrineResearchCurrent(owner)) return;
        if (owner.Question is { } question) { question.RequestCancel(); return; }
        if (owner.Effects is not null) { CloseDoctrineResearchEffects(owner); return; }
        if (escape && owner.Draft.SelectedPanelId is not null)
        { owner.Draft.ClearSelection(); RefreshDoctrineResearch(owner); return; }
        if (!owner.Draft.HasChanges) { CloseDoctrineResearch(); return; }
        AskDoctrineResearch(owner, owner.Draft.QueryDiscardConfirmation(), () =>
        { owner.Draft.DiscardChanges(); CloseDoctrineResearch(); });
    }

    private void AskDoctrineResearch(DoctrineResearchWorkspace owner, AreaAdministrationQuery<string> message, Action accepted)
    {
        if (!CanEditDoctrineResearch(owner)) return;
        if (!message.Available || message.Value is null)
        { DoctrineResearchUnavailable(message.UnresolvedInput ?? "doctrine-research.confirmation"); return; }
        var question = new DeploymentTargetConfirmation(); owner.Question = question; AddChild(question); owner.View.SetBusy(true);
        question.ConfigureWrappedMessage(_repository!, _ui, message.Value, answer => RunUiAction(() =>
        {
            if (!IsDoctrineResearchCurrent(owner) || owner.Question != question) return;
            owner.Question = null; question.Abandon(); question.GetParent()?.RemoveChild(question); question.QueueFree();
            owner.View.SetBusy(false);
            if (answer) accepted();
        }));
    }

    private void OpenDoctrineResearchEffects(DoctrineResearchWorkspace owner)
    {
        if (!CanEditDoctrineResearch(owner)) return;
        var guide = owner.Draft.QueryEffectsGuide();
        if (!guide.Available) { DoctrineResearchUnavailable(guide.UnresolvedInput ?? "doctrine-research.effects_guide"); return; }
        var rows = owner.Draft.EffectRows.Select(row => new MapInformationListRow(row.PanelId, [row.Name], true)).ToArray();
        var view = new MapInformationListView { ZIndex = 8 }; owner.Effects = view; AddChild(view); owner.View.SetBusy(true);
        view.Configure(_repository!, _ui, PersonnelDataText(0x829), [new("name", PersonnelDataText(0x829), 1464)], rows,
            () => IsDoctrineResearchCurrent(owner) && owner.Effects == view,
            () => RunUiAction(() => CloseDoctrineResearchEffects(owner)),
            id => RunUiAction(() =>
            { CloseDoctrineResearchEffects(owner); if (owner.Draft.SelectPanel(id)) RefreshDoctrineResearch(owner); }),
            CurrentListScrollSpeed, notice: guide.Value);
    }

    private void CloseDoctrineResearchEffects(DoctrineResearchWorkspace owner)
    {
        var view = owner.Effects; owner.Effects = null;
        if (view is not null) { view.Abandon(); view.GetParent()?.RemoveChild(view); view.QueueFree(); }
        owner.View.SetBusy(false);
    }

    private void CloseDoctrineResearch()
    {
        var owner = _doctrineResearch; _doctrineResearch = null;
        if (owner is null) return;
        CloseDoctrineResearchEffects(owner);
        if (owner.Question is { } question)
        { owner.Question = null; question.Abandon(); question.GetParent()?.RemoveChild(question); question.QueueFree(); }
        owner.View.Abandon(); owner.View.GetParent()?.RemoveChild(owner.View); owner.View.QueueFree();
        if (GodotObject.IsInstanceValid(owner.Map) && _residentMap == owner.Map)
        { owner.Map.SetDetailsOpen(false); if (!_failed && !_shuttingDown) SetMapFocus(_mapFocused); }
    }

    private void DoctrineResearchUnavailable(string missing)
    { _playMessage.Text = _ui.Get("information_partial"); GD.PushWarning(missing); }
}
