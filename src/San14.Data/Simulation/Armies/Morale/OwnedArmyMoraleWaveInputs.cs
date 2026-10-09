using San14.Core;

namespace San14.Data;

/// <summary>Current office and rank inputs for the morale wave. The office
/// leadership bonus uses the selected encoding of its five-entry bonus vector.</summary>
public sealed class OwnedArmyMoraleWaveInputs
{
    private const string Prefix = "ce-army-morale-wave-current:";
    public const string VersionKey = Prefix + "version";
    public static string ForceRank47Key(int p) => Prefix + $"force:{p}:47";
    public static string RankACKey(int p) => Prefix + $"rank:{p}:AC";
    public static string Post34Key(int p) => Prefix + $"post:{p}:34";
    public static string Post38Key(int p) => Prefix + $"post:{p}:38";
    public static string PostVectorCountKey(int p) => Prefix + $"post:{p}:vector-count";
    public static string PostVectorEntry1Key(int p) => Prefix + $"post:{p}:vector-entry1";
    private readonly LoadedScenario _source;
    private readonly string _language, _ruleVersion;

    private OwnedArmyMoraleWaveInputs(LoadedScenario source, string language)
    {
        _source = source; _language = language;
        _ruleVersion = SimulationRuleIdentity.ForLocale(language);
        if (source.Snapshot.Locale != language || source.Snapshot.RankParameters is not
            { FormatMarker: 0x66, RankRecordStride: 166, OfficeRecordStride: 46 } table ||
            table.Ranks.Count != 13 || table.Offices.Count != 256 || source.Snapshot.Forces.Count != 52 ||
            table.Ranks.Where((r, i) => r.Id != i).Any() || table.Offices.Where((r, i) => r.Id != i).Any())
            throw new InvalidDataException("Morale wave needs the complete selected CE rank/office/source Force tables.");
        foreach (var row in table.Ranks)
        {
            byte[] raw = Convert.FromHexString(row.RawBytesHex);
            if (raw.Length != 166 || raw[156] != row.Raw156)
                throw new InvalidDataException("Morale rankAC differs from its original fixed row.");
        }
        foreach (var row in table.Offices)
        {
            byte[] raw = Convert.FromHexString(row.RawBytesHex);
            if (raw.Length != 46 || raw[39] != row.Raw39 || raw[40] != row.AbilityBonus.Leadership)
                throw new InvalidDataException("Morale Post37/38 differ from their original fixed row.");
        }
    }
    public static OwnedArmyMoraleWaveInputs Load(GameResourceRepository repository, LoadedScenario source, string language)
    { ArgumentNullException.ThrowIfNull(repository); return new(source, language); }

    public GameState InstallFresh(GameState state)
    {
        if (state.NativeArmySimulation is not { } simulation || simulation.Parameters.Language != _language ||
            simulation.Parameters.RuleVersion != _ruleVersion || state.Units.Count != 0 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.ContentHashes.GetValueOrDefault(_source.FixedDataSource.ResourceUri) != _source.FixedDataSource.Sha256 ||
            state.ContentHashes.GetValueOrDefault(_source.ScenarioSource.ResourceUri) != _source.ScenarioSource.Sha256 ||
            simulation.Parameters.Int32Values.Keys.Any(k => k.StartsWith(Prefix, StringComparison.Ordinal)))
            throw new InvalidDataException("Morale wave source inputs require their unused fresh current owner.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values)
        { [VersionKey] = 1 };
        foreach (var row in _source.Snapshot.Forces)
        {
            if (row.RankId is not int rank || rank is < 0 or > 255) throw new InvalidDataException("Unknown fresh Force rank47.");
            values.Add(ForceRank47Key(row.Slot), rank);
        }
        foreach (var row in _source.Snapshot.RankParameters!.Ranks) values.Add(RankACKey(row.Id), row.Raw156);
        foreach (var row in _source.Snapshot.RankParameters.Offices)
        {
            values.Add(Post34Key(row.Id), row.CommandLimit);
            values.Add(Post38Key(row.Id), row.AbilityBonus.Leadership);
            //Ctor reserves five but count is0; static23F480 then appends five.
            //Only entry1 is read by this source window; preserve its Int32
            //store and extract BYTE at the caller, including after current edits.
            values.Add(PostVectorCountKey(row.Id), 5);
            byte x = (byte)(row.AbilityBonus.Leadership ^ (byte)row.Id);
            values.Add(PostVectorEntry1Key(row.Id), (byte)((x << 3) | (x >> 5)));
        }
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
        { Int32Values = values, OfficeBonusEncoding = OfficeBonusEncoding.RotateByteXorOffice } } };
    }
    public void BindCurrent(GameState state, SourceWorldContext current)
    {
        for (int p = 0; p < 52; p++)
        {
            byte? rank = state.Forces.TryGetValue(p, out var force)
                ? force.RankId is int value && value is >= 0 and <= 255 ? (byte)value : null
                : ReadByte(state, ForceRank47Key(p));
            current.SetForceRankSlot47(p, rank);
        }
        current.OfficeLeadershipBonusDecoder = (post, encodedLow, rotation) =>
            ReadInt(state, VersionKey) == 1
                ? OfficeBonusCodec.Decode(state.NativeArmySimulation?.Parameters.OfficeBonusEncoding, encodedLow, rotation, post)
                : null;
        current.MoraleWavePersonPostE7Reader = physical => ReadPersonPostE7(state, current, physical);
    }
    public static bool? ReadPostNativeValidity(GameState state, int post)
        => post == 0 ? false : ReadInt(state, Post34Key(post)) is int limit && limit is >= 0 and <= 65535 ? limit != 0 : null;
    public static byte? ReadPost37(GameState state, int post) =>
        state.NativeArmySimulation?.Parameters.Int32Values.GetValueOrDefault(OwnedArmyGovernorComparatorInputs.VersionKey) == 1 &&
        state.NativeArmySimulation.Parameters.Int32Values.TryGetValue(OwnedArmyGovernorComparatorInputs.OfficeOrder37Key(post), out int v) &&
        v is >= 0 and <= 255 ? (byte)v : null;
    public static byte? ReadPost38(GameState state, SourceWorldContext current, int post)
    {
        if (ReadInt(state, PostVectorCountKey(post)) is not int count) return null;
        if (count != 5) return ReadByte(state, Post38Key(post));
        if (ReadInt(state, PostVectorEntry1Key(post)) is not int encoded) return null;
        return current.OfficeLeadershipBonusDecoder?.Invoke(post, unchecked((byte)encoded), 5);
    }
    public static byte? ReadRankAC(GameState state, int rank) => ReadByte(state, RankACKey(rank is >= 0 and <= 12 ? rank : 0));
    public static byte? ReadPersonPostE7(GameState state, SourceWorldContext current, int physical)
    {
        if (current.ReadPersonIdentity(physical) is not ushort id || !state.Officers.TryGetValue(id, out var person) ||
            person.NativeAbilityRuntime?.PhysicalSlot != physical) return null;
        return person.OfficeId is int office ? office is >= 0 and <= 255 ? (byte)office : null : person.NativeAbilityRuntime.Office11F;
    }
    private static int? ReadInt(GameState state, string key) => state.NativeArmySimulation?.Parameters.Int32Values is { } values &&
        values.GetValueOrDefault(VersionKey) == 1 && values.TryGetValue(key, out int v) ? v : null;
    private static byte? ReadByte(GameState state, string key) => ReadInt(state, key) is int v && v is >= 0 and <= 255 ? (byte)v : null;
}

public sealed partial class SourceWorldContext
{
    private readonly Dictionary<int, byte?> _moraleWavePersonPostE7 = [];
    public Func<int, byte, int, byte?>? OfficeLeadershipBonusDecoder { get; set; }
    public Func<int, byte?>? MoraleWavePersonPostE7Reader { get; set; }
    public byte? ReadMoraleWavePersonPostE7(int physical)
    { Check(physical, 1851, "current Person PostE7"); return _moraleWavePersonPostE7.TryGetValue(physical, out byte? value) ? value : MoraleWavePersonPostE7Reader?.Invoke(physical); }
    public void SetMoraleWavePersonPostE7(int physical, byte? value)
    { Check(physical, 1851, "current Person PostE7"); _moraleWavePersonPostE7[physical] = value; }
    public byte? ReadMoraleWaveForceRank47(int physical)
    { Check(physical, 52, "morale wave Force47"); return _forceRanks47.GetValueOrDefault(physical); }
    public ushort? ReadMoraleWaveRuler22EAC0(ForceReference force)
    {
        int? physical = ReadForcePhysicalSlot(force);
        return physical == 0 ? (ushort)5014 : physical.HasValue ? ReadForceRecord(physical.Value)?.RawRuler10 : null;
    }
}
