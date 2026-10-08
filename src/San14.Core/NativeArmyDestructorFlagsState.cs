namespace San14.Core;

public sealed partial record NativeArmyLifecycleState
{
    /// <summary>Current raw byte150 in complete Army physical slot order.
    /// Fresh constructor and actual reset write zero; null preserves an
    /// unknown current byte or a legacy module that has no saved owner.</summary>
    public byte?[]? ArmyFlags150 { get; init; }
}

public static class NativeArmyDestructorFlagsStateValidation
{
    public static void Validate(NativeArmyLifecycleState lifecycle)
    {
        if (lifecycle.ArmyFlags150 is not null &&
            (lifecycle.Slots.Length != 501 || lifecycle.ArmyFlags150.Length != 501))
            throw new InvalidDataException("Native Army flags150 physical table is malformed.");
    }
}
