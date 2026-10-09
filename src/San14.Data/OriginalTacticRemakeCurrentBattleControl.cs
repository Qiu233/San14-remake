using San14.Core;

namespace San14.Data;

/// <summary>Owned CTutorialTimer header and its13-byte control bitmap.
/// The paired constructors explicitly clear both. This new session allocation
/// does not read an original process pointer or repair an older save.</summary>
public static class OriginalTacticRemakeCurrentBattleControl
{
    public const string ProfileId = "san14:ce-owned-tutorial-battle-control-20261002";
    public const string HeaderKey = "ce-tutorial-battle-control:header0";
    public const string VersionKey = "ce-tutorial-battle-control:version";
    public static string BitmapKey(int index) => index is >= 0 and < 13
        ? $"ce-tutorial-battle-control:bitmap:{index}"
        : throw new ArgumentOutOfRangeException(nameof(index));

    public static GameState CreateFreshOwnedControl(GameState state)
    {
        if (!Bound(state) || state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.NativeArmySimulation!.Parameters.Int32Values.Keys.Any(key =>
                key.StartsWith("ce-tutorial-battle-control:", StringComparison.Ordinal)))
            throw new InvalidDataException("Fresh tutorial battle control requires a new empty owned Army session.");
        NativeArmyParameterState parameters = state.NativeArmySimulation!.Parameters;
        var values = new Dictionary<string, int>(parameters.Int32Values)
        { [VersionKey] = 1, [HeaderKey] = 0 };
        for (int index = 0; index < 13; index++) values.Add(BitmapKey(index), 0);
        return state with { NativeArmySimulation = state.NativeArmySimulation with
            { Parameters = parameters with { Int32Values = values } } };
    }

    /// <summary>SC17A1D0/TC17A470. Header0 zero returns false before the
    /// bitmap query; a missing current header remains unknown. Only actual
    /// owned tutorial writers may change or invalidate these current values.</summary>
    public static bool? Read(GameState state, int nativeControlIndex)
    {
        if (nativeControlIndex is < 0 or >= 104)
            throw new InvalidDataException("Tutorial battle control index exceeds the inspected bitmap.");
        if (!Bound(state) || !state.NativeArmySimulation!.Parameters.Int32Values.TryGetValue(VersionKey, out int version) ||
            version != 1 || !state.NativeArmySimulation.Parameters.Int32Values.TryGetValue(HeaderKey, out int header)) return null;
        if (header == 0) return false;
        return state.NativeArmySimulation.Parameters.Int32Values.TryGetValue(BitmapKey(nativeControlIndex >> 3), out int bits) &&
            bits is >= 0 and <= byte.MaxValue ? (bits & (1 << (nativeControlIndex & 7))) != 0 : null;
    }

    private static bool Bound(GameState state) => state.NativeClock is not null &&
        state.NativeArmySimulation?.Parameters is { } parameters && !string.IsNullOrWhiteSpace(parameters.Revision) &&
        (parameters.Language is ("SC" or "TC"));
}
