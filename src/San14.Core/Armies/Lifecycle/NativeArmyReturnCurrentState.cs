namespace San14.Core;

/// <summary>Current independent Base fields. Gold/food/ready remain the
/// authoritative CityState quantities; no static recapture fills these fields.</summary>
public sealed record NativeCityReturnRuntime(string Revision, byte? Morale44, int? Wounded40, ushort? Governor2E);
public sealed partial record CityState
{
    public NativeCityReturnRuntime? NativeReturnRuntime { get; init; }
}
/// <summary>Gate current Base40/44. Governor2E remains in the independent
/// detached lifecycle owner; it is not duplicated into this combat stock.</summary>
public sealed record NativeGateCombatRuntime(string Revision, byte? Morale44, int? Wounded40);
public sealed partial record GateState
{
    public NativeGateCombatRuntime? NativeCombatRuntime { get; init; }
}
public static class NativeArmyReturnCurrentStateValidation
{
    public static void Validate(GameState state)
    {
        foreach (var city in state.Cities.Values)
            if (city.NativeReturnRuntime is { } fields &&
                (string.IsNullOrWhiteSpace(fields.Revision) || fields.Wounded40 is < 0))
                throw new InvalidDataException("Current native City return fields have invalid revision or wounded stock.");
        foreach (var gate in state.Gates.Values)
            if (gate.NativeCombatRuntime is { } fields &&
                (string.IsNullOrWhiteSpace(fields.Revision) || fields.Wounded40 is < 0))
                throw new InvalidDataException("Current native Gate combat fields have invalid revision or wounded stock.");
    }
}
