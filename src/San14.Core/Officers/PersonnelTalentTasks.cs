namespace San14.Core;

/// <summary>A search or recruitment journey in the shared personnel command manager.</summary>
public sealed record PersonnelTalentTask(int Id, int DispatchSlot, int[] MemberIds,
    TalentOrderKind Kind, int ForceId, int SourceCityId, int SourceDistrictId,
    int? TargetHexId, int? TargetOfficerId, int StartHexId, int EndHexId,
    NativeMapSeasonDate StartDate, NativeMapSeasonDate DueDate, int SavedSample,
    bool Started, bool Cancelled, int? TargetOriginalForceId,
    int? TargetOriginalHomeLocationId, int RecruitMode, bool NeedsRefresh)
{
    /// <summary>The stationary recruitment retry created on arrival (native command22).</summary>
    public bool IsRecruitWait { get; init; }
    public bool IsRetiring { get; init; }
    public bool IsAssociated { get; init; } = true;
}

/// <summary>Command eligibility and costs; unknown rules remain unavailable.</summary>
public sealed record TalentCommandView(IReadOnlyList<int> ExecutorOfficerIds,
    IReadOnlyList<int> TargetOfficerIds, bool? CanSubmit, string? MissingRuleKey,
    int? CommandBooks, int? GoldCost);

/// <summary>Current command facts: membership controls permission while the
/// executor's actual Home Base District pays and owns its task registration.</summary>
public sealed record TalentRecruitCommandSource(bool? IsAvailable, int? MembershipDistrictId,
    int? PaymentDistrictId, string? MissingRuleKey);

public interface IPlayableTalentCommandRules
{
    TalentRecruitCommandSource QueryRecruitSource(GameState state, int forceId, int sourceBaseId, int executorOfficerId);
    TalentCommandView QueryTalentSearch(GameState state, int forceId, int cityId, int targetHexId);
    TalentCommandView QueryTalentRecruit(GameState state, int forceId, int cityId, int? targetOfficerId = null);
    CommandResult PrepareTalent(GameState state, ForceState force, PreparedTalentOrder task,
        CommandContext context);
}
