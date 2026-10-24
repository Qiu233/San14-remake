using San14.Core;

namespace San14.Data;

public sealed record PersonnelLearnBook(int GemId, int TacticId, string Name, int Count,
    bool? Eligible, string? UnresolvedOwner)
{ public string TacticName { get; init; } = ""; }
public sealed record PersonnelLearnTactic(int TacticId, string Name, bool CanReplace)
{ public int SlotIndex { get; init; } }
public sealed record PersonnelLearnTacticSlot(int Index, int? TacticId, string Text,
    bool ButtonVisible, bool MarkerVisible, bool Enabled, bool Selected);

/// <summary>The officer, owned book and optional erased tactic are separate
/// selections. Eligibility is read only when this page's selection changes.</summary>
public sealed class PersonnelLearnDraft
{
    private readonly LearnCommandCurrentRules? rules;
    private readonly Func<int, int, bool>? eligible;
    private readonly Func<int, bool>? requiresReplacement;
    private readonly IReadOnlyList<PersonnelDevelopmentOfficer> people;
    private IReadOnlyList<PersonnelLearnBook> books = [];
    private IReadOnlyList<PersonnelLearnTactic> tactics = [];
    private bool? replacementRequired;
    private readonly Func<int, IReadOnlyList<string>>? effects;
    private readonly string newTacticText = "";
    private readonly Func<int, string>? tacticName;
    private readonly Func<int, int, int?, AreaAdministrationQuery<string>>? confirmation;
    private AreaAdministrationQuery<string>? confirmationCache;
    private IReadOnlyList<PersonnelLearnTacticSlot> tacticSlots = [];
    private IReadOnlyList<string> effectLines = [];
    private readonly Dictionary<int, AreaAdministrationQuery<IReadOnlyList<string>>> effectCache = [];

    internal PersonnelLearnDraft(LearnCommandCurrentRules rules,
        IReadOnlyList<PersonnelDevelopmentOfficer> people, Func<int, int, bool> eligible,
        Func<int, bool> requiresReplacement, string newTacticText, Func<int, IReadOnlyList<string>> effects,
        Func<int, string> tacticName, Func<int, int, int?, AreaAdministrationQuery<string>> confirmation)
    {
        this.rules = rules; this.people = people; this.eligible = eligible;
        this.requiresReplacement = requiresReplacement; ForceId = rules.ForceId;
        this.newTacticText = newTacticText; this.effects = effects; this.tacticName = tacticName;
        this.confirmation = confirmation; Refresh();
    }
    internal PersonnelLearnDraft(int forceId, string missing)
    { ForceId = forceId; people = []; UnresolvedOwner = missing; }

    public int ForceId { get; }
    public int DistrictId => rules?.DistrictId ?? 0;
    public IReadOnlyList<PersonnelDevelopmentOfficer> People => people;
    public IReadOnlyList<PersonnelDevelopmentOfficer> Rows => people;
    public IReadOnlyList<PersonnelLearnBook> Books => books;
    public IReadOnlyList<PersonnelLearnTactic> Tactics => tactics;
    public IReadOnlyList<PersonnelLearnTacticSlot> TacticSlots => tacticSlots;
    public IReadOnlyList<string> Effects => effectLines;
    public PersonnelLearnBook? SelectedBook => books.FirstOrDefault(row => row.GemId == GemId);
    public int? SelectedBookCountBefore => SelectedBook?.Count;
    public int? SelectedBookCountAfter => SelectedBook is { } book ? book.Count - 1 : null;
    public int? SelectedBookCountCost => SelectedBook is not null ? 1 : null;
    public IReadOnlyList<PersonnelLearnTactic> ReplacementCandidates => tactics.Where(row => row.CanReplace).ToArray();
    public int? OfficerId { get; private set; }
    public int? GemId { get; private set; }
    public int? ReplacedTacticId { get; private set; }
    public PersonnelDevelopmentOfficer? OfficerCard => people.FirstOrDefault(row => row.OfficerId == OfficerId);
    public int? CommandBooksBefore => rules?.AvailableBooks;
    public int? CommandBookCost => rules?.BookCost;
    public int? CommandBooksAfter => rules is null ? null : rules.AvailableBooks - rules.BookCost;
    public bool? RequiresReplacement => replacementRequired;
    public string? UnresolvedOwner { get; private set; }
    public bool CanConfirm => rules is not null && UnresolvedOwner is null && OfficerId is not null &&
        GemId is int gem && books.Any(row => row.GemId == gem && row.Eligible == true && row.Count > 0) &&
        rules.AvailableBooks >= rules.BookCost && replacementRequired is bool need &&
        (need ? ReplacedTacticId is int old && tactics.Any(row => row.TacticId == old && row.CanReplace)
            : ReplacedTacticId is null);
    public bool CanContinue => CanConfirm && rules is not null &&
        (long)rules.AvailableBooks >= 2L * rules.BookCost && rules.People.Count > 0 &&
        rules.Books.Count > 0 && (rules.Books.Count != 1 || rules.Books[0].Count != 1);

    public bool SelectOfficer(int officerId)
    {
        if (!people.Any(row => row.OfficerId == officerId && row.Enabled == true)) return false;
        OfficerId = officerId; GemId = null; ReplacedTacticId = null; Refresh(); return true;
    }
    public bool SelectGem(int gemId)
    {
        if (!books.Any(row => row.GemId == gemId && row.Eligible == true && row.Count > 0)) return false;
        GemId = gemId; confirmationCache = null;
        var preview = QueryTacticEffects(SelectedBook!.TacticId);
        effectLines = preview.Value ?? []; EffectUnresolvedOwner = preview.UnresolvedInput;
        RefreshSlots(); return true;
    }
    public bool SelectReplacement(int? tacticId)
    {
        if (tacticId is int id && !tactics.Any(row => row.TacticId == id && row.CanReplace)) return false;
        if (tacticId is null && replacementRequired == true) return false;
        ReplacedTacticId = tacticId; confirmationCache = null; RefreshSlots(); return true;
    }
    public GameOrder? BuildOrder() => CanConfirm ? new LearnOfficerTacticOrder(ForceId,
        OfficerId!.Value, GemId!.Value, ReplacedTacticId) : null;
    public AreaAdministrationQuery<string> QueryConfirmation() => confirmationCache ??=
        CanConfirm && confirmation is not null ? confirmation(OfficerId!.Value, GemId!.Value, ReplacedTacticId)
            : AreaAdministrationQuery<string>.Unknown(UnresolvedOwner ?? "personnel-learn.page.selection");
    public AreaAdministrationQuery<IReadOnlyList<string>> QueryTacticEffects(int tacticId)
    {
        if (OfficerId is null) return AreaAdministrationQuery<IReadOnlyList<string>>.Known([]);
        if (!tactics.Any(row => row.TacticId == tacticId) && SelectedBook?.TacticId != tacticId)
            return AreaAdministrationQuery<IReadOnlyList<string>>.Unknown("personnel-learn.page.effect_tactic_not_in_selection");
        if (effectCache.TryGetValue(tacticId, out var cached)) return cached;
        try
        {
            return effectCache[tacticId] = effects is null
                ? AreaAdministrationQuery<IReadOnlyList<string>>.Unknown(UnresolvedOwner ?? "personnel-learn.page.effects")
                : AreaAdministrationQuery<IReadOnlyList<string>>.Known(effects(tacticId));
        }
        catch (InvalidDataException error)
        { return effectCache[tacticId] = AreaAdministrationQuery<IReadOnlyList<string>>.Unknown(error.Message); }
    }

    private void Refresh()
    {
        if (rules is null) return;
        UnresolvedOwner = null; confirmationCache = null; EffectUnresolvedOwner = null; effectLines = []; tactics = []; replacementRequired = null;
        if (OfficerId is int officer)
        {
            var person = rules.People.First(row => row.Id == officer);
            tactics = person.Tactics.Select(row => new PersonnelLearnTactic(row.TacticId, row.Name, row.CanReplace)
                { SlotIndex = row.SlotIndex }).ToArray();
            try { replacementRequired = requiresReplacement!(officer); }
            catch (InvalidDataException error) { UnresolvedOwner = error.Message; }
        }
        books = rules.Books.Select(book =>
        {
            bool? allowed = null; string? missing = null;
            if (OfficerId is int id)
            {
                try { allowed = eligible!(id, book.GemId); }
                catch (InvalidDataException error) { missing = error.Message; }
            }
            return new PersonnelLearnBook(book.GemId, book.TacticId, book.Name, book.Count, allowed, missing)
            { TacticName = tacticName!(book.TacticId) };
        }).ToArray();
        RefreshSlots();
    }

    public string? EffectUnresolvedOwner { get; private set; }
    private void RefreshSlots()
    {
        var slots = new List<PersonnelLearnTacticSlot>(10);
        var book = SelectedBook;
        for (int index = 0; index < 10; index++)
        {
            if (replacementRequired == true && tactics.FirstOrDefault(row => row.SlotIndex == index) is { } fullTactic)
            {
                var tactic = fullTactic; bool selected = book is not null && tactic.TacticId == ReplacedTacticId;
                slots.Add(new(index, tactic.TacticId, selected ? book!.TacticName : tactic.Name, true, false, tactic.CanReplace, selected));
            }
            else if (replacementRequired == false && index == 0)
                slots.Add(new(index, null, book?.TacticName ?? newTacticText, true, false, true, book is not null));
            else if (replacementRequired == false && index > 0 &&
                tactics.FirstOrDefault(row => row.SlotIndex == index - 1) is { } priorTactic)
            {
                var tactic = priorTactic;
                slots.Add(new(index, tactic.TacticId, tactic.Name, false, true, false, false));
            }
            else slots.Add(new(index, null, "", false, false, false, false));
        }
        tacticSlots = slots.AsReadOnly();
    }
}
