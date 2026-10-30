using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public enum CampaignDeploymentMapStage { TargetProvince, District, Foothold }

/// <summary>A captured existing Foothold's temporary campaign presentation.
/// Location and physical receiver identity remain separate for City/Gate.</summary>
public sealed record CampaignMapMarkerPresentation(BaseKind Kind, int PhysicalSlot,
    int LocationId, int HexId, bool Enabled, int? DistrictNumber, byte? ForceColor49)
{
    public CampaignDistrictLeaderFace? DistrictLeaderFace { get; init; }
}

public sealed record CampaignDistrictLeaderFace(int OfficerId, ushort AppearanceIndex, OfficerPortraitRoute Portrait);

public sealed record CampaignMapPresentation(CampaignDeploymentMapStage Stage,
    ImmutableArray<CampaignMapMarkerPresentation> Markers);

public sealed record CampaignDeploymentHint(int? MessageId, string? UnresolvedOwner);

public sealed partial class CampaignDeploymentPage
{
    /// <summary>Prepared once at an actual stage boundary, over this page's
    /// existing current. Unavailable markers stay present with Enabled=false.</summary>
    public AreaAdministrationQuery<CampaignMapPresentation> QueryMapPresentation(
        CampaignDeploymentMapStage stage, int? provinceId = null, int? districtId = null)
    {
        try { return AreaAdministrationQuery<CampaignMapPresentation>.Known(source.MapPresentation(stage, provinceId, districtId)); }
        catch (InvalidDataException error) { return AreaAdministrationQuery<CampaignMapPresentation>.Unknown(error.Message); }
    }

    public CampaignDeploymentHint QueryHoverHint(CampaignDeploymentMapStage stage,
        int? provinceId, int? districtId, int? actualIdentity)
        => source.HoverHint(stage, provinceId, districtId, actualIdentity);
}
