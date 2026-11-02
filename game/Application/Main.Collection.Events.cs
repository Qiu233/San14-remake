using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private CollectionEventsView? _collectionEvents;
    private CollectionEventCatalog? _collectionEventCatalog;
    private CollectionEventSelectionWorkspace? _collectionEventSelection;
    private CollectionEventPlaybackWorkspace? _collectionEventPlayback;
    private readonly SemaphoreSlim collectionEventPreparationGate = new(1, 1);

    private sealed class CollectionEventSelectionWorkspace(CollectionEventsView view, int rowId, long requestId)
    {
        internal readonly CollectionEventsView View = view;
        internal readonly int RowId = rowId;
        internal readonly long RequestId = requestId;
        internal readonly CancellationTokenSource Lifetime = new();
        internal CollectionEventPreparation? Preparation;
        internal bool Preparing = true, Retired;
    }

    private sealed class CollectionEventPlaybackWorkspace(CollectionEventSelectionWorkspace selection, EventStorySession session)
    {
        internal readonly CollectionEventSelectionWorkspace Selection = selection;
        internal readonly EventStorySession Session = session;
        internal readonly CancellationTokenSource Lifetime = new();
        internal readonly MapPresentationState Presentation = new();
        internal MapSceneView? Map;
        internal StoryPlaybackHost? Host;
        internal bool Opening = true, Retired, AudioEntered;
    }

    private void OpenCollectionEvents()
    {
        if (_failed || _shuttingDown || _repository is null ||
            _collectionMenu is not { } parent || HasCollectionChild) return;
        var catalog = CollectionEventCatalog.Load(_repository, _ui.SourceLocale,
            GetApplicationProfile().Current);
        _collectionEventCatalog = catalog;
        var view = new CollectionEventsView(); _collectionEvents = view; AddChild(view); parent.Hide();
        try
        {
            view.Configure(_repository, _ui, catalog,
                () => !_failed && !_shuttingDown && _collectionMenu == parent &&
                    _collectionEvents == view && _titleMenu is not null && _collectionEventPlayback is null,
                () => RunUiAction(() => CloseCollectionEvents(true)), CurrentListScrollSpeed, RunUiAction,
                rowId => RunUiAction(() => PlayCollectionEvent(rowId)),
                (rowId, requestId) => PrepareCollectionEventSelection(view, catalog, rowId, requestId),
                resources: TryBorrowApplicationUiResources(view));
        }
        catch { CloseCollectionEvents(true); throw; }
    }

    private void CloseCollectionEvents(bool restoreMenu)
    {
        CloseCollectionEventPlayback(restore: false);
        RetireCollectionEventSelection();
        _collectionEventCatalog = null;
        var view = _collectionEvents; _collectionEvents = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        if (restoreMenu && !_failed && !_shuttingDown) _collectionMenu?.Show();
    }

    private bool IsCollectionEventSelectionCurrent(CollectionEventSelectionWorkspace selection) =>
        !_failed && !_shuttingDown && !selection.Retired &&
        ReferenceEquals(_collectionEvents, selection.View) && ReferenceEquals(_collectionEventSelection, selection);

    private void PrepareCollectionEventSelection(CollectionEventsView view, CollectionEventCatalog catalog,
        int rowId, long requestId)
    {
        RetireCollectionEventSelection();
        if (rowId < 0 || _collectionEvents != view || _repository is null) return;
        var selection = new CollectionEventSelectionWorkspace(view, rowId, requestId);
        _collectionEventSelection = selection;
        string locale = _ui.SourceLocale;
        var resourceSources = CaptureCampaignResourceSources();
        // This is the same project World RNG bootstrap as normal scenario
        // loading. Original App32 and CRT retain their separate actual owners.
        var inputs = new CollectionEventSessionInputs(1, new(_presentationRandom.State), CaptureApplicationCrtRandom(),
            new CampaignNormalEventAudioInputs(locale, readApplicationField1AC: () => 0,
                readApplicationPointerPresent: () => true), _preferences.Voice.Capture(), _newGameOptions);
        _ = PrepareAsync();

        async Task PrepareAsync()
        {
            try
            {
                await collectionEventPreparationGate.WaitAsync(selection.Lifetime.Token);
                CollectionEventPreparation result;
                try
                {
                    result = await Task.Run(() =>
                    {
                        selection.Lifetime.Token.ThrowIfCancellationRequested();
                        // A private worker reader/ledger borrows process-owned
                        // static content and the caller-captured portrait facts.
                        var repository = resourceSources.CreateReadScope();
                        return catalog.PrepareSession(repository, rowId, inputs);
                    }, selection.Lifetime.Token);
                }
                finally { collectionEventPreparationGate.Release(); }
                if (!IsCollectionEventSelectionCurrent(selection)) return;
                selection.Preparation = result;
                view.ApplyConditions(rowId, requestId, result.Conditions);
                view.SetPlaybackPrepared(rowId, requestId, result.UnresolvedOwner is null && result.Session is not null);
                if (result.UnresolvedOwner is { } missing) view.SetMeta("selection_unresolved_owner", missing);
                else view.RemoveMeta("selection_unresolved_owner");
                if (result.World is { } world) AdoptStoryRandom(world);
            }
            catch (OperationCanceledException) when (selection.Lifetime.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (!IsCollectionEventSelectionCurrent(selection)) return;
                view.ApplyConditions(rowId, requestId, new(rowId, [], error.Message));
                view.SetPlaybackPrepared(rowId, requestId, false);
                view.SetMeta("selection_unresolved_owner", error.Message);
            }
            finally
            {
                selection.Preparing = false;
                if (selection.Retired) selection.Lifetime.Dispose();
            }
        }
    }

    private void RetireCollectionEventSelection()
    {
        var selection = _collectionEventSelection; _collectionEventSelection = null;
        if (selection is null || selection.Retired) return;
        selection.Retired = true; selection.Lifetime.Cancel();
        if (!selection.Preparing) selection.Lifetime.Dispose();
    }

    private void AdoptStoryRandom(GameState world)
    {
        if (world.CampaignAppRandom32 is { } app) _presentationRandom.InstallCommittedState(app.State);
        SynchronizeApplicationCrtRandom(world);
    }

    private bool IsCollectionEventPlaybackCurrent(CollectionEventPlaybackWorkspace owner) =>
        !_failed && !_shuttingDown && !owner.Retired &&
        ReferenceEquals(_collectionEventPlayback, owner) && IsCollectionEventSelectionCurrent(owner.Selection);

    private void PlayCollectionEvent(int rowId)
    {
        if (_collectionEventPlayback is not null || _collectionEventCatalog is not { } catalog ||
            _collectionEventSelection is not { Preparation: { Session: not null, Scenario: { }, Map: { } } preparation } selection ||
            !IsCollectionEventSelectionCurrent(selection) || selection.RowId != rowId || preparation.UnresolvedOwner is not null)
            return;
        // The directory owns the actual profile gate; preparation alone never unlocks playback.
        var playbackPreparation = preparation with { World = preparation.World! with
            { CampaignAppRandom32 = new(_presentationRandom.State), CampaignCrtRandom = CaptureApplicationCrtRandom() } };
        var session = catalog.CreatePlaybackSession(rowId, playbackPreparation,
            new CampaignNormalEventAudioInputs(_ui.SourceLocale, readApplicationField1AC: () => 0,
                readApplicationPointerPresent: () => true));
        var owner = new CollectionEventPlaybackWorkspace(selection, session); _collectionEventPlayback = owner;
        selection.View.Hide();
        _ = OpenAsync();

        async Task OpenAsync()
        {
            try
            {
                var map = new MapSceneView { Name = "CollectionEventWorld" };
                map.SetStaticMapResources(BorrowApplicationMapResources());
                ApplyCommittedCgQuality(map); owner.Map = map; AddChild(map);
                map.SetSourceNavigationOptions(_preferences.Navigation); map.SetKeyboardBindings(_preferences.KeyboardBindings);
                map.SetD3D11CurrentWorldQueries(session.Rules.CaptureCurrentDefinitions,
                    session.Rules.CaptureGraphicsMotionInputs, session.Rules.CaptureMapArmyGroups, session.Rules.CaptureMapArmyPresence);
                owner.Presentation.BeginResidentMap();
                map.SetSceneryMapBinding(owner.Presentation, session.Rules.CaptureSceneryMapCurrent);
                map.Configure(_repository!, _ui, _safeUi, () => { }, preparation.Map!.Map, session.World,
                    playerForceId: preparation.PlayerForceId, embedded: true, sourceSnapshot: preparation.Scenario!.Snapshot,
                    worldScene: true, cameraEntry: MapCameraEntry.FreshController);
                await map.InitialSceneLoad.WaitAsync(owner.Lifetime.Token);
                if (!IsCollectionEventPlaybackCurrent(owner)) return;
                if (!map.IsPlayableMapReady)
                    throw new InvalidDataException(map.GetMeta("scene_error", "collection.event.world_map_not_ready").AsString());
                var terrain = map.SourceMainCameraTerrain ?? throw new InvalidDataException(
                    map.GetMeta("source_main_camera_terrain_error", "collection.event.camera_terrain").AsString());
                session.Rules.ConfigureStoryCameraTerrain(terrain);
                var audio = _sceneAudioBinding ?? throw new InvalidOperationException("story.audio.scene_owner");
                owner.AudioEntered = true;
                await audio.BeginCollectionEvent(session.SceneKey, catalog.Rows[rowId].EventId, _ui.SourceLocale,
                    _preferences.MusicSelection, (session.ExecutionMode & 2) != 0, owner.Lifetime.Token);
                if (!IsCollectionEventPlaybackCurrent(owner)) return;
                if (session.Story is null && !session.MoveNextEvent())
                    throw new InvalidDataException(session.UnresolvedOwner ?? "event.story.start");
                AdoptStoryRandom(session.World);
                var host = new StoryPlaybackHost(); owner.Host = host; AddChild(host);
                host.Configure(_repository!, _ui, new(session.Rules, () => session.World, () => session.Story,
                    () =>
                    {
                        var prepared = session.PrepareCurrentStep();
                        if (prepared.UnresolvedOwner is not null) throw new InvalidDataException(prepared.UnresolvedOwner);
                    }, receipt => session.Story is { } story &&
                        session.CompleteStep(story, receipt)), map, audio, _preferences.MusicSelection,
                    () => IsCollectionEventPlaybackCurrent(owner) && CanAdvanceNarration,
                    () => _preferences.AutomaticMessageAdvance, CurrentListScrollSpeed, HandleNarrationKeyboard,
                    AdoptStoryRandom, () => CompleteCollectionEventStory(owner),
                    error => RunUiAction(() => { CloseCollectionEventPlayback(true); throw error; }), inputWaitKeyboard: HandleNarrationKeyboard);
            }
            catch (OperationCanceledException) when (owner.Lifetime.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (IsCollectionEventPlaybackCurrent(owner))
                    RunUiAction(() => { CloseCollectionEventPlayback(true); throw error; });
            }
            finally
            {
                owner.Opening = false;
                if (owner.Retired) owner.Lifetime.Dispose();
            }
        }
    }

    private void CompleteCollectionEventStory(CollectionEventPlaybackWorkspace owner)
    {
        if (!IsCollectionEventPlaybackCurrent(owner)) return;
        var session = owner.Session;
        if (!session.MoveNextEvent()) throw new InvalidDataException(session.UnresolvedOwner ?? "event.story.next");
        AdoptStoryRandom(session.World);
        if (session.Completed) CloseCollectionEventPlayback(true);
        else owner.Host!.ContinuePlayback();
    }

    private void CloseCollectionEventPlayback(bool restore)
    {
        var owner = _collectionEventPlayback; _collectionEventPlayback = null;
        if (owner is null || owner.Retired) return;
        owner.Retired = true; owner.Lifetime.Cancel();
        owner.Host?.Abandon(); owner.Host?.QueueFree();
        if (owner.Map is { } map) { map.Hide(); map.QueueFree(); }
        owner.Presentation.LeaveResidentMap();
        if (owner.AudioEntered && _sceneAudioBinding is { } audio) _ = RestoreAudio(audio);
        else if (restore && !_failed && !_shuttingDown) owner.Selection.View.Show();
        if (!owner.Opening) owner.Lifetime.Dispose();

        async Task RestoreAudio(SceneAudioBinding audio)
        {
            try { await audio.EndCollectionEvent(restore && !_failed && !_shuttingDown); }
            catch (Exception error) { if (!_failed && !_shuttingDown) ShowFailure(error, startup: false); }
            if (restore && !_failed && !_shuttingDown && _collectionEvents == owner.Selection.View)
                owner.Selection.View.Show();
        }
    }

    private bool RequestCollectionEventPlaybackBack()
    {
        if (_collectionEventPlayback is not { } owner) return false;
        owner.Host?.RequestSkip();
        return true;
    }
}
