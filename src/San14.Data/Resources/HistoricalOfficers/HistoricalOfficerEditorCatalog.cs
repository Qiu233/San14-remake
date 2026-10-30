using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed record HistoricalOfficerEditorRow(int Id, string Name, string? NameSortKey,
    ushort BirthYear, byte Sex, HistoricalOfficerEdit Original)
{
    public ushort AppearanceYear { get; init; }
    public string FamilyName { get; init; } = "";
    public string GivenName { get; init; } = "";
    public string CourtesyName { get; init; } = "";
    public ushort FamilyId { get; init; }
    public ushort FatherId { get; init; }
    public ushort MotherId { get; init; }
    public ushort SpouseId { get; init; }
    public ushort SwornBrotherId { get; init; }
    public byte Compatibility { get; init; }
    public byte VoiceType { get; init; }
}

public sealed record HistoricalOfficerPortraitCategory(int Id,
    IReadOnlyList<RegisteredOfficerEditorChoice> Choices, IReadOnlyList<string> UnresolvedOwners)
{
    public string Caption { get; init; } = "";
}

/// <summary>Immutable title scenario08 editor inputs. Loading does not create
/// or mutate a campaign, and profile values remain owned by the caller.</summary>
public sealed class HistoricalOfficerEditorCatalog
{
    private readonly IReadOnlyDictionary<int, HistoricalOfficerEditorRow> byId;
    private readonly IReadOnlySet<int> femaleOnlyTraits;
    public string Locale { get; }
    public IReadOnlyList<HistoricalOfficerEditorRow> Rows { get; }
    public IReadOnlyList<RegisteredOfficerEditorChoice> Personalities { get; }
    public IReadOnlyList<RegisteredOfficerEditorChoice> Doctrines { get; }
    public IReadOnlyList<RegisteredOfficerEditorChoice> Policies { get; }
    public IReadOnlyList<RegisteredOfficerEditorChoice> Tactics { get; }
    public IReadOnlyList<RegisteredOfficerEditorChoice> Formations { get; }
    public IReadOnlyList<RegisteredOfficerEditorChoice> Portraits { get; }
    public IReadOnlyList<HistoricalOfficerPortraitCategory> PortraitCategories => PortraitCatalog.Categories;
    public OfficerEditorPortraitCatalog PortraitCatalog { get; }
    public IReadOnlyList<RegisteredOfficerEditorChoice> Officers { get; }
    public IReadOnlyList<SourceProvenance> Sources { get; }
    public IReadOnlyList<CollectionOfficerUnresolvedCandidate> UnresolvedCandidates { get; }
    public IReadOnlyList<string> UnresolvedOwners { get; }
    public bool PortraitCatalogComplete => PortraitCategories.All(category => category.UnresolvedOwners.Count == 0);

    public static HistoricalOfficerEditorCatalog Load(GameResourceRepository repository,
        string locale, RegisteredOfficerLibrary registeredLibrary,
        CollectionOfficerAdmission? admission = null, PersonalityEditorLibrary? personalities = null) =>
        repository.LoadHistoricalOfficerEditorCatalog(locale, registeredLibrary, admission, personalities);

    internal HistoricalOfficerEditorCatalog(string locale, HistoricalOfficerEditorRow[] rows,
        RegisteredOfficerEditorChoice[] personalities, RegisteredOfficerEditorChoice[] doctrines,
        RegisteredOfficerEditorChoice[] policies, RegisteredOfficerEditorChoice[] tactics,
        RegisteredOfficerEditorChoice[] formations, OfficerEditorPortraitCatalog portraitCatalog,
        RegisteredOfficerEditorChoice[] officers, IReadOnlySet<int> femaleOnlyTraits,
        IReadOnlyList<SourceProvenance> sources,
        IReadOnlyList<CollectionOfficerUnresolvedCandidate> unresolved)
    {
        Locale = locale; Rows = Array.AsReadOnly(rows); byId = rows.ToDictionary(row => row.Id);
        Personalities = Array.AsReadOnly(personalities); Doctrines = Array.AsReadOnly(doctrines);
        Policies = Array.AsReadOnly(policies); Tactics = Array.AsReadOnly(tactics);
        Formations = Array.AsReadOnly(formations); PortraitCatalog = portraitCatalog;
        Portraits = portraitCatalog.Choices;
        Officers = Array.AsReadOnly(officers); this.femaleOnlyTraits = femaleOnlyTraits;
        Sources = sources; UnresolvedCandidates = unresolved;
        UnresolvedOwners = Array.AsReadOnly(PortraitCategories.SelectMany(category => category.UnresolvedOwners)
            .Concat(unresolved.Select(candidate => candidate.UnresolvedOwner)).Distinct().ToArray());
    }

    public HistoricalOfficerEditorRow Find(int id) => byId.TryGetValue(id, out var row)
        ? row : throw new ArgumentOutOfRangeException(nameof(id), "The historical officer is not admitted.");
    public bool IsFemaleOnlyTrait(int id) => femaleOnlyTraits.Contains(id);
    public static bool CanEdit(int section) => section is 2 or 3 or 4 or 5 or 6 or 7 or 9 or 10 or 12;
    public int PolicyCostMaximum(HistoricalOfficerEdit edit)
    {
        var original = Find(edit.Id).Original;
        return edit.PolicyId == original.PolicyId ? Math.Max(5, (int)original.PolicyBaseCost) : 5;
    }

    public void Validate(HistoricalOfficerEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit); edit.Validate();
        var row = Find(edit.Id); var original = row.Original;
        if (edit.DeathYear != original.DeathYear &&
            (edit.DeathYear < Math.Min(350, (int)row.AppearanceYear) || edit.DeathYear > Math.Min(350, row.BirthYear + 98)))
            throw Invalid("death_year");
        Choice(edit.PortraitId, original.PortraitId, Portraits, "portrait");
        Choice(edit.DoctrineId, original.DoctrineId, Doctrines, "doctrine");
        Choice(edit.PolicyId, original.PolicyId, Policies, "policy");
        if (edit.PolicyBaseCost != original.PolicyBaseCost || edit.PolicyId != original.PolicyId)
            if (edit.PolicyBaseCost < 1 || edit.PolicyBaseCost > PolicyCostMaximum(edit)) throw Invalid("policy_cost");
        Slots(edit.LikePersonIds, original.LikePersonIds, Officers, 8, "likes");
        Slots(edit.DislikePersonIds, original.DislikePersonIds, Officers, 8, "dislikes");
        Slots(edit.TraitIds, original.TraitIds, Personalities, 5, "traits");
        if (!edit.TraitIds.SequenceEqual(original.TraitIds) && row.Sex == 0 &&
            edit.TraitIds.Any(id => id != 0 && !original.TraitIds.Contains(id) && IsFemaleOnlyTrait(id)))
            throw Invalid("female_trait");
        Slots(edit.TacticIds, original.TacticIds, Tactics, 10, "tactics");
        Slots(edit.FormationIds, original.FormationIds, Formations, 5, "formations", zeroIsEmpty: false);
        if (!edit.FormationIds.SequenceEqual(original.FormationIds) && edit.FormationIds.Length == 0)
            throw Invalid("formations");
    }

    private static void Choice(int selected, int original,
        IReadOnlyList<RegisteredOfficerEditorChoice> choices, string field)
    {
        if (selected != original && !choices.Any(choice => choice.Id == selected)) throw Invalid(field);
    }
    private static void Slots(ImmutableArray<int> selected, ImmutableArray<int> original,
        IReadOnlyList<RegisteredOfficerEditorChoice> choices, int maximum, string field, bool zeroIsEmpty = true)
    {
        if (selected.SequenceEqual(original)) return;
        int[] used = selected.Where(id => !zeroIsEmpty || id != 0).ToArray();
        if (used.Length > maximum || used.Distinct().Count() != used.Length ||
            used.Any(id => !original.Contains(id) && !choices.Any(choice => choice.Id == id))) throw Invalid(field);
    }
    private static InvalidDataException Invalid(string field) => new($"historical_officers.editor.{field}");
}
