namespace San14.Core;

/// <summary>Actual raw receiver target. Missing module/field remains unknown;
/// it is independent from the client's provisional DeploymentAction.</summary>
public sealed record NativeArmyLifecycleTargetState(byte? Kind38, ushort? Identity3A);
public sealed partial record NativeArmyLifecycleState
{
    public NativeArmyLifecycleTargetState[]? Targets38 { get; init; }
}
public static class NativeArmyLifecycleTargetStateValidation
{
    public static void Validate(NativeArmyLifecycleState arena)
    {
        if (arena.Targets38 is { } targets && (targets.Length != 501 || arena.Slots.Length != 501 || targets.Any(x => x is null)))
            throw new InvalidDataException("Current native Army targets require all501 physical receiver rows.");
    }
}
