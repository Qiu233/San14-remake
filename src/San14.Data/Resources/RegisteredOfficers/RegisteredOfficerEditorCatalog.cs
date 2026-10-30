using System.Globalization;
using System.Text;
using San14.Core;

namespace San14.Data;

public sealed record RegisteredOfficerEditorChoice(int Id, string Name);

/// <summary>Resource-backed editor choices, captured once for an editing
/// page. Registration does not borrow a current campaign's mutable Person.</summary>
public sealed class RegisteredOfficerEditorCatalog
{
    private IReadOnlySet<int> femaleOnlyTraits = new HashSet<int>();
    public bool IsFemaleOnlyTrait(int id) => femaleOnlyTraits.Contains(id);
    public IReadOnlyList<RegisteredOfficerEditorChoice> Personalities { get; private init; } = [];
    public IReadOnlyList<RegisteredOfficerEditorChoice> Doctrines { get; private init; } = [];
    public IReadOnlyList<RegisteredOfficerEditorChoice> Policies { get; private init; } = [];
    public IReadOnlyList<RegisteredOfficerEditorChoice> Tactics { get; private init; } = [];
    public IReadOnlyList<RegisteredOfficerEditorChoice> Formations { get; private init; } = [];
    public IReadOnlyList<RegisteredOfficerEditorChoice> Portraits => PortraitCatalog.Choices;
    public OfficerEditorPortraitCatalog PortraitCatalog { get; private init; } = null!;
    public IReadOnlyList<HistoricalOfficerPortraitCategory> PortraitCategories => PortraitCatalog.Categories;
    public IReadOnlyList<RegisteredOfficerEditorChoice> Voices { get; private init; } = [];
    public IReadOnlyList<RegisteredOfficerEditorChoice> Officers { get; private init; } = [];
    public IReadOnlyList<RegisteredOfficerEditorChoice> Sexes { get; private init; } = [];
    public IReadOnlyList<RegisteredOfficerEditorChoice> PolicyCosts { get; private init; } = [];
    public IReadOnlyList<RegisteredOfficerEditorChoice> VoicesForSex(byte sex) =>
        Voices.Where(voice => sex == 0 ? voice.Id <= 9 : voice.Id >= 10).ToArray();

    public static RegisteredOfficerEditorCatalog Load(GameResourceRepository repository, string locale,
        LoadedScenario context, RegisteredOfficerLibrary library,
        IReadOnlyList<RegisteredOfficerEditorChoice> portraits, PersonalityEditorLibrary? personalities = null,
        CollectionOfficerAdmission? admission = null)
    {
        if (locale is not ("SC" or "TC") || context.Snapshot.Locale != locale)
            throw new InvalidDataException("Registered-officer editor resource locale differs.");
        string fixedPath = $"0010_{locale}/fixdataexce.s14";
        var skills = repository.LoadSkillDefinitions(fixedPath);
        var traits = PersonalityEditorRuntime.Compose(repository.LoadPersonalities(fixedPath),
            personalities ?? PersonalityEditorLibrary.Empty).Catalog;
        var normalized = new CollectionOfficerSkills(traits, skills.Formations, skills.Gems);
        var data = repository.LoadMessages(locale, Formats.MessageKind.Data).Messages.Sections[1].Records;
        string Text(int id) => data[id].Text ?? throw new InvalidDataException("Registered-officer editor caption is missing.");
        var people = context.Snapshot.Characters.Where(person => person.CharacterId > 0 &&
                person.CharacterId is not (>= 3001 and <= 3150) && person.Name.Length != 0)
            .Select(person => new RegisteredOfficerEditorChoice(person.CharacterId, person.Name))
            .Concat(library.Officers.Select(person => new RegisteredOfficerEditorChoice(person.Id, person.Name)))
            .OrderBy(person => person.Id).ToArray();
        return new()
        {
            femaleOnlyTraits = traits.Rows.Where(row => normalized.ReadPersonalityByte(new(row.Slot), 0xA4) is 21 or 22)
                .Select(row => row.Slot).ToHashSet(),
            Personalities = traits.Rows.Where(row => row.Slot > 0 &&
                    normalized.ReadSkillPersonalityValidity(new(row.Slot)) == true &&
                    normalized.ReadPersonalityByte(new(row.Slot), 0xBE) != 0 &&
                    (normalized.ReadSkillPersonalityFlags(new(row.Slot))!.Value & 0x10) != 0)
                .Select(row => new RegisteredOfficerEditorChoice(row.Slot, normalized.TraitName(row.Slot))).ToArray(),
            Doctrines = repository.LoadDoctrines(fixedPath).Catalog.Doctrines.Where(row => row.Slot is >= 1 and <= 6)
                .Select(row => new RegisteredOfficerEditorChoice(row.Slot, row.Name)).ToArray(),
            Policies = repository.LoadPolicies(fixedPath).Catalog.Policies.Where(row => row.Slot is >= 1 and <= 120)
                .Select(row => new RegisteredOfficerEditorChoice(row.Slot, row.Name)).ToArray(),
            Tactics = skills.Formations.Tactics.Where(row => row.Slot > 0 && (row.RawBytes[51] & 2) != 0)
                .Select(row => new RegisteredOfficerEditorChoice(row.Slot, row.Name)).ToArray(),
            Formations = skills.Formations.Formations.Where(row => row.Slot > 0 && row.Slot is not (14 or 19 or 20))
                .Select(row => new RegisteredOfficerEditorChoice(row.Slot, row.Name)).ToArray(),
            PortraitCatalog = OfficerEditorPortraitCatalog.Load(repository, locale, admission, library),
            Voices = Enumerable.Range(1, 13).Select(id => new RegisteredOfficerEditorChoice(id, Text(0xE8 + id))).ToArray(),
            Officers = people,
            Sexes = [new(0, Text(0x495)), new(1, Text(0x496))],
            PolicyCosts = Enumerable.Range(1, 5).Select(id => new RegisteredOfficerEditorChoice(id,
                id.ToString(CultureInfo.InvariantCulture))).ToArray()
        };
    }

    public void Validate(RegisteredOfficer value)
    {
        value.Validate();
        if (!PortraitCatalog.Contains(value.PortraitId) ||
            !Doctrines.Any(choice => choice.Id == value.DoctrineId) || !Policies.Any(choice => choice.Id == value.PolicyId) ||
            !PolicyCosts.Any(choice => choice.Id == value.PolicyBaseCost) ||
            !VoicesForSex(value.Sex).Any(choice => choice.Id == value.VoiceType) ||
            value.TraitIds.Where(id => id != 0).Any(id => !Personalities.Any(choice => choice.Id == id)) ||
            value.TraitIds.Count(id => id != 0) > 5 ||
            value.TraitIds.Where(id => id != 0).Distinct().Count() != value.TraitIds.Count(id => id != 0) ||
            value.FormationIds.Any(id => !Formations.Any(choice => choice.Id == id)) ||
            value.TacticIds.Where(id => id != 0).Any(id => !Tactics.Any(choice => choice.Id == id)))
            throw new InvalidDataException("The registered officer contains an unavailable editor choice.");
    }
}
