using San14.Core;

namespace San14.Data;

public sealed record DistrictLeaderSelection(OfficerInformationPageSet Pages,
    int? InitialLeaderId, string GuideText)
{
    public IReadOnlyList<int> EligibleOfficerIds { get; init; } = [];
    public IReadOnlyDictionary<int, AreaAdministrationQuery<bool>> AlteredHealth { get; init; }
        = new Dictionary<int, AreaAdministrationQuery<bool>>();
}

public sealed record DistrictCitySelectionAdmission(bool CanConfirm, string? UnresolvedOwner);

public sealed record DistrictCitySelection(CityInformationPageSet Pages,
    IReadOnlyList<int> InitialSelection, IReadOnlyList<int> EligibleCityIds,
    IReadOnlyList<int> LockedSelectedCityIds, string GuideText)
{
    internal Func<IReadOnlyList<int>, DistrictCitySelectionAdmission>? ReadAdmission { private get; init; }
    public DistrictCitySelectionAdmission QuerySelection(IReadOnlyList<int> selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        return (ReadAdmission ?? throw new InvalidOperationException("district.selection.admission_owner"))(selected);
    }
}

internal sealed record DistrictAdministrationSelectionSource(GameState State, SourceWorldContext Current);
