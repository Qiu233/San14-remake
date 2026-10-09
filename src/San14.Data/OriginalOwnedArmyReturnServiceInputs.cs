using San14.Core;

namespace San14.Data;

/// <summary>CWorldData16A8 numeric current owner used by ordinary commander
/// transfer. TC326990→31B080→326230 initializes the whole dword to zero.
/// Static338EC0 does not overwrite it. The owned client does not implement
/// original33BA60's player/AI mode writer; a saved current value is retained.</summary>
public static class OriginalOwnedArmyReturnServiceInputs
{
    public const string VersionKey = "ce-army-return-service:version";
    public const string Flags16A8Key = "ce-army-return-service:dword16A8";
    public static GameState InstallFresh(GameState state)
    {
        if (!Bound(state) || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation!.Parameters.Int32Values.ContainsKey(VersionKey) ||
            state.NativeArmySimulation.Parameters.Int32Values.ContainsKey(Flags16A8Key))
            throw new InvalidDataException("Army return service flags require their explicit fresh constructor owner.");
        var simulation = state.NativeArmySimulation!;
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
            { Int32Values = new(simulation.Parameters.Int32Values) { [VersionKey] = 1, [Flags16A8Key] = 0 } } } };
    }
    public static uint? ReadCurrent(GameState state) => Bound(state) &&
        state.NativeArmySimulation!.Parameters.Int32Values.GetValueOrDefault(VersionKey) == 1 &&
        state.NativeArmySimulation.Parameters.Int32Values.TryGetValue(Flags16A8Key, out int flags)
        ? unchecked((uint)flags) : null;
    public static void BindCurrent(GameState state, OriginalEventInitializationContext current) =>
        current.RawTransferServiceFlags16A8 = ReadCurrent(state);
    private static bool Bound(GameState state) => state.NativeArmySimulation?.Parameters is { } parameters &&
        (parameters.Language is ("SC" or "TC"));
}
