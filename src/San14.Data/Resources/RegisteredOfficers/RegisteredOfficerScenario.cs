using System.Buffers.Binary;
using System.Text;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Applies the selected editor library at the new-game import
/// boundary. The installed resource and an existing campaign are untouched.</summary>
public static class RegisteredOfficerScenario
{
    public static LoadedScenario Apply(GameResourceRepository repository, LoadedScenario source,
        RegisteredOfficerLibrary library, RegisteredOfficerNewGameSelection selection, uint initializationSeed,
        OfficerEditorPortraitCatalog? portraitCatalog = null)
    {
        library.Validate(); selection.Validate(library);
        if (library.Officers.Length == 0) return source;
        if (source.Metadata.FormatMarker != 0x66 || source.Snapshot.Locale is not ("TC" or "SC"))
            throw new InvalidDataException("Registered officers require a CE new-game source.");
        string path = string.IsNullOrWhiteSpace(source.ScenarioSource.PhysicalPath)
            ? Path.Combine(repository.SourceDirectory, source.ScenarioSource.SourcePath) : source.ScenarioSource.PhysicalPath;
        S14Document original = source.InitialScenarioDocument ?? S14Reader.Read(File.ReadAllBytes(path), true);
        var document = original with { DecodedPayload = original.DecodedPayload.ToArray() };
        string locale = source.Snapshot.Locale;
        var table = S14ScenarioTables.ReadCharacterNames(document, locale);
        int delta = locale == "SC" ? 56 : 0;
        bool timeless = source.Snapshot.WorldParameters?.RawModeValue == 2;
        int year = source.Metadata.StartYear ?? throw new InvalidDataException("Registered-officer start year is missing.");
        var fixedPath = $"0010_{locale}/fixdataexce.s14";
        var traits = PersonalityEditorRuntime.Compose(repository.LoadPersonalities(fixedPath),
            new PersonalityEditorLibrary(source.InitialCreatedPersonalities ?? [])).Catalog;
        var skills = repository.LoadSkillDefinitions(fixedPath);
        var normal = new CollectionOfficerSkills(traits, skills.Formations, skills.Gems);
        portraitCatalog ??= OfficerEditorPortraitCatalog.Load(repository, locale);
        foreach (var officer in library.Officers)
        {
            portraitCatalog.ValidateChoice(officer.PortraitId);
            int physical = Physical(officer.Id);
            Span<byte> row = document.DecodedPayload.AsSpan(table.RecordOffset + physical * table.RecordStride, table.RecordStride);
            row.Clear();
            int nameBytes = locale == "SC" ? 18 : 14;
            Text(row[..nameBytes], officer.FamilyName); Text(row.Slice(nameBytes, nameBytes), officer.GivenName);
            Text(row.Slice(locale == "SC" ? 36 : 28, locale == "SC" ? 18 : 6), officer.CourtesyName);
            Text(row.Slice(locale == "SC" ? 54 : 34, locale == "SC" ? 50 : 38), officer.FamilyNameSortKey);
            Text(row.Slice(locale == "SC" ? 104 : 72, locale == "SC" ? 50 : 38), officer.GivenNameSortKey);
            Word(row, 148 + delta, officer.PortraitId); Word(row, 150 + delta, officer.Id);
            Word(row, 152 + delta, officer.SpouseId); Word(row, 154 + delta, officer.SwornBrotherId);
            row[161 + delta] = 9;
            row[166 + delta] = officer.Leadership; row[167 + delta] = officer.Strength;
            row[168 + delta] = officer.Intelligence; row[169 + delta] = officer.Politics; row[170 + delta] = officer.Charm;
            int traitIndex = 0;
            foreach (int id in officer.TraitIds)
                if (id > 0 && id < traits.Rows.Count && normal.ReadSkillPersonalityValidity(new(id)) == true &&
                    normal.ReadPersonalityByte(new(id), 0xBE) != 0 && (normal.ReadSkillPersonalityFlags(new(id))!.Value & 0x10) != 0)
                    Word(row, 176 + delta + traitIndex++ * 2, id);
            uint formations = officer.FormationIds.Where(id => id is not (14 or 19 or 20)).Aggregate(0u, (mask, id) => mask | (1u << id));
            BinaryPrimitives.WriteUInt32LittleEndian(row.Slice(194 + delta, 4), formations);
            int tacticIndex = 0;
            foreach (int id in officer.TacticIds)
                if (id is > 0 and <= 200 && (skills.Formations.Tactics[id].RawBytes[51] & 2) != 0)
                    row[198 + delta + tacticIndex++] = (byte)id;
            row[208 + delta] = officer.PolicyId;
            row[209 + delta] = officer.VoiceType is >= 1 and <= 13 ? officer.VoiceType : (byte)0;
            row[210 + delta] = 255; row[211 + delta] = officer.VoiceType;
            row[216 + delta] = officer.Sex;
            Word(row, 217 + delta, Math.Min(350, officer.BirthYear + 14));
            Word(row, 219 + delta, officer.BirthYear); Word(row, 221 + delta, officer.DeathYear);
            row[223 + delta] = 0;
            Word(row, 224 + delta, officer.FamilyId); Word(row, 226 + delta, officer.FatherId); Word(row, 228 + delta, officer.MotherId);
            row[230 + delta] = 1; row[231 + delta] = officer.Compatibility;
            for (int i = 0; i < 8; i++)
            {
                Word(row, 232 + delta + i * 2, officer.LikePersonIds[i]);
                Word(row, 248 + delta + i * 2, officer.DislikePersonIds[i]);
            }
            row[274 + delta] = officer.DoctrineId; row[300 + delta] = officer.PolicyBaseCost;
            if (selection.AdoptedOfficerIds.Contains(officer.Id))
            {
                row[161 + delta] = timeless || year - officer.BirthYear + 1 > 14 ? (byte)8 : (byte)7;
                row[162 + delta] = 73;
            }
            if (selection.Placements.FirstOrDefault(value => value.OfficerId == officer.Id) is { } placement)
            {
                var city = source.Snapshot.Cities.Single(value => value.Slot == placement.CityId);
                bool adult = timeless || year - officer.BirthYear + 1 >= 15;
                bool serving = placement.Serving && adult;
                bool newForceCity = selection.NewForces.Any(force => force.CityId == placement.CityId || force.AdditionalCityIds.Contains(placement.CityId));
                if (serving && city.ForceSlot == 0 && !newForceCity)
                    throw new InvalidDataException("Registered officer cannot serve a city without a force.");
                row[156 + delta] = serving ? city.DistrictSlot : (byte)0;
                Word(row, 157 + delta, placement.CityId); Word(row, 159 + delta, placement.CityId);
                row[161 + delta] = !adult ? (byte)7 : serving ? (byte)4 : (byte)5;
                Word(row, 213 + delta, serving ? year : 0);
            }
        }
        var projected = RefreshPersons(source, document);
        projected = RegisteredOfficerNewGameForceProjection.Apply(projected, selection);
        document = projected.InitialScenarioDocument ?? document;
        table = S14ScenarioTables.ReadCharacterNames(document, locale);
        uint crt = initializationSeed;
        // This source step explicitly reseeds once, independently of earlier
        // fresh hire work. Its resulting stream is adopted after that work.
        foreach (var officer in library.Officers.OrderBy(value => value.Id))
        {
            int physical = Physical(officer.Id);
            Span<byte> row = document.DecodedPayload.AsSpan(table.RecordOffset + physical * table.RecordStride, table.RecordStride);
            int home = ReadWord(row, 157 + delta);
            byte status = row[161 + delta];
            if (home is < 1 or > 61 && status is not (7 or 8)) continue;
            if (home is < 1 or > 61)
            {
                home = ParentHome(officer.FatherId);
                if (home == 0) home = ParentHome(officer.MotherId);
                if (home == 0)
                {
                    crt = unchecked(crt * 0x343FDu + 0x269EC3u);
                    home = (int)((crt >> 16) & 0x7FFF) % 46 + 1;
                }
                Word(row, 157 + delta, home);
            }
            if (ReadWord(row, 159 + delta) is < 1 or > 1061) Word(row, 159 + delta, home);
            if (ReadWord(row, 224 + delta) is < 1 or > 6000) Word(row, 224 + delta, officer.Id);
        }
        var complete = RefreshPersons(projected, document);
        string decodedSha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(document.DecodedPayload));
        return complete with
        {
            InitialRegisteredOfficerRandom = new(crt), InitialRegisteredOfficerSelection = selection,
            Metadata = complete.Metadata with { DecodedSha256 = decodedSha },
            Snapshot = complete.Snapshot with
            {
                DecodedSha256 = decodedSha, ObjectSourceRecords = S14ScenarioObjectReader.Read(document),
                WorldParameters = S14ScenarioWorldReader.Read(document, locale)
            }
        };

        int ParentHome(int id)
        {
            if (id == 0) return 0;
            var parent = projected.Snapshot.Characters.FirstOrDefault(value => value.CharacterId == id);
            if (parent is null) return 0;
            int slot = parent.Slot;
            int home = ReadWord(document.DecodedPayload.AsSpan(table.RecordOffset + slot * table.RecordStride, table.RecordStride), 157 + delta);
            return home is >= 1 and <= 61 ? home : 0;
        }
    }

    public static LoadedScenario RefreshPersons(LoadedScenario source, S14Document document)
    {
        string locale = source.Snapshot.Locale;
        var names = S14ScenarioTables.ReadCharacterNames(document, locale);
        var identities = S14ScenarioTables.ReadCharacterIdentities(document, locale);
        var placements = S14ScenarioTables.ReadCharacterPlacements(document, locale);
        var abilities = S14ScenarioTables.ReadCharacterAbilities(document, locale);
        var sex = S14ScenarioTables.ReadCharacterSexes(document, locale);
        var dialogue = S14CharacterDialogueReader.Read(document, locale);
        var lifespan = S14ScenarioTables.ReadCharacterLifespans(document, locale);
        var appearance = S14ScenarioTables.ReadCharacterAppearanceYears(document, locale);
        var offices = S14RankReader.ReadCharacterOffices(document, locale);
        var policies = S14PolicyReader.ReadCharacterReferences(document, locale);
        var doctrines = S14DoctrineReader.ReadCharacterReferences(document, locale);
        var credits = S14CreditEffectReader.ReadCharacterReferences(document, locale);
        var skills = S14PersonSkillReader.ReadCharacterReferences(document, locale)!;
        var duel = S14PersonDuelConditionReader.Read(document, locale)!;
        var people = source.Snapshot.Characters.Select(person =>
        {
            int p = person.Slot;
            var placement = placements[p]; var ability = abilities[p]; var voice = dialogue[p]; var skill = skills.Characters[p];
            int force = source.Snapshot.Districts.FirstOrDefault(district => district.Slot == placement.DistrictSlot)?.ForceSlot ?? 0;
            return person with
            {
                Name = names.Names[p].DisplayName, DistrictSlot = placement.DistrictSlot, ForceSlot = checked((byte)force),
                CitySlot = placement.CitySlot, SecondaryCitySlot = placement.SecondaryCitySlot, StatusCode = placement.StatusCode,
                RawBytesHex = placement.RawBytesHex, CharacterId = identities[p].CharacterId, AppearanceCode = identities[p].AppearanceCode,
                NameSortKey = names.Names[p].NameSortKey, SurnameSortKey = names.Names[p].SurnameSortKey, GivenNameSortKey = names.Names[p].GivenNameSortKey,
                FamilyName = names.Names[p].FamilyName, GivenName = names.Names[p].GivenName, CourtesyName = voice.CourtesyName,
                Leadership = ability.Leadership, Strength = ability.Strength, Intelligence = ability.Intelligence, Politics = ability.Politics, Charm = ability.Charm,
                RawSexCode = sex[p].RawCode, DialogueStyle = voice.Style, VoiceType = voice.VoiceType, SpecialVoiceType = voice.SpecialVoiceType,
                BirthYear = voice.BirthYear, FamilyGeneration = voice.FamilyGeneration, AppearanceYear = appearance[p], TerminalYear = lifespan[p].TerminalYear,
                LifespanKind = lifespan[p].Kind, RawOfficeId = offices[p].OfficeId, RawPolicySlot = policies.Characters[p].RawPolicySlot,
                RawPolicyBaseCost = policies.Characters[p].RawPolicyBaseCost, RawPolicyFieldBytes = policies.Characters[p].RawFieldBytes,
                RawDoctrineId = doctrines.Characters[p].RawDoctrineId, RawDoctrineWord = doctrines.Characters[p].RawDoctrineWord,
                RawDoctrineFieldBytes = doctrines.Characters[p].RawFieldBytes, CreditReference = credits.Characters[p], SkillSource = skill,
                DuelConditionSource = duel[p], BaseFormationSlots = Enumerable.Range(0, 21).Where(id => (skill.RawFormationMask164 & (1u << id)) != 0).ToArray(),
                BaseTacticSlots = skill.RawTacticSlots168.Where(id => id != 0).Select(id => (int)id).ToArray()
            };
        }).ToArray();
        return source with { InitialScenarioDocument = document, Snapshot = source.Snapshot with
            { Characters = people, AffiliatedCharacters = people.Where(person => person.DistrictSlot > 0).ToArray() } };
    }

    private static int Physical(int id) => 1401 + id - 3001;
    private static int ReadWord(ReadOnlySpan<byte> row, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(offset, 2));
    private static void Word(Span<byte> row, int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(offset, 2), checked((ushort)value));
    private static void Text(Span<byte> destination, string text)
    {
        destination.Clear();
        if (Encoding.Unicode.GetByteCount(text) > destination.Length - 2) throw new InvalidDataException("Registered officer text exceeds its source field.");
        Encoding.Unicode.GetBytes(text, destination);
    }
}
