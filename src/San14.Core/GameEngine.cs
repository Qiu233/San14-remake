namespace San14.Core;

public abstract record GameOrder(int ForceId);
public sealed record AssignOverseerOrder(int ForceId, int CityId, int AreaId, int OfficerId)
    : GameOrder(ForceId);
public sealed record MarchOrder(int ForceId, int CityId, int OfficerId,
    int DestinationHexId, int Troops, int Supplies)
    : GameOrder(ForceId);

public sealed record CommandResult(GameState? State, string? Error)
{
    public string? ErrorCode { get; init; }
    public string? MissingRuleKey { get; init; }
    public bool Accepted => State is not null;
    public static CommandResult Reject(string reason, string? code = null) => new(null, reason) { ErrorCode = code };
    public static CommandResult RuleUnavailable(string key, string reason) => new(null, reason)
        { ErrorCode = "command.rule_unavailable", MissingRuleKey = key };
    public static CommandResult Accept(GameState state) => new(state, null);
}

public interface IOrderRules
{
    CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context);
}

/// <summary>Authority belongs to a session or future host, never to shared simulation state.</summary>
public interface IOrderAuthority
{
    bool CanControl(CommandContext context, int forceId);
}

/// <summary>
/// Null district selects direct control; a delegated district must be explicitly
/// authorized by the session. This is not an untrusted "is AI" permission flag.
/// </summary>
public readonly record struct CommandContext(string ActorId, int? DelegatedDistrictId = null);

/// <summary>Shared command validation for human and computer players.</summary>
public static class GameEngine
{
    public static CommandResult Apply(GameState state, GameOrder order, IOrderRules rules,
        CommandContext context, IOrderAuthority authority)
    {
        if (state.NativePlayableTerminal is not null)
            return CommandResult.Reject("The campaign has ended.", "command.campaign_ended");
        if (state.Phase != TurnPhase.Strategy)
            return CommandResult.Reject("Orders can only be issued during the strategy phase.", "command.phase");
        if (!state.Forces.TryGetValue(order.ForceId, out ForceState? force))
            return CommandResult.Reject("Force does not exist.", "command.force_missing");
        if (!force.IsActive)
            return CommandResult.Reject("Force is not active.", "command.force_inactive");
        if (string.IsNullOrWhiteSpace(context.ActorId) ||
            !authority.CanControl(context, order.ForceId))
            return CommandResult.Reject("Actor is not authorized to command this force and scope.", "command.unauthorized");
        if (context.DelegatedDistrictId is int districtId &&
            (!state.Districts.TryGetValue(districtId, out DistrictState? district) || district.ForceId != force.Id))
            return CommandResult.Reject("Delegated district does not belong to this force.", "command.district_missing");
        StateValidator.Validate(state);
        // Optional adapters receive an owned working copy. A rejection or an
        // exception cannot change the caller's dictionaries or nested arrays.
        GameState working = GameStateSnapshot.Detach(state, out byte[] previousSnapshot);
        CommandResult result = rules.Apply(working, working.Forces[force.Id], order, context);
        if (result.Accepted)
        {
            GameState next = GameStateSnapshot.Detach(result.State!, out byte[] nextSnapshot);
            ValidateWorldIdentity(state, next);
            if (next.Phase != state.Phase || next.Date != state.Date ||
                next.PeriodNumber != state.PeriodNumber || next.NativeClock != state.NativeClock)
                throw new InvalidDataException("Command rules changed the turn clock or phase.");
            StateValidator.Validate(next);
            // Preserve the established identity of a genuine no-op. The adapter
            // never received this instance, so its retained copies cannot alter it.
            return result with { State = previousSnapshot.AsSpan().SequenceEqual(nextSnapshot) ? state : next };
        }
        return result;
    }

    public static GameState AdvancePeriod(GameState state, IAdvancementRules rules)
    {
        if (state.NativePlayableTerminal is not null)
            throw new InvalidOperationException("The campaign has ended.");
        if (state.Phase != TurnPhase.Strategy)
            throw new InvalidOperationException("The state is not in the strategy phase.");
        StateValidator.Validate(state);
        GameState next = GameStateSnapshot.Detach(rules.AdvanceTenDays(GameStateSnapshot.Detach(state)));
        ValidateWorldIdentity(state, next);
        if (next.NativePlayableTerminal is not null)
        {
            //The reached capture can end before the remaining movement/economy
            //and period date rollover. Its report gets this operation's ordinal;
            //the actual native cursor is retained at the reached stop.
            if (next.Phase != TurnPhase.Reporting || next.Date != state.Date ||
                next.PeriodNumber != checked(state.PeriodNumber + 1) ||
                next.PreparedDeployments.Count != 0 || next.PreparedOfficerMoves is { Count: > 0 } ||
                next.PreparedTalentOrders is { Count: > 0 })
                throw new InvalidDataException("Terminal advancement returned an invalid report boundary.");
            if (state.NativeClock is { } prior &&
                (next.NativeClock is not { } stopped || stopped.Revision != prior.Revision ||
                 stopped.AnchorDate != prior.AnchorDate || stopped.UpdateOrdinal < prior.UpdateOrdinal ||
                 stopped.UpdateOrdinal - prior.UpdateOrdinal != stopped.UpdatesIntoPeriod - prior.UpdatesIntoPeriod))
                throw new InvalidDataException("Terminal advancement returned an invalid native stop cursor.");
            if (state.NativeClock is null && next.NativeClock is not null)
                throw new InvalidDataException("Terminal advancement initialized an unresolved native clock.");
            StateValidator.Validate(next);
            return next;
        }
        if (next.Phase != TurnPhase.Reporting || next.Date != state.Date.Next() ||
            next.PeriodNumber != checked(state.PeriodNumber + 1) || next.PreparedDeployments.Count != 0 ||
            next.PreparedOfficerMoves is { Count: > 0 } ||
            next.PreparedTalentOrders is { Count: > 0 } ||
            next.Units.Values.Any(unit => unit.TacticRuntime?.Pending is not null))
            throw new InvalidDataException("Advancement rules returned an invalid phase or date.");
        if (state.NativeClock is { } clock &&
            (next.NativeClock is not { } advancedClock || advancedClock.Revision != clock.Revision ||
             advancedClock.AnchorDate != next.Date || advancedClock.UpdatesIntoPeriod != 0 ||
             advancedClock.UpdateOrdinal != checked(clock.UpdateOrdinal + NativeSubdayStateValidation.UpdatesPerPeriod - clock.UpdatesIntoPeriod)))
            throw new InvalidDataException("Advancement rules returned an invalid native subday clock.");
        if (state.NativeClock is null && next.NativeClock is not null)
            throw new InvalidDataException("Advancement rules initialized an unresolved native clock.");
        StateValidator.Validate(next);
        return next;
    }

    public static GameState FinishReport(GameState state, IReportingRules rules)
    {
        if (state.Phase != TurnPhase.Reporting)
            throw new InvalidOperationException("There is no report to finish.");
        StateValidator.Validate(state);
        GameState next = GameStateSnapshot.Detach(rules.CompleteReport(GameStateSnapshot.Detach(state)));
        ValidateWorldIdentity(state, next);
        if (next.Phase != TurnPhase.Strategy || next.Date != state.Date || next.PeriodNumber != state.PeriodNumber || next.NativeClock != state.NativeClock ||
            next.NativePlayableTerminal != state.NativePlayableTerminal)
            throw new InvalidDataException("Reporting rules returned an invalid phase or date.");
        StateValidator.Validate(next);
        return next;
    }

    // Commands and period adapters operate on one world. Starting a scenario,
    // upgrading a ruleset or replacing a map are explicit operations outside these
    // transitions, so a faulty optional module cannot quietly replace the session.
    private static void ValidateWorldIdentity(GameState previous, GameState next)
    {
        if (next is null || next.ScenarioKey != previous.ScenarioKey ||
            next.RulesetId != previous.RulesetId || next.MapSha256 != previous.MapSha256 ||
            next.MapWidth != previous.MapWidth || next.MapHeight != previous.MapHeight ||
            next.ContentHashes is null || previous.ContentHashes.Count != next.ContentHashes.Count ||
            previous.ContentHashes.Any(pair => !next.ContentHashes.TryGetValue(pair.Key, out string? value) ||
                value != pair.Value))
            throw new InvalidDataException("Rules returned a different or missing game world.");
    }
}

/// <summary>Owns all serialized state collections without applying save migration or initialization.</summary>
internal static class GameStateSnapshot
{
    internal static GameState Detach(GameState state) => Detach(state, out _);

    internal static GameState Detach(GameState state, out byte[] serialized)
    {
        if (state is null) throw new InvalidDataException("Rules returned a missing game state.");
        serialized = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(state);
        return System.Text.Json.JsonSerializer.Deserialize<GameState>(serialized)
            ?? throw new InvalidDataException("Game state could not be copied.");
    }
}

public interface IAdvancementRules
{
    GameState AdvanceTenDays(GameState state);
}

public interface IReportingRules
{
    GameState CompleteReport(GameState state);
}
