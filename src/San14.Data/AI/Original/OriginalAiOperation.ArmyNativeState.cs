using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus ClearArmyAiAction()
        => ApplyArmyActorFlagCommand((force, unit) => new ClearArmyAiActionOrder(force, unit));

    internal OriginalAiNodeStatus AvoidArmyPoison()
        => ApplyArmyActorFlagCommand((force, unit) => new AvoidArmyPoisonOrder(force, unit));

    private OriginalAiNodeStatus ApplyArmyActorFlagCommand(Func<int, int, GameOrder> command)
    {
        //B5D20 resolves the current native actor. These two natives have no
        //variable argument and must not use the generic Army-key resolver.
        if (Actor.AgentKind is null) throw new InvalidDataException("original_ai.Army.actor_AgentType");
        if (Actor.AgentKind != OriginalAiAgentKind.Army) return OriginalAiNodeStatus.Failure;
        int unitId = Actor.ArmyId ?? throw new InvalidDataException("original_ai.Army.actor_logical_identity");
        return Adapter.ReadOriginalAiInstructionArmyCurrent(Current, InputsCurrent, unitId) is not null
            ? ApplyNativeCommand(command(Actor.ForceId, unitId)) : OriginalAiNodeStatus.Failure;
    }
}

internal sealed record ArmyInformationSkill(OriginalAiPersonSkillReference Value) : TroopsInformationValue;
