using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed record PersonnelDevelopmentBudget(int? GoldBefore, int? GoldAfter, int? GoldCost,
    int? CommandBooksBefore, int? CommandBooksAfter, int? CommandBookCost);

/// <summary>Hiring selects actual ordered templates and a destination city;
/// the displayed count never manufactures a template selection.</summary>
public sealed class PersonnelHireDraft
{
    private readonly PersonnelHireRules? rules;
    private readonly IReadOnlyList<PersonnelDevelopmentOfficer> people;
    private ImmutableArray<int> selected = [];
    private IReadOnlyList<PersonnelDevelopmentOfficer> targets = [];
    internal PersonnelHireDraft(PersonnelHireRules rules, IReadOnlyList<PersonnelDevelopmentOfficer> people)
    {
        this.rules = rules; this.people = people; ForceId = rules.ForceId;
        CityId = rules.DefaultCityId; Budget = Quote();
    }
    internal PersonnelHireDraft(int forceId, string missing)
    { ForceId = forceId; people = []; UnresolvedOwner = missing; Budget = new(null, null, null, null, null, null); }
    public int ForceId { get; }
    public int DistrictId => rules?.DistrictId ?? 0;
    public int? CityId { get; private set; }
    public IReadOnlyList<PersonnelHireCity> Cities => rules?.Cities ?? [];
    public IReadOnlyList<PersonnelDevelopmentOfficer> People => people;
    public IReadOnlyList<PersonnelDevelopmentOfficer> Rows => people;
    public IReadOnlyList<int> SelectedOfficerIds => selected;
    public IReadOnlyList<int> SelectedTemplateIds => selected;
    public IReadOnlyList<PersonnelDevelopmentOfficer> CardTargets => targets;
    public int Count => selected.Length;
    public int? CurrentHired => rules?.CurrentHired;
    public int? CurrentHiredAfter => rules is null ? null : rules.CurrentHired + selected.Length;
    public string StatusText => selected.IsEmpty ? "(+0)" : "(^02+" + selected.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "^00)";
    public int? Capacity => rules is null ? null : rules.CurrentHired +
        Math.Min(rules.RemainingSlots, Math.Max(0, rules.Capacity - rules.CurrentHired));
    public int? RemainingSlots => rules?.RemainingSlots;
    public int? MaximumCount => rules?.MaximumSelection;
    public PersonnelDevelopmentBudget Budget { get; private set; }
    public string? UnresolvedOwner { get; }
    public bool CanConfirm => rules is not null && CityId is int city &&
        rules.Cities.Any(row => row.CityId == city) && selected.Length > 0 &&
        selected.Length <= rules.MaximumSelection && rules.Books >= rules.BookCost &&
        (long)rules.Gold >= (long)selected.Length * rules.GoldPerPerson;
    public bool CanContinue => CanConfirm && rules is not null &&
        selected.Length != Math.Min(rules.RemainingSlots, rules.Capacity - rules.CurrentHired) &&
        selected.Length != rules.Candidates.Count && rules.Cities.Count > 0 &&
        Budget.GoldAfter >= rules.GoldPerPerson && Budget.CommandBooksAfter >= rules.BookCost;

    public bool SelectCity(int cityId)
    {
        if (rules?.Cities.Any(row => row.CityId == cityId) != true) return false;
        CityId = cityId; return true;
    }
    public bool SelectTargets(IReadOnlyList<int> officerIds)
    {
        if (rules is null || officerIds.Count > rules.MaximumSelection || officerIds.Distinct().Count() != officerIds.Count ||
            officerIds.Any(id => !people.Any(row => row.OfficerId == id && row.Enabled == true))) return false;
        selected = officerIds.ToImmutableArray();
        var byId = people.ToDictionary(row => row.OfficerId);
        targets = selected.Select(id => byId[id]).ToArray(); Budget = Quote(); return true;
    }
    public bool Toggle(int officerId) => SelectTargets(selected.Contains(officerId)
        ? selected.Where(id => id != officerId).ToArray() : selected.Add(officerId));
    public GameOrder? BuildOrder() => CanConfirm ? new HireOfficersOrder(ForceId, DistrictId, CityId!.Value, SelectedTemplateIds) : null;
    //6CEF10 has no pre-execution YesNo. An empty known prompt preserves that
    //distinction for hosts sharing a confirmation result API.
    public AreaAdministrationQuery<string> QueryConfirmation() => AreaAdministrationQuery<string>.Known("");
    private PersonnelDevelopmentBudget Quote()
    {
        if (rules is null) return new(null, null, null, null, null, null);
        int cost = checked(rules.GoldPerPerson * selected.Length);
        return new(rules.Gold, rules.Gold - cost, cost,
            rules.Books, rules.Books - rules.BookCost, rules.BookCost);
    }
}
