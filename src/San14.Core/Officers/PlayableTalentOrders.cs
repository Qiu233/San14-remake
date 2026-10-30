namespace San14.Core;

public enum TalentOrderKind { Search, Recruit }

/// <summary>The player's automatic search allocation choices, retained after a successful batch.</summary>
public sealed record TalentAutoSearchPreferences(int ExecutorPolicy, int TravelRange, int AllocationPolicy)
{
    internal bool HasSupportedValues => ExecutorPolicy is >= 0 and <= 3 &&
        TravelRange is >= 0 and <= 2 && AllocationPolicy is >= 0 and <= 1;
}

public sealed record PreparedTalentOrder(int ExecutorOfficerId, int ForceId, int CityId,
    int DistrictId, TalentOrderKind Kind, int? TargetOfficerId)
{
    public int? TargetHexId { get; init; }
    public int RecruitMode { get; init; }
}

public sealed record PrepareTalentSearchOrder(int ForceId, int CityId,
    int ExecutorOfficerId) : GameOrder(ForceId)
{
    public int? TargetHexId { get; init; }
}

public sealed record PrepareTalentRecruitOrder(int ForceId, int CityId,
    int ExecutorOfficerId, int TargetOfficerId) : GameOrder(ForceId)
{
    public int RecruitMode { get; init; }
}

/// <summary>One ordered executor/target pair. Search uses TargetHexId;
/// recruitment uses TargetOfficerId. Null search Hex retains the existing
/// single-command source-City default.</summary>
public sealed record TalentAssignment(int CityId, int ExecutorOfficerId,
    int? TargetHexId = null, int? TargetOfficerId = null)
{
    public int RecruitMode { get; init; }
}

/// <summary>One command transaction. Search preserves the ordered Area/executor
/// pairs; recruitment preserves ordered executors sharing the same target.</summary>
public sealed record PrepareTalentBatchOrder(int ForceId, TalentOrderKind Kind,
    IReadOnlyList<TalentAssignment> Assignments) : GameOrder(ForceId)
{
    public TalentAutoSearchPreferences? AutoSearchPreferences { get; init; }
}

/// <summary>
/// Prepares talent commands through the installed current-world rules.
/// </summary>
public sealed class PlayableTalentOrders(IOrderRules otherCommands,
    IPlayableTalentCommandRules? rules = null) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order,
        CommandContext context)
    {
        if (order is PrepareTalentBatchOrder batch) return ApplyBatch(state, force, batch, context);
        int cityId, executorId, recruitMode = 0;
        int? targetId;
        TalentOrderKind kind;
        switch (order)
        {
            case PrepareTalentSearchOrder search:
                (cityId, executorId, targetId, kind) =
                    (search.CityId, search.ExecutorOfficerId, null, TalentOrderKind.Search);
                break;
            case PrepareTalentRecruitOrder recruit:
                (cityId, executorId, targetId, kind) =
                    (recruit.CityId, recruit.ExecutorOfficerId, recruit.TargetOfficerId,
                        TalentOrderKind.Recruit);
                recruitMode = recruit.RecruitMode;
                break;
            default:
                return otherCommands.Apply(state, force, order, context);
        }
        if (recruitMode is not (0 or 10 or 30))
            return CommandResult.Reject("Recruitment mode is unsupported.", "talent.recruit_mode");
        CityState? city = state.Cities.GetValueOrDefault(cityId);
        int? sourceDistrict;
        if (city is not null && city.ForceId == force.Id) sourceDistrict = city.DistrictId;
        else if (kind == TalentOrderKind.Recruit && state.Gates.TryGetValue(cityId, out var gate) && gate.ForceId == force.Id)
            sourceDistrict = gate.DistrictId;
        else return CommandResult.Reject("Talent command requires an owned source Base.", "talent.city");
        if (sourceDistrict is not int districtId ||
            !state.Districts.TryGetValue(districtId, out DistrictState? district))
            return CommandResult.RuleUnavailable("rules.city_district",
                "Talent command district is unresolved.");
        int paymentDistrictId = district.Id;
        if (kind == TalentOrderKind.Recruit)
        {
            if (rules is null) return CommandResult.RuleUnavailable("talent.current_command_rules", "Talent source rules are not installed.");
            var source = rules.QueryRecruitSource(state, force.Id, cityId, executorId);
            if (source.IsAvailable is null) return CommandResult.RuleUnavailable(source.MissingRuleKey ?? "talent.recruit.source", "Recruitment source is unresolved.");
            if (!source.IsAvailable.Value) return CommandResult.Reject("Recruitment source is unavailable.", "talent.qualification");
            if (source.MembershipDistrictId is not int member || !state.Districts.TryGetValue(member, out district) ||
                source.PaymentDistrictId is not int payment || !state.Districts.ContainsKey(payment))
                return CommandResult.RuleUnavailable("talent.recruit.source_district", "Recruitment source District is unresolved.");
            if (district.ForceId != force.Id) return CommandResult.Reject("Recruitment executor is outside this force.", "talent.district_scope");
            paymentDistrictId = payment;
        }
        if (context.DelegatedDistrictId is int delegated ? delegated != district.Id : district.Number != 1)
            return CommandResult.Reject("Talent command is outside the authorized district.",
                "talent.district_scope");
        if (rules is not null)
            return rules.PrepareTalent(state, force, new PreparedTalentOrder(executorId,
                force.Id, cityId, paymentDistrictId, kind, targetId)
            {
                TargetHexId = order is PrepareTalentSearchOrder searchOrder
                    ? searchOrder.TargetHexId ?? city!.HexId : null,
                RecruitMode = recruitMode
            }, context);
        return CommandResult.RuleUnavailable("talent.current_command_rules",
            "Talent command source rules are not installed.");

    }

    private CommandResult ApplyBatch(GameState state, ForceState force, PrepareTalentBatchOrder batch,
        CommandContext context)
    {
        if (!Enum.IsDefined(batch.Kind) || batch.Assignments is not { Count: > 0 } ||
            batch.Assignments.Any(assignment => assignment is null))
            return CommandResult.Reject("Talent batch requires ordered assignments.", "talent.batch_input");
        if (batch.AutoSearchPreferences is { } preferences &&
            (batch.Kind != TalentOrderKind.Search || !preferences.HasSupportedValues))
            return CommandResult.Reject("Automatic search preferences are invalid.", "talent.auto_search_preferences");
        int? recruitTarget = batch.Assignments[0].TargetOfficerId;
        foreach (var assignment in batch.Assignments)
        {
            if (batch.Kind == TalentOrderKind.Search ? assignment.RecruitMode != 0 :
                assignment.RecruitMode is not (0 or 10 or 30))
                return CommandResult.Reject("Talent batch recruitment mode is invalid.", "talent.recruit_mode");
            if (batch.Kind == TalentOrderKind.Search ? assignment.TargetOfficerId is not null :
                assignment.TargetHexId is not null || assignment.TargetOfficerId is null || assignment.TargetOfficerId != recruitTarget)
                return CommandResult.Reject("Talent batch targets do not match the command kind.", "talent.batch_target");
        }
        var working = state;
        //Keep the same envoy guard as the single-order pipeline. Calling the
        //rules directly retains this already-owned GameEngine transaction;
        //no nested GameEngine, world copy or intermediate publication occurs.
        var singles = new PlayableDiplomaticOfficerGuard(this);
        foreach (var assignment in batch.Assignments)
        {
            GameOrder single = batch.Kind == TalentOrderKind.Search
                ? new PrepareTalentSearchOrder(batch.ForceId, assignment.CityId, assignment.ExecutorOfficerId)
                    { TargetHexId = assignment.TargetHexId }
                : new PrepareTalentRecruitOrder(batch.ForceId, assignment.CityId, assignment.ExecutorOfficerId,
                    assignment.TargetOfficerId!.Value) { RecruitMode = assignment.RecruitMode };
            var result = singles.Apply(working, working.Forces[force.Id], single, context);
            if (!result.Accepted) return result;
            working = result.State!;
        }
        if (batch.AutoSearchPreferences is { } completedPreferences)
            working = working with { TalentAutoSearchPreferences = completedPreferences };
        return CommandResult.Accept(working);
    }
}
