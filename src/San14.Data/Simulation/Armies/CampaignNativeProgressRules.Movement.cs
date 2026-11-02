using San14.Core;

namespace San14.Data;

internal sealed record LegacyArmyMovementOperation(ArmyMarchSourceRuntime Runtime,
    SourceWorldContext Current, WorldIndexContext Pool)
{
    internal GameState Publish(GameState state) => state.WithArmyLifecycle(state.ReadArmyLifecycle()! with
        { Indexes = Pool.ProjectOwnedRoles() });
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal LegacyArmyMovementOperation CreateLegacyMovementOperation(GameState state)
    {
        var current = CaptureWritableTransportRouteCurrent(state);
        var pool = current.ReadOwnedCurrentListPool()
            ?? throw new InvalidDataException("legacy.movement.current_pool");
        var runtime = new ArmyMarchSourceRuntime(new ArmyMarchCurrentProducer(_repository, this, _map,
            operationCurrent: current), new(this, current), current);
        return new(runtime, current, pool);
    }
}

public sealed partial class CampaignNativeProgressRules
{
    internal Func<GameState, LegacyArmyMovementOperation>? CreateMovement { get; init; }

    public CampaignProgressTransition RetryArmy(CampaignArmyProgressOperation pending)
    {
        if (!pending.CanContinue || pending.Revision != Revision)
            throw Missing("movement_retry_boundary");
        try
        {
            stories.BeginOperation(pending.ServiceChanges);
            return Drive(pending.Candidate, pending.Request, pending.BlockIndex, pending.Calendar,
                pending.ServiceChanges, pending.LastStorySequence, null, armyResume: pending);
        }
        catch (InvalidDataException error)
        {
            // A failed private tail is rolled back to this exact held return.
            // It is not a stage6 retry and never publishes a partial suffix.
            return new(CampaignProgressOutcome.Unresolved, null, null, error.Message)
            { ArmyProgress = pending with { ServiceChanges = stories.PreserveClockReads(pending.ServiceChanges) } };
        }
    }

    private static CampaignProgressTransition AwaitingArmyProgress(CampaignArmyProgressOperation pending) =>
        new(CampaignProgressOutcome.AwaitingArmyProgress, null, null, null) { ArmyProgress = pending };
}
