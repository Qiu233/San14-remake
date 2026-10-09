using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The proved CE fresh five-XP writer and growth notification
/// constructor. Current saved XP is never rebuilt by a query or reload.
/// The ordinary growth modifiers/application remain separate producers.</summary>
public sealed partial class PersonGrowthSource
{
    public const string ProfileId = "san14:ce-owned-person-growth-20261003";
    private readonly SourceProvenance _source;
    private readonly ExperienceGrowthCatalog _growth;

    public PersonGrowthSource(LoadedExperienceData loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        if (loaded.Document.Tables.Count != 4 ||
            loaded.Source.SourcePath.Replace('\\', '/') != "0000/experience_data_exce.bin" ||
            string.IsNullOrWhiteSpace(loaded.Source.ResourceUri) ||
            loaded.Source.Sha256 is not { Length: 64 } hash || !hash.All(Uri.IsHexDigit) ||
            loaded.Source.Bytes is < 1 or > int.MaxValue)
            throw new InvalidDataException("Current CE growth requires its actual ExCe experience source.");
        byte[] document = new byte[(int)loaded.Source.Bytes];
        BinaryPrimitives.WriteInt32LittleEndian(document, loaded.Document.Tables.Count);
        int previous = 4 + loaded.Document.Tables.Count * 8;
        foreach (ExperienceDataTable table in loaded.Document.Tables)
        {
            if (table.Index is < 0 or > 3 || table.Offset < previous ||
                table.Offset > document.Length - table.Length ||
                table.LeadingPadding.Length != table.Offset - previous)
                throw new InvalidDataException("Growth source document is not a complete experience container.");
            BinaryPrimitives.WriteInt32LittleEndian(document.AsSpan(4 + table.Index * 8), table.Offset);
            BinaryPrimitives.WriteInt32LittleEndian(document.AsSpan(8 + table.Index * 8), table.Length);
            table.LeadingPadding.CopyTo(document, previous);
            BinaryPrimitives.WriteInt32LittleEndian(document.AsSpan(table.Offset), table.Records.Count);
            foreach (ExperienceDataRecord row in table.Records)
            {
                if (row.RawBytes.Length != table.RecordStride || row.Index is < 0 || row.Index >= table.Records.Count)
                    throw new InvalidDataException("Growth source document has an invalid experience record.");
                row.RawBytes.CopyTo(document, table.Offset + 4 + row.Index * table.RecordStride);
            }
            previous = table.Offset + table.Length;
        }
        if (previous != document.Length || Convert.ToHexString(SHA256.HashData(document)) != hash)
            throw new InvalidDataException("Growth source projection does not match its actual file fingerprint.");
        //Reparse the reconstructed actual bytes before retaining independent
        //table rows. A caller's mutable projected catalog is not current truth.
        _growth = ExperienceGrowthReader.Read(ExperienceDataReader.Read(document));
        if (_growth.RawTableSha256 != loaded.Growth.RawTableSha256)
            throw new InvalidDataException("Growth table projection differs from its source container.");
        _source = loaded.Source;
    }

    public GameState InstallFresh(GameState state, LoadedScenario scenario,
        SourceWorldContext current)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(current);
        if (state.NativeArmySimulation?.Parameters is not { } parameters ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            scenario.Metadata.FormatMarker != 0x66 || scenario.Snapshot.Locale != parameters.Language ||
            state.ContentHashes.GetValueOrDefault(scenario.ScenarioSource.ResourceUri) != scenario.ScenarioSource.Sha256 ||
            state.Officers.Values.Any(person => person.NativeGrowthRuntime is not null) ||
            state.ContentHashes.TryGetValue(_source.ResourceUri, out string? existing) && existing != _source.Sha256)
            throw new InvalidDataException("Fresh current growth cannot replace a saved or incomplete owner.");
        var officers = new Dictionary<int, OfficerState>(state.Officers);
        foreach (OfficerState officer in state.Officers.Values)
        {
            if (officer.NativeAbilityRuntime is not { } owner ||
                owner.Revision != TacticRemakeCurrentAbilities.ProfileId ||
                owner.Language != parameters.Language || owner.RuleVersion != parameters.RuleVersion ||
                owner.SourceScenarioUri != scenario.ScenarioSource.ResourceUri ||
                owner.SourceScenarioSha256 != scenario.ScenarioSource.Sha256 ||
                owner.PhysicalSlot is < 1 or > 1850 ||
                current.ReadPersonLookupReference(officer.Id)?.PhysicalSlot != owner.PhysicalSlot ||
                current.ReadPersonIdentity(owner.PhysicalSlot) != officer.Id ||
                current.ReadPersonNativeValidity(owner.PhysicalSlot) is not bool valid ||
                OfficerServiceStatusSourceAdapter.Encode(officer.ServiceStatus) is not byte status || officer.BaseAbilities is not { } basic)
                throw new InvalidDataException("Fresh current growth has no complete current Person identity.");
            uint[]? xp = null;
            if (valid && status != 9)
            {
                int[] raw = [basic.Leadership, basic.Strength, basic.Intelligence, basic.Politics, basic.Charm];
                if (raw.Any(value => value is < 0 or > 255))
                    throw new InvalidDataException("Fresh growth must consume actual current base bytes.");
                xp = Enumerable.Range(0, 5).Select(index => ReadColumn(raw[index], index)).ToArray();
            }
            //The real map constructor has no entries. Only this fresh source
            //allocation establishes the known empty per-Person vector. The
            //separate complete fresh credit consumer owns pending merit later.
            officers[officer.Id] = officer with { NativeGrowthRuntime = new(ProfileId,
                _source.ResourceUri, _source.Sha256, xp, []) };
        }
        var hashes = new Dictionary<string, string>(state.ContentHashes) { [_source.ResourceUri] = _source.Sha256 };
        GameState result = state with { Officers = officers, ContentHashes = hashes };
        StateValidator.Validate(result); return result;
    }

    public uint ReadColumn(int nativeBaseByte, int abilityIndex)
    {
        if (nativeBaseByte is < 0 or > 255 || abilityIndex is < 0 or > 4)
            throw new InvalidDataException("Growth lookup is outside the proved native byte/five-column window.");
        ExperienceGrowthRecord row = _growth.Records[nativeBaseByte < 101 ? nativeBaseByte : 0];
        return abilityIndex switch { 0 => row.Leadership, 1 => row.Strength,
            2 => row.Intelligence, 3 => row.Politics, _ => row.Charm };
    }
}
