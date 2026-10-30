using System.Collections.Immutable;

namespace San14.Core;

public sealed record CampaignStoryForcePair(int FirstForcePhysicalSlot, int SecondForcePhysicalSlot);
public sealed record CampaignStoryForcePeople(int ForcePhysicalSlot, ImmutableArray<int> PersonPhysicalSlots);
public sealed record CampaignStoryPersonStatus(int PersonPhysicalSlot, int Status);
public sealed record CampaignStoryPersonItems(int PersonPhysicalSlot, ImmutableArray<int> ItemPhysicalSlots);
public sealed record CampaignStoryBaseReceiver(bool IsGate, int PhysicalSlot);

/// <summary>The source event invocation's ordered result occurrences. This is
/// not an event log and does not reconstruct facts from the final world.</summary>
public sealed record CampaignStoryOutcomes
{
    public ImmutableArray<int> RaisedArmyPersonSlots { get; init; } = [];
    public ImmutableArray<CampaignStoryForcePair> AlliancesCreated { get; init; } = [];
    public ImmutableArray<CampaignStoryForcePair> AlliancesExtended { get; init; } = [];
    public ImmutableArray<CampaignStoryForcePair> AlliancesCancelled { get; init; } = [];
    public ImmutableArray<int> DeathPersonSlots { get; init; } = [];
    public ImmutableArray<int> FreePersonSlots { get; init; } = [];
    public ImmutableArray<CampaignStoryForcePeople> CapturedByForce { get; init; } = [];
    public ImmutableArray<CampaignStoryForcePeople> JoinedByForce { get; init; } = [];
    public ImmutableArray<CampaignStoryBaseReceiver> TroopIncreasedBases { get; init; } = [];
    public ImmutableArray<CampaignStoryPersonStatus> StatusChanges { get; init; } = [];
    public ImmutableArray<int> MeritPersonSlots { get; init; } = [];
    public ImmutableArray<CampaignStoryPersonItems> ItemsGainedByPerson { get; init; } = [];
    public int CurrentPlayerCityIncreaseKind { get; init; }

    public static CampaignStoryOutcomes Empty { get; } = new();

    internal void Validate()
    {
        if (!People(RaisedArmyPersonSlots) || !People(DeathPersonSlots) || !People(FreePersonSlots) ||
            !People(MeritPersonSlots) || !Pairs(AlliancesCreated) || !Pairs(AlliancesExtended) || !Pairs(AlliancesCancelled) ||
            !Groups(CapturedByForce) || !Groups(JoinedByForce) || TroopIncreasedBases.IsDefault ||
            TroopIncreasedBases.Any(value => value is null || value.PhysicalSlot < 0 || value.PhysicalSlot > (value.IsGate ? 10 : 51)) ||
            StatusChanges.IsDefault || StatusChanges.Any(value => value is null || value.PersonPhysicalSlot is < 0 or >= 1851) ||
            ItemsGainedByPerson.IsDefault || ItemsGainedByPerson.Any(value => value is null || value.PersonPhysicalSlot is < 0 or >= 1851 ||
                value.ItemPhysicalSlots.IsDefault || value.ItemPhysicalSlots.Any(item => item is < 0 or > 201)))
            throw new InvalidDataException("The ordinary Story result occurrences are incomplete or invalid.");
    }

    private static bool People(ImmutableArray<int> values) => !values.IsDefault && values.All(value => value is >= 0 and < 1851);
    private static bool Pairs(ImmutableArray<CampaignStoryForcePair> values) => !values.IsDefault && values.All(value => value is not null &&
        value.FirstForcePhysicalSlot is >= 0 and <= 51 && value.SecondForcePhysicalSlot is >= 0 and <= 51);
    private static bool Groups(ImmutableArray<CampaignStoryForcePeople> values) => !values.IsDefault && values.All(value => value is not null &&
        value.ForcePhysicalSlot is >= 0 and <= 51 && People(value.PersonPhysicalSlots));
}

public enum NormalTurnStoryResultPhase { Presentation = 0, Result = 1, Confirmed = 2, Entry = 3, Entered = 4, Exiting = 5, Exited = 6, PostResult = 7 }

/// <summary>Text prepared once at the ordinary event result-page boundary.</summary>
public sealed record NormalTurnStoryResultSummary(string Locale, string Title, string Body);
