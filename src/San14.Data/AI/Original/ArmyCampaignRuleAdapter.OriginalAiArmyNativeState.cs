using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    CommandResult IArmyInstructionExecution.ClearAction(GameState state, ForceState force,
        ClearArmyAiActionOrder order, CommandContext context)
        => ApplyAiArmyInstruction(state, order.UnitId,
            (_, army) => ArmyAiActionFlagsSource.Write(state, army.Id, 0));

    CommandResult IArmyInstructionExecution.AvoidPoison(GameState state, ForceState force,
        AvoidArmyPoisonOrder order, CommandContext context)
    {
        bool prepared = false;
        var result = ApplyAiArmyInstruction(state, order.UnitId, (current, army) =>
        {
            int physical = army.NativeArmyOrigin!.NativeSlotIndex!.Value;
            var next = TryPrepareOriginalAiTransportArmyCornerStrategyCurrent(state, current,
                army.Id, physical, out prepared);
            //01F070 sets its bit only after the actual09C5C0 callback returns
            //true. The automatic transport wrapper does not set this flag.
            return prepared ? ArmyAiActionFlagsSource.Or(next, army.Id, 1) : next;
        });
        return result.Accepted && !prepared
            ? CommandResult.Reject("The current Army does not enter poison avoidance.", "army.ai.poison_source_gate")
            : result;
    }
}
