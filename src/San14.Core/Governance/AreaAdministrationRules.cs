namespace San14.Core;

/// <summary>Complete desired settings: a null overseer explicitly clears the appointment.</summary>
public sealed record AreaAdministrationChange(int AreaId, int? OverseerId, AreaDevelopmentFocus DevelopmentFocus);
public sealed record SetAreaAdministrationOrder(int ForceId, int CityId,
    IReadOnlyList<AreaAdministrationChange> Changes) : GameOrder(ForceId);

// Zero is deliberately unavailable: an omitted eligibility decision cannot authorize an order.
public enum AdministrationEligibilityStatus { Unavailable, Allowed, Denied }
public readonly record struct AdministrationEligibility(AdministrationEligibilityStatus Status,
    string? Explanation = null, string? MissingRuleKey = null);

/// <summary>
/// Supplies verified appointment and focus eligibility, including current tasks, location,
/// health and any simultaneous appointments. Receives all changed regions together; combine
/// them with unchanged state when checking the final appointments. Do not mutate state.
/// This is required because the manual does not define the complete selection rules.
/// </summary>
public interface IAreaAdministrationEligibility
{
    AdministrationEligibility Evaluate(GameState state, CityState city,
        IReadOnlyList<AreaAdministrationChange> changes, CommandContext context);
}

/// <summary>
/// Verified regional command structure and per-changed-region costs. Original initialization,
/// appointment eligibility, city policy and period settlement remain separate rule/data requirements.
/// </summary>
public sealed class AreaAdministrationRules : IOrderRules
{
    private readonly IAreaAdministrationEligibility _eligibility;

    public AreaAdministrationRules(IAreaAdministrationEligibility eligibility) =>
        _eligibility = eligibility ?? throw new ArgumentNullException(nameof(eligibility));

    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not SetAreaAdministrationOrder administration)
            return CommandResult.Reject("Order type is unsupported.", "command.unsupported");
        if (!state.Cities.TryGetValue(administration.CityId, out CityState? city) || city.ForceId != force.Id)
            return CommandResult.Reject("City is not controlled by this force.", "administration.city_control");
        if (city.DistrictId is not int districtId || !state.Districts.TryGetValue(districtId, out DistrictState? district))
            return CommandResult.RuleUnavailable("rules.city_district", "City district is unresolved.");
        if (context.DelegatedDistrictId is int delegated ? delegated != district.Id : district.Number != 1)
            return CommandResult.Reject("City is outside the authorized district scope.", "administration.district_scope");
        if (administration.Changes is null)
            return CommandResult.Reject("Regional changes are missing.", "administration.changes_missing");

        var seen = new HashSet<int>();
        var changes = new List<AreaAdministrationChange>();
        foreach (AreaAdministrationChange change in administration.Changes)
        {
            if (change is null || !seen.Add(change.AreaId))
                return CommandResult.Reject("Regional changes are null or duplicated.", "administration.duplicate_area");
            if (!state.Areas.TryGetValue(change.AreaId, out AreaState? area) || area.CityId != city.Id ||
                !city.AreaIds.Contains(area.Id))
                return CommandResult.Reject("Area does not belong to this city.", "administration.area_city");
            if (area.ForceId != force.Id)
                return CommandResult.Reject("Area is not controlled by this force.", "administration.area_control");
            if (!Enum.IsDefined(change.DevelopmentFocus))
                return CommandResult.Reject("Development focus is invalid.", "administration.focus");
            if (change.OverseerId is int officerId)
            {
                if (!state.Officers.TryGetValue(officerId, out OfficerState? officer) || officer.ForceId != force.Id)
                    return CommandResult.Reject("Officer does not belong to this force.", "administration.officer_force");
                if (officer.Presence is not (null or OfficerPresence.Present))
                    return CommandResult.Reject("Officer has not appeared, is undiscovered or is dead.", "administration.ineligible");
            }
            if (area.Administration is not { DevelopmentFocus: not null } current)
                return CommandResult.RuleUnavailable("rules.administration_state", $"Area {area.Id} administration is unresolved.");
            if (current.OverseerId != change.OverseerId || current.DevelopmentFocus != change.DevelopmentFocus)
            {
                // The original has a known "no focus" state. Its use as a desired command
                // setting has not yet been verified; retaining it unchanged is still inert.
                if (change.DevelopmentFocus == AreaDevelopmentFocus.None)
                    return CommandResult.RuleUnavailable("rules.administration_clear_focus",
                        "Submitting a changed region with no development focus has not been verified.");
                changes.Add(change);
            }
        }

        // Idempotent engine submission; this does not assert the original UI's button behavior.
        if (changes.Count == 0) return CommandResult.Accept(state);
        AdministrationEligibility eligibility = _eligibility.Evaluate(state, city, changes.AsReadOnly(), context);
        if (eligibility.Status == AdministrationEligibilityStatus.Denied)
            return CommandResult.Reject(eligibility.Explanation ?? "Regional administration is unavailable for these officers or areas.",
                "administration.ineligible");
        if (eligibility.Status != AdministrationEligibilityStatus.Allowed)
            return CommandResult.RuleUnavailable(eligibility.MissingRuleKey ?? "rules.administration_eligibility",
                eligibility.Explanation ?? "Regional administration eligibility has not been verified.");
        if (district.OrdersRemaining is not int remaining)
            return CommandResult.RuleUnavailable("rules.command_budget", "Command budget is unresolved.");
        if (remaining - CommandBookAvailability.Reserved(state, district.Id) < changes.Count)
            return CommandResult.Reject("Not enough orders for the changed regions.", "administration.insufficient_orders");

        var areas = new Dictionary<int, AreaState>(state.Areas);
        foreach (AreaAdministrationChange change in changes)
            areas[change.AreaId] = areas[change.AreaId] with
            {
                Administration = new AreaAdministrationState(change.OverseerId, change.DevelopmentFocus)
            };
        var districts = new Dictionary<int, DistrictState>(state.Districts)
        {
            [district.Id] = district with { OrdersRemaining = remaining - changes.Count }
        };
        return CommandResult.Accept(state with { Areas = areas, Districts = districts });
    }
}
