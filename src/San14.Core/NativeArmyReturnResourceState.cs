namespace San14.Core;

/// <summary>Independent saved Army dwords used by the native return resource
/// stage. These are not the combat unit's food supply or transport cargo.</summary>
public sealed record NativeArmyReturnResources(int Gold5C, int Supplies60);

public sealed partial record UnitState
{
    public NativeArmyReturnResources? NativeReturnResources { get; init; }
}
