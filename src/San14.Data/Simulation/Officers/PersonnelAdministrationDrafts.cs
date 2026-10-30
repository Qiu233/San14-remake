using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed record PersonnelRewardCard(int OfficerId, string Name, OfficerPortraitRoute? Portrait,
    int? LoyaltyBefore, int? LoyaltyAfter, bool Selected, bool? Eligible, string? UnresolvedOwner);
public sealed record PersonnelRewardBudget(int DistrictId, int? GoldBefore, int? GoldAfter, int? GoldCost,
    int? CommandBooksBefore, int? CommandBooksAfter, int? CommandBookCost, int? MaximumSelection,
    string? UnresolvedOwner)
{
    public bool Available => UnresolvedOwner is null;
}

/// <summary>The reward page's local target selection. All current queries and
/// portrait routes belong to its one captured publication. Only BuildOrder
/// crosses into the ordinary command API; selecting a card never changes it.</summary>
public sealed class PersonnelRewardDraft
{
    private readonly int forceId;
    private readonly int districtId;
    private readonly ImmutableArray<PersonnelRewardCard> people;
    private readonly Func<IReadOnlyList<int>, PersonnelRewardBudget>? quote;
    private readonly Func<IReadOnlyList<int>>? automatic;
    private ImmutableArray<int> selected = [];
    private ImmutableArray<PersonnelRewardCard> rows;
    private ImmutableArray<PersonnelRewardCard> targets = [];
    private PersonnelRewardBudget budget;

    internal PersonnelRewardDraft(int forceId, int districtId, IEnumerable<PersonnelRewardCard> people,
        Func<IReadOnlyList<int>, PersonnelRewardBudget> quote,
        Func<IReadOnlyList<int>> automatic, PersonnelAdministrationSelectionSource selectionSource)
    {
        this.forceId = forceId; this.districtId = districtId; this.people = people.ToImmutableArray();
        this.quote = quote; this.automatic = automatic; SelectionSource = selectionSource;
        rows = this.people; budget = quote(selected);
        ApplyRecommended();
    }

    internal PersonnelRewardDraft(int forceId, string missing)
    {
        this.forceId = forceId; people = []; rows = [];
        UnresolvedOwner = missing;
        budget = new(0, null, null, null, null, null, null, null, missing);
    }

    internal PersonnelAdministrationSelectionSource? SelectionSource { get; }
    public int ForceId => forceId;
    public int DistrictId => districtId;
    public IReadOnlyList<PersonnelRewardCard> Rows => rows;
    public IReadOnlyList<PersonnelRewardCard> CardTargets => targets;
    public IReadOnlyList<int> SelectedOfficerIds => selected;
    public PersonnelRewardBudget Budget => budget;
    public string? UnresolvedOwner { get; }
    public bool CanConfirm => UnresolvedOwner is null && !selected.IsEmpty && budget.Available &&
        budget.GoldBefore is int gold && budget.GoldCost is int goldCost && gold >= goldCost &&
        budget.CommandBooksBefore is int books && budget.CommandBookCost is int bookCost && books >= bookCost;

    public bool Toggle(int officerId)
    {
        if (!people.Any(person => person.OfficerId == officerId && person.Eligible == true)) return false;
        return SelectTargets(selected.Contains(officerId) ? selected.Where(id => id != officerId).ToArray()
            : selected.Add(officerId));
    }

    public bool SelectTargets(IReadOnlyList<int> officerIds)
    {
        if (quote is null || officerIds.Count != officerIds.Distinct().Count() ||
            officerIds.Any(id => !people.Any(person => person.OfficerId == id && person.Eligible == true))) return false;
        //753870 sorts accepted output by raw loyalty again before publishing.
        //Equal loyalties retain the selector's actual current-body order.
        var chosenPeople = officerIds.Select(id => people.First(person => person.OfficerId == id)).ToArray();
        if (chosenPeople.Any(person => person.LoyaltyBefore is null)) return false;
        var chosen = chosenPeople.OrderBy(person => person.LoyaltyBefore)
            .Select(person => person.OfficerId).ToImmutableArray();
        var next = quote(chosen);
        if (next.MaximumSelection is not int maximum || chosen.Length > maximum) return false;
        selected = chosen; budget = next;
        rows = people.Select(person => person with { Selected = selected.Contains(person.OfficerId) }).ToImmutableArray();
        targets = selected.Select(id => rows.First(person => person.OfficerId == id)).ToImmutableArray();
        return true;
    }

    public bool ApplyRecommended() => automatic is not null && SelectTargets(automatic());
    public GameOrder? BuildOrder() => CanConfirm ? new RewardOfficersOrder(forceId, districtId, selected) : null;
}

/// <summary>Read-only candidates and local multi-selection for the actual
/// banishment command. Foreign captives remain distinct admitted candidates.</summary>
public sealed class PersonnelBanishDraft
{
    private readonly int forceId;
    private readonly Func<IReadOnlyList<int>, AreaAdministrationQuery<string>>? confirmation;
    private ImmutableArray<int> selected = [];
    private AreaAdministrationQuery<string>? confirmedText;
    internal PersonnelBanishDraft(int forceId, IReadOnlyList<BanishOfficerCandidate> candidates,
        Func<IReadOnlyList<int>, AreaAdministrationQuery<string>> confirmation,
        PersonnelAdministrationSelectionSource selectionSource)
    { this.forceId = forceId; Rows = candidates.ToImmutableArray(); this.confirmation = confirmation;
        SelectionSource = selectionSource; }
    internal PersonnelBanishDraft(int forceId, string missing)
    { this.forceId = forceId; Rows = []; UnresolvedOwner = missing; }
    internal PersonnelAdministrationSelectionSource? SelectionSource { get; }
    internal int ForceId => forceId;
    public IReadOnlyList<BanishOfficerCandidate> Rows { get; }
    public IReadOnlyList<int> SelectedOfficerIds => selected;
    public string? UnresolvedOwner { get; }
    public bool CanConfirm => UnresolvedOwner is null && !selected.IsEmpty;
    public bool SelectTargets(IReadOnlyList<int> officerIds)
    {
        if (officerIds.Count != officerIds.Distinct().Count() ||
            officerIds.Any(id => !Rows.Any(row => row.OfficerId == id && row.Enabled == true))) return false;
        selected = officerIds.ToImmutableArray();
        confirmedText = null; return true;
    }
    public bool Toggle(int officerId) => SelectTargets(selected.Contains(officerId)
        ? selected.Where(id => id != officerId).ToArray() : selected.Add(officerId));
    public AreaAdministrationQuery<string> QueryConfirmation() => confirmedText ??= !CanConfirm || confirmation is null
        ? AreaAdministrationQuery<string>.Unknown(UnresolvedOwner ?? "personnel-banish.no_targets") : confirmation(selected);
    public GameOrder? BuildOrder() => CanConfirm ? new BanishOfficersOrder(forceId, selected) : null;
}
