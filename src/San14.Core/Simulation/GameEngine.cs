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
    // Transient creation outcome. A cleared target can still accompany an
    // accepted world containing the actual allocated/registered relation reset.
    public ArmyCautionCreation? ArmyCautionCreation { get; init; }
    public PersonnelHireExecution? PersonnelHireExecution { get; init; }
    public PersonnelItemTransferAnnouncement? PersonnelItemTransferAnnouncement { get; init; }
    public TransportFoodWarningResult? TransportFoodWarning { get; init; }
    public bool Accepted => State is not null;
    public static CommandResult Reject(string reason, string? code = null) => new(null, reason) { ErrorCode = code };
    public static CommandResult RuleUnavailable(string key, string reason) => new(null, reason)
        { ErrorCode = "command.rule_unavailable", MissingRuleKey = key };
    public static CommandResult Accept(GameState state) => new(state, null);
}

public interface IOrderRules
{
    // The state belongs to the current transaction. Rules may change its collections,
    // but must not retain them after returning or introduce shared mutable state.
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
        // ApplyCandidate already owns a detached transaction. Rules cannot
        // retain its collections, so the caller takes that same result.
        => ApplyCandidate(state, order, rules, context, authority);

    internal static CommandResult ApplyCandidate(GameState state, GameOrder order,
        IOrderRules rules, CommandContext context, IOrderAuthority authority)
    {
        CommandResult? error = CheckOrder(state, order, context, authority);
        return error ?? ApplyOwned(state, GameStateCopy.Copy(state), order, rules, context);
    }

    internal static CommandResult? CheckOrder(GameState state, GameOrder order,
        CommandContext context, IOrderAuthority authority)
        => CheckOrderPermissions(state, order, context, authority, true);

    //The caller must own a real pending presentation editor. All normal
    //force/actor/scope checks are identical; no synthetic Strategy phase is made.
    internal static CommandResult? CheckPresentationOrder(GameState state, GameOrder order,
        CommandContext context, IOrderAuthority authority)
        => CheckOrderPermissions(state, order, context, authority, false);

    private static CommandResult? CheckOrderPermissions(GameState state, GameOrder order,
        CommandContext context, IOrderAuthority authority, bool requireStrategyPhase)
    {
        if (state.EditedEventPlayback is not null)
            return CommandResult.Reject("The campaign is awaiting its manual edited event.", "command.pending_edited_event");
        if (state.NativePlayableTerminal is not null || state.CampaignTurnStop is not null)
            return CommandResult.Reject("The campaign has ended.", "command.campaign_ended");
        if (requireStrategyPhase && state.Phase != TurnPhase.Strategy &&
            !(order is SetOfficerPriorityOrder && state.Phase == TurnPhase.Reporting))
            return CommandResult.Reject("Orders can only be issued during the strategy phase.", "command.phase");
        if (!state.Forces.TryGetValue(order.ForceId, out ForceState? force))
            return CommandResult.Reject("Force does not exist.", "command.force_missing");
        if (!force.IsActive)
            return CommandResult.Reject("Force is not active.", "command.force_inactive");
        if (string.IsNullOrWhiteSpace(context.ActorId) ||
            !authority.CanControl(context, order.ForceId))
            return CommandResult.Reject("Actor is not authorized to command this force and scope.", "command.unauthorized");
        if (order is CurrentWorldEditOrder &&
            (authority is not ICurrentWorldEditAuthority editor || !editor.CanEditCurrentWorld(context, order.ForceId)))
            return CommandResult.Reject("The actor has no current-world editing capability.", "world_edit.unauthorized");
        if (context.DelegatedDistrictId is int districtId &&
            (!state.Districts.TryGetValue(districtId, out DistrictState? district) || district.ForceId != force.Id))
            return CommandResult.Reject("Delegated district does not belong to this force.", "command.district_missing");
        return null;
    }

    internal static CommandResult ApplyOwned(GameState state, GameState working,
        GameOrder order, IOrderRules rules, CommandContext context)
    {
        CommandResult result = rules.Apply(working, working.Forces[order.ForceId], order, context);
        if (result.Accepted)
        {
            GameState next = result.State!;
            ValidateWorldIdentity(state, next);
            if (next.Phase != state.Phase || next.Date != state.Date ||
                next.PeriodNumber != state.PeriodNumber || next.NativeClock != state.NativeClock)
                throw new InvalidDataException("Command rules changed the turn clock or phase.");
        }
        return result;
    }

    public static GameState AdvancePeriod(GameState state, IAdvancementRules rules)
        => AdvancePeriodOwned(state, GameStateCopy.Copy(state), rules);

    internal static GameState AdvancePeriodOwned(GameState state, GameState working, IAdvancementRules rules)
    {
        if (state.EditedEventPlayback is not null)
            throw new InvalidOperationException("The campaign is awaiting its manual edited event.");
        if (state.NativePlayableTerminal is not null || state.CampaignTurnStop is not null)
            throw new InvalidOperationException("The campaign has ended.");
        if (state.Phase != TurnPhase.Strategy)
            throw new InvalidOperationException("The state is not in the strategy phase.");
        GameState next = rules.AdvanceTenDays(working);
        ValidateWorldIdentity(state, next);
        if (NormalPeriodOperation.HasContinuation(next))
        {
            new NormalPeriodOperation(next).Validate(state);
            return next;
        }
        if (next.NativePlayableTerminal is not null || next.CampaignTurnStop is not null)
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
        return next;
    }

    public static GameState FinishReport(GameState state, IReportingRules rules)
        => FinishReportOwned(state, GameStateCopy.Copy(state), rules);

    internal static GameState FinishReportOwned(GameState state, GameState working, IReportingRules rules)
    {
        if (state.Phase != TurnPhase.Reporting)
            throw new InvalidOperationException("There is no report to finish.");
        GameState next = rules.CompleteReport(working);
        ValidateWorldIdentity(state, next);
        if (next.Phase != TurnPhase.Strategy || next.Date != state.Date || next.PeriodNumber != state.PeriodNumber || next.NativeClock != state.NativeClock ||
            next.NativePlayableTerminal != state.NativePlayableTerminal || next.CampaignTurnStop != state.CampaignTurnStop)
            throw new InvalidDataException("Reporting rules returned an invalid phase or date.");
        return next;
    }

    // Commands and period adapters operate on one world. Starting a scenario,
    // upgrading a ruleset or replacing a map are explicit operations outside these
    // transitions, so a faulty optional module cannot quietly replace the session.
    internal static void ValidateWorldIdentity(GameState previous, GameState next)
    {
        // Full resource-ledger comparisons belong to offline verification.
        // Runtime transitions retain only the session's scalar identity.
        if (next is null || next.ScenarioKey != previous.ScenarioKey ||
            next.RulesetId != previous.RulesetId || next.MapSha256 != previous.MapSha256 ||
            next.MapWidth != previous.MapWidth || next.MapHeight != previous.MapHeight ||
            next.ContentHashes is null)
            throw new InvalidDataException("Rules returned a different or missing game world.");
    }
}

/// <summary>Creates an independent world without save encoding, migration or initialization.</summary>
internal static class GameStateSnapshot
{
    internal static GameState Detach(GameState state) => GameStateCopy.Copy(state);
}

public interface IAdvancementRules
{
    // The input belongs to this transaction. Return its completed world;
    // do not retain mutable collections or return another owner's world.
    GameState AdvanceTenDays(GameState state);
}

public interface IReportingRules
{
    // The input belongs to this transaction, with the same ownership transfer
    // as advancement and command rules.
    GameState CompleteReport(GameState state);
}
