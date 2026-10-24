using San14.Core;

namespace San14.Data;

public sealed record UnitSourceIdentity(int CommanderId, string? CommanderName,
    int? CurrentHexId, int? ForceLookupId, int? ForcePhysicalSlot, string? ForceName,
    int? DistrictId, int? DistrictNumber, bool? NameplateVisible);
public sealed record UnitSourceFormation(int? CurrentFormationId, int? LandFormationId,
    int? NavalFormationId, bool? LandSelected, DeploymentFormationPresentation? Selected);
public sealed record UnitSourceResources(int? ReadyTroops, int? WoundedTroops,
    int? CurrentMorale, int? MaximumMorale);
/// <summary>Source counters, not guessed days or already formatted labels.</summary>
public sealed record UnitSourceAbnormalCounts(int Confusion, int Immobilization, int Provocation);
/// <summary>False means ordinary-army cargo does not apply; null means unknown.</summary>
public sealed record UnitSourceCargo(bool? IsApplicable, int? Gold, int? Food);

/// <summary>A finite read-only publication for one existing army. Missing keys
/// belong to individual groups and never erase unrelated known values.</summary>
public sealed record UnitSourceDetails(int UnitId, bool? Exists, UnitSourceIdentity? Identity,
    UnitSourceFormation? Formation, UnitSourceResources Resources,
    UnitSourceAbnormalCounts? AbnormalCounts, UnitSourceCargo Cargo,
    DeploymentNarrationPortraitRoute? PortraitRoute,
    DeploymentCombatAxes? WithoutConditionalModifiers, DeploymentCombatAxes? WithConditionalModifiers,
    IReadOnlyDictionary<string, string> Missing);

/// <summary>Selected existing armies in caller row order. All rows borrow one
/// current source capture; missing groups remain attached to their own row.</summary>
public sealed record UnitSourceDetailsList(IReadOnlyList<UnitSourceDetails> Rows, string? UnresolvedInput);
