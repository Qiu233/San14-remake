using San14.Core;

namespace San14.Data;

public sealed record AreaAdministrationQuery<T>(T? Value, string? UnresolvedInput)
{
    public bool Available => UnresolvedInput is null;
    internal static AreaAdministrationQuery<T> Known(T value) => new(value, null);
    internal static AreaAdministrationQuery<T> Unknown(string input) => new(default, input);
}

public sealed record AreaAdministrationDraftResult(bool Available, string? UnresolvedInput = null);
public sealed record AreaAdministrationFocusOption(AreaDevelopmentFocus Focus, int? Progress,
    bool? Enabled, bool Selected, string? UnresolvedInput);
public sealed record AreaAdministrationPanel(int AreaId, int? OfficerId, AreaDevelopmentFocus Focus,
    int? Population, IReadOnlyList<AreaAdministrationFocusOption> FocusOptions,
    AreaAdministrationQuery<int?> OccupationExtent, AreaAdministrationQuery<bool?> HighlightOccupationExtent,
    AreaAdministrationQuery<int?> DevelopmentIncrease, AreaAdministrationQuery<int?> SecurityIncrease,
    AreaAdministrationQuery<int?> GoldBefore, AreaAdministrationQuery<int?> GoldAfter,
    AreaAdministrationQuery<int?> FoodBefore, AreaAdministrationQuery<int?> FoodAfter,
    AreaAdministrationQuery<int?> SoldiersBefore, AreaAdministrationQuery<int?> SoldiersAfter,
    int ChangedAreaCount, int? CommandBooksRequired)
{
    public AreaAdministrationQuery<int?> SecurityBefore { get; init; } = AreaAdministrationQuery<int?>.Unknown("area-draft.security_current");
    public AreaAdministrationQuery<int?> SecurityAfter { get; init; } = AreaAdministrationQuery<int?>.Unknown("area-draft.security_increase_source");
    public AreaAdministrationQuery<AreaAdministrationParentStatus> ParentStatus { get; init; } = AreaAdministrationQuery<AreaAdministrationParentStatus>.Unknown("area-draft.parent_status");
    public AreaAdministrationQuery<AreaAdministrationChildStatus> ChildStatus { get; init; } = AreaAdministrationQuery<AreaAdministrationChildStatus>.Unknown("area-draft.child_status");
}

public sealed record AreaAdministrationAreaRow(int AreaId,
    AreaAdministrationQuery<string> AreaName, AreaAdministrationQuery<int?> CityId,
    AreaAdministrationQuery<string> CityName, AreaAdministrationQuery<int?> OfficerId,
    AreaAdministrationQuery<AreaDevelopmentFocus?> Focus, int? Population, int? Commerce,
    int? Agriculture, int? Barracks, int? Security, bool IsChanged)
{
    public AreaAdministrationQuery<string> OfficerName { get; init; } = AreaAdministrationQuery<string>.Unknown("area-draft.parent_officer_name");
    public AreaAdministrationQuery<int?> LandCurrent { get; init; } = AreaAdministrationQuery<int?>.Unknown("area-draft.land_statistics");
    public AreaAdministrationQuery<int?> LandMaximum { get; init; } = AreaAdministrationQuery<int?>.Unknown("area-draft.land_statistics");
    public AreaAdministrationParentDevelopment CommerceDisplay { get; init; } = new(null, null, null, "area-draft.development_display");
    public AreaAdministrationParentDevelopment AgricultureDisplay { get; init; } = new(null, null, null, "area-draft.development_display");
    public AreaAdministrationParentDevelopment BarracksDisplay { get; init; } = new(null, null, null, "area-draft.development_display");
}
public sealed record AreaAdministrationParentDevelopment(int? Level, int? Experience,
    bool? UsesSourceUnknownText, string? UnresolvedInput);
public sealed record AreaAdministrationCommandBooks(int? DistrictId,
    AreaAdministrationQuery<int?> Current, AreaAdministrationQuery<long?> Reserved,
    AreaAdministrationQuery<long?> Unreserved, AreaAdministrationQuery<int?> Required,
    AreaAdministrationQuery<int?> Difference)
{
    public AreaAdministrationQuery<int?> SourceHeaderAfter { get; init; } = AreaAdministrationQuery<int?>.Unknown("area-draft.current_command_books");
    public int SourceHeaderDifference { get; init; }
}
public sealed record AreaAdministrationParentStatus(int CityId, bool HasChanges,
    int ChangedAreaCount, AreaAdministrationCommandBooks CommandBooks);
public sealed record AreaAdministrationChildStatus(int AreaId,
    AreaAdministrationQuery<bool?> CanAccept, AreaAdministrationQuery<bool?> CanDismiss);

public enum AreaAdministrationSortColumn
{
    Place, AreaName, Officer, CommerceLevel, CommerceExperience, AgricultureLevel,
    AgricultureExperience, BarracksLevel, BarracksExperience, Security, OwnedLand, Focus
}
public enum AreaAdministrationPlaceKind { Hidden, City, Gate, Port }
public sealed record AreaAdministrationMapTarget(int? AreaId, bool IsHeld);

public sealed record AreaAdministrationParentEconomy(int CityId, long Revision,
    AreaAdministrationQuery<int?> CityGold,
    AreaAdministrationQuery<int?> CurrentUpkeepPerPeriod);

public sealed record AreaAdministrationDevelopmentValues(
    AreaAdministrationQuery<int?> CommerceIncrease, AreaAdministrationQuery<int?> AgricultureIncrease,
    AreaAdministrationQuery<int?> BarracksIncrease, AreaAdministrationQuery<int?> SecurityIncrease);
public sealed record AreaAdministrationDevelopmentPreview(int AreaId, long Revision,
    AreaAdministrationDevelopmentValues Normal, AreaAdministrationDevelopmentValues FullEffort);
