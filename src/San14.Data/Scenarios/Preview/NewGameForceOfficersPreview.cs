using San14.Core;

namespace San14.Data;

/// <summary>A single ordinary pre-start subordinate-list publication. Rows
/// always preserves the complete known native iterator sequence. NativeSortedRows
/// is supplied only when every comparison actually reached by472980 is known;
/// missing sort/display inputs do not discard the usable list.</summary>
public sealed record NewGameForceOfficersPreview(int ForceId, bool? ForceValid,
    bool MembershipComplete, IReadOnlyList<NewGameForceOfficerPreview> Rows,
    IReadOnlyList<NewGameForceOfficerPreview>? NativeSortedRows,
    string? SortUnresolvedInput, IReadOnlyList<string> UnresolvedInputs);

public sealed record NewGameOfficerAbilityFigures(int? Leadership, int? Strength,
    int? Intelligence, int? Politics, int? Charm)
{
    internal static NewGameOfficerAbilityFigures From(OfficerAbilities? value) => value is null
        ? new(null, null, null, null, null)
        : new(value.Leadership, value.Strength, value.Intelligence, value.Politics, value.Charm);
}

/// <summary>PhysicalPerson and OfficerId preserve independent current identities.
/// Basic values are explicitly separate from22FE40 effective values. DetailOfficerId
/// is an existing current domain officer route, not a fabricated detail snapshot.</summary>
public sealed record NewGameForceOfficerPreview(int SourceOrdinal, int PhysicalPerson,
    int? OfficerId, string? Name, byte? ServiceStatus, int? OfficeId, string? OfficeName,
    int? OfficeCommandLimit, int? DistrictId, int? LocationId, string? LocationName,
    NewGameOfficerAbilityFigures BaseAbilities, NewGameOfficerAbilityFigures EffectiveAbilities,
    ushort? StoredMeritWord, int? PortraitAppearance, OfficerPortraitRoute? PortraitRoute,
    int? DetailOfficerId, IReadOnlyDictionary<string, string> UnresolvedInputs);
