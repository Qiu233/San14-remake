using System.Collections.Immutable;

namespace San14.Data;

/// <summary>Current person information shared by the normal details host and
/// source-time recruitment details. Tab changes only consume this publication.</summary>
public sealed record OfficerDetailPublication(int OfficerId, int PhysicalSlot, string Name,
    OfficerPortraitRoute Portrait, OfficerDetailOverview Overview, OfficerDetailMilitary Military,
    OfficerDetailRelations Relations, ImmutableArray<AreaAdministrationOfficerAbility> Abilities,
    ImmutableArray<OfficerDetailTrait> Traits, ImmutableArray<string> UnresolvedOwners)
{
    /// <summary>The current Person's actual detail-page count. Source15E
    /// bit14 removes the third page; absent knowledge must not imply three.</summary>
    public AreaAdministrationQuery<int> PageCount { get; init; }
        = AreaAdministrationQuery<int>.Unknown("officer-detail.page_count");
    public AreaAdministrationQuery<DeploymentNarrationPortraitRoute> StandingPortrait { get; init; }
        = AreaAdministrationQuery<DeploymentNarrationPortraitRoute>.Unknown("officer-detail.standing_portrait");
    public AreaAdministrationQuery<AreaAdministrationOfficerFlag> ForceFlag { get; init; }
        = AreaAdministrationQuery<AreaAdministrationOfficerFlag>.Unknown("officer-detail.force_flag");
    public OfficerDetailPriority Priority { get; init; } = new(null, [], "officer-detail.priority");
    public AreaAdministrationQuery<DetailMiniMapValues?> MiniMap { get; init; }
        = AreaAdministrationQuery<DetailMiniMapValues?>.Unknown("officer-detail.minimap");
}

public sealed record OfficerDetailPriorityChoice(int Value, string Caption, bool Enabled);
public sealed record OfficerDetailPriority(int? SelectedValue,
    ImmutableArray<OfficerDetailPriorityChoice> Choices, string? UnresolvedOwner)
{
    public bool Visible { get; init; } = true;
}

public enum OfficerDetailEntityKind { Force, District, City, Gate, Area, Army, Person, Policy, Doctrine }
public sealed record OfficerDetailEntity(OfficerDetailEntityKind Kind, int? Id, string Name);

public sealed record OfficerDetailOverview(
    AreaAdministrationQuery<int?> Age,
    AreaAdministrationQuery<string?> Sex,
    AreaAdministrationQuery<string?> Health,
    AreaAdministrationQuery<OfficerDetailEntity?> Force,
    AreaAdministrationQuery<OfficerDetailEntity?> District,
    AreaAdministrationQuery<string?> ServiceStatus,
    AreaAdministrationQuery<OfficerDetailEntity?> Home,
    AreaAdministrationQuery<OfficerDetailEntity?> Location,
    AreaAdministrationQuery<string?> Office,
    AreaAdministrationQuery<int?> CommandCapacity,
    AreaAdministrationQuery<int?> Salary,
    AreaAdministrationQuery<int?> Loyalty,
    AreaAdministrationQuery<int?> ServiceYears,
    AreaAdministrationQuery<int?> ItemCount,
    AreaAdministrationQuery<string?> MeritRank,
    AreaAdministrationQuery<int?> Merit,
    AreaAdministrationQuery<string?> Task,
    AreaAdministrationQuery<string?> TaskTarget,
    AreaAdministrationQuery<string?> TaskDuration,
    AreaAdministrationQuery<string?> GovernorPriority,
    AreaAdministrationQuery<string?> DeploymentPriority)
{
    public AreaAdministrationQuery<int?> HealthCode { get; init; }
        = AreaAdministrationQuery<int?>.Unknown("officer-detail.health_code");
    public AreaAdministrationQuery<bool?> CommandEmphasized { get; init; }
        = AreaAdministrationQuery<bool?>.Unknown("officer-detail.command_emphasis");
    public AreaAdministrationQuery<bool?> LoyaltyVisible { get; init; }
        = AreaAdministrationQuery<bool?>.Unknown("officer-detail.loyalty_visibility");
    public AreaAdministrationQuery<bool?> MeritVisible { get; init; }
        = AreaAdministrationQuery<bool?>.Unknown("officer-detail.merit_visibility");
    public AreaAdministrationQuery<string?> CourtesyName { get; init; }
        = AreaAdministrationQuery<string?>.Unknown("officer-detail.courtesy_name");
}

public sealed record OfficerDetailTrait(int Slot, int? LookupId, int? PhysicalId,
    string? Name, byte? Style, AreaAdministrationQuery<string?> Description, string? UnresolvedOwner);

public sealed record OfficerDetailTactic(int Slot, int? TacticId, string? Name,
    ImmutableArray<string> Effects, string? UnresolvedOwner);
public sealed record OfficerDetailFormation(int FormationId, string Name,
    AreaAdministrationQuery<bool?> Available);
public sealed record OfficerDetailMilitary(
    AreaAdministrationQuery<OfficerDetailEntity?> Doctrine,
    AreaAdministrationQuery<OfficerDetailEntity?> Policy,
    AreaAdministrationQuery<int?> PolicyCost,
    ImmutableArray<OfficerDetailTactic> Tactics,
    ImmutableArray<OfficerDetailFormation> Formations)
{
    public ImmutableArray<string> UnresolvedOwners { get; init; } = [];
    public AreaAdministrationQuery<int?> PolicyCostBeforeDiscount { get; init; }
        = AreaAdministrationQuery<int?>.Unknown("officer-detail.policy_cost_before_discount");
}

public sealed record OfficerDetailRelatedPerson(int? OfficerId, string Name,
    OfficerPortraitRoute? Portrait, string? UnresolvedOwner);
public sealed record OfficerDetailRelations(
    OfficerDetailRelatedPerson Father, OfficerDetailRelatedPerson Mother,
    OfficerDetailRelatedPerson Spouse, ImmutableArray<OfficerDetailRelatedPerson> SwornBrothers,
    ImmutableArray<OfficerDetailRelatedPerson> Likes, ImmutableArray<OfficerDetailRelatedPerson> Dislikes,
    AreaAdministrationQuery<string?> Biography)
{
    public ImmutableArray<string> UnresolvedOwners { get; init; } = [];
}
