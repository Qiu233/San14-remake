using System.Collections.ObjectModel;

namespace San14.Data;

/// <summary>One page publication. Hover and selection read ByForceId; neither
/// operation captures or scans the world again. Null fields mean unavailable
/// source input; empty text is a known original empty result. ForceValid=false
/// represents the original cleared info panel, not missing numeric zeroes.</summary>
public sealed record NewGameForceSelectionPreview(
    IReadOnlyList<NewGameForcePreview> Rows,
    IReadOnlyDictionary<int, NewGameForcePreview> ByForceId,
    IReadOnlyList<NewGameForceBaseMarker> BaseMarkers,
    int? DefaultForceId,
    IReadOnlyList<string> UnresolvedInputs);

public sealed record NewGameForcePreview(int ForceId)
{
    public bool? ForceValid { get; init; }
    public int? RulerId { get; init; }
    /// <summary>Actual Person-to-Force receiver used by avatar click/hover.</summary>
    public int? RulerForceId { get; init; }
    public bool? RulerValid { get; init; }
    public string? RulerName { get; init; }
    /// <summary>Null with RulerValid=false is a known hidden portrait.</summary>
    public int? PortraitId { get; init; }
    public int? ReportColorIndex { get; init; }
    public DetailForceAppearance? FlagAppearance { get; init; }
    /// <summary>2AD190's one-UTF16-unit force identifier, not a fabricated long name.</summary>
    public string? ForceDisplayName { get; init; }
    public int? RankId { get; init; }
    public string? RankName { get; init; }
    public int? DoctrineId { get; init; }
    public string? DoctrineName { get; init; }
    public int? CityCount { get; init; }
    public int? ActiveOfficerCount { get; init; }
    public int? Gold { get; init; }
    public int? Food { get; init; }
    public int? Troops { get; init; }
    public string? ScenarioAnnotation { get; init; }
    public string? HeavenTiming { get; init; }
    public int? GeographicAdvantageCount { get; init; }
    public IReadOnlyList<NewGameForceGeographicAdvantage>? GeographicAdvantages { get; init; }
    public IReadOnlyList<NewGameForceBaseMarker>? Bases { get; init; }
    public IReadOnlyDictionary<string, string> UnresolvedInputs { get; init; } =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
    //521110/5569D0 do not gate these on unrelated display operands.
    public bool? CanConfirm => ForceValid;
    public bool? CanOpenGovernment => ForceValid;
    public bool? CanOpenOfficers => ForceValid;
    public bool? CanOpenGeographicAdvantages => GeographicAdvantageCount is int count ? count > 0 : null;
}

/// <summary>Province/State, not one of the12 larger Regions. Preserves the
/// actual qualification and current owned-city count used by521110.</summary>
public sealed record NewGameForceGeographicAdvantage(int ProvinceId, string? ProvinceName,
    int AdvantageId, string? AdvantageName, int RequiredCities, int OwnedCities);

/// <summary>Actual registered Base identity and current affiliation. Null owner
/// or position is unresolved and must not be converted into unaffiliated/Hex0.</summary>
public sealed record NewGameForceBaseMarker(BaseKind Kind, int BaseId,
    int? ForceId, int? HexId);

/// <summary>Actual application owners for358110. The ordinary new-game host
/// supplies false/null/false from its real route; Initializing alone proves
/// none of these. A missing caller input stays unknown.</summary>
public sealed record NewGameForcePageApplication(bool? TaskScenePresent,
    int? TaskIndex, bool? CccPersonSelectStatePresent);
