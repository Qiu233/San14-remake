using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus RecoverArmyLogistics()
        => ApplyArmyActorFlagCommand((force, unit) => new RecoverArmyLogisticsOrder(force, unit));

    internal OriginalAiNodeStatus ExcludeArmyFromPlayerFoothold()
        => ApplyArmyActorFlagCommand((force, unit) => new ExcludeArmyFromPlayerFootholdOrder(force, unit));
}
