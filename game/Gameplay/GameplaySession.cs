using San14.Core;

namespace San14.Client.Gameplay;

/// <summary>
/// Owns the live world and its local player's command authority. Views and the
/// renderer read this world; only successful application operations replace it.
/// </summary>
public sealed partial class GameplaySession
{
    private const string LocalActor = "local";
    private readonly GameSimulation _simulation;
    private readonly SingleForceAuthority? _authority;
    private readonly Func<GameState, GameState>? _advanceCalendar;
    private readonly ICommandPlanner? _planner;
    private readonly Func<PlayableRuleSet, int, ICommandPlanner>? _commandPlannerFactory;
    internal Func<PlayableRuleSet, int, ICommandPlanner>? CommandPlannerFactory => _commandPlannerFactory;
    private readonly SessionSaveStore _saves;
    private readonly SaveStore _previewSaves;
    private Func<GameState, GameState>? _prepareTutorialWorld;
    private Action<GameState, GameState>? _adoptTutorialWorld;
    private Func<GameOrder, bool>? _tutorialOrderAdmission;

    private GameplaySession(GameState world, PlayableRuleSet rules, int? playerForceId,
        Func<GameState, GameState>? advanceCalendar, ISaveCodec saveCodec,
        Func<PlayableRuleSet, int, ICommandPlanner>? commandPlannerFactory = null)
        : this(new GameSimulation(world), rules, playerForceId, advanceCalendar, saveCodec, commandPlannerFactory) { }

    private GameplaySession(GameSimulation simulation, PlayableRuleSet rules, int? playerForceId,
        Func<GameState, GameState>? advanceCalendar, ISaveCodec saveCodec,
        Func<PlayableRuleSet, int, ICommandPlanner>? commandPlannerFactory = null)
    {
        _simulation = simulation;
        Rules = rules;
        PlayerForceId = playerForceId;
        _advanceCalendar = advanceCalendar;
        _authority = playerForceId is int player ? new SingleForceAuthority(player, LocalActor) : null;
        bool originalStrategy = rules.OrdinaryDeploymentRules is San14.Data.ArmyCampaignRuleAdapter
            { HasOriginalAiStrategyInstallation: true };
        if (originalStrategy && commandPlannerFactory is not null)
            throw new InvalidOperationException("Choose either the original Strategy lifecycle or a pre-period command planner.");
        _commandPlannerFactory = commandPlannerFactory;
        _planner = !originalStrategy && playerForceId is int human
            ? commandPlannerFactory is null ? new PlayableAiPlanner(rules, new HashSet<int> { human })
                : commandPlannerFactory(rules, human) ?? throw new InvalidOperationException("The selected command planner factory returned no planner.")
            : null;
        _saves = new(saveCodec);
        _previewSaves = new(saveCodec);
    }

    public GameState World => _simulation.State;
    public CampaignActionReportPreparationResult PrepareActionReport(ICampaignActionReportSource source,
        ICampaignAppRandomBinding? appRandom = null) => _simulation.PrepareActionReport(source, appRandom);
    public CampaignActionReportPreparationResult SelectActionReportCategory(ICampaignActionReportSource source,
        byte category, ICampaignAppRandomBinding? appRandom = null) => _simulation.SelectActionReportCategory(source, category, appRandom);
    public bool HasPendingOperation => _simulation.HasPendingOperation;
    public CampaignProgressOperation? PendingProgress => _simulation.PendingProgress;
    public ManualTacticProgressOperation? PendingManualTactic => _simulation.PendingManualTactic;
    public GameState? PendingManualTacticCandidate => _simulation.PendingManualTacticCandidate;
    public CampaignArmyProgressOperation? PendingArmyProgress => _simulation.PendingArmyProgress;
    public GameState? PendingArmyProgressCandidate => _simulation.PendingArmyProgressCandidate;
    public bool TryBeginOrdinaryTacticPresentation(GameState expectedOwner,
        ManualTacticInterventionState expectedManager, long sequence, GameState? preparedState = null) =>
        _simulation.TryBeginOrdinaryTacticPresentation(expectedOwner, expectedManager, sequence, preparedState);
    public bool TryCompleteOrdinaryTacticResults(GameState expectedOwner,
        ManualTacticInterventionState expectedManager, long sequence) =>
        _simulation.TryCompleteOrdinaryTacticResults(expectedOwner, expectedManager, sequence);
    public bool TryCompleteOrdinaryTacticPresentation(GameState expectedOwner,
        ManualTacticInterventionState expectedManager, long sequence) =>
        _simulation.TryCompleteOrdinaryTacticPresentation(expectedOwner, expectedManager, sequence);
    public bool TryCompleteUniqueDirectionPresentation(GameState expectedOwner,
        ManualTacticInterventionState expectedManager, long sequence) =>
        _simulation.TryCompleteUniqueDirectionPresentation(expectedOwner, expectedManager, sequence);
    public bool TryAdoptUniqueDirectionPreparedAudio(GameState expectedOwner,
        ManualTacticInterventionState expectedManager, long sequence, GameState preparedState) =>
        _simulation.TryAdoptUniqueDirectionPreparedAudio(expectedOwner, expectedManager, sequence, preparedState);
    public NormalPeriodOperation? PendingNormalPeriod => _simulation.PendingNormalPeriod;
    public CommandResult CompleteForeignTradeReport(GameState expectedOwner, CompleteForeignTradeReportOrder order,
        CampaignAppRandomState? appRandomSnapshot = null)
    {
        if (_authority is null) throw new InvalidOperationException("The campaign has not started.");
        return _simulation.CompleteForeignTradeReport(expectedOwner, order, Rules.OrderRules,
            new CommandContext(LocalActor), _authority, appRandomSnapshot);
    }
    public bool TryPrepareNormalTurnStoryStep(GameState expectedOwner, ICampaignStoryPreparationRules rules,
        out CampaignStoryPreparationResult result) =>
        _simulation.TryPrepareNormalTurnStoryStep(expectedOwner, rules, out result);
    public bool TryCompleteNormalTurnStoryStep(GameState expectedOwner, long sequence, int step) =>
        _simulation.TryCompleteNormalTurnStoryStep(expectedOwner, new(sequence, step));
    public bool TryCompleteNormalTurnStoryStep(GameState expectedOwner, CampaignStoryStepReceipt receipt) =>
        _simulation.TryCompleteNormalTurnStoryStep(expectedOwner, receipt);
    public bool TrySelectNormalTurnStoryKingdomName(GameState expectedOwner, long sequence,
        CampaignStoryForceRankContinuation expectedRequest, int? selectedId) =>
        _simulation.TrySelectNormalTurnStoryKingdomName(expectedOwner, sequence, expectedRequest, selectedId);
    public bool TryCompleteNormalTurnStoryForceRankMessage(GameState expectedOwner, long sequence,
        CampaignStoryForceRankContinuation expectedRequest) =>
        _simulation.TryCompleteNormalTurnStoryForceRankMessage(expectedOwner, sequence, expectedRequest);
    public bool TryBeginNormalTurnStoryAudio(GameState expectedOwner, long sequence,
        NormalTurnStoryAudioReturn snapshot) =>
        _simulation.TryBeginNormalTurnStoryAudio(expectedOwner, sequence, snapshot);
    public bool TryCompleteNormalTurnStoryAudio(GameState expectedOwner, long sequence,
        NormalTurnStoryAudioReturn snapshot) =>
        _simulation.TryCompleteNormalTurnStoryAudio(expectedOwner, sequence, snapshot);
    public bool TryCompleteNormalTurnStoryResult(GameState expectedOwner, long sequence,
        NormalTurnStoryResultSummary expectedSummary) =>
        _simulation.TryCompleteNormalTurnStoryResult(expectedOwner, sequence, expectedSummary);
    public bool TryCompletePersonnelPresentation(GameState expectedOwner,long batchSequence,int requestIndex) =>
        _simulation.TryCompletePersonnelPresentation(expectedOwner,batchSequence,requestIndex);
    public bool TryCompleteForceEmergenceChronicle(GameState expectedOwner, long batchSequence,
        int requestIndex, IForceEmergenceChronicleRules rules) =>
        _simulation.TryCompleteForceEmergenceChronicle(expectedOwner, batchSequence, requestIndex, rules);
    internal bool TryCompleteRegionEncirclementPresentation(GameState expectedOwner,RegionEncirclementPresentation expectedRequest) =>
        _simulation.TryCompleteRegionEncirclementPresentation(expectedOwner,expectedRequest);
    internal bool TryCompleteOccupationMapObjectPresentation(GameState expectedOwner,long batchSequence,int requestIndex,MapObjectPresentationRequest expectedRequest) =>
        _simulation.TryCompleteOccupationMapObjectPresentation(expectedOwner,batchSequence,requestIndex,expectedRequest);
    public bool TryCompleteProposalPresentation(GameState expectedOwner, long batchSequence, int requestIndex) =>
        _simulation.TryCompleteProposalPresentation(expectedOwner, batchSequence, requestIndex);
    public bool TryCompleteMapMusicIntent(GameState expectedOwner, CampaignEffectSource expectedSource,
        long batchSequence, int requestIndex) =>
        _simulation.TryCompleteMapMusicIntent(expectedOwner, expectedSource, batchSequence, requestIndex);
    public bool TryCompletePendingMapMusicIntent(GameState expectedOwner, MapMusicIntent expectedIntent) =>
        _simulation.TryCompletePendingMapMusicIntent(expectedOwner, expectedIntent);
    public bool TryCompleteApplicationExit(GameState expectedOwner, CampaignEffectSource expectedSource,
        long batchSequence, int requestIndex, ApplicationExitOperation operation) =>
        _simulation.TryCompleteApplicationExit(expectedOwner, expectedSource, batchSequence, requestIndex, operation);
    public bool TryCompleteForceCoalitionVisual(GameState expectedOwner, CampaignEffectSource expectedSource,
        long batchSequence, int requestIndex, ForceCoalitionVisualOperation operation,
        CampaignCameraSnapshot? capturedCamera = null) =>
        _simulation.TryCompleteForceCoalitionVisual(expectedOwner, expectedSource, batchSequence, requestIndex,
            operation, capturedCamera);
    public bool TryCompleteEmperorScene(GameState expectedOwner, CampaignEffectSource expectedSource,
        long batchSequence, int requestIndex, MonthlyMaintenanceOperation operation) =>
        _simulation.TryCompleteEmperorScene(expectedOwner, expectedSource, batchSequence, requestIndex, operation);
    public bool TryStartArmyMapSpeech(GameState expectedOwner, ArmyMapSpeechState snapshot, long sequence,
        bool armyLayoutAvailable, IArmyMapSpeechRules rules, out ArmyMapSpeechStartResult? result,
        CampaignAppRandomState? appRandomSnapshot = null) =>
        _simulation.TryStartArmyMapSpeech(expectedOwner, snapshot, sequence, armyLayoutAvailable, rules,
            out result, appRandomSnapshot);
    public bool TryCompleteArmyMapSpeech(GameState expectedOwner, ArmyMapSpeechState snapshot, long sequence) =>
        _simulation.TryCompleteArmyMapSpeech(expectedOwner, snapshot, sequence);
    public bool TryCompleteArmyMapSpeechVoices(GameState expectedOwner, long sequence) =>
        _simulation.TryCompleteArmyMapSpeechVoices(expectedOwner, sequence);
    public bool TryPrepareBaseAbnormalFeedback(GameState expectedOwner, CampaignEffectSource expectedSource,
        long batchSequence, int requestIndex, IBaseAbnormalFeedbackRules rules,
        out BaseAbnormalFeedbackPreparationResult? result, CampaignAppRandomState? appRandomSnapshot = null) =>
        _simulation.TryPrepareBaseAbnormalFeedback(expectedOwner, expectedSource, batchSequence, requestIndex,
            rules, out result, appRandomSnapshot);
    public bool TryCompleteArmyTraitViewRefresh(GameState expectedOwner, CampaignEffectSource expectedSource,
        long batchSequence, int requestIndex) =>
        _simulation.TryCompleteArmyTraitViewRefresh(expectedOwner, expectedSource, batchSequence, requestIndex);
    public bool TryCompleteArmyDeparturePresentation(GameState expectedOwner, long batchSequence, int requestIndex) =>
        _simulation.TryCompleteArmyDeparturePresentation(expectedOwner, batchSequence, requestIndex);
    public bool TryCompleteFacilityConstructionPresentation(GameState expectedOwner, CampaignEffectSource source,
        long batchSequence,int requestIndex,FacilityConstructionPresentationOperation operation) =>
        _simulation.TryCompleteFacilityConstructionPresentation(expectedOwner,source,batchSequence,requestIndex,operation);
    public bool TryCompleteFacilityRepairPresentation(GameState expectedOwner, CampaignEffectSource source,
        long batchSequence,int requestIndex,FacilityRepairPresentationOperation operation) =>
        _simulation.TryCompleteFacilityRepairPresentation(expectedOwner,source,batchSequence,requestIndex,operation);
    public bool TryPrepareFacilityArmySpeech(GameState expectedOwner, CampaignEffectSource expectedSource,
        long batchSequence,int requestIndex,IFacilityArmySpeechRules rules,
        out FacilityArmySpeechPreparationResult? result,CampaignAppRandomState? appRandomSnapshot=null) =>
        _simulation.TryPrepareFacilityArmySpeech(expectedOwner,expectedSource,batchSequence,requestIndex,
            rules,out result,appRandomSnapshot);
    public bool TryCompleteFacilityEffectsPresentation(GameState expectedOwner,CampaignEffectSource source,
        long batchSequence,int requestIndex,CampaignEffectRequest expectedRequest) =>
        _simulation.TryCompleteFacilityEffectsPresentation(expectedOwner,source,batchSequence,requestIndex,expectedRequest);
    public bool TryCompleteForceExperiencePresentation(GameState expectedOwner, long batchSequence, int requestIndex,
        int previousDisplayLevel, IForceExperiencePresentationRules rules) =>
        _simulation.TryCompleteForceExperiencePresentation(expectedOwner, batchSequence, requestIndex, previousDisplayLevel, rules);
    public bool TryAdvanceForcePolicyLevelUp(GameState expectedOwner, long batchSequence, int requestIndex,
        ForcePolicyLevelUpInput input, IForceExperiencePresentationRules rules) =>
        _simulation.TryAdvanceForcePolicyLevelUp(expectedOwner, batchSequence, requestIndex, input, rules);
    public CampaignProgressTransition ResumeManualTactic(ICampaignManualTacticProgressRules rules, TacticPresentationReceipt receipt) => _simulation.ResumeManualTactic(rules, receipt);
    public long LastStorySequence => _simulation.LastStorySequence;
    public CampaignProgressTransition BeginProgress(ICampaignProgressRules rules, CampaignProgressRequest request) =>
        _simulation.BeginProgress(rules, request, _planner);
    public CampaignProgressTransition ResumeStory(ICampaignProgressRules rules, CampaignStoryStepReceipt receipt) =>
        _simulation.ResumeStory(rules, receipt);
    public CampaignProgressTransition RetryProgress(ICampaignProgressRules rules) => _simulation.RetryProgress(rules);
    public void AbortProgress() => _simulation.AbortProgress();
    public PlayableRuleSet Rules { get; }
    public int? PlayerForceId { get; }

    internal GameSimulation CaptureSimulationFork(CampaignAppRandomState? appRandomSnapshot = null) =>
        _simulation.Fork(appRandomSnapshot, HasPendingOperation ? null : _prepareTutorialWorld?.Invoke(World));

    internal void ConfigureTutorialWorld(Func<GameState, GameState>? prepare,
        Action<GameState, GameState>? adopted, Func<GameOrder, bool>? admitted = null)
    {
        _prepareTutorialWorld = prepare;
        _adoptTutorialWorld = adopted;
        _tutorialOrderAdmission = admitted;
    }

    internal bool TryAdoptTutorialBusinessWorld(GameState exactOwner, GameState preparedWorld)
    {
        if (!_simulation.TryAdoptTutorialBusinessWorld(exactOwner, preparedWorld)) return false;
        _adoptTutorialWorld?.Invoke(exactOwner, World);
        return true;
    }

    internal static GameplaySession FromSimulationFork(GameSimulation simulation,
        PlayableRuleSet rules, int playerForceId, Func<GameState, GameState> calendar, ISaveCodec codec,
        Func<PlayableRuleSet, int, ICommandPlanner>? commandPlannerFactory = null) =>
        new(simulation, rules, playerForceId, calendar, codec, commandPlannerFactory);

    public static GameplaySession Preview(GameState world, PlayableRuleSet rules, ISaveCodec? saveCodec = null) =>
        new(world, rules, null, null, saveCodec ?? new JsonGzipSaveCodec());

    public static GameplaySession Restore(GameState world, int playerForceId,
        PlayableRuleSet rules, Func<GameState, GameState> advanceCalendar, ISaveCodec? saveCodec = null,
        CampaignProgressOperation? pending = null, long lastStorySequence = 0, ManualTacticProgressOperation? manualTactic = null,
        NormalPeriodOperation? normalPeriod = null,
        Func<PlayableRuleSet, int, ICommandPlanner>? commandPlannerFactory = null,
        CampaignArmyProgressOperation? armyProgress = null)
    {
        var session = new GameplaySession(world, rules, playerForceId, advanceCalendar, saveCodec ?? new JsonGzipSaveCodec(), commandPlannerFactory);
        session._simulation.RestoreProgress(pending, lastStorySequence, manualTactic, armyProgress);
        if (normalPeriod is not null) session._simulation.RestoreNormalPeriod(normalPeriod);
        return session;
    }

    public PersonnelEvaluation EvaluatePersonnelChange(PersonnelChange change)
    {
        int player = RequirePlayer();
        return Rules.PersonnelRules.Evaluate(World, World.Forces[player], change, new CommandContext(LocalActor));
    }

    public GovernanceRosterEvaluation EvaluateGovernanceRoster(GovernanceRoster target)
    {
        int player = RequirePlayer();
        return Rules.GovernanceRosterRules?.EvaluateGovernanceRoster(World, World.Forces[player], target,
            new CommandContext(LocalActor)) ?? new(null, null, null, null, null, null, null, "governance.roster_rules");
    }

    public GovernanceRosterEvaluation EvaluateGovernanceRosterForPolicyLevelUp(GameState expectedOwner,
        long batchSequence, int requestIndex, GovernanceRoster target)
    {
        int player = RequirePlayer();
        if (!_simulation.IsForcePolicyEditorOwner(expectedOwner, batchSequence, requestIndex, player))
            return new(false, null, null, null, null, null, "The policy editor no longer owns this draft.", null);
        return Rules.GovernanceRosterRules?.EvaluateGovernanceRoster(expectedOwner, expectedOwner.Forces[player], target,
            new CommandContext(LocalActor)) ?? new(null, null, null, null, null, null, null, "governance.roster_rules");
    }

    public CommandResult IssuePolicyLevelUpOrder(GameState expectedOwner, long batchSequence, int requestIndex,
        GameOrder order, CampaignAppRandomState? appRandomSnapshot = null)
    {
        if (_authority is null) throw new InvalidOperationException("The campaign has not started.");
        return _simulation.ApplyPolicyLevelUpOrder(expectedOwner, batchSequence, requestIndex, order, Rules.OrderRules,
            new CommandContext(LocalActor), _authority, appRandomSnapshot);
    }

    public CommandResult Issue(GameOrder order) => Issue(order, null);

    public CommandResult Issue(GameOrder order, CampaignAppRandomState? appRandomSnapshot)
    {
        if (_authority is null) throw new InvalidOperationException("The campaign has not started.");
        if (_tutorialOrderAdmission?.Invoke(order) == false)
            return CommandResult.Reject("The current lesson step does not permit this command.", "tutorial.command_not_allowed");
        GameState before = World;
        bool direct = !HasPendingOperation;
        GameState? prepared = direct ? _prepareTutorialWorld?.Invoke(before) : null;
        IOrderAuthority authority = order is CurrentWorldEditOrder
            ? new LocalPlayerWorldEditAuthority(_authority, PlayerForceId!.Value, LocalActor) : _authority;
        CommandResult result = _simulation.Apply(order, Rules.OrderRules,
            new CommandContext(LocalActor), authority, appRandomSnapshot, prepared);
        if (result.Accepted && direct) _adoptTutorialWorld?.Invoke(before, World);
        return result;
    }

    public bool AdvancePeriod()
    {
        int player = RequirePlayer();
        if (PlayableOutcome.Evaluate(World, player) != PlayableResult.Ongoing) return false;
        _simulation.AdvancePeriod(Rules.AdvancementRules,
            _planner, _advanceCalendar);
        return true;
    }

    public void FinishReport(CampaignAppRandomState? appRandomSnapshot = null)
    {
        _ = RequirePlayer();
        GameState before = World;
        _simulation.FinishReport(Rules.ReportingRules, appRandomSnapshot, _prepareTutorialWorld?.Invoke(before));
        _adoptTutorialWorld?.Invoke(before, World);
    }

    public void Save(string path, DateTimeOffset? savedAt = null, CampaignAppRandomState? appRandomSnapshot = null,
        ArmyMapSpeechState? speechSnapshot = null, GameState? speechExpectedOwner = null)
    {
        //Outside a suspended transaction the live host owns presentation
        //draws, including cancelled command drafts. Lend that stream only to
        //this saved snapshot; saving must not replace the live world. Pending
        //operations already own both their committed and candidate streams.
        var savedWorld = !HasPendingOperation ? _prepareTutorialWorld?.Invoke(World) ?? World : World;
        if (!HasPendingOperation && appRandomSnapshot is not null)
            savedWorld = savedWorld with { CampaignAppRandom32 = appRandomSnapshot };
        var savedNormalPeriod = PendingNormalPeriod;
        var savedArmyProgress = PendingArmyProgress;
        var speechOwner = savedNormalPeriod?.Candidate ?? PendingArmyProgressCandidate ?? World;
        if (appRandomSnapshot is not null && savedNormalPeriod?.Candidate.NativePeriodContinuation?.Cursor?.AwaitingIntelligenceTaskId is int intelligenceTask &&
            savedNormalPeriod.Candidate.PersonnelCommands?.Intelligences?.GetValueOrDefault(intelligenceTask)?.NarrationProgress?.Pending is
                { Phase: PersonnelIntelligenceNarrationPhase.AwaitingNarration or PersonnelIntelligenceNarrationPhase.ReadyToResume } &&
            (speechExpectedOwner is null || ReferenceEquals(speechExpectedOwner, speechOwner)))
            savedNormalPeriod = savedNormalPeriod with
            { Candidate = savedNormalPeriod.Candidate with { CampaignAppRandom32 = appRandomSnapshot } };
        if (appRandomSnapshot is not null && savedNormalPeriod is not null &&
            (BaseCapturePersonPrefixProgress.IsPresentationPending(speechOwner) ||
                HiredArmyCaptureProgress.IsPresentationPending(speechOwner)) &&
            (speechExpectedOwner is null || ReferenceEquals(speechExpectedOwner, speechOwner)))
            savedNormalPeriod = savedNormalPeriod with
            { Candidate = savedNormalPeriod.Candidate with { CampaignAppRandom32 = appRandomSnapshot } };
        // A stale display snapshot must not replace a newer queue or another
        // suspended transaction's owner. Saving never mutates the live session.
        if (speechSnapshot is not null && ReferenceEquals(speechExpectedOwner, speechOwner) &&
            (savedNormalPeriod is not null || savedArmyProgress is not null || PendingCampaignEventCommand is not null || !HasPendingOperation))
        {
            if (savedNormalPeriod is not null)
                savedNormalPeriod = savedNormalPeriod with
                { Candidate = savedNormalPeriod.Candidate with { ArmyMapSpeech = speechSnapshot } };
            else if (savedArmyProgress is not null)
                savedArmyProgress = savedArmyProgress with
                { Candidate = savedArmyProgress.Candidate with { ArmyMapSpeech = speechSnapshot } };
            else savedWorld = savedWorld with { ArmyMapSpeech = speechSnapshot };
        }
        _saves.Save(path, savedWorld, RequirePlayer(), PendingProgress, LastStorySequence, PendingManualTactic, savedAt,
            normalPeriod: savedNormalPeriod, armyProgress: savedArmyProgress);
    }

    public void SavePreview(string path) => _previewSaves.Save(path, World);

    public static GameSessionSave ReadSave(string path, ISaveCodec? codec = null) =>
        new SessionSaveStore(codec ?? new JsonGzipSaveCodec()).Load(path);

    public static GameState ReadPreview(string path, ISaveCodec? codec = null)
    {
        GameState world = new SaveStore(codec ?? new JsonGzipSaveCodec()).Load(path);
        if (world.Phase != TurnPhase.Initializing)
            throw new InvalidDataException("This browser loads initialized-world snapshots only.");
        return world;
    }

    private int RequirePlayer() => PlayerForceId ??
        throw new InvalidOperationException("The campaign has not started.");
}
