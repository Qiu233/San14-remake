using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus OccupyArmyNeighborCapital()
        => ApplyArmyActorFlagCommand((force, unit) => new OccupyArmyNeighborCapitalOrder(force, unit));

    internal OriginalAiNodeStatus OccupyArmyNeighborScenery()
        => ApplyArmyActorFlagCommand((force, unit) => new OccupyArmyNeighborSceneryOrder(force, unit));
}
