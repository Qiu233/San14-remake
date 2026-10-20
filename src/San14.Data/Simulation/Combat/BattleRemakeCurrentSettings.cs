using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Explicit owned current CEnvironment settings. The complete static
/// CE block is followed by this game's ordinary new-game choices. These saved
/// fields are independent of tutorial control and never refilled by a query.</summary>
public static class BattleRemakeCurrentSettings
{
    public const string ProfileId = "san14:ce-owned-battle-environment-settings-20261002";
    private const string Prefix = "ce-battle-current-settings:";
    public const string VersionKey = Prefix + "profile-version";
    private const string FieldCountKey = Prefix + "source-field-count";
    public static string FieldKey(int nativeMemberOffset) => $"{Prefix}field:{nativeMemberOffset:X3}";
    private static string WidthKey(int nativeMemberOffset) => $"{Prefix}width:{nativeMemberOffset:X3}";
    //Actual61-entry SC3EC0C4 / TC3EBA84 dispatch. Values include the
    //800C80/830350 primary-pointer +8, so they label current CEnvironment.
    private static readonly int[] AdvancedMembers =
    [404, 428, 524, 548, 596, 620, 644, 668, 740, 788, 812, 836, 860, 908, 956, 980,
     1004, 1076, 1100, 1124, 1148, 1196, 1244, 1268, 1316, 1340, 1364, 1388, 1412,
     1436, 1460, 1484, 1508, 1532, 1556, 1580, 1604, 1628, 1652, 1676, 1700, 1724,
     1748, 308, 332, 356, 380, 452, 476, 500, 572, 692, 716, 764, 884, 932, 1028,
     1052, 1172, 1220, 1292];

    /// <summary>New allocation, complete CE static projection, then explicit
    /// ordinary settings confirmation. No final original first-player snapshot
    /// is claimed: unmodeled persistent/UI values are not read into this owner.</summary>
    public static GameState InstallFresh(GameState state, LoadedScenario source)
    {
        if (!Bound(state) || state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.NewGameOptions is not { } options || state.DifficultyId != options.DifficultyId ||
            source.Metadata.FormatMarker != 0x66 || source.Snapshot.Locale != state.NativeArmySimulation!.Parameters.Language ||
            source.Snapshot.WorldParameters?.SourceBoundary is not { Settings: { } settings } boundary ||
            settings.Offset != boundary.SettingsOffset || settings.RawBytes.Length != S14ScenarioSettingsReader.SerializedBytes ||
            settings.Fields.Count != S14ScenarioSettingsReader.FieldCount ||
            string.IsNullOrWhiteSpace(source.ScenarioSource.ResourceUri) ||
            state.ContentHashes.GetValueOrDefault(source.ScenarioSource.ResourceUri) != source.ScenarioSource.Sha256 ||
            state.NativeArmySimulation.Parameters.Int32Values.Keys.Concat(state.NativeArmySimulation.Parameters.Float32Values.Keys)
                .Any(key => key.StartsWith(Prefix, StringComparison.Ordinal)))
            throw new InvalidDataException("Fresh current settings require a complete new ordinary CE source owner.");

        int difficulty = options.DifficultyId switch
        { "beginner" => 0, "intermediate" => 1, "advanced" => 2, "super" => 3, "top" => 4,
          _ => throw new InvalidDataException("Current ordinary difficulty has no proved native setting identity.") };
        NativeArmySimulationState simulation = state.NativeArmySimulation!;
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values)
        { [VersionKey] = 1, [FieldCountKey] = S14ScenarioSettingsReader.FieldCount };
        foreach (S14ScenarioSettingsField field in settings.Fields)
        {
            values.Add(FieldKey(field.NativeMemberOffset), field.Width == 1 ? (byte)field.RawValue : unchecked((int)field.RawValue));
            values.Add(WidthKey(field.NativeMemberOffset), field.Width);
        }
        //The ordinary settings-page entry explicitly sets E58=1, separately
        //from the source block and the primary/secondary copy. Selected options
        //are restored after loading; their source coincidence is not a default.
        values[FieldKey(0xE58)] = 1;
        values[FieldKey(0x28)] = difficulty;
        values[FieldKey(0x120)] = options.InitialCreditEnabled ? 0 : 1;
        values[FieldKey(0x128)] = options.CapturedOfficersEnabled ? 0 : 1;
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    /// <summary>SC3ECCB0/TC3EC670 reads this saved E58 directly.</summary>
    public static int? ReadSelectorE58(GameState state) => ReadInt32(state, 0xE58);
    /// <summary>SC3ECD00/TC3EC6C0 tests E58==0, with no tutorial query.</summary>
    public static bool? ReadAdvancedSettings(GameState state) => ReadSelectorE58(state) is int selector ? selector == 0 : null;
    /// <summary>SC3EB960/TC3EB340 reads this saved EC directly.</summary>
    public static int? ReadMoraleModeEC(GameState state) => ReadInt32(state, 0xEC);
    /// <summary>SC3EB510/TC22DAB0 reads this saved D8 directly. The TC compiler
    /// reuses an identical leaf at a different class's address; caller binding
    /// still passes this CEnvironment, not an Army or Base object.</summary>
    public static int? ReadNeighborModeD8(GameState state) => ReadInt32(state, 0xD8);

    /// <summary>TC3EB280 selects current primary80 or advanced164.</summary>
    public static int? ReadWoundedRecoveryMode(GameState state) => ReadSelectorE58(state) is int selector
        ? ReadInt32(state, selector == 0 ? 0x164 : 0x80) : null;

    /// <summary>TC3EB360 selects primary C0 or advanced23C. The center
    /// occupation writer queues encirclement work only when this value is zero.</summary>
    public static bool? ReadEncirclementEnabled(GameState state) => ReadSelectorE58(state) is int selector &&
        ReadInt32(state, selector == 0 ? 0x23C : 0xC0) is int mode ? mode == 0 : null;

    /// <summary>SC3EBE00/TC3EB7C0 over the current primary+8. The native
    /// switch order is independent of serialized column order. The owned
    /// static block establishes exactly six rows; unsupported rows stay null.</summary>
    public static int? ReadAdvancedParameter(GameState state, int nativeIndex, int row = 0) =>
        nativeIndex is >= 0 and < 61 && row is >= 0 and < 6
            ? ReadInt32(state, AdvancedMembers[nativeIndex] + row * 4) : null;

    /// <summary>SC2A9489..2A9511's current coefficient selection. Signed
    /// i32 multiply/truncation matches the advanced index8 branch.367EC0 is
    /// a read-only diagnostic whose release stub returns after stack writes.</summary>
    public static int? ReadNeighborCoefficient(GameState state)
    {
        int? selector = ReadSelectorE58(state);
        if (selector is null) return null;
        if (selector == 0)
        {
            int? percent = ReadAdvancedParameter(state, 8), normal = Parameter(SimulationParameterKeys.BattleAttributes.NormalFormationPercentScale);
            return percent is null || normal is null ? null : unchecked(percent.Value * normal.Value) / 100;
        }
        int? mode = ReadNeighborModeD8(state);
        return mode is null ? null : Parameter(mode == 0 ? SimulationParameterKeys.BattleAttributes.AlternateFormationPercentScale : SimulationParameterKeys.BattleAttributes.NormalFormationPercentScale);
        int? Parameter(string key) => state.NativeArmySimulation!.Parameters.Int32Values
            .TryGetValue(key, out int value) ? value : null;
    }

    /// <summary>SC2A923F..2A933F/TC2A93EF..2A94EF's ordinary branch. Any
    /// nonzero E58 reaches EC; EC1 selects alternate globals75/175, all other
    /// i32 values retain ordinary200/300. E58==0 reaches an independent dynamic
    /// advanced-percent formula which cannot be represented by this bool.</summary>
    public static bool? ReadAlternateMoraleGlobals(GameState state)
    {
        int? selector = ReadSelectorE58(state);
        if (selector is null || selector == 0) return null;
        return ReadMoraleModeEC(state) is int mode ? mode == 1 : null;
    }
    public static bool? ReadAlternateMoraleGlobals(GameState state, UnitState army) =>
        state.Units.GetValueOrDefault(army.Id) == army &&
        army.NativeArmyOrigin is { } origin && origin.Language == state.NativeArmySimulation?.Parameters.Language &&
        origin.RuleVersion == state.NativeArmySimulation?.Parameters.RuleVersion
            ? ReadAlternateMoraleGlobals(state) : null;

    /// <summary>Raw current owner field; width4 preserves every signed i32.</summary>
    public static int? ReadInt32(GameState state, int nativeMemberOffset) =>
        Values(state) is { } values && values.GetValueOrDefault(WidthKey(nativeMemberOffset)) == 4 &&
        values.TryGetValue(FieldKey(nativeMemberOffset), out int value) ? value : null;
    public static byte? ReadByte(GameState state, int nativeMemberOffset) =>
        Values(state) is { } values && values.GetValueOrDefault(WidthKey(nativeMemberOffset)) == 1 &&
        values.TryGetValue(FieldKey(nativeMemberOffset), out int value) && value is >= 0 and <= 255 ? (byte)value : null;

    private static Dictionary<string, int>? Values(GameState state)
    {
        if (!Bound(state)) return null;
        Dictionary<string, int> values = state.NativeArmySimulation!.Parameters.Int32Values;
        if (values.GetValueOrDefault(VersionKey) != 1 ||
            values.GetValueOrDefault(FieldCountKey) != S14ScenarioSettingsReader.FieldCount) return null;
        return values;
    }
    private static bool Bound(GameState state) => state.NativeClock is not null &&
        state.NativeArmySimulation?.Parameters is { } parameters && !string.IsNullOrWhiteSpace(parameters.Revision) &&
        parameters.Language is ("SC" or "TC");
}
