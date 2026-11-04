using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class DuelCaptureWorkspace(GameplaySession session, GameState world, MapSceneView map,
        PersonnelPresentationEntry entry, DuelCaptureAnnouncementRequest request,
        CampaignPeriodPlayback? playback, CancellationToken cancellation, bool mapWasFocused)
    {
        internal readonly GameplaySession Session = session;
        internal GameState World = world;
        internal readonly bool RetainedHiredOwner = playback is null &&
            ReferenceEquals(session.PendingNormalPeriod?.Candidate ?? session.PendingArmyProgressCandidate, world) &&
            HiredArmyCaptureProgress.IsPresentationPending(world);
        internal readonly bool OwnedHiredWait = playback is null &&
            ReferenceEquals(session.PendingNormalPeriod?.Candidate ?? session.PendingArmyProgressCandidate, world) &&
            HiredArmyCaptureProgress.IsAwaitingAnnouncement(world) &&
            world.ArmyDefeatContinuation!.HiredCapture!.AnnouncementBatchSequence == entry.BatchSequence;
        internal readonly MapSceneView Map = map;
        internal readonly PersonnelPresentationEntry Entry = entry;
        internal readonly DuelCaptureAnnouncementRequest Request = request;
        internal readonly CampaignPeriodPlayback? Playback = playback;
        internal readonly bool MapWasFocused = mapWasFocused;
        internal readonly CancellationTokenSource Lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        internal DeploymentNarrationView? View;
        internal bool Running = true;
    }

    private DuelCaptureWorkspace? _duelCaptureAnnouncement;
    private bool HasDuelCaptureAnnouncement => _duelCaptureAnnouncement is not null;
    private bool IsDuelCaptureAnnouncementCurrent => _duelCaptureAnnouncement is { } owner && CurrentDuelCapture(owner);
    private bool CurrentDuelCapture(DuelCaptureWorkspace owner) => ReferenceEquals(_session, owner.Session) &&
        ReferenceEquals(_residentMap, owner.Map) && (owner.Playback is { } playback
            ? ReferenceEquals(_periodPlayback, playback) && ReferenceEquals(playback.Candidate, owner.World)
            : ReferenceEquals(PersonnelPresentationOwner(owner.Session), owner.World));

    private bool HandleDuelCaptureNarrationKeyboard(InputEvent input) =>
        _duelCaptureAnnouncement is { } owner && CurrentDuelCapture(owner) && owner.View is { } view &&
        HandleNarrationKeyboard(input, view.RequestKeyboardMessageMode,
            allowDuringPeriod: owner.Playback is not null);

    private bool TryPresentDuelCaptureAnnouncement(PersonnelPresentationEntry entry,
        DuelCaptureAnnouncementRequest request, GameplaySession session, GameState world,
        MapSceneView map, CampaignComposition composition, CampaignPeriodPlayback? playback = null)
    {
        if (entry.Source != CampaignEffectSource.ArmyBattleDefeat || request.TextAtSource is null)
        {
            const string missing = "duel.capture.source_time_text_unprepared";
            if (playback is not null) throw new InvalidDataException(missing);
            GD.PushWarning(missing); _personnelPresentationInspectedOwner = world; return false;
        }
        if (_duelCaptureAnnouncement is not null) throw new InvalidOperationException("duel.capture.concurrent_host");
        IReadOnlyList<LoadedSprite> layers = [];
        if (request.MessageRoot == 0x2A8C)
        {
            var speaker = request.Speaker ?? throw new InvalidDataException("duel.capture.source_actor");
            var query = composition.PrepareArmyQueries(world).QueryPersonnelPresentationSpeaker(world, speaker);
            var actor = query.Value ?? throw new InvalidDataException(query.UnresolvedInput);
            var standing = actor.StandingPortrait ?? throw new InvalidDataException(actor.PortraitUnresolvedInput ??
                "duel.capture.source_standing_portrait");
            var sprites = new LocalizedUiSpriteSource(_repository!);
            layers = standing.LoadSprites(sprites.LoadTexturePackSprite);
        }
        else if (request.MessageRoot != 0x34AA || request.Speaker is not null)
            throw new InvalidDataException("duel.capture.source_view");
        var audio = _sceneAudioBinding ?? throw new InvalidOperationException("duel.capture.scene_audio");
        var owner = new DuelCaptureWorkspace(session, world, map, entry, request, playback,
            playback is not null ? _periodAdvanceCancellation?.Token ?? CancellationToken.None : CancellationToken.None,
            _mapFocused);
        _duelCaptureAnnouncement = owner;
        RetirePeriodReportView(); CloseActionReport(); map.CloseCityCommandMenu();
        map.SetDetailsOpen(true, keepMapVisible: true); SetMapFocus(false); HideDeploymentHud();
        _ = Dispatch();
        return true;

        async Task Dispatch()
        {
            try
            {
                //405E40 precedes the view. This waits for actual ordinary SE
                //queue admission, not for playback completion.
                await audio.PlayNormalSoundAsync(request.SoundId, owner.Lifetime.Token);
                if (_duelCaptureAnnouncement != owner) return;
                if (!CurrentDuelCapture(owner)) { CloseDuelCaptureAnnouncement(); return; }
                var view = new DeploymentNarrationView { KeyboardInput = HandleDuelCaptureNarrationKeyboard };
                owner.View = view; AddChild(view);
                var resources = TryBorrowApplicationUiResources(view);
                if (request.MessageRoot == 0x34AA)
                    view.ConfigureAnnouncement(_repository!, _ui, request.TextAtSource, Completed,
                        () => _preferences.AutomaticMessageAdvance, Admitted, resources: resources);
                else view.Configure(_repository!, _ui, request.Speaker!.NameAtSource, request.TextAtSource,
                    layers, Completed, () => _preferences.AutomaticMessageAdvance, Admitted, resources: resources);
                view.Name = "DuelCaptureAnnouncement"; view.ZIndex = 95;
            }
            catch (OperationCanceledException) when (owner.Lifetime.IsCancellationRequested)
            { if (_duelCaptureAnnouncement == owner) CloseDuelCaptureAnnouncement(); }
            catch (Exception error)
            {
                if (_duelCaptureAnnouncement == owner)
                {
                    if (playback is not null) playback.FailPresentation(error);
                    CloseDuelCaptureAnnouncement();
                    if (playback is null) ReportPlayFailure(error);
                }
            }
            finally
            {
                owner.Running = false;
                if (_duelCaptureAnnouncement != owner) owner.Lifetime.Dispose();
            }
        }

        bool Admitted() => _duelCaptureAnnouncement == owner && CurrentDuelCapture(owner) && CanAdvanceNarration;
        void Completed() => RunUiAction(() =>
        {
            if (_duelCaptureAnnouncement != owner) return;
            if (!CurrentDuelCapture(owner)) { CloseDuelCaptureAnnouncement(); return; }
            bool accepted = playback is not null
                ? playback.TryCompleteDuelCaptureAnnouncement(owner.World, entry.Source, entry.BatchSequence, entry.RequestIndex, request)
                : session.TryCompleteDuelCaptureAnnouncement(owner.World, entry.Source, entry.BatchSequence, entry.RequestIndex, request,
                    owner.OwnedHiredWait ? new(_presentationRandom.State) : null);
            if (!accepted) throw new InvalidOperationException("duel.capture.completion_owner");
            CloseDuelCaptureAnnouncement();
            if (playback is not null)
            {
                var next = playback.Candidate!;
                map.PublishPeriodPlayback(playback, next);
                BeginPeriodOccupationPresentation(playback, map, next);
                BeginPeriodDuelCaptureAnnouncement(playback, map, next);
            }
            else if (!TryOpenPersonnelPresentation()) { RefreshPlayControls(); RefreshDiplomaticNegotiation(); }
        }, allowDuringPeriod: playback is not null);
    }

    private static PersonnelPresentationEntry? NextLiveDuelCaptureAnnouncement(GameState world)
    {
        foreach (var batch in world.CampaignEffectRequests?.PendingBatches ?? [])
        {
            if (batch.Source != CampaignEffectSource.ArmyBattleDefeat) continue;
            for (int index = 0; index < batch.Requests.Length; index++)
            {
                var request = batch.Requests[index];
                if (request.HistoryNotification is not null) continue;
                //A different same-source head retains its actual consumer.
                return request.DuelCaptureAnnouncement is not null
                    ? new(batch.Sequence, index, request, batch.Source) : null;
            }
        }
        return null;
    }

    private void BeginPeriodDuelCaptureAnnouncement(CampaignPeriodPlayback playback, MapSceneView? map,
        GameState candidate)
    {
        if (playback.DuelCaptureAnnouncementOpen || map is null || _campaignComposition is not { } composition ||
            !ReferenceEquals(playback.Candidate, candidate) || NextLiveDuelCaptureAnnouncement(candidate) is null) return;
        playback.DuelCaptureAnnouncementOpen = true;
        var token = _periodAdvanceCancellation?.Token ?? CancellationToken.None;
        _ = Begin();
        async Task Begin()
        {
            bool opened = false;
            try
            {
                while (playback.OccupationPresentationOpen || playback.DuelPresentationOpen || playback.ManualCommandSelectionOpen ||
                    playback.Candidate?.ManualTacticIntervention is { } tactics && (tactics.Effects.Length != 0 || tactics.Directions.Length != 0))
                {
                    token.ThrowIfCancellationRequested();
                    if (!ReferenceEquals(_periodPlayback, playback) || playback.Candidate is null || _residentMap != map) return;
                    await Task.Delay(16, token);
                }
                if (ReferenceEquals(_periodPlayback, playback) && playback.Candidate is { } world && _residentMap == map &&
                    NextLiveDuelCaptureAnnouncement(world) is { } entry)
                    opened = TryPresentDuelCaptureAnnouncement(entry, entry.Request.DuelCaptureAnnouncement!,
                        _session!, world, map, composition, playback);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception error) { playback.FailPresentation(error); }
            finally { if (!opened) playback.DuelCaptureAnnouncementOpen = false; }
        }
    }

    private void RetirePeriodDuelCaptureAnnouncement(CampaignPeriodPlayback playback)
    {
        if (_duelCaptureAnnouncement is { } owner && ReferenceEquals(owner.Playback, playback))
            CloseDuelCaptureAnnouncement();
        playback.DuelCaptureAnnouncementOpen = false;
    }

    private void CloseDuelCaptureAnnouncement()
    {
        var owner = _duelCaptureAnnouncement; _duelCaptureAnnouncement = null;
        if (owner is null) return;
        owner.Lifetime.Cancel();
        if (!owner.Running) owner.Lifetime.Dispose();
        if (owner.View is { } view) { view.Abandon(); view.GetParent()?.RemoveChild(view); view.QueueFree(); }
        if (owner.Playback is not null) owner.Playback.DuelCaptureAnnouncementOpen = false;
        if (GodotObject.IsInstanceValid(owner.Map) && _residentMap == owner.Map)
        {
            owner.Map.SetDetailsOpen(false);
            if (ReferenceEquals(_session, owner.Session) && CanAdvanceNarration && !HasPersonnelPresentation &&
                !HasForcePresentation && !HasMilitaryOrderPage && !HasAdministrationPage && !HasCommandWorkspace)
                SetMapFocus(owner.MapWasFocused);
        }
    }
}
