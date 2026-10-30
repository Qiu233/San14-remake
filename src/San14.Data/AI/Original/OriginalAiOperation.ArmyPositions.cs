using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus MoveArmyToAdvantagePosition()
        => ApplyArmyActorFlagCommand((force, unit) => new MoveArmyToAdvantagePositionOrder(force, unit));

    internal OriginalAiNodeStatus MoveArmyToObjectAttackPosition()
        => ApplyArmyActorFlagCommand((force, unit) => new MoveArmyToObjectAttackPositionOrder(force, unit));
}
