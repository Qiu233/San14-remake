using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Projects this new game's explicitly adopted historical edits into
/// its detached resource document, before ordinary fresh initialization.</summary>
public static class HistoricalOfficerScenario
{
    /// <summary>The source management confirmation records whether this
    /// campaign adopted historical edits in the existing transfer flags.</summary>
    public static GameState InstallFreshFlags(GameState state, bool historicalEditsAdopted)
    {
        uint before = OwnedArmyReturnServiceInputs.ReadCurrent(state)
            ?? throw new InvalidDataException("Historical edit adoption requires the ordinary fresh transfer-flags owner.");
        uint after = historicalEditsAdopted ? before | 4u : before & ~4u;
        if (before == after) return state;
        var simulation = state.NativeArmySimulation!;
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
        {
            Int32Values = new(simulation.Parameters.Int32Values)
            { [OwnedArmyReturnServiceInputs.Flags16A8Key] = unchecked((int)after) }
        } } };
    }

    /// <summary>514EF0 visits only saved overrides. Normal historical people
    /// require a valid source Person; the bonus band uses the separately
    /// captured title admission and an actual target receiver.</summary>
    public static IReadOnlyList<HistoricalOfficerEditorRow> ReadNewGameCandidates(LoadedScenario source,
        HistoricalOfficerProfile profile, HistoricalOfficerEditorCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(catalog);
        profile.Validate();
        if (source.Metadata.FormatMarker != 0x66 || source.Snapshot.Locale != catalog.Locale)
            throw new InvalidDataException("Historical officer new-game candidates require coherent CE source and title locale.");
        var people = source.Snapshot.Characters.Where(person => person.CharacterId != 0)
            .ToDictionary(person => (int)person.CharacterId);
        return catalog.Rows.Where(Eligible).ToArray();
        bool Eligible(HistoricalOfficerEditorRow row)
        {
            if (profile.Find(row.Id) is null || !people.TryGetValue(row.Id, out var person)) return false;
            if (row.Id >= 2001) return true;
            if (person.Slot == 0) return false;
            var credit = person.CreditReference
                ?? throw new InvalidDataException("Historical officer candidate validity source is missing.");
            ushort flags = credit.RawFlagsWord
                ?? throw new InvalidDataException("Historical officer candidate validity flags are missing.");
            return (flags & 0x4000) != 0 ? credit.RawTailCode != 0 : person.StatusCode != 0;
        }
    }

    public static LoadedScenario Apply(GameResourceRepository repository, LoadedScenario source,
        HistoricalOfficerProfile profile, IReadOnlyCollection<int> adoptedHistoricalIds,
        OfficerEditorPortraitCatalog? portraitCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(adoptedHistoricalIds);
        profile.Validate();
        var adopted = adoptedHistoricalIds.ToHashSet();
        if (adopted.Count != adoptedHistoricalIds.Count || adopted.Any(id => profile.Find(id) is null))
            throw new InvalidDataException("New-game adoption must select distinct saved historical officer edits.");
        if (adopted.Count == 0) return source;
        string locale = source.Snapshot.Locale;
        if (source.Metadata.FormatMarker != 0x66 || locale is not ("SC" or "TC"))
            throw new InvalidDataException("Historical officer adoption requires a coherent CE new-game source.");
        string path = string.IsNullOrWhiteSpace(source.ScenarioSource.PhysicalPath)
            ? Path.Combine(repository.SourceDirectory, source.ScenarioSource.SourcePath) : source.ScenarioSource.PhysicalPath;
        var original = source.InitialScenarioDocument ?? S14Reader.Read(File.ReadAllBytes(path), true);
        if (original.FormatMarker != source.Metadata.FormatMarker)
            throw new InvalidDataException("Historical officer new-game document edition differs.");
        portraitCatalog ??= OfficerEditorPortraitCatalog.Load(repository, locale);
        foreach (int id in adopted)
        {
            var edit = profile.Find(id)!;
            var baseline = source.Snapshot.Characters.FirstOrDefault(person => person.CharacterId == id)
                ?? throw new InvalidDataException("historical_officers.portrait.person_source");
            if (edit.PortraitId != baseline.AppearanceCode) portraitCatalog.ValidateChoice(edit.PortraitId);
        }
        var document = original with { DecodedPayload = original.DecodedPayload.ToArray() };
        var names = S14ScenarioTables.ReadCharacterNames(document, locale);
        var identities = S14ScenarioTables.ReadCharacterIdentities(document, locale);
        var physical = identities.Where(person => person.CharacterId != 0).ToDictionary(person => (int)person.CharacterId, person => person.Slot);
        string fixedPath = $"0010_{locale}/fixdataexce.s14";
        var traits = PersonalityEditorRuntime.Compose(repository.LoadPersonalities(fixedPath),
            new PersonalityEditorLibrary(source.InitialCreatedPersonalities ?? [])).Catalog;
        var skillDefinitions = repository.LoadSkillDefinitions(fixedPath);
        var skills = new CollectionOfficerSkills(traits, skillDefinitions.Formations, skillDefinitions.Gems);
        int delta = locale == "SC" ? 56 : 0;
        foreach (var edit in profile.Overrides.Where(person => adopted.Contains(person.Id)))
        {
            if (!physical.TryGetValue(edit.Id, out int slot))
                throw new InvalidDataException($"Historical officer {edit.Id} has no source Person in this scenario.");
            Span<byte> row = document.DecodedPayload.AsSpan(names.RecordOffset + slot * names.RecordStride, names.RecordStride);
            Word(row, 148 + delta, edit.PortraitId);
            Word(row, 221 + delta, Math.Min((int)edit.DeathYear, 350));
            for (int index = 0; index < 8; index++)
            {
                Word(row, 232 + delta + index * 2, edit.LikePersonIds[index]);
                Word(row, 248 + delta + index * 2, edit.DislikePersonIds[index]);
            }
            row[166 + delta] = edit.Leadership; row[167 + delta] = edit.Strength;
            row[168 + delta] = edit.Intelligence; row[169 + delta] = edit.Politics; row[170 + delta] = edit.Charm;
            row.Slice(176 + delta, 18).Clear();
            int destination = 0;
            foreach (int id in edit.TraitIds)
                if (skills.ReadSkillPersonalityValidity(new(id)) == true && skills.ReadPersonalityByte(new(id), 0xBE) != 0)
                    Word(row, 176 + delta + destination++ * 2, id);
            uint previous = BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(194 + delta, 4));
            uint formations = edit.FormationIds.Aggregate(0u, (mask, id) => mask | (1u << id));
            BinaryPrimitives.WriteUInt32LittleEndian(row.Slice(194 + delta, 4), (previous & ~0x1FFFFFu) | formations);
            row.Slice(198 + delta, 10).Clear();
            destination = 0;
            foreach (int id in edit.TacticIds)
                if (id is > 0 and <= 200 && skillDefinitions.Formations.Tactics[id].RawBytes[48] != 0)
                    row[198 + delta + destination++] = (byte)id;
            row[274 + delta] = edit.DoctrineId;
            row[208 + delta] = edit.PolicyId;
            // The history editor displays the original total policy level,
            // while the source adoption writer stores at most six in 182.
            row[300 + delta] = (byte)Math.Min((int)edit.PolicyBaseCost, 6);
            Word(row, 305 + delta, ReadWord(row, 305 + delta) | 0x800);
        }
        var projected = RegisteredOfficerScenario.RefreshPersons(source, document);
        string decodedHash = Convert.ToHexString(SHA256.HashData(document.DecodedPayload));
        return projected with
        {
            InitialHistoricalOfficerEditsAdopted = true,
            Metadata = projected.Metadata with { DecodedSha256 = decodedHash },
            Snapshot = projected.Snapshot with { DecodedSha256 = decodedHash }
        };
    }

    private static int ReadWord(ReadOnlySpan<byte> row, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(offset, 2));
    private static void Word(Span<byte> row, int offset, int value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(offset, 2), checked((ushort)value));
}
