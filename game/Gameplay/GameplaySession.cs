using San14.Core;

namespace San14.Client.Gameplay;

/// <summary>
/// Owns the live world and its local player's command authority. Views and the
/// renderer read this world; only successful application operations replace it.
/// </summary>
public sealed class GameplaySession
{
    private const string LocalActor = "local";
    private readonly GameSimulation _simulation;
    private readonly SingleForceAuthority? _authority;
    private readonly Func<GameState, GameState>? _advanceCalendar;
    private readonly ICommandPlanner? _planner;
    private readonly SessionSaveStore _saves;
    private readonly SaveStore _previewSaves;

    private GameplaySession(GameState world, PlayableRuleSet rules, int? playerForceId,
        Func<GameState, GameState>? advanceCalendar, ISaveCodec saveCodec)
        : this(new GameSimulation(world), rules, playerForceId, advanceCalendar, saveCodec) { }

    private GameplaySession(GameSimulation simulation, PlayableRuleSet rules, int? playerForceId,
        Func<GameState, GameState>? advanceCalendar, ISaveCodec saveCodec)
    {
        _simulation = simulation;
        Rules = rules;
        PlayerForceId = playerForceId;
        _advanceCalendar = advanceCalendar;
        _authority = playerForceId is int player ? new SingleForceAuthority(player, LocalActor) : null;
        _planner = playerForceId is int human ? new PlayableAiPlanner(rules, new HashSet<int> { human }) : null;
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
    public NormalPeriodOperation? PendingNormalPeriod => _simulation.PendingNormalPeriod;
    public bool TryCompletePersonnelPresentation(GameState expectedOwner,long batchSequence,int requestIndex) =>
        _simulation.TryCompletePersonnelPresentation(expectedOwner,batchSequence,requestIndex);
    public bool TryStartArmyMapSpeech(GameState expectedOwner, ArmyMapSpeechState snapshot, long sequence,
        bool armyLayoutAvailable, IArmyMapSpeechRules rules, out ArmyMapSpeechStartResult? result,
        CampaignAppRandomState? appRandomSnapshot = null) =>
        _simulation.TryStartArmyMapSpeech(expectedOwner, snapshot, sequence, armyLayoutAvailable, rules,
            out result, appRandomSnapshot);
    public bool TryCompleteArmyMapSpeech(GameState expectedOwner, ArmyMapSpeechState snapshot, long sequence) =>
        _simulation.TryCompleteArmyMapSpeech(expectedOwner, snapshot, sequence);
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

    internal GameSimulation CaptureSimulationFork(CampaignAppRandomState? appRandomSnapshot = null) => _simulation.Fork(appRandomSnapshot);

    internal static GameplaySession FromSimulationFork(GameSimulation simulation,
        PlayableRuleSet rules, int playerForceId, Func<GameState, GameState> calendar, ISaveCodec codec) =>
        new(simulation, rules, playerForceId, calendar, codec);

    public static GameplaySession Preview(GameState world, PlayableRuleSet rules, ISaveCodec? saveCodec = null) =>
        new(world, rules, null, null, saveCodec ?? new JsonGzipSaveCodec());

    public static GameplaySession Restore(GameState world, int playerForceId,
        PlayableRuleSet rules, Func<GameState, GameState> advanceCalendar, ISaveCodec? saveCodec = null,
        CampaignProgressOperation? pending = null, long lastStorySequence = 0, ManualTacticProgressOperation? manualTactic = null,
        NormalPeriodOperation? normalPeriod = null)
    {
        var session = new GameplaySession(world, rules, playerForceId, advanceCalendar, saveCodec ?? new JsonGzipSaveCodec());
        session._simulation.RestoreProgress(pending, lastStorySequence, manualTactic);
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
        return _simulation.Apply(order, Rules.OrderRules,
            new CommandContext(LocalActor), _authority, appRandomSnapshot);
    }

    public bool AdvancePeriod()
    {
        int player = RequirePlayer();
        if (PlayableOutcome.Evaluate(World, player) != PlayableResult.Ongoing) return false;
        _simulation.AdvancePeriod(Rules.AdvancementRules,
            _planner, _advanceCalendar);
        return true;
    }

    public void FinishReport()
    {
        _ = RequirePlayer();
        _simulation.FinishReport(Rules.ReportingRules);
    }

    public void Save(string path, DateTimeOffset? savedAt = null, CampaignAppRandomState? appRandomSnapshot = null,
        ArmyMapSpeechState? speechSnapshot = null, GameState? speechExpectedOwner = null)
    {
        //Outside a suspended transaction the live host owns presentation
        //draws, including cancelled command drafts. Lend that stream only to
        //this saved snapshot; saving must not replace the live world. Pending
        //operations already own both their committed and candidate streams.
        var savedWorld = !HasPendingOperation && appRandomSnapshot is not null
            ? World with { CampaignAppRandom32 = appRandomSnapshot } : World;
        var savedNormalPeriod = PendingNormalPeriod;
        var speechOwner = savedNormalPeriod?.Candidate ?? World;
        // A stale display snapshot must not replace a newer queue or another
        // suspended transaction's owner. Saving never mutates the live session.
        if (speechSnapshot is not null && ReferenceEquals(speechExpectedOwner, speechOwner) &&
            (savedNormalPeriod is not null || !HasPendingOperation))
        {
            if (savedNormalPeriod is not null)
                savedNormalPeriod = savedNormalPeriod with
                { Candidate = savedNormalPeriod.Candidate with { ArmyMapSpeech = speechSnapshot } };
            else savedWorld = savedWorld with { ArmyMapSpeech = speechSnapshot };
        }
        _saves.Save(path, savedWorld, RequirePlayer(), PendingProgress, LastStorySequence, PendingManualTactic, savedAt,
            normalPeriod: savedNormalPeriod);
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
