namespace San14.Core;

/// <summary>The Army's current AI supply-line objective. Null at the receiver
/// means unknown. Inactive needs no target; active targets a stable reusable
/// receiver, whose current native validity is checked when it is consumed.
/// This fact is not evidence that the AI has successfully selected a route.</summary>
public sealed record ArmySupplyInterdictionIntent(bool IsActive, ArmyReceiverId? TargetReceiverId)
{
    public static ArmySupplyInterdictionIntent Inactive => new(false, null);
}
