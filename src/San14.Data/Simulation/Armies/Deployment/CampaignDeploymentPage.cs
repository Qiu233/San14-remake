using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed record CampaignProvinceChoice(int Id, string Name, bool? Available,
    int? HintMessageId, string? UnresolvedOwner);
public sealed record CampaignDistrictChoice(int Id, string Name, bool? Available,
    int? HintMessageId, string? UnresolvedOwner);
public sealed record CampaignFootholdChoice(int Id, string Name, int? HexId, bool? Available,
    int? HintMessageId, int? ArmyCount, int? Troops, string? UnresolvedOwner);
public sealed record CampaignTroopSegment(int ForceId, int Troops, double Fraction, byte? ForceColor49);
public sealed record CampaignCommanderPreview(int OfficerId, string Name, int Troops,
    OfficerPortraitRoute? FacePortrait, string? UnresolvedOwner);
public sealed record CampaignDeploymentBar(int? OurTroops, int? OtherTroops,
    ImmutableArray<CampaignTroopSegment> Segments, ImmutableArray<CampaignCommanderPreview> Commanders,
    string? UnresolvedOwner)
{
    public byte? PlayerForceColor49 { get; init; }
    public string? AdditionalArmiesText { get; init; }
}
public sealed record CampaignDeploymentConfirmation(int? PayerDistrictId, int? CommandBooks,
    int? CommandBookCost, int? CommandBooksAfter, string? Text, bool CanConfirm, string? UnresolvedOwner);

/// <summary>A single page's detached current inputs and lazy candidate caches.
/// Hover lookups never capture a world or build deployment packets.</summary>
public sealed class CampaignDeploymentPage
{
    private readonly ICampaignDeploymentPageSource source;
    internal CampaignDeploymentPage(ICampaignDeploymentPageSource source) => this.source = source;
    public int ForceId => source.ForceId;
    public bool Available => UnresolvedOwner is null;
    public string? UnresolvedOwner => source.UnresolvedOwner;
    public IReadOnlyList<CampaignProvinceChoice> QueryTargets() => source.Targets();
    public IReadOnlyList<CampaignDistrictChoice> QueryDistricts(int provinceId) => source.Districts(provinceId);
    public IReadOnlyList<CampaignFootholdChoice> QueryFootholds(int provinceId, int districtId) => source.Footholds(provinceId, districtId);
    public IReadOnlyList<CampaignFootholdChoice> QueryTargetMapFootholds() => source.TargetMapFootholds();
    public IReadOnlyList<CampaignFootholdChoice> QueryDistrictMapFootholds(int provinceId) => source.DistrictMapFootholds(provinceId);
    public int? ResolveProvinceAtHex(int hexId) => source.ProvinceAtHex(hexId);
    public int? ResolveProvinceFocusAtHex(int hexId) => source.ProvinceFocusAtHex(hexId);
    public int? ResolveDistrictAtHex(int hexId) => source.DistrictAtHex(hexId);
    public int? ResolveDistrictFocusAtHex(int hexId) => source.DistrictFocusAtHex(hexId);
    public int? ResolveFootholdAtHex(int hexId) => source.FootholdAtHex(hexId);
    public bool? CanPickMapHex(int physicalHex) => source.CanPickMapHex(physicalHex);
    public CampaignDeploymentDraft CreateDraft(int provinceId, int districtId) => new(this, source, provinceId, districtId);
    public CampaignDeploymentBar QueryBar(int provinceId, CampaignDeploymentDraft? draft = null)
        => source.Bar(provinceId, draft?.Armies ?? []);
    public CampaignDeploymentConfirmation QueryConfirmation(CampaignDeploymentDraft draft) => source.Confirmation(draft.Armies);
}

public sealed class CampaignDeploymentDraft
{
    private readonly ICampaignDeploymentPageSource source;
    private readonly List<(int Source, ImmutableArray<CampaignArmyDeployment> Armies)> selected = [];
    private DeploymentNarrationQuery? handover;
    public CampaignDeploymentPage Page { get; }
    public int ProvinceId { get; }
    public int DistrictId { get; }
    public string? UnresolvedOwner { get; private set; }
    public ImmutableArray<int> Sources => selected.Select(row => row.Source).ToImmutableArray();
    public ImmutableArray<CampaignArmyDeployment> Armies => selected.SelectMany(row => row.Armies).ToImmutableArray();
    internal CampaignDeploymentDraft(CampaignDeploymentPage page, ICampaignDeploymentPageSource source,
        int provinceId, int districtId)
    { Page = page; this.source = source; ProvinceId = provinceId; DistrictId = districtId; }
    public bool ToggleSource(int locationId)
    {
        int index = selected.FindIndex(row => row.Source == locationId);
        if (index >= 0) { selected.RemoveAt(index); handover = null; UnresolvedOwner = null; return true; }
        try
        {
            var packets = source.Prepare(ProvinceId, DistrictId, locationId);
            if (packets.IsDefaultOrEmpty) return false;
            selected.Add((locationId, packets)); handover = null; UnresolvedOwner = null; return true;
        }
        catch (InvalidDataException error) { UnresolvedOwner = error.Message; return false; }
    }
    public LaunchCampaignOrder? BuildOrder() => UnresolvedOwner is null && selected.Count != 0
        ? new(Page.ForceId, ProvinceId, DistrictId, Armies) : null;
    public DeploymentNarrationQuery PrepareHandover(string locale, Func<int, int> appRandom)
        => handover ??= source.Handover(ProvinceId, Armies, locale, appRandom);
}

internal interface ICampaignDeploymentPageSource
{
    int ForceId { get; }
    string? UnresolvedOwner { get; }
    IReadOnlyList<CampaignProvinceChoice> Targets();
    IReadOnlyList<CampaignDistrictChoice> Districts(int province);
    IReadOnlyList<CampaignFootholdChoice> Footholds(int province, int district);
    IReadOnlyList<CampaignFootholdChoice> TargetMapFootholds();
    IReadOnlyList<CampaignFootholdChoice> DistrictMapFootholds(int province);
    int? ProvinceAtHex(int hex);
    int? ProvinceFocusAtHex(int hex);
    int? DistrictAtHex(int hex);
    int? DistrictFocusAtHex(int hex);
    int? FootholdAtHex(int hex);
    bool? CanPickMapHex(int hex);
    ImmutableArray<CampaignArmyDeployment> Prepare(int province, int district, int location);
    CampaignDeploymentBar Bar(int province, IReadOnlyList<CampaignArmyDeployment> armies);
    CampaignDeploymentConfirmation Confirmation(IReadOnlyList<CampaignArmyDeployment> armies);
    DeploymentNarrationQuery Handover(int province, IReadOnlyList<CampaignArmyDeployment> armies, string locale, Func<int, int> appRandom);
}
