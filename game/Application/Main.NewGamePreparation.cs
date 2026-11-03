using System.Globalization;
using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;
using San14.Client.Gameplay;

public partial class Main
{
    // Event-boundary diagnostics only; this does not enable developer UI.
    private sealed class NewGamePreparationTiming
    {
        private static long nextRequest;
        private readonly long request = System.Threading.Interlocked.Increment(ref nextRequest);
        private readonly long started = System.Diagnostics.Stopwatch.GetTimestamp();
        private readonly double cpuStarted = ReadCpuMilliseconds();
        private readonly string phase;
        private NewGamePreparationTiming(string phase) => this.phase = phase;
        internal static NewGamePreparationTiming? Begin(string phase)
        {
            if(System.Environment.GetEnvironmentVariable("SAN14_NEW_GAME_TIMINGS") != "1")return null;
            try { return new(phase); }
            catch(Exception) { return null; } // Diagnostic availability cannot block a new game.
        }
        internal void Mark(string stage)
        {
            try
            {
                long now = System.Diagnostics.Stopwatch.GetTimestamp();
                GD.Print(FormattableString.Invariant(
                    $"NEW_GAME_TIMING request={request} phase={phase} stage={stage} elapsedMs={System.Diagnostics.Stopwatch.GetElapsedTime(started,now).TotalMilliseconds:F3} stamp={now} frequency={System.Diagnostics.Stopwatch.Frequency} cpuMs={ReadCpuMilliseconds()-cpuStarted:F3} thread={System.Environment.CurrentManagedThreadId}"));
            }
            catch(Exception) { } // Logging is independent of the preparation transaction.
        }
        private static double ReadCpuMilliseconds()
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            return process.TotalProcessorTime.TotalMilliseconds;
        }
    }

    private sealed class NewGamePreparationWorkspace(int index, string locale, EffectiveScenario selection,
        GameplaySession? session, GameState? world, ScenarioSelectionView? scenarios, NewGameSetupView? setup,
        NewGamePreparationView view)
    {
        internal readonly int Index=index;
        internal readonly string Locale=locale;
        internal readonly EffectiveScenario Selection=selection;
        internal readonly GameplaySession? Session=session;
        internal readonly GameState? World=world;
        internal readonly ScenarioSelectionView? Scenarios=scenarios;
        internal readonly NewGameSetupView? Setup=setup;
        internal readonly NewGamePreparationView View=view;
        // Borrowed from this loading view's existing application resource lease.
        internal UiAssets.PortraitImages PortraitImages=null!;
        internal readonly CancellationTokenSource Lifetime=new();
        internal bool Retired, WorkerFinished;
    }
    private sealed record PreparedNewGamePage(CampaignNewGamePreviewPreparation Preparation,
        NewGameForcePageContent Content, NewGameForcePageHeader Header, NewGameForceMapContent Map);
    private NewGamePreparationWorkspace? _newGamePreparation;

    private sealed record CapturedNewGameEditors(uint Seed, PersonalityEditorLibrary Personalities,
        HistoricalOfficerProfile? Historical, int[] HistoricalIds, RegisteredOfficerLibrary? Registered,
        RegisteredOfficerNewGameSelection RegisteredSelection, ItemEditorLibrary Items,
        ItemEditorNewGameSelection ItemSelection, TransferServiceState? Profile,
        RegisteredOfficerLibrary? PortraitLibrary)
    {
        internal LoadedScenario Apply(GameResourceRepository repository, LoadedScenario original)
        {
            var current=PersonalityEditorRuntime.PrepareFresh(original,Personalities);
            OfficerEditorPortraitCatalog? portraits=null;
            if(Historical is not null || Registered is not null)
                portraits=OfficerEditorPortraitCatalog.Load(repository,original.Snapshot.Locale,
                    CollectionOfficerAdmission.FromProfile(Profile!),PortraitLibrary!);
            if(Historical is not null)
                current=HistoricalOfficerScenario.Apply(repository,current,Historical,HistoricalIds,portraits);
            if(Registered is not null)
                current=RegisteredOfficerScenario.Apply(repository,current,Registered,RegisteredSelection,Seed,portraits);
            return current.Metadata.FormatMarker==0x66
                ? ItemEditorScenario.Apply(repository,current,Items,ItemSelection,Seed) : current;
        }
    }
    // Called on Main before a preparation waits or enters a worker. Prefer
    // the current reader's application facts, not a possibly older root facade.
    // The returned template has no query/session/World state and each worker
    // creates its own reader from it.
    private GameResourceRepository CaptureCampaignResourceSources()
    {
        var current = _repository ?? throw new InvalidOperationException("Campaign resource reader is absent.");
        if (_applicationUiResourceRepository is { } root && !current.SharesResidentSourcesWith(root))
            throw new InvalidOperationException("The current resource reader does not share the application's static resource owner.");
        return current.CreateReadScope();
    }

    private CampaignNewGameInputs CaptureNewGamePreparationInputs(EffectiveScenario selection, NewGameOptions options)
    {
        var key=(selection.Number,selection.Edition);
        if(_registeredOfficerNewGameScenario!=key)
        { ResetRegisteredOfficerNewGame();_registeredOfficerNewGameScenario=key; }
        _registeredOfficerInitializationSeed??=CaptureApplicationCrtRandom().State;
        bool historical=_registeredOfficerNewGameAccepted && _historicalOfficerNewGameScenario==key &&
            _historicalOfficerNewGameProfile is not null && _historicalOfficerNewGameAdoptedIds.Length>0;
        bool registered=_registeredOfficerNewGameAccepted && _registeredOfficerNewGameLibrary is { } library &&
            !_registeredOfficerNewGameSelection.AdoptedOfficerIds.IsEmpty;
        var edits=new CapturedNewGameEditors(_registeredOfficerInitializationSeed.Value,GetPersonalityEditorStore().Current,
            historical?_historicalOfficerNewGameProfile:null,historical?_historicalOfficerNewGameAdoptedIds.ToArray():[],
            registered?_registeredOfficerNewGameLibrary:null,_registeredOfficerNewGameSelection,
            GetItemEditorStore().Current,_newGameItemEditorSelection,
            historical||registered?GetApplicationProfile().Current:null,
            historical||registered?GetRegisteredOfficerStore().Current:null);
        return new(_repository!.SourceDirectory,_repository.OverrideDirectory,selection,options,
            CampaignComposition.CaptureNewGamePersonnelMoveDestinations(_personnelGroupMoveDestinations),
            _preferences.Voice.Capture(),edits.Apply)
            { PresentationOptions=_preferences.Presentation, ResourceSources=CaptureCampaignResourceSources() };
    }
    private NewGamePreparationWorkspace BeginNewGamePreparation(int index)
    {
        CancelNewGamePreparation(restoreParent:false);
        var selection=_shownScenarios[index].Selection;
        var view=new NewGamePreparationView();AddChild(view);
        var workspace=new NewGamePreparationWorkspace(index,_ui.SourceLocale,selection,_session,_world,
            _scenarioSelection,_newGameSetup,view);
        _newGamePreparation=workspace;workspace.Setup?.SetBusy(true);
        try
        {
            var resources=TryBorrowApplicationUiResources(view);
            workspace.PortraitImages=resources.Assets.Portraits;
            view.Configure(_repository!,_ui,CancelNewGamePreparation,resources);
        }
        catch { CancelNewGamePreparation();FinishNewGamePreparationWorker(workspace);throw; }
        return workspace;
    }
    private bool IsNewGamePreparationCurrent(NewGamePreparationWorkspace owner) =>
        !owner.Retired && !_failed && !_shuttingDown && IsInsideTree() &&
        ReferenceEquals(_newGamePreparation,owner) && _ui.SourceLocale==owner.Locale &&
        ReferenceEquals(_session,owner.Session) && ReferenceEquals(_world,owner.World) &&
        ReferenceEquals(_scenarioSelection,owner.Scenarios) && ReferenceEquals(_newGameSetup,owner.Setup) &&
        owner.Index>=0 && owner.Index<_shownScenarios.Count && _shownScenarios[owner.Index].Selection==owner.Selection;

    private void CancelNewGamePreparation() => CancelNewGamePreparation(restoreParent:true);
    private void CancelNewGamePreparation(bool restoreParent)
    {
        var owner=_newGamePreparation;if(owner is null)return;
        _newGamePreparation=null;owner.Retired=true;owner.Lifetime.Cancel();
        // The worker still borrows this view's application portrait sources.
        // Hide/cancel now; retain its real UI lease until that worker exits.
        owner.View.Abandon();
        if(owner.WorkerFinished)owner.View.QueueFree();
        if(restoreParent && !_failed && !_shuttingDown)
        {
            if(owner.Setup is not null && ReferenceEquals(_newGameSetup,owner.Setup)) owner.Setup.SetBusy(false);
            else if(owner.Scenarios is null && _scenarioSelection is null && _newGameSetup is null && !_playerForceId.HasValue)
                OpenScenarioSelection();
        }
        if(owner.WorkerFinished)owner.Lifetime.Dispose();
    }
    private void FinishNewGamePreparationWorker(NewGamePreparationWorkspace owner)
    {
        owner.WorkerFinished=true;
        if(owner.Retired)
        {
            owner.View.QueueFree();
            owner.Lifetime.Dispose();
        }
    }

    private void BeginNewGameScenarioPreparation(int index, int? restoreForce=null, bool restoreSettings=false)
    {
        if(_failed||_shuttingDown||_repository is null||index<0||index>=_shownScenarios.Count||_playerForceId.HasValue)return;
        var timing=NewGamePreparationTiming.Begin("scenario");timing?.Mark("input-capture-begin");
        var inputs=CaptureNewGamePreparationInputs(_shownScenarios[index].Selection,_newGameOptions);
        var selectedMetadata=_repository.ScenarioDocuments.Single(document =>
            string.Equals(document.SourcePath, inputs.Selection.SourcePath, StringComparison.OrdinalIgnoreCase)).SelectionMetadata
            ?? throw new InvalidDataException("Selected scenario header metadata is missing.");
        bool? completedMark=GetApplicationProfile().Current.ReadScenarioCompleted(selectedMetadata.NativeId);
        timing?.Mark("input-capture-end");
        var owner=BeginNewGamePreparation(index);var token=owner.Lifetime.Token;
        var portraitImages=owner.PortraitImages;
        timing?.Mark("loading-view-configured");
        _=Prepare();
        async Task Prepare()
        {
            try
            {
                // The worker starts only after this loading surface was drawn
                // and its real frame completed; input stays on the old page.
                await owner.View.Drawn.WaitAsync(token);
                timing?.Mark("loading-frame-completed");
                var page=await Task.Run(()=>
                {
                    timing?.Mark("worker-begin");
                    var prepared=CampaignComposition.PrepareNewGamePreview(inputs,token,timing is null?null:timing.Mark);
                    var composition=prepared.Campaign.Composition;var world=prepared.Campaign.Session.World;
                    var queries=composition.PrepareArmyQueries(world);
                    var content=new NewGameForcePageContent(prepared.Repository,queries,
                        world,prepared.ForceSelection,portraitImages,token);
                    content.PrepareRulers(prepared.ForceSelection.Rows,token);
                    timing?.Mark("force-rulers-prepared");
                    var messages=prepared.Repository.LoadMessages(inputs.Selection.Locale,MessageKind.Data).Messages.Sections[1].Records;
                    string year=messages[0x2AB].Text??throw new InvalidDataException("Scenario year suffix is missing.");
                    string month=messages[0x2AC].Text??throw new InvalidDataException("Scenario month suffix is missing.");
                    var header=new NewGameForcePageHeader(string.Format(CultureInfo.InvariantCulture,"{0,3}{1}",world.Date.Year,year),
                        string.Format(CultureInfo.InvariantCulture,"{0,2}{1}",world.Date.Month,month),
                        composition.Scenario.Metadata.Title??throw new InvalidDataException("Scenario title is missing."),
                        ScenarioSelectionCatalog.LoadEditionBadge(new LocalizedUiSpriteSource(prepared.Repository), inputs.Selection.Locale,
                            selectedMetadata.EditionCode ?? (inputs.Selection.Edition switch { "CE" => 2, "PK" => 1, _ => 0 }),
                            selectedMetadata.NativeId), selectedMetadata.ScenarioKindCode != 0, completedMark);
                    token.ThrowIfCancellationRequested();
                    var calendar=MapSeasonRuntime.PrepareScenarioPreviewFrame(
                        prepared.Repository.LoadScenarioWorld(inputs.Selection.SourcePath,inputs.Selection.Locale),1);
                    var visualCurrent=queries.CaptureNewGameStaticVisualDefinitions(world);
                    var map=new NewGameForceMapContent(calendar,
                        visualCurrent,queries.ReadNewGameFootholdVisualMarkers(visualCurrent),
                        composition.Snapshot.Forces.ToDictionary(force=>force.Slot));
                    var page=new PreparedNewGamePage(prepared,content,header,map);
                    timing?.Mark("worker-end");
                    return page;
                },token);
                timing?.Mark("main-resumed");
                if(!IsNewGamePreparationCurrent(owner))return;
                CancelNewGamePreparation(restoreParent:false);
                RunUiAction(()=>
                {
                    timing?.Mark("adopt-begin");
                    CloseScenarioSelection();CloseNewGameSetup();_scenarioList.Select(index);
                    timing?.Mark("old-pages-closed");
                    AdoptPreparedScenario(page.Preparation.Campaign,inputs.Selection,showBrowserPreview:false,timing:timing);
                    OpenPreparedNewGameSetup(page,timing);
                    _newGameSetup?.RestoreSelection(restoreForce,restoreSettings);
                    timing?.Mark("adopt-end");
                });
            }
            catch(OperationCanceledException) when(token.IsCancellationRequested) { }
            catch(Exception error)
            {
                if(!IsNewGamePreparationCurrent(owner))return;
                GD.PushError("New-game preparation failed: "+error);
                owner.View.ShowFailure(_ui);
            }
            finally { timing?.Mark("finished");FinishNewGamePreparationWorker(owner); }
        }
    }
    private void BeginConfirmedNewGamePreparation(NewGameSetupView view,int forceId,NewGameOptions options)
    {
        int[] selected=_scenarioList.GetSelectedItems();
        if(selected.Length!=1||_campaignComposition is null||_session is null)
            throw new InvalidDataException("New-game scenario selection is missing.");
        int index=selected[0];
        var timing=NewGamePreparationTiming.Begin("start");timing?.Mark("input-capture-begin");
        var inputs=CaptureNewGamePreparationInputs(_shownScenarios[index].Selection,options);
        timing?.Mark("input-capture-end");
        var retained=options==_newGameOptions
            ?new CampaignNewGameRetainedPreview(_campaignComposition.Scenario,_campaignComposition.Map,_session.World):null;
        var appRandom=new CampaignAppRandomState(_presentationRandom.State);var crtRandom=CaptureApplicationCrtRandom();
        var owner=BeginNewGamePreparation(index);var token=owner.Lifetime.Token;
        timing?.Mark("loading-view-configured");
        _=Prepare();
        async Task Prepare()
        {
            try
            {
                await owner.View.Drawn.WaitAsync(token);
                timing?.Mark("loading-frame-completed");
                var prepared=await Task.Run(()=>
                {
                    timing?.Mark("worker-begin");
                    var result=CampaignComposition.PrepareNewGameStart(inputs,retained,forceId,
                        appRandom,crtRandom,token,timing is null?null:timing.Mark);
                    timing?.Mark("worker-end");
                    return result;
                },token);
                timing?.Mark("main-resumed");
                if(!IsNewGamePreparationCurrent(owner))return;
                CancelNewGamePreparation(restoreParent:false);
                RunUiAction(()=>
                {
                    timing?.Mark("adopt-begin");
                    _newGameOptions=options;
                    AdoptStartedNewGame(prepared.Composition,prepared.Session,prepared.Presentation,timing);
                    if(_playerForceId==forceId)CloseNewGameSetup();
                    timing?.Mark("adopt-end");
                });
            }
            catch(OperationCanceledException) when(token.IsCancellationRequested) { }
            catch(Exception error)
            {
                if(!IsNewGamePreparationCurrent(owner))return;
                GD.PushError("New-game start preparation failed: "+error);
                owner.View.ShowFailure(_ui);
            }
            finally { timing?.Mark("finished");FinishNewGamePreparationWorker(owner); }
        }
    }
    private void AdoptStartedNewGame(CampaignComposition composition,GameplaySession session,CampaignPresentation presentation,
        NewGamePreparationTiming? timing = null)
    {
        RetainNewGameMapForStartedCampaign();
        _campaignComposition=composition;_session=session;
        _personnelGroupMoveDestinations=composition.ApplicationGameplay.PersonnelGroupMoveDestinations;
        SynchronizeApplicationCrtRandom(session.World);
        if(session.World.CampaignAppRandom32 is {} appRandom)_presentationRandom.InstallCommittedState(appRandom.State);
        AttachCampaignApplication(playable:true);SyncNewGameOptions(session.World);
        timing?.Mark("start-application-attached");
        PublishCampaignPresentation(presentation);RefreshSceneAudioContext();
        timing?.Mark("start-audio-context-refreshed");
        _playMessage.Text=_ui.Get("play_started");
        // Formal new-game/map views own their art. Keep the browser's actual
        // choices in sync, without producing its hidden raster and portrait.
        // An explicitly enabled developer workspace retains that preview.
        RefreshCurrentPlayWorld(showBrowserPreview:DeveloperWorkspaceEnabled,timing:timing);
    }
}
