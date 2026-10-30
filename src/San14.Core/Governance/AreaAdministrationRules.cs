namespace San14.Core;

/// <summary>Complete desired settings: a null overseer explicitly clears the appointment.</summary>
public sealed record AreaAdministrationChange(int AreaId, int? OverseerId, AreaDevelopmentFocus DevelopmentFocus);
public sealed record SetAreaAdministrationOrder(int ForceId, int CityId,
    IReadOnlyList<AreaAdministrationChange> Changes) : GameOrder(ForceId);

// Legacy eligibility-only fixture contract. It cannot execute a current campaign order.
public enum AdministrationEligibilityStatus { Unavailable, Allowed, Denied }
public readonly record struct AdministrationEligibility(AdministrationEligibilityStatus Status,
    string? Explanation = null, string? MissingRuleKey = null);

/// <summary>
/// Historical synthetic-fixture eligibility contract, retained for source compatibility.
/// Normal campaigns use IAreaAdministrationExecution for the source-backed candidate
/// rules and complete transaction; this older contract alone cannot authorize execution.
/// </summary>
public interface IAreaAdministrationEligibility
{
    AdministrationEligibility Evaluate(GameState state, CityState city,
        IReadOnlyList<AreaAdministrationChange> changes, CommandContext context);
}

/// <summary>The campaign provider owns the whole appointment transaction.
/// Core supplies domain identities, ordered changes and authority scope; it
/// never edits the source compatibility bank or writes a second Area projection.</summary>
public interface IAreaAdministrationExecution
{
    CommandResult Execute(GameState state, CityState city,
        IReadOnlyList<AreaAdministrationChange> changes, long reservedCommandBooks, CommandContext context);
    // Older eligibility-only/single-City fixtures cannot fabricate execution
    // of an ordered cross-City transaction. Current providers implement this.
    CommandResult ExecuteOrdered(GameState state, IReadOnlyList<AreaAdministrationChange> changes,
        long reservedCommandBooks, CommandContext context)
        => CommandResult.RuleUnavailable("rules.administration_ordered_execution", "Ordered appointment writer is unavailable.");
}

public sealed partial class AreaAdministrationRules : IOrderRules
{
    private readonly IAreaAdministrationExecution? _execution;
    public AreaAdministrationRules(IAreaAdministrationExecution? execution) => _execution = execution;
    // Kept for source compatibility of old synthetic fixtures. Their old
    // eligibility-only provider cannot execute a faithful current transaction.
    public AreaAdministrationRules(IAreaAdministrationEligibility eligibility)
    { ArgumentNullException.ThrowIfNull(eligibility); }

    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is ApplyAreaAdministrationOrder ordered) return ApplyOrdered(state, force, ordered, context);
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
        foreach (var change in administration.Changes)
        {
            if (change is null || !seen.Add(change.AreaId))
                return CommandResult.Reject("Regional changes are null or duplicated.", "administration.duplicate_area");
            if (!state.Areas.TryGetValue(change.AreaId, out var area) || area.CityId != city.Id || !city.AreaIds.Contains(area.Id))
                return CommandResult.Reject("Area does not belong to this city.", "administration.area_city");
            if (area.ForceId != force.Id)
                return CommandResult.Reject("Area is not controlled by this force.", "administration.area_control");
            if (!Enum.IsDefined(change.DevelopmentFocus))
                return CommandResult.Reject("Development focus is invalid.", "administration.focus");
            if (change.OverseerId is int id && (!state.Officers.TryGetValue(id, out var officer) || officer.ForceId != force.Id))
                return CommandResult.Reject("Officer does not belong to this force.", "administration.officer_force");
            if (area.Administration is not { DevelopmentFocus: not null } current)
                return CommandResult.RuleUnavailable("rules.administration_state", "Current administration is unresolved.");
            if (current.OverseerId != change.OverseerId || current.DevelopmentFocus != change.DevelopmentFocus)
                changes.Add(change);
        }
        if (changes.Count == 0) return CommandResult.Accept(state);
        return _execution?.Execute(state, city, changes.AsReadOnly(), CommandBookAvailability.Reserved(state, district.Id), context)
            ?? CommandResult.RuleUnavailable("rules.administration_execution", "Current appointment writer is unavailable.");
    }
}
