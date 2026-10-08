namespace San14.Core;

/// <summary>Mutable native fields kept separately from immutable scenario
/// evidence. Null modules and nullable fields retain unknown old-save inputs.</summary>
public sealed record NativeNormalArmyForceAftermath(byte[]? Relations13, short? Development1BE);
public sealed record NativeNormalArmyAreaAftermath(ushort? Quantity52);
public sealed partial record AreaState
{
    public NativeNormalArmyAreaAftermath? NativeNormalAftermath { get; init; }
}

public static class NativeNormalArmyAftermathStateValidation
{
    public static void Validate(GameState state)
    {
        if (state.Areas.Values.Any(area => area.NativeNormalAftermath is not null && state.NativeArmySimulation is null) ||
            state.NativeArmySimulation?.Forces.Values.Any(force => force.NormalAftermath is { } current &&
                (current.Relations13 is { Length: not 52 } || current.Development1BE is < -1000 or > 1000)) == true)
            throw new InvalidDataException("Native normal Army aftermath current fields are malformed.");
    }
}
