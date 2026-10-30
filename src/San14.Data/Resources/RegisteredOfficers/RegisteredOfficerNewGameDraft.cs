using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

/// <summary>One new-game management page's reversible adoption and placement
/// choices. It never changes the library or a running campaign.</summary>
public sealed partial class RegisteredOfficerNewGameDraft
{
    public RegisteredOfficerLibrary Library { get; }
    public LoadedScenario Scenario { get; }
    public RegisteredOfficerNewGameSelection Selection { get; private set; }
    public IReadOnlyList<CityPreviewRecord> Cities => Scenario.Snapshot.Cities;
    public int Year => Scenario.Metadata.StartYear ?? throw Missing("scenario_year");
    public bool Timeless => (Scenario.Snapshot.WorldParameters ?? throw Missing("scenario_mode")).RawModeValue == 2;

    public RegisteredOfficerNewGameDraft(RegisteredOfficerLibrary library, LoadedScenario scenario,
        RegisteredOfficerNewGameSelection? initial = null)
    {
        ArgumentNullException.ThrowIfNull(library); ArgumentNullException.ThrowIfNull(scenario);
        Library = library; Scenario = scenario; Selection = initial ?? RegisteredOfficerNewGameSelection.None;
        Selection.Validate(library);
        foreach (var placement in Selection.Placements) RequireCity(placement.CityId);
    }

    public int Age(int officerId) => Math.Max(0, Year - Officer(officerId).BirthYear + 1);
    public bool IsAdult(int officerId) => Timeless || Age(officerId) > 14;
    public bool IsRuler(int officerId) => Selection.NewForces.Any(force => force.RulerOfficerId == officerId);
    public bool CanBeRuler(int officerId) => IsAdult(officerId) && !IsRuler(officerId);
    public bool IsForceMember(int officerId) => Selection.NewForces.Any(force => force.OfficerIds.Contains(officerId)) ||
        Selection.Placements.Any(entry => entry.OfficerId == officerId && entry.Serving);
    public int? Location(int officerId) => Selection.Placements.FirstOrDefault(entry => entry.OfficerId == officerId)?.CityId;
    public RegisteredOfficer Officer(int id) => Library.Find(id) ?? throw Missing("unknown_officer");

    public void Adopt(IEnumerable<int> selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        var ids = selected.ToImmutableArray();
        foreach (int id in ids) _ = Officer(id);
        if (ids.Distinct().Count() != ids.Length || Selection.AdoptedOfficerIds.Any(id => IsForceMember(id) && !ids.Contains(id)))
            throw Missing("adoption_has_serving_member_or_duplicate");
        Selection = Selection with { AdoptedOfficerIds = ids,
            Placements = [.. Selection.Placements.Where(entry => ids.Contains(entry.OfficerId))] };
    }

    public void Place(IEnumerable<int> officers, int cityId, bool serving = false)
    {
        var city = RequireCity(cityId);
        var ids = officers.ToArray(); RequireAdopted(ids);
        if (ids.Any(IsForceMember)) throw Missing("placement_serving_officer");
        if (serving && !CityHasForce(cityId))
            throw Missing("serving_city_has_no_force");
        Selection = Selection with { Placements = [.. Selection.Placements.Where(entry => !ids.Contains(entry.OfficerId)),
            .. ids.Select(id => new RegisteredOfficerNewGamePlacement(id, cityId, serving && IsAdult(id)))] };
    }

    public void ClearPlacements(IEnumerable<int> officers)
    {
        var ids = officers.ToArray(); RequireAdopted(ids);
        if (ids.Any(IsForceMember)) throw Missing("reset_serving_officer");
        Selection = Selection with { Placements = [.. Selection.Placements.Where(entry => !ids.Contains(entry.OfficerId))] };
    }

    public void Reset() => Selection = RegisteredOfficerNewGameSelection.None;

    public bool CanFoundForce => Selection.NewForces.Length < 4 &&
        Cities.Any(city => CanChooseForceCity(-1, city.Slot, true)) &&
        Selection.AdoptedOfficerIds.Any(CanBeRuler);

    public void FoundForce(int rulerOfficerId, int cityId)
    {
        var city = RequireCity(cityId); RequireAdopted([rulerOfficerId]);
        if (!CanFoundForce || !CanBeRuler(rulerOfficerId) || !CanChooseForceCity(-1, cityId, true)) throw Missing("new_force_selection");
        Selection = Selection with
        {
            Placements = [.. Selection.Placements.Where(entry => entry.OfficerId != rulerOfficerId), new(rulerOfficerId, cityId, true)],
            NewForces = [.. Selection.NewForces, new RegisteredOfficerNewGameForce(cityId, rulerOfficerId, [rulerOfficerId])
                { ColourId = Enumerable.Range(50, 10).First(colour => !Scenario.Snapshot.Forces.Any(force => force.RulerSlot != 0 &&
                    force.RawReportColor49 == colour) && !Selection.NewForces.Any(force => force.ColourId == colour)) }]
        };
    }

    public bool CityHasForce(int cityId) => RequireCity(cityId).ForceSlot != 0 || Selection.NewForces.Any(force =>
        force.CityId == cityId || force.AdditionalCityIds.Contains(cityId));

    private void RequireAdopted(IReadOnlyList<int> ids)
    {
        if (ids.Count == 0 || ids.Distinct().Count() != ids.Count || ids.Any(id => !Selection.AdoptedOfficerIds.Contains(id)))
            throw Missing("officers_not_adopted");
    }

    private CityPreviewRecord RequireCity(int id) => Cities.FirstOrDefault(city => city.Slot == id && city.Slot is >= 1 and <= 51)
        ?? throw Missing("city_pointer");
    private static InvalidDataException Missing(string field) => new("registered_officers.new_game." + field);
}
