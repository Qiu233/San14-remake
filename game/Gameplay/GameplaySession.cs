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
    private readonly SessionSaveStore _saves = new(new JsonGzipSaveCodec());

    private GameplaySession(GameState world, PlayableRuleSet rules, int? playerForceId,
        Func<GameState, GameState>? advanceCalendar)
    {
        _simulation = new GameSimulation(world);
        Rules = rules;
        PlayerForceId = playerForceId;
        _advanceCalendar = advanceCalendar;
        _authority = playerForceId is int player ? new SingleForceAuthority(player, LocalActor) : null;
        _planner = playerForceId is int human ? new PlayableAiPlanner(rules, new HashSet<int> { human }) : null;
    }

    public GameState World => _simulation.State;
    public PlayableRuleSet Rules { get; }
    public int? PlayerForceId { get; }

    public static GameplaySession Preview(GameState world, PlayableRuleSet rules) =>
        new(world, rules, null, null);

    public static GameplaySession Restore(GameState world, int playerForceId,
        PlayableRuleSet rules, Func<GameState, GameState> advanceCalendar) =>
        new(world, rules, playerForceId, advanceCalendar);

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

    public void SavePreview(string path) => new SaveStore(new JsonGzipSaveCodec()).Save(path, World);

    public static GameSessionSave ReadSave(string path) =>
        new SessionSaveStore(new JsonGzipSaveCodec()).Load(path);

    public static GameState ReadPreview(string path)
    {
        GameState world = new SaveStore(new JsonGzipSaveCodec()).Load(path);
        if (world.Phase != TurnPhase.Initializing)
            throw new InvalidDataException("This browser loads initialized-world snapshots only.");
        return world;
    }

    private int RequirePlayer() => PlayerForceId ??
        throw new InvalidOperationException("The campaign has not started.");
}
