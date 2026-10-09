using System.Buffers.Binary;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record PersonDestructionFreshSource(int PhysicalSlot, ushort PersonId, short Word1A4);

/// <summary>2277B0 constructor auxiliaries followed by2391C0's actual modern
/// CE static Person stream. Loading reads the scenario once; current queries
/// bind the saved mutable owner, never the initial rows.</summary>
public sealed class OwnedPersonDestructionRuntimeInputs
{
    public const string Revision = "san14:ce-owned-person-destruction-runtime-20261007";
    private readonly LoadedScenario _source;
    private readonly string _language, _ruleVersion;
    private readonly PersonDestructionFreshSource[] _people;
    private readonly OfficerSourceDirectory _identities = new();

    private OwnedPersonDestructionRuntimeInputs(LoadedScenario source, string language,
        string ruleVersion, PersonDestructionFreshSource[] people)
        {
        (_source, _language, _ruleVersion, _people) = (source, language, ruleVersion, people);
        _identities.ClearLogicalReferences();
        foreach (var row in people)
        {
            _identities.SetRecordIdentity(row.PhysicalSlot, row.PersonId);
            if (row.PhysicalSlot == 0 || row.PersonId != 0)
                _identities.SetLogicalReference(row.PersonId, row.PhysicalSlot);
        }
    }

    public static OwnedPersonDestructionRuntimeInputs Load(GameResourceRepository repository,
        LoadedScenario source, string language)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(source);
        string identity = SimulationRuleIdentity.ForLocale(language);
        if (source.Metadata.FormatMarker != 0x66 || source.Snapshot.Locale != language ||
            source.Snapshot.Characters.Count != 1851)
            throw new InvalidDataException("Person destruction inputs need the complete CE Person table.");
        string path = string.IsNullOrWhiteSpace(source.ScenarioSource.PhysicalPath)
            ? Path.Combine(repository.SourceDirectory, source.ScenarioSource.SourcePath)
            : source.ScenarioSource.PhysicalPath;
        byte[] bytes = File.ReadAllBytes(path);
        PersonDestructionFreshSource[] people = ReadSourceRows(S14Reader.Read(bytes, true), language);
        if (people.Where((row, physical) => row.PhysicalSlot != physical ||
            row.PersonId != source.Snapshot.Characters[physical].CharacterId).Any())
            throw new InvalidDataException("Person destruction serializer identities differ from the loaded scenario.");
        return new(source, language, identity, people);
    }

    /// <summary>2391C0 serializes twelve words beginning atTC16A. Modern CE
    /// static row begins that window atTC276/SC332; the second signed word is
    /// TC16C/SC1A4. Runtime162/164/165/168 are absent from this static branch.</summary>
    public static PersonDestructionFreshSource[] ReadSourceRows(S14Document scenario, string language)
    {
        if (scenario.FormatMarker != 0x66 || language is not ("SC" or "TC"))
            throw new InvalidDataException("Person destruction serializer requires CE SC/TC.");
        S14CharacterTable table = S14ScenarioTables.ReadCharacterNames(scenario, language);
        int identityOffset = language == "SC" ? 206 : 150;
        int wordOffset = language == "SC" ? 334 : 278;
        var result = new PersonDestructionFreshSource[table.Names.Count];
        for (int physical = 0; physical < result.Length; physical++)
        {
            ReadOnlySpan<byte> row = scenario.DecodedPayload.AsSpan(
                table.RecordOffset + physical * table.RecordStride, table.RecordStride);
            result[physical] = new(physical, BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(identityOffset, 2)),
                BinaryPrimitives.ReadInt16LittleEndian(row.Slice(wordOffset, 2)));
        }
        return result;
    }

    public GameState InstallFresh(GameState state)
    {
        if (!Bound(state) || state.Units.Count != 0 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.Officers.Values.Any(person => person.NativeDestructionRuntime is not null))
            throw new InvalidDataException("Person destruction auxiliaries can only be installed on their unused fresh owner.");
        var officers = new Dictionary<int, OfficerState>(state.Officers);
        foreach (OfficerState person in state.Officers.Values)
        {
            int physical = _identities.ResolveOfficer(person.Id)
                ?? throw new InvalidDataException("Fresh destruction auxiliaries have no matching officer source record.");
            PersonDestructionFreshSource row = _people[physical];
            //Actual2277B0 qword160 zeroes162/164/165; word168 zeroes168.
            //2391C0 static data overwrites16C from its full source word window.
            officers[person.Id] = person with { NativeDestructionRuntime = new(Revision, 0, 0, 0, 0, row.Word1A4) };
        }
        GameState result = state with { Officers = officers };
        NativePersonDestructionRuntimeStateValidation.Validate(result); return result;
    }

    public NativePersonDestructionRuntimeState? ReadCurrent(GameState state, int physical)
    {
        if (!Bound(state) || (uint)physical >= _people.Length ||
            !state.Officers.TryGetValue(_people[physical].PersonId, out OfficerState? person) ||
            _identities.ResolveOfficer(person.Id) != physical ||
            person.NativeDestructionRuntime is not { Revision: Revision } current)
            return null;
        return current;
    }

    /// <summary>A current numeric capture for259360 and its helpers. Other
    /// physical rows stay absent/unknown. This operation neither restores
    /// constructor values nor guesses captivity from the Core Force.</summary>
    public void BindCurrent(GameState state, SourceWorldContext current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (!Bound(state)) throw new InvalidDataException("Person destruction inputs belong to another current source world.");
        NativePersonDestructionRuntimeStateValidation.Validate(state);
        current.ClearPersonDestructionRuntimeFields();
        foreach (OfficerState person in state.Officers.Values)
        {
            if (current.ResolveOfficerSource(person.Id) is not int physical ||
                _identities.ResolveOfficer(person.Id) != physical)
                throw new InvalidDataException("Person destruction capture differs from its current source record.");
            current.SetPersonDestructionRuntimeFields(physical, ReadCurrent(state, physical));
        }
    }

    private bool Bound(GameState state) => state.NativeArmySimulation?.Parameters is { } parameters &&
        parameters.Language == _language && parameters.RuleVersion == _ruleVersion &&
        state.ContentHashes.GetValueOrDefault(_source.ScenarioSource.ResourceUri) == _source.ScenarioSource.Sha256;
}

public sealed partial class SourceWorldContext
{
    private readonly Dictionary<int, NativePersonDestructionRuntimeState?> _personDestructionRuntimeFields = [];
    internal void ClearPersonDestructionRuntimeFields() => _personDestructionRuntimeFields.Clear();
    public void SetPersonDestructionRuntimeFields(int physical, NativePersonDestructionRuntimeState? current)
    {
        Check(physical, 1851, "Person destruction receiver");
        _personDestructionRuntimeFields[physical] = current;
    }
    public NativePersonDestructionRuntimeState? ReadPersonDestructionRuntimeFields(int physical)
    {
        Check(physical, 1851, "Person destruction receiver");
        return _personDestructionRuntimeFields.GetValueOrDefault(physical);
    }
    public ushort? ReadPersonDestructionWord19A(int physical) => ReadPersonDestructionRuntimeFields(physical)?.Word19A;
    public byte? ReadPersonDestructionByte19C(int physical) => ReadPersonDestructionRuntimeFields(physical)?.Byte19C;
    public byte? ReadPersonDestructionByte19D(int physical) => ReadPersonDestructionRuntimeFields(physical)?.Byte19D;
    public byte? ReadPersonDestructionByte1A0(int physical) => ReadPersonDestructionRuntimeFields(physical)?.Byte1A0;
    public short? ReadPersonDestructionWord1A4(int physical) => ReadPersonDestructionRuntimeFields(physical)?.Word1A4;
}
