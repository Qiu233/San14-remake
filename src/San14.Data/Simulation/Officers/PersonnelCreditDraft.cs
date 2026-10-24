using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed record PersonnelCreditChoiceView(int Index, string Title, int State,
    bool Selected, bool Enabled, bool Locked, byte? NextChoiceCode);
public sealed record PersonnelCreditRow(int OfficerId, string Name, OfficerPortraitRoute? Portrait,
    bool Selected, int MeritBefore, int MeritAfter, int CurrentStage, int NextStage,
    string CurrentStageName, string NextStageName, string? DoctrineName, byte ChoiceCode,
    IReadOnlyList<PersonnelCreditChoiceView> Choices)
{
    public IReadOnlyList<string?> StageNames { get; init; } = [];
    public int? DoctrinePointCount { get; init; }
    public AreaAdministrationOfficerCard? OfficerCard { get; init; }
}
public sealed record PersonnelCreditDoctrineReserve(int DoctrineId, string Name,
    int? Before, int? After, int? Addition, string? UnresolvedOwner);
public sealed record PersonnelCreditBudget(int DistrictId, int? GoldBefore, int? GoldAfter, int? GoldCost,
    int? CommandBooksBefore, int? CommandBooksAfter, int? CommandBookCost, int? MaximumSelection,
    string? UnresolvedOwner);

/// <summary>Local credit targets and the original two cumulative three-cell
/// choices. Queries share one captured current and all edits stay in this draft.</summary>
public sealed class PersonnelCreditDraft
{
    private readonly int forceId;
    private readonly CreditCommandCurrentRules? rules;
    private readonly Func<CreditCommandPerson, bool, byte, PersonnelCreditRow>? project;
    private readonly Func<CreditCommandPerson, int, byte>? automatic;
    private readonly Func<IReadOnlyList<int>, IReadOnlyList<PersonnelCreditDoctrineReserve>>? reserves;
    private readonly Dictionary<int, byte> codes = [];
    private ImmutableArray<int> selected = [];
    private ImmutableArray<PersonnelCreditRow> rows = [];
    private ImmutableArray<PersonnelCreditRow> targets = [];
    private PersonnelCreditBudget budget;
    private IReadOnlyList<PersonnelCreditDoctrineReserve> doctrineReserves = [];

    internal PersonnelCreditDraft(int forceId, bool rankup, CreditCommandCurrentRules rules,
        Func<CreditCommandPerson, bool, byte, PersonnelCreditRow> project,
        Func<CreditCommandPerson, int, byte> automatic,
        Func<IReadOnlyList<int>, IReadOnlyList<PersonnelCreditDoctrineReserve>> reserves)
    {
        this.forceId = forceId; Rankup = rankup; this.rules = rules; this.project = project;
        this.automatic = automatic; this.reserves = reserves;
        foreach (var person in rules.People) codes.Add(person.Id, CreditChoiceSource.Original(person));
        budget = ReadBudget(0); Refresh();
    }
    internal PersonnelCreditDraft(int forceId, bool rankup, string missing)
    {
        this.forceId = forceId; Rankup = rankup; UnresolvedOwner = missing;
        budget = new(0, null, null, null, null, null, null, null, missing);
    }
    public bool Rankup { get; }
    public int ForceId => forceId;
    public int DistrictId => rules?.DistrictId ?? 0;
    public int? GoldPerOfficer => rules?.GoldPerPerson;
    public IReadOnlyList<PersonnelCreditRow> Rows => rows;
    public IReadOnlyList<PersonnelCreditRow> CardTargets => targets;
    public IReadOnlyList<int> SelectedOfficerIds => selected;
    public PersonnelCreditBudget Budget => budget;
    public IReadOnlyList<PersonnelCreditDoctrineReserve> DoctrineReserves => doctrineReserves;
    public string? UnresolvedOwner { get; }
    public string? LastError { get; private set; }
    public bool CanConfirm => rules is not null && !selected.IsEmpty && UnresolvedOwner is null &&
        budget.GoldBefore is int gold && budget.GoldCost is int goldCost && gold >= goldCost &&
        budget.CommandBooksBefore is int books && budget.CommandBookCost is int cost && books >= cost;

    public bool SelectTargets(IReadOnlyList<int> officerIds)
    {
        if (rules is null || officerIds.Count != officerIds.Distinct().Count() ||
            officerIds.Any(id => rules.People.All(person => person.Id != id))) return false;
        var next = ReadBudget(officerIds.Count);
        if (next.MaximumSelection is not int maximum || officerIds.Count > maximum) return false;
        selected = officerIds.ToImmutableArray();
        Refresh(); return true;
    }
    public bool Toggle(int officerId) => SelectTargets(selected.Contains(officerId)
        ? selected.Where(id => id != officerId).ToArray() : selected.Add(officerId));

    public bool ToggleChoice(int officerId, int group)
    {
        if (!Rankup || rules is null || !selected.Contains(officerId)) return false;
        try
        {
            var person = rules.People.First(row => row.Id == officerId);
            codes[officerId] = CreditChoiceSource.ToggleChoice(person, codes[officerId], group);
            LastError = null; Refresh(); return true;
        }
        catch (InvalidDataException error) { LastError = error.Message; return false; }
    }

    public bool ApplyAutomatic(int mode)
    {
        if (!Rankup || rules is null || automatic is null || mode is < 3 or > 5) return false;
        try
        {
            //Prepare every new value before changing the draft so a missing
            //ability in a later row cannot leave a partially changed plan.
            var changed = rules.People.Where(row => selected.Contains(row.Id))
                .Select(person => (person.Id, Code: automatic(person, mode))).ToArray();
            foreach (var row in changed) codes[row.Id] = row.Code;
            LastError = null; Refresh(); return true;
        }
        catch (InvalidDataException error) { LastError = error.Message; return false; }
    }

    public GameOrder? BuildOrder() => !CanConfirm ? null : Rankup
        ? new PromoteOfficerCreditOrder(forceId, DistrictId, selected.Select(id => new OfficerCreditChoice(id, codes[id])).ToArray())
        : new GrantOfficerMeritOrder(forceId, DistrictId, selected);

    private PersonnelCreditBudget ReadBudget(int count)
    {
        if (rules is null) return budget;
        if (rules.GoldPerPerson <= 0) throw new InvalidDataException("personnel-credit.page.gold_selection_divisor");
        int maximum = Math.Min(rules.People.Count, rules.AvailableGold / rules.GoldPerPerson);
        if (Rankup) maximum = Math.Min(maximum, rules.AvailableBooks);
        int goldCost = checked(count * rules.GoldPerPerson);
        int books = checked(count * rules.BooksPerPerson + rules.FixedBookCost);
        return new(rules.PaymentDistrictId, rules.AvailableGold, unchecked(rules.AvailableGold - goldCost), goldCost,
            rules.AvailableBooks, unchecked(rules.AvailableBooks - books), books, maximum, null);
    }
    private void Refresh()
    {
        if (rules is null || project is null) return;
        rows = rules.People.Select(person => project(person, selected.Contains(person.Id), codes[person.Id])).ToImmutableArray();
        targets = selected.Select(id => rows.First(row => row.OfficerId == id)).ToImmutableArray();
        budget = ReadBudget(selected.Length);
        doctrineReserves = Rankup && reserves is not null ? reserves(selected) : [];
    }
}
