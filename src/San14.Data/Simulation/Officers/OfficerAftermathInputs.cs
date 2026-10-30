using System.Buffers.Binary;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OfficerAllegianceSourceRow(int PhysicalSlot, ushort PersonId, short AllegianceDisposition);

/// <summary>Loads the scenario's initial allegiance observations once. Current
/// resentment and capture compatibility knowledge are initialized separately;
/// later queries use saved observations, never the initial table as a fallback.</summary>
public sealed class OfficerAftermathInputs
{
    private readonly LoadedScenario _source;
    private readonly string _language, _ruleVersion;
    private readonly OfficerAllegianceSourceRow[] _people;
    private readonly OfficerSourceDirectory _identities = new();

    private OfficerAftermathInputs(LoadedScenario source, string language,
        string ruleVersion, OfficerAllegianceSourceRow[] people)
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

    public static OfficerAftermathInputs Load(GameResourceRepository repository,
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
        var document = source.InitialScenarioDocument ?? S14Reader.Read(File.ReadAllBytes(path), true);
        OfficerAllegianceSourceRow[] people = ReadSourceRows(document, language);
        if (people.Where((row, physical) => row.PhysicalSlot != physical ||
            row.PersonId != source.Snapshot.Characters[physical].CharacterId).Any())
            throw new InvalidDataException("Person destruction serializer identities differ from the loaded scenario.");
        return new(source, language, identity, people);
    }

    /// <summary>Decodes the identity and signed allegiance observation in a CE
    /// character record. The two locale layouts have different record offsets.</summary>
    public static OfficerAllegianceSourceRow[] ReadSourceRows(S14Document scenario, string language)
    {
        if (scenario.FormatMarker != 0x66 || language is not ("SC" or "TC"))
            throw new InvalidDataException("Person destruction serializer requires CE SC/TC.");
        S14CharacterTable table = S14ScenarioTables.ReadCharacterNames(scenario, language);
        int identityOffset = language == "SC" ? 206 : 150;
        int wordOffset = language == "SC" ? 334 : 278;
        var result = new OfficerAllegianceSourceRow[table.Names.Count];
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
        var knowledge = CampaignSourceKnowledge.From(state.ResourceState);
        if (!Bound(state) || state.Units.Count != 0 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.Officers.Values.Any(person => person.Resentment is not null || person.AllegianceDisposition is not null) ||
            knowledge.Aftermath.Count != 0)
            throw new InvalidDataException("Officer aftermath observations need an unused fresh world.");
        var officers = new Dictionary<int, OfficerState>(state.Officers);
        foreach (OfficerState person in state.Officers.Values)
        {
            int physical = _identities.ResolveOfficer(person.Id)
                ?? throw new InvalidDataException("Fresh officer aftermath has no matching resource identity.");
            officers[person.Id] = person with
            {
                Resentment = new(0, 0),
                AllegianceDisposition = new(_people[physical].AllegianceDisposition)
            };
            knowledge = knowledge.WithAftermath(new(person.Id, null, 0, 0));
        }
        return state with { Officers = officers, ResourceState = knowledge };
    }

    public OfficerAftermathObservation? ReadCurrent(GameState state, int physical)
    {
        int id = CurrentPersonId(state, physical);
        if (!Bound(state) || (uint)physical >= _people.Length ||
            !state.Officers.TryGetValue(id, out OfficerState? person) ||
            (_identities.ResolveOfficer(person.Id) ?? PostRegistrationLookups.RegisteredPersonPhysicalSlot(person.Id)) != physical) return null;
        var residue = CampaignSourceKnowledge.From(state.ResourceState).FindAftermath(person.Id);
        return person.Resentment is null && person.AllegianceDisposition is null && residue is null ? null :
            new(person.Resentment, person.AllegianceDisposition, residue);
    }

    public void BindCurrent(GameState state, SourceWorldContext current)
    {
        if (!Bound(state)) throw new InvalidDataException("Officer aftermath resource belongs to another world.");
        current.ClearOfficerAftermath();
        foreach (OfficerState person in state.Officers.Values)
        {
            int physical = current.ResolveOfficerSource(person.Id)
                ?? throw new InvalidDataException("Officer aftermath capture has no current resource identity.");
            if ((_identities.ResolveOfficer(person.Id) ?? PostRegistrationLookups.RegisteredPersonPhysicalSlot(person.Id)) != physical)
                throw new InvalidDataException("Officer aftermath capture has a different resource record.");
            current.SetOfficerAftermath(physical, ReadCurrent(state, physical));
        }
    }

    private int CurrentPersonId(GameState state, int physical) =>
        PostRegistrationLookups.RegisteredPersonIdAtPhysical(physical) is int registered && state.Officers.ContainsKey(registered)
            ? registered : (uint)physical < _people.Length ? _people[physical].PersonId : 0;

    private bool Bound(GameState state) => state.NativeArmySimulation?.Parameters is { } parameters &&
        parameters.Language == _language && parameters.RuleVersion == _ruleVersion &&
        state.ContentHashes.GetValueOrDefault(_source.ScenarioSource.ResourceUri) == _source.ScenarioSource.Sha256;
}

/// <summary>Resource bridge for the independently owned game observations and
/// source-only compatibility knowledge. Neither the game rules nor Core own
/// the unclassified capture bytes.</summary>
public sealed record OfficerAftermathObservation(OfficerResentment? Resentment,
    OfficerAllegianceDisposition? AllegianceDisposition, OfficerAftermathSourceResidue? SourceKnowledge);

public sealed partial class SourceWorldContext
{
    private readonly Dictionary<int, OfficerAftermathObservation?> _officerAftermath = [];
    internal void ClearOfficerAftermath() => _officerAftermath.Clear();
    public void SetOfficerAftermath(int physical, OfficerAftermathObservation? observation)
    {
        int id = ReadPersonIdentity(physical) ?? throw new InvalidDataException("Officer aftermath identity is unknown.");
        if (ResolveOfficerSource(id) != physical) throw new InvalidDataException("Officer aftermath receiver does not own its identity.");
        if (observation?.SourceKnowledge is { } knowledge && knowledge.OfficerId != id)
            throw new InvalidDataException("Officer aftermath source knowledge belongs to another officer.");
        _officerAftermath[id] = observation;
    }
    public OfficerAftermathObservation? ReadOfficerAftermath(int physical) =>
        ReadPersonIdentity(physical) is ushort id && ResolveOfficerSource(id) == physical
            ? _officerAftermath.GetValueOrDefault(id) : null;
    public TransferPersonReference? ReadResentmentTarget(int physical)
    {
        var observation = ReadOfficerAftermath(physical);
        if (observation?.Resentment?.TargetOfficerId is int id)
            return id <= 6000 ? ReadPersonLookupReference(id) : null;
        return observation?.SourceKnowledge?.UnmappedResentmentTarget is not null ? ReadTransferPersonTableReference(0) : null;
    }
    public int? ReadAllegianceDisposition(int physical) => ReadOfficerAftermath(physical)?.AllegianceDisposition?.Value;
    public void ClearCaptureResetKnowledge(int physical)
    {
        var old = ReadOfficerAftermath(physical) ?? throw new InvalidDataException("Officer aftermath is unknown.");
        int id = ReadPersonIdentity(physical)!.Value;
        SetOfficerAftermath(physical, old with { SourceKnowledge =
            (old.SourceKnowledge ?? new(id, null, null, null)) with { CaptureResetCode = 0 } });
    }
    public void WriteFormerForceKnowledge(int physical, int code)
    {
        var old = ReadOfficerAftermath(physical) ?? throw new InvalidDataException("Officer aftermath is unknown.");
        int id = ReadPersonIdentity(physical)!.Value;
        SetOfficerAftermath(physical, old with { SourceKnowledge =
            (old.SourceKnowledge ?? new(id, null, null, null)) with { PreviousForceCode = code } });
    }
    /// <summary>Reached unconditional death stores. Unlike a partial capture
    /// update, these writes do not consume the previous channel values.</summary>
    internal GameState ResetPersonDeathAftermath(GameState state, int physical)
    {
        int id = ReadPersonIdentity(physical) ?? throw new InvalidDataException("person-death.aftermath_identity");
        SetOfficerAftermath(physical, new(new(0, 0), new(0), new(id, null, 0, 0)));
        return PublishOfficerAftermath(state, id);
    }
    public GameState PublishOfficerAftermath(GameState state, int officerId)
    {
        int physical = ResolveOfficerSource(officerId) ?? throw new InvalidDataException("Officer aftermath publication identity is unknown.");
        var observed = ReadOfficerAftermath(physical) ?? throw new InvalidDataException("Officer aftermath publication is unknown.");
        var person = state.Officers[officerId];
        var knowledge = CampaignSourceKnowledge.From(state.ResourceState);
        knowledge = observed.SourceKnowledge is { } residue
            ? knowledge.WithAftermath(residue) : knowledge.WithoutAftermath(officerId);
        return state with { Officers = new(state.Officers) { [officerId] = person with
        {
            Resentment = observed.Resentment, AllegianceDisposition = observed.AllegianceDisposition
        } }, ResourceState = knowledge };
    }
    internal void CopyOfficerAftermathTo(SourceWorldContext target)
    {
        target._officerAftermath.Clear();
        foreach (var row in _officerAftermath) target._officerAftermath.Add(row.Key, row.Value);
    }
}
