using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public enum DistrictAdministrationStage { SelectDistrict, SelectCities, Settings, SelectLeader, SelectTarget, Ready }

public sealed record DistrictAdministrationSummary(string Name, string CityCaption, string LeaderCaption,
    string TargetCaption, int CityCount, int? OfficerCount, int? Troops, string? UnresolvedOwner);
public sealed record DistrictAdministrationOption(int Id, string Name, bool? Available = true);
public sealed record DistrictMilitaryIntelligenceAmountOptions(int Current, int? Minimum, int? Maximum,
    IReadOnlyList<int> PresetAmounts, bool CanChooseOther, string? UnresolvedOwner);

/// <summary>One captured new/edit district page. Map picks, list picks and four
/// settings tabs edit this same local packet; only BuildOrder publishes intent.</summary>
public sealed partial class DistrictAdministrationDraft
{
    private readonly DistrictAdministrationRules? rules;
    private readonly Func<int?, IReadOnlyList<int>, DistrictSettings, DistrictAdministrationSelection>? query;
    private readonly Func<int?, IReadOnlyList<int>, DistrictSettings, int?, bool, (DistrictSettings Settings, int? LeaderId)>? defaults;
    private readonly Func<DistrictSettings, int, IReadOnlyList<int>, DistrictSettings>? changePolicy;
    private readonly Func<IReadOnlyList<int>, int?, DistrictSettings?, int, DistrictAdministrationSummary>? summarize;
    private readonly Func<int, AreaAdministrationQuery<string>>? dissolveConfirmation;
    private ImmutableArray<int> cities = [];
    private DistrictAdministrationSelection? selection;
    private bool initialCities = true;
    private IReadOnlyList<PersonnelDevelopmentOfficer> leaderCards = [];
    private readonly Func<int, PersonnelDevelopmentOfficer>? readLeader;
    private readonly Func<int?, int?>? readIntelligence;
    private int? intelligenceBefore;
    private IReadOnlyList<DistrictAdministrationOption> facilityCatalog = [], missionCatalog = [];
    private IReadOnlyList<DistrictAdministrationOption> facilities = [], missions = [];

    internal DistrictAdministrationDraft(bool reorganize, DistrictAdministrationRules rules,
        Func<int?, IReadOnlyList<int>, DistrictSettings, DistrictAdministrationSelection> query,
        Func<int?, IReadOnlyList<int>, DistrictSettings, int?, bool, (DistrictSettings Settings, int? LeaderId)> defaults,
        Func<DistrictSettings, int, IReadOnlyList<int>, DistrictSettings> changePolicy,
        Func<IReadOnlyList<int>, int?, DistrictSettings?, int, DistrictAdministrationSummary> summarize,
        Func<int, PersonnelDevelopmentOfficer> readLeader, Func<int, AreaAdministrationQuery<string>> dissolveConfirmation,
        Func<int?, int?> readIntelligence)
    {
        Reorganize = reorganize; this.rules = rules; ForceId = rules.ForceId; this.query = query;
        this.defaults = defaults; this.changePolicy = changePolicy; this.summarize = summarize;
        this.readLeader = readLeader; this.dissolveConfirmation = dissolveConfirmation; this.readIntelligence = readIntelligence;
        Stage = reorganize ? DistrictAdministrationStage.SelectDistrict : DistrictAdministrationStage.SelectCities;
        Settings = rules.NewSettings; Refresh();
    }
    internal DistrictAdministrationDraft(int forceId, bool reorganize, string missing)
    { ForceId = forceId; Reorganize = reorganize; UnresolvedOwner = missing; }

    internal DistrictAdministrationSelectionSource? SelectionSource { get; init; }
    internal DistrictCitySelectionAdmission QueryCityListSelection(IReadOnlyList<int> selected)
    {
        if (rules is null || query is null || Settings is null)
            return new(false, UnresolvedOwner ?? "district.selection.owner");
        if (selected.Distinct().Count() != selected.Count ||
            selected.Any(id => !rules.Cities.Any(row => row.Id == id && (row.Available == true || IsLockedCity(row)))) ||
            rules.Cities.Any(row => IsLockedCity(row) && !selected.Contains(row.Id)))
            return new(false, "district.selection.City_membership");
        try
        {
            //The child owns its tentative checkboxes. This is the existing
            //captured query only: no Refresh, defaults, Next or draft write.
            var result = query(DistrictId, selected, Settings);
            return new(result.CanConfirmCities == true, result.UnresolvedOwner);
        }
        catch (InvalidDataException error) { return new(false, error.Message); }
    }

    public int ForceId { get; }
    public bool Reorganize { get; }
    public DistrictAdministrationStage Stage { get; private set; }
    public int? DistrictId { get; private set; }
    public int Number => SelectedDistrict?.Number ?? rules?.FirstAvailableNumber ?? 0;
    public DistrictAdministrationDistrict? SelectedDistrict => rules?.Districts.FirstOrDefault(row => row.Id == DistrictId);
    public IReadOnlyList<DistrictAdministrationDistrict> Districts => rules?.Districts ?? [];
    public IReadOnlyList<DistrictAdministrationCity> Cities => rules?.Cities ?? [];
    public IReadOnlyList<int> SelectedCityIds => cities;
    public IReadOnlyList<int> LockedSelectedCityIds => SelectedDistrict?.CityIds
        .Where(id => Cities.Any(row => row.Id == id && IsLockedCity(row))).ToArray() ?? [];
    public bool AnyEditableSelected => Cities.Any(row => row.Available == true && cities.Contains(row.Id));
    public bool CanConfirmCities => cities.Length > 0 && selection?.CanConfirmCities == true;
    public bool CanContinueCities => cities.Length > 0 && selection?.CanContinueCities == true;
    public int? LeaderId { get; private set; }
    public DistrictSettings? Settings { get; private set; }
    public IReadOnlyList<DistrictAdministrationOption> Policies { get; internal init; } = [];
    private IReadOnlyList<DistrictAdministrationOption> priorityCatalog = [], resourceCatalog = [], priorities = [], resourceChoices = [];
    public IReadOnlyList<DistrictAdministrationOption> Priorities
    { get => priorities; internal init { priorityCatalog = value; RefreshChoices(); } }
    public IReadOnlyList<DistrictAdministrationOption> ResourceUsageChoices
    { get => resourceChoices; internal init { resourceCatalog = value; RefreshChoices(); } }
    public IReadOnlyList<DistrictAdministrationOption> Facilities
    { get => facilities; internal init { facilityCatalog = value; RefreshChoices(); } }
    public IReadOnlyList<DistrictAdministrationOption> Missions
    { get => missions; internal init { missionCatalog = value; RefreshChoices(); } }
    public bool FacilitiesEditable => Settings is { PolicyId: not 1 };
    public bool MissionsEditable => Settings is { PolicyId: not 1, Military.Enabled: true };
    public bool CanAllowAttack => Settings?.PolicyId is 1 or 2 or 3;
    public bool CanDisallowAttack => Settings?.PolicyId is 4 or 5;
    public bool CanAllowTransport => Settings?.PolicyId is 1 or 2 or 3 or 5;
    public bool CanDisallowTransport => Settings?.PolicyId is 2 or 3 or 4;
    public bool CanDisallowRecruitOfficers => Settings is { PolicyId: not 1 };
    public bool CanDisallowRecruitTroops => Settings is { PolicyId: not 1, PriorityId: not 4 };
    public bool CanDisallowMerchant => Settings is { PolicyId: not 1 };
    public bool CanChangePriority => Settings is { PolicyId: not 1 };
    public bool CanDisallowDemolition => Settings is { PolicyId: not 1 };
    public bool CanDisableMilitary => Settings is { PolicyId: not 1 };
    public bool CanEditSettings => Settings is not null && Stage == DistrictAdministrationStage.Settings && SelectedDistrict?.IsPrimary != true;
    public bool CanEditLeader => CanEditSettings && Number != 1 && leaderCards.Count > 1;
    public bool CanEditTarget => CanEditSettings && Settings?.PolicyId is 2 or 3 or 5;
    public bool CanEditIntelligence => CanEditSettings && Settings?.Military.Enabled == true && MaximumMilitaryIntelligenceTransfer > 0;
    public IReadOnlyList<PersonnelDevelopmentOfficer> Leaders => leaderCards;
    public IReadOnlyList<DistrictAdministrationTarget> Targets => selection?.Targets ?? [];
    public DistrictAdministrationSummary? Summary { get; private set; }
    public string? UnresolvedOwner { get; private set; }
    public string? LastError { get; private set; }
    public int MilitaryIntelligenceTransfer { get; private set; }
    public int? IntelligenceBefore => intelligenceBefore;
    public int? IntelligenceAfter => intelligenceBefore + TransferCost;
    public int TransferCost => Settings?.Military.Enabled == true ? MilitaryIntelligenceTransfer : 0;
    public int? MaximumMilitaryIntelligenceTransfer => selection?.MaximumMilitaryIntelligenceTransfer ?? rules?.MaximumMilitaryIntelligenceTransfer;
    public bool CanConfirm => UnresolvedOwner is null && Settings is not null && cities.Length > 0 &&
        selection?.CanSubmit == true && LeaderId is int leader && selection.Leaders.Any(row => row.Id == leader) &&
        (Reorganize ? DistrictId is not null : Number is >= 1 and <= 8) &&
        (TransferCost == 0 || MaximumMilitaryIntelligenceTransfer is int max && TransferCost <= max);
    public bool CanContinue => CanConfirm && selection?.CanContinue == true;
    public bool CanDissolve => Reorganize && SelectedDistrict?.CanDissolve == true;
    public DistrictMilitaryIntelligenceAmountOptions QueryMilitaryIntelligenceAmountOptions()
    {
        int? maximum = MaximumMilitaryIntelligenceTransfer;
        if (maximum is not int max) return new(MilitaryIntelligenceTransfer, null, null, [], false, "district.page.intelligence_capacity");
        int[] values = max <= 1000 ? [max, 0, 0] : [max * 25 / 100 / 10 * 10, max * 50 / 100 / 10 * 10, max / 10 * 10];
        return new(MilitaryIntelligenceTransfer, 1, max, values, max >= 1, null);
    }

    public bool SelectDistrict(int districtId)
    {
        var district = rules?.Districts.FirstOrDefault(row => row.Id == districtId);
        if (!Reorganize || district is null) return false;
        DistrictId = district.Id; cities = district.CityIds.ToImmutableArray(); LeaderId = district.LeaderId;
        Settings = district.Settings; MilitaryIntelligenceTransfer = 0; initialCities = false;
        Stage = DistrictAdministrationStage.SelectCities; Refresh(); return true;
    }
    public bool SelectCities(IReadOnlyList<int> cityIds)
    {
        if (rules is null || cityIds.Distinct().Count() != cityIds.Count ||
            cityIds.Any(id => !rules.Cities.Any(row => row.Id == id && (row.Available == true || IsLockedCity(row)))) ||
            rules.Cities.Any(row => IsLockedCity(row) && !cityIds.Contains(row.Id))) return false;
        cities = cityIds.ToImmutableArray(); Refresh(); return true;
    }
    public bool ToggleCity(int cityId) => SelectCities(cities.Contains(cityId)
        ? cities.Where(id => id != cityId).ToArray() : cities.Add(cityId));
    public void ClearCities() { cities = LockedSelectedCityIds.ToImmutableArray(); Refresh(); }
    public bool SelectAllCities() => SelectCities(Cities.Where(row => row.Available == true || IsLockedCity(row)).Select(row => row.Id).ToArray());
    public bool SetSettings(DistrictSettings settings)
    {
        if (rules is null) return false;
        if (Settings is { } prior && prior.PolicyId != settings.PolicyId) return SetPolicy(settings.PolicyId);
        Settings = settings.PriorityId == 4 ? settings with { RecruitTroops = true } : settings;
        Refresh(); return true;
    }
    public bool SetPolicy(int policyId)
    {
        if (Settings is null || changePolicy is null || policyId is < 1 or > 5) return false;
        if (Settings.PolicyId == policyId) return true;
        var changed = ArmyCampaignRuleAdapter.ApplyDistrictAdministrationPolicyFields(Settings, policyId);
        try { Settings = changePolicy(changed, policyId, cities); Refresh(); return true; }
        catch (InvalidDataException error)
        {
            Settings = changed with { Target = null };
            Refresh(); LastError = error.Message; return true;
        }
    }
    public bool SetMilitaryIntelligenceTransfer(int amount)
    {
        if (amount < 0 || amount != 0 && (MaximumMilitaryIntelligenceTransfer is not int max || amount > max)) return false;
        MilitaryIntelligenceTransfer = amount; RefreshSummary(); return true;
    }
    public bool SelectLeader(int officerId)
    {
        if (selection?.Leaders.Any(row => row.Id == officerId) != true) return false;
        LeaderId = officerId; Stage = DistrictAdministrationStage.Settings; RefreshSummary(); return true;
    }
    public bool SelectTarget(DistrictTarget target)
    {
        if (Settings is null || !Targets.Any(row => row.Target == target && row.Available == true)) return false;
        Settings = Settings with { Target = target }; Stage = DistrictAdministrationStage.Settings; Refresh(); return true;
    }
    public bool BeginCitySelection() { if (rules is null) return false; Stage = DistrictAdministrationStage.SelectCities; return true; }
    public bool BeginLeaderSelection() { if (selection is null) return false; Stage = DistrictAdministrationStage.SelectLeader; return true; }
    public bool BeginTargetSelection()
    { if (Settings?.PolicyId is not (2 or 3 or 5)) return false; Stage = DistrictAdministrationStage.SelectTarget; return true; }
    public bool Next()
    {
        if (Stage == DistrictAdministrationStage.SelectCities && CanConfirmCities && Settings is not null && defaults is not null)
        {
            try
            {
                var prepared = defaults(DistrictId, cities, Settings, LeaderId, initialCities && !Reorganize);
                Settings = prepared.Settings; LeaderId = prepared.LeaderId; initialCities = false;
                Stage = Reorganize && SelectedDistrict?.IsPrimary == true
                    ? DistrictAdministrationStage.Ready : DistrictAdministrationStage.Settings;
                Refresh(); return true;
            }
            catch (InvalidDataException error) { LastError = error.Message; return false; }
        }
        if (Stage == DistrictAdministrationStage.Settings && CanConfirm) { Stage = DistrictAdministrationStage.Ready; return true; }
        return false;
    }
    public bool Back()
    {
        switch (Stage)
        {
            case DistrictAdministrationStage.SelectLeader:
            case DistrictAdministrationStage.SelectTarget:
            case DistrictAdministrationStage.Ready: Stage = DistrictAdministrationStage.Settings; return true;
            case DistrictAdministrationStage.Settings:
                if (Reorganize) { Stage = DistrictAdministrationStage.SelectDistrict; return true; }
                cities = []; initialCities = !Reorganize; Stage = DistrictAdministrationStage.SelectCities; Refresh(); return true;
            case DistrictAdministrationStage.SelectCities when Reorganize:
                Stage = DistrictAdministrationStage.SelectDistrict; return true;
            default: return false;
        }
    }
    public GameOrder? BuildOrder() => !CanConfirm || Settings is null || LeaderId is not int leader ? null
        : Reorganize ? new ReorganizeDistrictOrder(ForceId, DistrictId!.Value, cities, leader, Settings)
            { MilitaryIntelligenceTransfer = MilitaryIntelligenceTransfer }
        : new CreateDistrictOrder(ForceId, cities, leader, Settings) { MilitaryIntelligenceTransfer = MilitaryIntelligenceTransfer };
    public GameOrder? BuildDissolveOrder() => CanDissolve ? new DissolveDistrictOrder(ForceId, DistrictId!.Value) : null;
    public AreaAdministrationQuery<string> QueryDissolveConfirmation() => CanDissolve && dissolveConfirmation is not null
        ? dissolveConfirmation(DistrictId!.Value) : AreaAdministrationQuery<string>.Unknown("district.page.dissolve_selection");

    public int? ResolveCityAtHex(int physicalHex) => Cities.FirstOrDefault(row => row.HexId == physicalHex)?.Id;
    public int? ResolveDistrictAtHex(int physicalHex) => Cities.FirstOrDefault(row => row.HexId == physicalHex)?.DistrictId;
    public DistrictTarget? ResolveTargetAtHex(int physicalHex) => Targets.FirstOrDefault(row => row.MapHexIds.Contains(physicalHex))?.Target;
    public bool CanPickMapHex(int physicalHex) => Stage switch
    {
        DistrictAdministrationStage.SelectDistrict => ResolveDistrictAtHex(physicalHex) is int id && Districts.Any(row => row.Id == id),
        DistrictAdministrationStage.SelectCities => Cities.Any(row => row.HexId == physicalHex && row.Available == true),
        DistrictAdministrationStage.SelectTarget => Targets.Any(row => row.MapHexIds.Contains(physicalHex) && row.Available == true),
        _ => false
    };
    private void Refresh()
    {
        LastError = null;
        if (rules is null) return;
        UnresolvedOwner = rules.UnresolvedOwner;
        if (Settings is not null && query is not null)
        {
            try
            {
                selection = query(DistrictId, cities, Settings);
                UnresolvedOwner = selection.UnresolvedOwner ?? UnresolvedOwner;
                leaderCards = readLeader is null ? [] : selection.Leaders.Select(row => readLeader(row.Id)).ToArray();
            }
            catch (InvalidDataException error) { selection = null; leaderCards = []; UnresolvedOwner = error.Message; }
        }
        else { selection = null; leaderCards = []; UnresolvedOwner ??= "district.page.settings_owner"; }
        RefreshSummary();
        RefreshChoices();
    }
    private void RefreshSummary()
    {
        if (summarize is not null) Summary = summarize(cities, LeaderId, Settings, Number);
        intelligenceBefore = selection?.MilitaryIntelligenceBefore ?? readIntelligence?.Invoke(DistrictId);
    }
    private void RefreshChoices()
    {
        facilities = facilityCatalog.Select(row => row with { Available = FacilitiesEditable }).ToArray();
        priorities = priorityCatalog.Select(row => row with { Available = Settings is not null && (row.Id == 1 || CanChangePriority) }).ToArray();
        resourceChoices = resourceCatalog.Select(row => row with { Available = Settings is not null && (row.Id == 0 || Settings.PolicyId != 1) }).ToArray();
        missions = missionCatalog.Select(row => row with
        { Available = MissionsEditable && (row.Id == 0 || Settings?.PolicyId is 2 or 3) }).ToArray();
    }
    private bool IsLockedCity(DistrictAdministrationCity row) => SelectedDistrict is { IsPrimary: true } selected &&
        row.Available == false && selected.CityIds.Contains(row.Id);
}
