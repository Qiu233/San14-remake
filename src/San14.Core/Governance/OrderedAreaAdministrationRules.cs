namespace San14.Core;

/// <summary>Apply every regional appointment row in source order, retaining
/// repeated targets and baseline-equal rows. The first row's current City and
/// District own the batch budget. This is the ordinary command authority path.</summary>
public sealed record ApplyAreaAdministrationOrder(int ForceId,
    IReadOnlyList<AreaAdministrationChange> Changes) : GameOrder(ForceId);

public sealed partial class AreaAdministrationRules
{
    private CommandResult ApplyOrdered(GameState state, ForceState force,
        ApplyAreaAdministrationOrder administration, CommandContext context)
    {
        if (administration.Changes is null)
            return CommandResult.Reject("Regional changes are missing.", "administration.changes_missing");
        if (administration.Changes.Count == 0) return CommandResult.Accept(state);
        DistrictState? budgetDistrict = null;
        var changes = new List<AreaAdministrationChange>(administration.Changes.Count);
        foreach (var change in administration.Changes)
        {
            if (change is null)
                return CommandResult.Reject("A regional change is missing.", "administration.change_missing");
            if (!state.Areas.TryGetValue(change.AreaId, out var area) || area.CityId is not int cityId ||
                !state.Cities.TryGetValue(cityId, out var city) || !city.AreaIds.Contains(area.Id))
                return CommandResult.Reject("Area does not belong to a current city.", "administration.area_city");
            if (city.ForceId != force.Id)
                return CommandResult.Reject("City is not controlled by this force.", "administration.city_control");
            if (area.ForceId != force.Id)
                return CommandResult.Reject("Area is not controlled by this force.", "administration.area_control");
            if (city.DistrictId is not int districtId || !state.Districts.TryGetValue(districtId, out var district))
                return CommandResult.RuleUnavailable("rules.city_district", "City district is unresolved.");
            if (district.ForceId != force.Id)
                return CommandResult.Reject("City district is not controlled by this force.", "administration.district_control");
            if (context.DelegatedDistrictId is int delegated ? delegated != district.Id : district.Number != 1)
                return CommandResult.Reject("City is outside the authorized district scope.", "administration.district_scope");
            if (!Enum.IsDefined(change.DevelopmentFocus))
                return CommandResult.Reject("Development focus is invalid.", "administration.focus");
            if (change.OverseerId is int id && (!state.Officers.TryGetValue(id, out var officer) || officer.ForceId != force.Id))
                return CommandResult.Reject("Officer does not belong to this force.", "administration.officer_force");
            if (area.Administration is not { DevelopmentFocus: not null })
                return CommandResult.RuleUnavailable("rules.administration_state", "Current administration is unresolved.");
            budgetDistrict ??= district;
            //Do not remove repeats or baseline-equal rows: an earlier clear
            //may make a later same-person installation a real source write.
            changes.Add(change);
        }
        return _execution?.ExecuteOrdered(state, changes.AsReadOnly(),
            CommandBookAvailability.Reserved(state, budgetDistrict!.Id), context)
            ?? CommandResult.RuleUnavailable("rules.administration_ordered_execution", "Ordered appointment writer is unavailable.");
    }
}
