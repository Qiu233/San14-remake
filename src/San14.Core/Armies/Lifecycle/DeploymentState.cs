namespace San14.Core;

public enum DeploymentTargetKind { Hex, Unit, Location, Area, Structure }
public sealed record DeploymentTarget(DeploymentTargetKind Kind, int Id);
/// <summary>Action keys are supplied by a ruleset. Target order is significant.</summary>
public sealed record DeploymentAction(string ActionKey, DeploymentTarget Target,
    string? StructureKindId = null);
public enum DeploymentAfterAction { Stay, Retreat }
public sealed record DeploymentBehavior(bool AttackNearbyEnemies, bool AllowRetreat,
    bool PursueRetreatingEnemies, DeploymentAfterAction AfterAction);
public sealed record DeploymentUnitRequest(int OfficerId, int FormationId, int Troops,
    IReadOnlyList<int> TacticIds, IReadOnlyList<DeploymentAction> Actions)
{
    // An explicit normal command input. Absence is unknown, never the short-script default11.
    public int? NavalFormationId { get; init; }
    public IReadOnlyList<int> WaypointHexIds { get; init; } = [];
    // No guessed initial behavior. The request must explicitly choose all four settings.
    public DeploymentBehavior? Behavior { get; init; }
}
public sealed record PrepareDeploymentOrder(int ForceId, int SourceLocationId,
    IReadOnlyList<DeploymentUnitRequest> Units) : GameOrder(ForceId);
public sealed record CancelPreparedDeploymentOrder(int ForceId, int DeploymentId) : GameOrder(ForceId);

/// <summary>
/// One prepared unit, still inside its source. Debit flags record what actually happened,
/// so cancellation and execution never infer accounting from a later policy version.
/// Morale is read at departure, not frozen during preparation. No caller-provided food load.
/// </summary>
public sealed record PreparedDeployment(int Id, int BatchId, int ForceId, int DistrictId, int SourceLocationId,
    int OfficerId, int FormationId, int Troops, int[] TacticIds, DeploymentAction[] Actions,
    int[] WaypointHexIds, DeploymentBehavior Behavior, bool TroopsDebited, bool CommandBookDebited);

public sealed record DeploymentSource(int LocationId, int ForceId, int DistrictId, int HexId, int Troops);
public sealed record OrdinaryDeploymentNavalFormation(int? FormationId, string? UnresolvedInput)
{
    public bool Available => FormationId.HasValue && UnresolvedInput is null;
}
public interface IPlayableOrdinaryDeploymentCommitRules
{
    OrdinaryDeploymentNavalFormation ResolveDefaultNavalFormation(GameState state, int officerId);
    CommandResult CommitOrdinaryDeployment(GameState state, DeploymentSource source,
        IReadOnlyList<DeploymentUnitRequest> units, CommandContext context);
}
public enum DeploymentDecisionStatus { Unavailable, Allowed, Denied }
public readonly record struct DeploymentEligibility(DeploymentDecisionStatus Status,
    string? Explanation = null, string? MissingRuleKey = null);
public enum DeploymentDebitTiming { Unavailable, Preparation, Execution }
public readonly record struct DeploymentPreparationResources(DeploymentDebitTiming Troops,
    DeploymentDebitTiming CommandBooks);
public readonly record struct DeploymentCancellationResources(int? TroopsReturned, int? CommandBooksReturned);

public interface IDeploymentEligibility
{
    // Must verify officer tasks/health, capacity/minimum troops, formation and tactics,
    // action permissions and movement targets; inspect the full batch without mutation.
    DeploymentEligibility Evaluate(GameState state, DeploymentSource source,
        IReadOnlyList<DeploymentUnitRequest> units, CommandContext context);
    // Also required when nothing was debited: source evidence has not established cancellation granularity.
    DeploymentEligibility EvaluateCancellation(GameState state, PreparedDeployment deployment, CommandContext context);
}

public interface IDeploymentResourceRules
{
    DeploymentPreparationResources Prepare(GameState state, DeploymentSource source,
        IReadOnlyList<DeploymentUnitRequest> units, CommandContext context);
    DeploymentCancellationResources Cancel(GameState state, PreparedDeployment deployment, CommandContext context);
}

public static class DeploymentAvailability
{
    public static long ReservedTroops(GameState state, int locationId) => state.PreparedDeployments.Values
        .Where(p => p.SourceLocationId == locationId && !p.TroopsDebited).Sum(p => (long)p.Troops);
    public static int ReservedCommandBooks(GameState state, int districtId) => state.PreparedDeployments.Values
        .Count(p => p.DistrictId == districtId && !p.CommandBookDebited);
}

internal static class DeploymentStateValidation
{
    public static bool IsActionTargetValid(GameState state, DeploymentAction? action,
        bool allowMissingUnitTargets = false)
    {
        if (action is null || string.IsNullOrWhiteSpace(action.ActionKey) || action.Target is not { } target || target.Id < 0)
            return false;
        return target.Kind switch
        {
            DeploymentTargetKind.Hex => target.Id < checked(state.MapWidth * state.MapHeight),
            //New submissions require a live target. An already-submitted
            //playable action can outlive its target until runtime termination.
            DeploymentTargetKind.Unit => allowMissingUnitTargets || state.Units.ContainsKey(target.Id),
            DeploymentTargetKind.Location => state.Cities.ContainsKey(target.Id) || state.Gates.ContainsKey(target.Id),
            DeploymentTargetKind.Area => state.Areas.ContainsKey(target.Id),
            // Structure identities need a later state adapter; current original commands explicitly reject them.
            DeploymentTargetKind.Structure => true,
            _ => false
        };
    }
}
