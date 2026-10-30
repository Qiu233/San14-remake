using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplyOriginalAiSpecialForceStrategyCurrent(GameState state, int forceId)
    {
        if (forceId is not (>= 46 and <= 50)) throw new InvalidDataException("original_ai.special.Force_scope");
        var world = CaptureOwnedArmyReturnWorld(state);
        var source = new NormalTurnSpecialForceRaidsSource(this, state, world.Context, world.Pool);
        //These are the special worker's actual three source operations. The
        //ordinary Force worker deliberately does not call this family.
        var result = source.RunSpecialDefenseAndOffense(forceId);
        result = ApplyOriginalAiSpecialForceWithdrawalCurrent(result, world.Context, forceId);
        var arena = result.ReadArmyLifecycle() ?? throw new InvalidDataException("original_ai.special.completed_lifecycle");
        return result.WithArmyLifecycle(OwnedArmyDestructorFlags.PublishCurrent(arena, world.Context) with
            { Indexes = world.Pool.ExportOwnedState() });
    }
}
