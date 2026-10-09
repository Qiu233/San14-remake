namespace San14.Core;

/// <summary>The shared raw override/provocation target bytes. A missing owner
/// or field is unknown; typed UI targets do not reconstruct these values.</summary>
public sealed record NativeArmyOverrideTargetRuntime(byte? Kind3C, ushort? Identity3E);

public sealed partial record UnitState
{
    public NativeArmyOverrideTargetRuntime? NativeOverrideTarget { get; init; }
}

public static class NativeArmyOverrideTargetWrites
{
    /// <summary>Adopts an explicit source writer and its existing march view.
    /// Null means that this operation did not write the shared raw target.</summary>
    public static UnitState Apply(UnitState army, NativeArmyOverrideTargetRuntime? fields) => fields is null ? army :
        army with { NativeOverrideTarget = fields,
            NativeMarch = fields.Kind3C is byte kind && army.NativeMarch is { } march
                ? march with { OverrideTargetType3C = kind } : army.NativeMarch };
}
