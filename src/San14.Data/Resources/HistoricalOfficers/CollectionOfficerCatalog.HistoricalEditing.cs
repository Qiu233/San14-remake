using System.Collections.Immutable;
using System.Globalization;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class CollectionOfficerCatalog
{
    internal HistoricalOfficerEditorCatalog CreateHistoricalEditorCatalog(S14Document prototype,
        S14Document fixedData, RegisteredOfficerLibrary registeredLibrary, S14PersonalityCatalog? editedTraits = null, GameResourceRepository? repository = null)
    {
        var skills = editedTraits is null ? this.skills : new CollectionOfficerSkills(editedTraits, combat, S14GemReader.ReadFixed(fixedData));
        var appearanceYears = S14ScenarioTables.ReadCharacterAppearanceYears(prototype, Locale);
        var credit = S14CreditEffectReader.ReadCharacterReferences(prototype, Locale).Characters;
        var creditEffects = S14CreditEffectReader.Read(fixedData).Effects;
        var skillFields = S14PersonSkillReader.ReadCharacterReferences(prototype, Locale)
            ?? throw new InvalidDataException("historical_officers.editor.skills");
        var rows = admitted.Where(entry => HistoricalOfficerEdit.IsSupportedId(entry.Person.Id))
            .Select(entry => Build(entry.Person)).ToArray();
        var personalities = Enumerable.Range(1, 355).Where(id =>
                skills.ReadSkillPersonalityValidity(new(id)) == true &&
                skills.ReadPersonalityByte(new(id), 0xBE) != 0 &&
                (skills.ReadSkillPersonalityFlags(new(id))!.Value & 0x10) != 0)
            .Select(id => new RegisteredOfficerEditorChoice(id, skills.TraitName(id))).ToArray();
        var female = personalities.Where(choice => skills.ReadPersonalityByte(new(choice.Id), 0xA4) is 21 or 22)
            .Select(choice => choice.Id).ToHashSet();
        var formations = combat.Formations.Where(value => value.Slot > 0 && value.Slot is not (14 or 19 or 20) &&
                ((value.RawBytes[110] & 0xFC) != 0 || value.RawBytes[110] == 1))
            .Select(value => new RegisteredOfficerEditorChoice(value.Slot, value.Name)).ToArray();
        var tactics = combat.Tactics.Where(value => value.Slot > 0 && (value.RawBytes[51] & 2) != 0)
            .Select(value => new RegisteredOfficerEditorChoice(value.Slot, value.Name)).ToArray();
        var portraitCatalog = CreateEditorPortraitCatalog(repository ?? throw new InvalidDataException("historical_officers.portrait.repository"), registeredLibrary);
        var officers = admitted.Select(entry => new RegisteredOfficerEditorChoice(entry.Person.Id, entry.Person.Name.DisplayName))
            .Where(choice => choice.Id is > 0 and <= 6000)
            .Concat(registeredLibrary.Officers.Select(person => new RegisteredOfficerEditorChoice(person.Id, person.Name)))
            .DistinctBy(choice => choice.Id).ToArray();
        return new(Locale, rows, personalities,
            doctrines.Doctrines.Where(value => value.Slot is >= 1 and <= 6).Select(value => new RegisteredOfficerEditorChoice(value.Slot, value.Name)).ToArray(),
            policies.Policies.Where(value => value.Slot is >= 1 and <= 120).Select(value => new RegisteredOfficerEditorChoice(value.Slot, value.Name)).ToArray(),
            tactics, formations, portraitCatalog, officers, female, Sources,
            UnresolvedCandidates.Where(candidate => HistoricalOfficerEdit.IsSupportedId(candidate.OfficerId)).ToArray());

        HistoricalOfficerEditorRow Build(Person person)
        {
            // Run the same normal post-load skill producer, then 51B020's
            // validity/style filter and ordered compaction into editor slots.
            var projected = skills.Project(skillFields.Characters[person.Slot]);
            var traits = Enumerable.Range(0, 9).Select(index => (int)skills.ReadPersonTraitSlot150(0, index)!.Value)
                .Where(id => skills.ReadSkillPersonalityValidity(new(id < 356 ? id : 0)) == true &&
                    skills.ReadPersonalityByte(new(id), 0xBE) != 0).ToArray();
            var original = new HistoricalOfficerEdit(person.Id)
            {
                PortraitId = person.Appearance, DeathYear = checked((ushort)Math.Min(350, (int)unchecked((ushort)person.TerminalYear))),
                LikePersonIds = person.Relations.C0.Select(id => id is > 0 and <= 6000 ? (int)id : 0).ToImmutableArray(),
                DislikePersonIds = person.Relations.D0.Select(id => id is > 0 and <= 6000 ? (int)id : 0).ToImmutableArray(),
                Leadership = (byte)Math.Min(100, person.Abilities[0]), Strength = (byte)Math.Min(100, person.Abilities[1]),
                Intelligence = (byte)Math.Min(100, person.Abilities[2]), Politics = (byte)Math.Min(100, person.Abilities[3]),
                Charm = (byte)Math.Min(100, person.Abilities[4]),
                TraitIds = Pad(traits, 9),
                FormationIds = Enumerable.Range(0, 21).Where(id => (projected.Formations & 1u << id) != 0).ToImmutableArray(),
                TacticIds = Pad(projected.Tactics.Where(id => id is > 0 and < 201 && combat.Tactics[id].RawBytes[48] != 0)
                    .Select(id => (int)id), 10),
                DoctrineId = (byte)Math.Min(10, (int)person.Doctrine), PolicyId = (byte)Math.Min(120, (int)person.Policy),
                PolicyBaseCost = (byte)Math.Min(10, person.RawPolicyCost + CreditBonus(person))
            };
            return new(person.Id, person.Name.DisplayName, person.Name.NameSortKey, person.Dialogue.BirthYear, person.Sex, original)
            {
                AppearanceYear = appearanceYears[person.Slot], FamilyName = person.Name.FamilyName,
                GivenName = person.Name.GivenName, CourtesyName = person.Dialogue.CourtesyName,
                FamilyId = person.Relations.B2, FatherId = person.Relations.B4, MotherId = person.Relations.B6,
                SpouseId = person.Relations.BA, SwornBrotherId = person.Relations.BC, Compatibility = person.Relations.BE,
                VoiceType = person.Dialogue.VoiceType
            };
        }
        int CreditBonus(Person person)
        {
            // Title has no active user strategy/CCCPersonSelect state. The
            // historical identity ranges cannot enter the five raider bands.
            if (person.Status == 1 || person.ForceSlot is >= 46 and <= 50 || (person.Flags & 0x4000) != 0) return 0;
            var source = CreditInitialization.ReadSource(credit[person.Slot], resetCode: 0);
            if (source.Stage == 0) return 0;
            int total = 0;
            for (int index = 0; index < source.FirstSelectionCount; index++) Add(index + 1);
            for (int index = 0; index < source.SecondSelectionCount; index++) Add(index + 4);
            return total;
            void Add(int slot)
            {
                var effect = creditEffects[slot < 7 ? slot : 0];
                if (effect.RawTypeCode == 3) total = unchecked(total + effect.RawAddition);
            }
        }
        static ImmutableArray<int> Pad(IEnumerable<int> selected, int count)
        {
            int[] values = selected.ToArray();
            if (values.Length > count) throw new InvalidDataException("historical_officers.editor.slot_count");
            return values.Concat(Enumerable.Repeat(0, count - values.Length)).ToImmutableArray();
        }
    }
}
