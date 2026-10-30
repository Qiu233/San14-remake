using San14.Core;

namespace San14.Data;

public sealed record SceneryInvestmentCityRow(int CityId, string Name, int Gold);
public sealed record SceneryInvestmentSiteRow(int SceneryId, string Name, int Level, int GoldCost, int Duration)
{
    public IReadOnlyList<string>? Cells { get; init; }
    public bool? CanSelect { get; init; }
    public string? UnresolvedOwner { get; init; }
}

public sealed record SceneryInvestmentPage(int ForceId, int DistrictId, int CityId, int? SceneryId)
{
    public string CityName { get; init; } = "";
    public string SceneryName { get; init; } = "";
    public string ProvinceName { get; init; } = "";
    public int? CurrentLevel { get; init; }
    public int? NextLevel { get; init; }
    public int? CurrentEffect { get; init; }
    public int? NextEffect { get; init; }
    public int? EffectKind { get; init; }
    public int? Duration { get; init; }
    public int? CityGold { get; init; }
    public int? AfterGold { get; init; }
    public int? GoldDelta { get; init; }
    public int? Books { get; init; }
    public int? AfterBooks { get; init; }
    public int? BookDelta { get; init; }
    public int? SceneryIllustrationIndex { get; init; }
    public bool? CanConfirm { get; init; }
    public bool? CanContinue { get; init; }
    public bool? CanSelectScenery { get; init; }
    public string? UnresolvedOwner { get; init; }
    public DetailMiniMapValues? MiniMap { get; init; }
}

/// <summary>One command page's selected City and Scenery. All numerical and
/// eligibility inputs come from the same immutable business baseline.</summary>
public sealed class SceneryInvestmentDraft
{
    private readonly ArmyCampaignRuleAdapter owner;
    internal SceneryInvestmentDraft(ArmyCampaignRuleAdapter owner, GameState state, int forceId,
        int districtId, int? cityId, int? sceneryId)
    {
        this.owner = owner; SourceState = state; ForceId = forceId; DistrictId = districtId;
        CityId = cityId ?? 0; SceneryId = sceneryId; Refresh();
    }
    public GameState SourceState { get; }
    public int ForceId { get; }
    public int DistrictId { get; }
    public int CityId { get; private set; }
    public int? SceneryId { get; private set; }
    public SceneryInvestmentPage Page { get; private set; } = null!;
    public IReadOnlyList<SceneryInvestmentCityRow> CityCandidates { get; private set; } = [];
    public IReadOnlyList<SceneryInvestmentSiteRow> SceneryCandidates { get; private set; } = [];
    public string? UnresolvedOwner => Page.UnresolvedOwner;
    public void SelectCity(int cityId) { CityId = cityId; SceneryId = null; Refresh(); }
    public void SelectScenery(int sceneryId) { SceneryId = sceneryId; Refresh(); }
    public InvestSceneryOrder CreateOrder() => Page.CanConfirm == true && SceneryId is int id
        ? new(ForceId, id) : throw new InvalidOperationException(Page.UnresolvedOwner ?? "No eligible scenery investment is selected.");
    private void Refresh()
    {
        var projection = owner.QuerySceneryInvestmentDraft(SourceState, ForceId, DistrictId, CityId, SceneryId);
        CityCandidates = projection.Cities; SceneryCandidates = projection.Sceneries; Page = projection.Page;
        CityId = Page.CityId; SceneryId = Page.SceneryId;
    }
}

internal sealed record SceneryInvestmentProjection(SceneryInvestmentPage Page,
    IReadOnlyList<SceneryInvestmentCityRow> Cities, IReadOnlyList<SceneryInvestmentSiteRow> Sceneries);
