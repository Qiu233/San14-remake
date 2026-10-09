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
    {
        _simulation = new GameSimulation(world);
        Rules = rules;
        PlayerForceId = playerForceId;
        _advanceCalendar = advanceCalendar;
        _authority = playerForceId is int player ? new SingleForceAuthority(player, LocalActor) : null;
        _planner = playerForceId is int human ? new PlayableAiPlanner(rules, new HashSet<int> { human }) : null;
        _saves = new(saveCodec);
        _previewSaves = new(saveCodec);
    }

    public GameState World => _simulation.State;
    public PlayableRuleSet Rules { get; }
    public int? PlayerForceId { get; }

    public static GameplaySession Preview(GameState world, PlayableRuleSet rules, ISaveCodec? saveCodec = null) =>
        new(world, rules, null, null, saveCodec ?? new JsonGzipSaveCodec());

    public static GameplaySession Restore(GameState world, int playerForceId,
        PlayableRuleSet rules, Func<GameState, GameState> advanceCalendar, ISaveCodec? saveCodec = null) =>
        new(world, rules, playerForceId, advanceCalendar, saveCodec ?? new JsonGzipSaveCodec());

    public CommandResult Issue(GameOrder order)
    {
        if (_authority is null) throw new InvalidOperationException("The campaign has not started.");
        return _simulation.Apply(order, Rules.OrderRules,
            new CommandContext(LocalActor), _authority);
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

    public void Save(string path) => _saves.Save(path, World, RequirePlayer());

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
