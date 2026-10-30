namespace San14.Core;

/// <summary>The actual overseas outward or return journey in the shared
/// personnel dispatcher. The country and offered-item identities retain their
/// native zero sentinels; the outward and return dates are separate trips.</summary>
public sealed record PersonnelCaravanTask(int Id, int DispatchSlot, int[] MemberIds,
    int NativeKind, int CountryId, int SourceForceId, int SourceDistrictId,
    int SourceLocationId, int StartHexId, int EndHexId,
    int HomeLocationId, NativeMapSeasonDate StartDate, NativeMapSeasonDate DueDate,
    int OfferedItemId, int GoldOffer, int SuppliesOffer, int TroopOffer,
    byte RewardStage, byte JourneyEvent, byte ReturnDisposition,
    bool Started, bool NeedsRefresh, bool Cancelled)
{
    public bool IsRetiring { get; init; }
    public bool IsAssociated { get; init; } = true;
    public bool ReportToPlayer { get; init; }
    /// <summary>The source adviser result at command5C; its -1 sentinel is
    /// independent of the reward-stage and travelling-event outcome bytes.</summary>
    public int SavedResult { get; init; } = -1;
    /// <summary>The source applies return rewards before its report opens.
    /// Retain that result until the real report completes, preventing a load
    /// or a repeated morning/evening cut from applying the rewards again.</summary>
    public PersonnelCaravanReport? PendingReport { get; init; }
}

/// <summary>The five result slots of the real return producer and the separate
/// return-gold amount. IDs remain zero when that reward was not produced.</summary>
public sealed record PersonnelCaravanReport(bool Successful, int GoldAmount,
    int ItemId, int GemId, int GemCount, int WeaponId, int BonusGold);
