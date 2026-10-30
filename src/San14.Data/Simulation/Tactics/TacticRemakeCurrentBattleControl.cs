using San14.Core;

namespace San14.Data;

/// <summary>Owned tutorial controller header and its13-byte control bitmap.
/// The controller starts inactive with no selected lesson; its separately
/// allocated bitmap is absent until actual lesson entry. This session owner
/// neither reads an original pointer nor repairs an older save.</summary>
public static class TacticRemakeCurrentBattleControl
{
    public const string ProfileId = "san14:ce-owned-tutorial-battle-control-20261002";
    public const string HeaderKey = "ce-tutorial-battle-control:header0";
    public const string LessonIndexKey = "ce-tutorial-battle-control:selected-lesson-index";
    public const string BitmapPresenceKey = "ce-tutorial-battle-control:bitmap-present";
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
        { [VersionKey] = 1, [HeaderKey] = 0, [LessonIndexKey] = -1, [BitmapPresenceKey] = 0 };
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
        if (ReadHeader(state) is not int header) return null;
        if (header == 0) return false;
        //17A470 explicitly returns false when570840 is a known null
        //bitmap, even with an active controller. Missing ownership is unknown.
        if (ReadBitmapPresence(state) is not bool present) return null;
        if (!present) return false;
        if (state.NativeArmySimulation!.Parameters.Float32Values.ContainsKey(BitmapKey(nativeControlIndex >> 3))) return null;
        return state.NativeArmySimulation.Parameters.Int32Values.TryGetValue(BitmapKey(nativeControlIndex >> 3), out int bits) &&
            bits is >= 0 and <= byte.MaxValue ? (bits & (1 << (nativeControlIndex & 7))) != 0 : null;
    }

    /// <summary>The same current header consumed by the duel manager's
    /// tutorial receiver gate. Known zero permits; unknown is not absent.</summary>
    internal static int? ReadHeader(GameState state) => Bound(state) &&
        state.NativeArmySimulation!.Parameters.Int32Values.GetValueOrDefault(VersionKey) == 1 &&
        state.NativeArmySimulation.Parameters.Int32Values.TryGetValue(HeaderKey, out int header) ? header : null;

    internal static bool? ReadBitmapPresence(GameState state)
    {
        if (!Bound(state) || state.NativeArmySimulation!.Parameters is not { } parameters ||
            parameters.Int32Values.GetValueOrDefault(VersionKey) != 1 ||
            parameters.Float32Values.ContainsKey(BitmapPresenceKey) ||
            !parameters.Int32Values.TryGetValue(BitmapPresenceKey, out int value)) return null;
        return value switch { 0 => false, 1 => true, _ => null };
    }

    /// <summary>The controller's selected tutorial lesson, not its timer or
    /// paragraph phase. Fresh allocation/reset writes -1; actual selection
    /// writes one of the eight lesson indices. Older missing values stay unknown.</summary>
    internal static int? ReadSelectedLessonIndex(GameState state)
    {
        if (!Bound(state) || state.NativeArmySimulation!.Parameters is not { } parameters ||
            parameters.Int32Values.GetValueOrDefault(VersionKey) != 1 ||
            parameters.Float32Values.ContainsKey(LessonIndexKey)) return null;
        return parameters.Int32Values.TryGetValue(LessonIndexKey, out int value) && value is >= -1 and <= 7 ? value : null;
    }

    private static bool Bound(GameState state) => state.NativeClock is not null &&
        state.NativeArmySimulation?.Parameters is { } parameters && !string.IsNullOrWhiteSpace(parameters.Revision) &&
        (parameters.Language is ("SC" or "TC"));
}
