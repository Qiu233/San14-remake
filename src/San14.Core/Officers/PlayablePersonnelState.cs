namespace San14.Core;

public enum PersonnelActionKind { GrantItem, RevokeItem, SetOffice }
public sealed record GrantItemOrder(int ForceId, int ItemId, int RecipientOfficerId) : GameOrder(ForceId);
public sealed record RevokeItemOrder(int ForceId, int ItemId, int OwnerOfficerId) : GameOrder(ForceId);
public sealed record SetOfficerOfficeOrder(int ForceId, int OfficerId, int OfficeId) : GameOrder(ForceId);

public sealed record PersonnelChange(PersonnelActionKind Kind, int OfficerId,
    int? ItemId = null, int? PreviousHolderId = null, int? NextHolderId = null,
    int? PreviousOfficeId = null, int? NextOfficeId = null);

/// <summary>Explicit immediate accounting; no original costs or loyalty changes are inferred.</summary>
public sealed record PersonnelQuote(int? DistrictId, int? GoldCityId, int CommandBooks,
    int Gold, int LoyaltyDelta, string Revision);
public sealed record PersonnelEvaluation(bool? Allowed, PersonnelQuote? Quote = null,
    string? MissingRuleKey = null, string? Reason = null);

public interface IPersonnelCommandRules
{
    PersonnelEvaluation Evaluate(GameState state, ForceState force,
        PersonnelChange change, CommandContext context);
}

public sealed class UnresolvedPersonnelRules : IPersonnelCommandRules
{
    public static UnresolvedPersonnelRules Instance { get; } = new();
    private UnresolvedPersonnelRules() { }
    public PersonnelEvaluation Evaluate(GameState state, ForceState force,
        PersonnelChange change, CommandContext context) => new(null,
            MissingRuleKey: change.Kind == PersonnelActionKind.SetOffice
                ? "personnel.office_policy" : "personnel.item_policy",
            Reason: "Original personnel eligibility, accounting or loyalty effects are unresolved.");
}

/// <summary>Used by a source or deliberately custom adapter; the order cannot supply its own fee.</summary>
public sealed class ExplicitPersonnelRules(
    Func<GameState, ForceState, PersonnelChange, CommandContext, PersonnelEvaluation> evaluate) : IPersonnelCommandRules
{
    public PersonnelEvaluation Evaluate(GameState state, ForceState force,
        PersonnelChange change, CommandContext context) => evaluate(state, force, change, context);
}

public sealed record PersonnelReportEntry(PersonnelActionKind Kind, int Period, int ForceId,
    int OfficerId, int? ItemId, int? PreviousHolderId, int? NextHolderId,
    int? PreviousOfficeId, int? NextOfficeId, int CommandBooks, int Gold,
    int LoyaltyDelta, string PolicyRevision)
{
    public int? DistrictId { get; init; }
    public int? GoldCityId { get; init; }
    public int? ReportedInPeriod { get; init; }
}

public sealed partial record OfficerState
{
    // Unknown in the current source projection and legacy saves. Costs and effects
    // may never interpret this missing current value as zero loyalty.
    public int? Loyalty { get; init; }
}

public sealed partial record ItemDefinition
{
    // Independent original loyalty parameter; the ability bonus and current holder
    // do not establish it, and it is not by itself the final gift/revoke effect.
    public int? LoyaltyValue { get; init; }
}

public sealed partial record GameState
{
    public PersonnelReportEntry[]? PersonnelReports { get; init; }
}

public static class PlayablePersonnelStateValidation
{
    public static void Validate(GameState state)
    {
        foreach (OfficerState officer in state.Officers.Values)
            if (officer.Loyalty is < 0 or > 100)
                throw new InvalidDataException("Officer current loyalty is invalid.");
        foreach (ItemDefinition item in state.ItemDefinitions.Values)
            if (item.LoyaltyValue is < 0 or > 255)
                throw new InvalidDataException("Item loyalty source parameter is invalid.");
        foreach (PersonnelReportEntry report in state.PersonnelReports ?? [])
            if (report is null || !Enum.IsDefined(report.Kind) || report.Period < 0 ||
                report.Period > state.PeriodNumber || !state.Forces.ContainsKey(report.ForceId) ||
                report.ReportedInPeriod < report.Period || report.ReportedInPeriod > state.PeriodNumber ||
                (report.DistrictId is int district && !state.Districts.ContainsKey(district)) ||
                (report.GoldCityId is int city && !state.Cities.ContainsKey(city)) ||
                !state.Officers.ContainsKey(report.OfficerId) || report.CommandBooks < 0 || report.Gold < 0 ||
                report.LoyaltyDelta is < -100 or > 100 || string.IsNullOrWhiteSpace(report.PolicyRevision) ||
                (report.ItemId is int item && state.Items?.ContainsKey(item) != true) ||
                (report.PreviousHolderId is int previous && !state.Officers.ContainsKey(previous)) ||
                (report.NextHolderId is int next && !state.Officers.ContainsKey(next)) ||
                (report.PreviousOfficeId is int previousOffice && !state.OfficerOffices.ContainsKey(previousOffice)) ||
                (report.NextOfficeId is int nextOffice && !state.OfficerOffices.ContainsKey(nextOffice)) ||
                (report.Kind == PersonnelActionKind.SetOffice
                    ? report.ItemId is not null || report.PreviousHolderId is not null || report.NextHolderId is not null ||
                      report.NextOfficeId is null
                    : report.ItemId is null || report.PreviousHolderId is null || report.NextHolderId is null ||
                      report.PreviousOfficeId is not null || report.NextOfficeId is not null))
                throw new InvalidDataException("Personnel report contains an invalid reference or accounting.");
    }
}
