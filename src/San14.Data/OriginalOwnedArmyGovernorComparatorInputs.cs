using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalArmyGovernorPersonSource(int PhysicalSlot, ushort PersonId,
    ushort Word166, byte RawE8);

/// <summary>Inputs of TC254440. The native Person160 priority flag starts in
/// 2277B0's qword160 zero; static2391C0 does not overwrite it. Word166 is in
/// the static stream, and E8 is its raw loyalty byte (which may be250/255).
/// Once installed, mutable inputs are read from this program's saved owners.
/// CPostData is the complete256x58 table at World784F8; its3D/37 bytes are
/// fixed-row45/39, and22E0D0 returns its physical ordinal.</summary>
public sealed class OriginalOwnedArmyGovernorComparatorInputs
{
    public const string Revision = "san14:ce-owned-army-governor-comparator-20261007";
    private const string Prefix = "ce-army-governor-current:";
    public static string VersionKey => Prefix + "version";
    public static string PersonFlags160Key(int physical) => Prefix + $"person:{physical}:160";
    public static string PersonWord166Key(int physical) => Prefix + $"person:{physical}:166";
    public static string PersonRawE8Key(int physical) => Prefix + $"person:{physical}:E8";
    public static string OfficeFlags3DKey(int physical) => Prefix + $"office:{physical}:3D";
    public static string OfficeOrder37Key(int physical) => Prefix + $"office:{physical}:37";
    public static string OfficeGuard36Key(int physical) => Prefix + $"office:{physical}:36";

    private readonly LoadedScenario _source;
    private readonly string _language, _executableHash;
    private readonly OriginalArmyGovernorPersonSource[] _people;
    private readonly S14OfficeDefinition[] _offices;

    private OriginalOwnedArmyGovernorComparatorInputs(LoadedScenario source, string language,
        string executableHash, OriginalArmyGovernorPersonSource[] people, S14OfficeDefinition[] offices)
        => (_source, _language, _executableHash, _people, _offices) =
            (source, language, executableHash, people, offices);

    public static OriginalOwnedArmyGovernorComparatorInputs Load(OriginalGameRepository repository,
        LoadedScenario source, string language)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(source);
        string expected = language switch
        {
            "SC" => OriginalCeHexWorldCoordinates.VerifiedScExecutableSha256,
            "TC" => OriginalCeHexWorldCoordinates.VerifiedTcExecutableSha256,
            _ => throw new InvalidDataException("Governor comparator requires CE SC/TC.")
        };
        if (source.Metadata.FormatMarker != 0x66 || source.Snapshot.Locale != language ||
            source.Snapshot.RankParameters is not { FormatMarker: 0x66, OfficeRecordStride: 46 } ranks ||
            ranks.Offices.Count != 256 || ranks.Offices.Where((x, i) => x.Id != i).Any())
            throw new InvalidDataException("Governor comparator requires complete coherent CE source tables.");
        // Read and verify the scenario once at source load, including overrides.
        string path = string.IsNullOrWhiteSpace(source.ScenarioSource.PhysicalPath)
            ? Path.Combine(repository.SourceDirectory, source.ScenarioSource.SourcePath)
            : source.ScenarioSource.PhysicalPath;
        byte[] bytes = File.ReadAllBytes(path);
        if (Convert.ToHexString(SHA256.HashData(bytes)) != source.ScenarioSource.Sha256)
            throw new InvalidDataException("Governor scenario changed while its source inputs were loaded.");
        var people = ReadSourceRows(S14Reader.Read(bytes, true), language);
        foreach (var office in ranks.Offices)
        {
            byte[] row = Convert.FromHexString(office.RawBytesHex);
            if (row.Length != 46 || row[39] != office.Raw39 || row[45] != office.Raw45)
                throw new InvalidDataException("Governor CPostData source fields differ from their retained row.");
        }
        return new(source, language, expected, people, ranks.Offices.ToArray());
    }

    public static OriginalArmyGovernorPersonSource[] ReadSourceRows(S14Document scenario, string language)
    {
        if (scenario.FormatMarker != 0x66 || language is not ("SC" or "TC"))
            throw new InvalidDataException("Governor Person serializer requires CE SC/TC.");
        var table = S14ScenarioTables.ReadCharacterNames(scenario, language);
        int identity = language == "SC" ? 206 : 150;
        int word166 = language == "SC" ? 269 : 213;
        int loyalty = language == "SC" ? 219 : 163;
        var result = new OriginalArmyGovernorPersonSource[table.Names.Count];
        for (int physical = 0; physical < result.Length; physical++)
        {
            var row = scenario.DecodedPayload.AsSpan(table.RecordOffset + physical * table.RecordStride,
                table.RecordStride);
            result[physical] = new(physical, BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(identity, 2)),
                BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(word166, 2)), row[loyalty]);
        }
        return result;
    }

    public GameState InstallFresh(GameState state)
    {
        if (!Bound(state) || state.Units.Count != 0 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.NativeArmySimulation!.Parameters.Int32Values.Keys
                .Concat(state.NativeArmySimulation.Parameters.Float32Values.Keys)
                .Any(key => key.StartsWith(Prefix, StringComparison.Ordinal)))
            throw new InvalidDataException("Governor inputs require their unused fresh saved owner.");
        //32F020 has a special two-Person startup writer in mode48==19.
        //Its independent scenario adapter is outside this ordinary producer.
        if (_source.Snapshot.WorldParameters?.RawField48 == 0x13)
            throw new InvalidDataException("Governor inputs need the special mode48==19 startup Person writer.");
        var simulation = state.NativeArmySimulation!;
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        foreach (var person in _people)
        {
            values.Add(PersonFlags160Key(person.PhysicalSlot), 0);
            values.Add(PersonWord166Key(person.PhysicalSlot), person.Word166);
            values.Add(PersonRawE8Key(person.PhysicalSlot), person.RawE8);
        }
        foreach (var office in _offices)
        {
            values.Add(OfficeFlags3DKey(office.Id), office.Raw45);
            values.Add(OfficeOrder37Key(office.Id), office.Raw39);
            values.Add(OfficeGuard36Key(office.Id), office.Raw38);
        }
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    public OriginalArmyReturnGovernorComparatorFields? ReadCurrentComparatorFields(GameState state, int physical)
    {
        if (!Bound(state) || ReadInt(state, VersionKey) != 1 || (uint)physical >= _people.Length) return null;
        var source = _people[physical];
        if (!state.Officers.TryGetValue(source.PersonId, out var person) ||
            person.NativeAbilityRuntime is not { } ability || ability.PhysicalSlot != physical) return null;
        byte? currentOffice = person.OfficeId is int appointed
            ? appointed is >= 0 and <= 255 ? (byte)appointed : null : ability.Office11F;
        if (currentOffice is not byte office) return null;
        //Core retains ordinary0..100 loyalty and its actual personnel writes.
        //The saved raw byte retains special250/255 source values that Core does
        //not display as an ordinary loyalty score. No implemented command
        //silently changes a missing Core loyalty into a numeric value.
        byte? loyalty = person.Loyalty is int current
            ? current is >= 0 and <= 255 ? (byte)current : null
            : ReadByte(state, PersonRawE8Key(physical));
        byte? leadership = person.BaseAbilities?.Leadership is int value && value is >= 0 and <= 255
            ? (byte)value : null;
        return new(ReadByte(state, PersonFlags160Key(physical)), office,
            ReadByte(state, OfficeFlags3DKey(office)), ReadByte(state, OfficeOrder37Key(office)), office,
            ReadWord(state, PersonWord166Key(physical)), loyalty, leadership);
    }

    private bool Bound(GameState state) => state.NativeArmySimulation?.Parameters is { } p &&
        p.Language == _language && p.ExecutableSha256 == _executableHash &&
        state.ContentHashes.GetValueOrDefault(_source.ScenarioSource.ResourceUri) == _source.ScenarioSource.Sha256 &&
        state.ContentHashes.GetValueOrDefault(_source.FixedDataSource.ResourceUri) == _source.FixedDataSource.Sha256;
    private static int? ReadInt(GameState state, string key) => state.NativeArmySimulation?.Parameters is { } p &&
        !p.Float32Values.ContainsKey(key) && p.Int32Values.TryGetValue(key, out int value) ? value : null;
    private static byte? ReadByte(GameState state, string key) => ReadInt(state, key) is int value &&
        value is >= 0 and <= 255 ? (byte)value : null;
    private static ushort? ReadWord(GameState state, string key) => ReadInt(state, key) is int value &&
        value is >= 0 and <= 65535 ? (ushort)value : null;
}
