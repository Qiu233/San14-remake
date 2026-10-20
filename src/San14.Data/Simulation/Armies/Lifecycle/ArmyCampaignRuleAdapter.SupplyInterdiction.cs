using San14.Core;

namespace San14.Data;

public enum ArmySupplyInterdictionClearOutcome { Cleared, InvalidActor, Unresolved }
public sealed record ArmySupplyInterdictionClearResult(ArmySupplyInterdictionClearOutcome Outcome,
    GameState? State, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Only the proven supply-interdiction effect of the source AI
    /// clear-action node. It neither selects an AI task nor finds a route.</summary>
    public ArmySupplyInterdictionClearResult TryClearArmySupplyInterdiction(GameState state, ArmyReceiverId receiverId)
    {
        try
        {
            var lifecycle = state.ReadArmyLifecycle() ?? throw NotificationMissing("clear_lifecycle");
            var receiver = lifecycle.Receivers.Find(receiverId) ?? throw NotificationMissing("clear_receiver_identity");
            int physical = lifecycle.Slots.FirstOrDefault(slot => slot.ReceiverId == receiverId)?.PhysicalSlot
                ?? throw NotificationMissing("clear_receiver_slot");
            var current = CaptureWritableTransportRouteCurrent(state);
            if (!NotificationKnown(current.ReadArmyUnitNativeValidity(new(physical)), "clear_actor_validity"))
                return new(ArmySupplyInterdictionClearOutcome.InvalidActor, state, null);
            return new(ArmySupplyInterdictionClearOutcome.Cleared,
                state.WithArmyLifecycle(lifecycle with { Receivers = lifecycle.Receivers.With(receiver with
                    { SupplyInterdiction = ArmySupplyInterdictionIntent.Inactive }) }), null);
        }
        catch (InvalidDataException error) { return new(ArmySupplyInterdictionClearOutcome.Unresolved, null, error.Message); }
    }
}
