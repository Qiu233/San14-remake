using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private ProposalView? _proposalResultsView;
    private GameState? _proposalResultsWorld;
    private MapSceneView? _proposalResultsMap;
    private bool HasProposalResults => _proposalResultsView is not null;
    private OfficerDetailsView? _proposalOfficerDetail;
    private MapSceneView? _proposalOfficerDetailMap;
    private bool HasProposalOfficerDetail => _proposalOfficerDetail is not null;

    private void RetainProposalEffectSpeechWorld(Control view, GameplaySession session, MapSceneView map,
        Func<bool> active, Func<GameState> readWorld, Action<GameState> adoptWorld)
    {
        Action<GameState, GameState> retained = (previous, replacement) =>
        {
            if (!active() || _session != session || _residentMap != map ||
                !ReferenceEquals(readWorld(), previous)) return;
            adoptWorld(replacement);
        };
        _proposalEffectsAdoptSpeechWorld = retained;
        _proposalEffectsCurrent = () => active() && _session == session && _residentMap == map &&
            ReferenceEquals(PersonnelPresentationOwner(session), readWorld());
        view.TreeExiting += () =>
        {
            if (ReferenceEquals(_proposalEffectsAdoptSpeechWorld, retained))
            {
                _proposalEffectsAdoptSpeechWorld = null;
                _proposalEffectsCurrent = null;
            }
        };
    }

    private bool TryPresentProposalEffects(GameplaySession session, CampaignComposition composition, MapSceneView map)
    {
        while (PersonnelPresentationQueue.Next(PersonnelPresentationOwner(session)) is { Source: CampaignEffectSource.ProposalCommands } entry)
        {
            var owner = PersonnelPresentationOwner(session);
            var rules = composition.PrepareArmyQueries(owner);
            if (entry.Request.ProposalOfficerDetail is { } detail)
            {
                var view = new OfficerDetailsView(); _proposalOfficerDetail = view; _proposalOfficerDetailMap = map;
                AddChild(view);
                RetainProposalEffectSpeechWorld(view, session, map, () => _proposalOfficerDetail == view,
                    () => owner, next => { owner = next; view.AdoptRetainedSpeechWorld(next); });
                view.BindRetainedSpeechHost(Current);
                try
                {
                    ConfigureCurrentOfficerDetail(view, owner, detail.OfficerId, CompleteDetails, Current, next => owner = next);
                    view.RegisterHeader(_sourceHeaders);
                    map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true); SetMapFocus(false); HideDeploymentHud();
                    return true;
                }
                catch { CloseProposalOfficerDetail(); throw; }
                bool Current() => !_failed && !_shuttingDown && _proposalOfficerDetail == view && _session == session &&
                    ReferenceEquals(PersonnelPresentationOwner(session), owner) && _residentMap == map;
                void CompleteDetails()
                {
                    if (!Current()) return;
                    CloseProposalOfficerDetail();
                    if (session.TryCompleteProposalPresentation(owner, entry.BatchSequence, entry.RequestIndex)) Continue();
                }
            }
            if (entry.Request.MonthlyMaintenance is { } scene)
            {
                var route = rules.QueryProposalScene(owner, scene);
                if (!route.Available) { GD.PushWarning(route.UnresolvedInput); return false; }
                if (route.Value is { } illustration) ShowMonthlyScene(session, map, illustration); else CloseMonthlyScene();
                if (!session.TryCompleteProposalPresentation(owner, entry.BatchSequence, entry.RequestIndex)) return false;
                continue;
            }
            if (entry.Request.ProposalResultTable is { } table)
            {
                var sprites = new LocalizedUiSpriteSource(_repository!);
                var rows = table.Rows.Select(row => new ProposalResultLine(row.NameAtSource, row.BeforeText, row.AfterText,
                    row.OfficerAtSource is { } person ? Portrait(person) : null)).ToArray();
                var view = new ProposalView(); _proposalResultsView = view; _proposalResultsWorld = owner; _proposalResultsMap = map;
                AddChild(view);
                RetainProposalEffectSpeechWorld(view, session, map, () => _proposalResultsView == view,
                    () => owner, next => { owner = next; _proposalResultsWorld = next; });
                try
                {
                    view.ConfigureResults(_repository!, _ui, Current, CompleteTable, RunUiAction, rows);
                    map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true); SetMapFocus(false); HideDeploymentHud();
                    return true;
                }
                catch { CloseProposalResults(); throw; }
                bool Current() => !_failed && !_shuttingDown && _proposalResultsView == view && _session == session &&
                    ReferenceEquals(PersonnelPresentationOwner(session), owner) && _residentMap == map;
                void CompleteTable()
                {
                    if (!Current()) return;
                    CloseProposalResults();
                    if (session.TryCompleteProposalPresentation(owner, entry.BatchSequence, entry.RequestIndex)) Continue();
                }
                LoadedSprite? Portrait(TalentOfficerPresentationContext person)
                {
                    var query = rules.QueryProposalPortrait(owner, person);
                    if (query.Value is { Kind: OfficerPortraitRouteKind.StockNamed, ResourceName: not null, SpriteName: not null } face)
                        return sprites.LoadTexturePackSprite(face.ResourceName, face.SpriteName);
                    throw new InvalidDataException(query.UnresolvedInput ?? "proposal.result_portrait");
                }
            }
            var payload = entry.Request.TalentCommandPresentation;
            if (payload?.Operation == TalentCommandPresentationOperation.SoundCue)
            {
                if (_sceneAudioBinding is not { } audio || !audio.PlayPersonnelPresentationAudio(payload, null, _preferences.Voice.Language))
                    throw new InvalidDataException("proposal.sound_queue");
                if (!session.TryCompleteProposalPresentation(owner, entry.BatchSequence, entry.RequestIndex)) return false;
                continue;
            }
            if (payload?.Operation is not (TalentCommandPresentationOperation.Announcement or TalentCommandPresentationOperation.ConditionalOfficerText))
                return false;
            var narration = new DeploymentNarrationView { KeyboardInput = HandleGameplayNarrationKeyboard };
            _personnelPresentation = narration; _personnelPresentationSession = session;
            _personnelPresentationWorld = owner; _personnelPresentationMap = map; AddChild(narration);
            RetainProposalEffectSpeechWorld(narration, session, map, () => _personnelPresentation == narration,
                () => owner, next => { owner = next; _personnelPresentationWorld = next; });
            try
            {
                var resources = TryBorrowApplicationUiResources(narration);
                if (payload.Operation == TalentCommandPresentationOperation.Announcement)
                    narration.ConfigureAnnouncement(_repository!, _ui, payload.TextAtSource!, Completed,
                        () => _preferences.AutomaticMessageAdvance, Admitted, resources: resources);
                else
                {
                    var person = payload.Speaker ?? throw new InvalidDataException("proposal.source_speaker");
                    var query = rules.QueryProposalPortrait(owner, person);
                    LoadedSprite? face = null;
                    if (query.Value is { Kind: OfficerPortraitRouteKind.StockNamed, ResourceName: not null, SpriteName: not null } portrait)
                        face = new LocalizedUiSpriteSource(_repository!).LoadTexturePackSprite(portrait.ResourceName, portrait.SpriteName);
                    else throw new InvalidDataException(query.UnresolvedInput ?? "proposal.speaker_portrait");
                    narration.ConfigurePersonMessage(_repository!, _ui, person.NameAtSource, payload.TextAtSource!, face,
                        Completed, () => _preferences.AutomaticMessageAdvance, Admitted, resources: resources);
                }
                if (payload.Voice is { } voice && _preferences.Voice.Enabled)
                {
                    if (_sceneAudioBinding is null) throw new InvalidOperationException("proposal.voice_owner");
                    _sceneAudioBinding.PlayPersonnelNarrationVoice(voice); _personnelPresentationOwnsVoice = true;
                }
                narration.Name = "ProposalPresentation"; narration.ZIndex = 95;
                map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true); SetMapFocus(false); HideDeploymentHud();
                return true;
            }
            catch { ClosePersonnelPresentation(); throw; }
            bool Admitted() => CanAdvanceNarration && _personnelPresentation == narration && IsPersonnelPresentationCurrent;
            void Completed() => RunUiAction(() =>
            {
                if (_personnelPresentation != narration || !IsPersonnelPresentationCurrent) return;
                ClosePersonnelPresentation();
                if (session.TryCompleteProposalPresentation(owner, entry.BatchSequence, entry.RequestIndex)) Continue();
            });
        }
        return false;
        void Continue()
        {
            if (!TryOpenPersonnelPresentation()) { RefreshPlayControls(); RefreshDiplomaticNegotiation(); }
        }
    }

    private void CloseProposalResults()
    {
        var view = _proposalResultsView; var map = _proposalResultsMap;
        _proposalResultsView = null; _proposalResultsWorld = null; _proposalResultsMap = null;
        if (view is not null) { view.Abandon(); view.GetParent()?.RemoveChild(view); view.QueueFree(); }
        if (map is not null && GodotObject.IsInstanceValid(map) && _residentMap == map) map.SetDetailsOpen(false);
    }

    private void CloseProposalOfficerDetail()
    {
        var view = _proposalOfficerDetail; var map = _proposalOfficerDetailMap;
        _proposalOfficerDetail = null; _proposalOfficerDetailMap = null;
        if (view is not null) { view.GetParent()?.RemoveChild(view); view.QueueFree(); }
        if (map is not null && GodotObject.IsInstanceValid(map) && _residentMap == map) map.SetDetailsOpen(false);
    }
}
