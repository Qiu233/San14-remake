using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace San14.Core;

/// <summary>The ordered branch frames at a real ordinary Story suspension.
/// Indices refer to its retained resource revision, never a copied native VM.</summary>
public sealed record CampaignStoryDispatchFrame(int Begin, int End, bool All, int Depth,
    int OrDepth, int Cursor, bool Fold, int PendingGroup, int IfPhase, int ThenMarker,
    int ElseMarker, bool SelectedThen);

public sealed record CampaignStoryPersonListContinuation(int NextPersonIndex, bool AwaitingDeathCompletion);

/// <summary>The single resolved death target whose actual successor decision is unfinished.</summary>
public sealed record CampaignStorySinglePersonDeathContinuation(int PersonPhysicalSlot);

/// <summary>A known selected City pointer; null PhysicalSlot is a known reset pointer.
/// An absent selection record instead means that the old save has no knowledge.</summary>
public sealed record CampaignStoryCitySelection(int? PhysicalSlot);

/// <summary>Invocation-local semantic selections and formatter banks preserved
/// while a real presentation instruction is consumed. No mutable runtime is saved.</summary>
public sealed record CampaignStoryExecutionCursor(
    ImmutableArray<CampaignStoryDispatchFrame> Frames, int SuccessCount, int EvaluatedCount, bool RootFold,
    CampaignStoryMessageContext MessageContext, ImmutableArray<int> CandidatePersonPhysicalSlots,
    ImmutableArray<int?> ActorPersonPhysicalSlots, CampaignStoryDialogueRoles? DialogueRoles,
    bool PersonListBlocked, ImmutableArray<int> ConditionPersonPhysicalSlots,
    ImmutableArray<int?> ConditionCityPhysicalSlots, ImmutableArray<int?> ConditionForcePhysicalSlots,
    bool ConditionBlocked)
{
    public CampaignStoryPersonListContinuation? PersonListContinuation { get; init; }
    public CampaignStoryForceRankContinuation? ForceRankContinuation { get; init; }
    public CampaignStorySinglePersonDeathContinuation? SinglePersonDeathContinuation { get; init; }
    //Null retains pre-owner saves; fresh execution explicitly captures an empty or populated source list.
    public ImmutableArray<int>? ConditionAreaPhysicalSlots { get; init; }
    public ImmutableArray<int>? ConditionForceListPhysicalSlots { get; init; }
    public ImmutableArray<CampaignStoryCitySelection?>? SelectedCityPhysicalSlots { get; init; }

    //The two search destinations are independent of the five selected214 Cities.
    public ImmutableArray<CampaignStoryCitySelection?>? MoveCityPhysicalSlots { get; init; }

    [JsonIgnore] public bool IsCompleted => !Frames.IsDefault && Frames.IsEmpty && PersonListContinuation is null && ForceRankContinuation is null && SinglePersonDeathContinuation is null;

    internal CampaignStoryExecutionCursor Copy() => this with { MessageContext = MessageContext.Copy() };

    internal void Validate()
    {
        if (Frames.IsDefault || Frames.Any(frame => frame is null || frame.Begin < 0 || frame.End < frame.Begin ||
            frame.Cursor < frame.Begin || frame.Depth < 0 || frame.OrDepth < 0 || frame.PendingGroup < -1 ||
            frame.IfPhase is < 0 or > 2 || frame.ThenMarker < -1 || frame.ElseMarker < -1) ||
            SuccessCount < 0 || EvaluatedCount < SuccessCount || MessageContext is null ||
            !People(CandidatePersonPhysicalSlots) || ActorPersonPhysicalSlots.IsDefault ||
            ActorPersonPhysicalSlots.Length != 5 || ActorPersonPhysicalSlots.Any(person => person is < 0 or >= 1851) ||
            !People(ConditionPersonPhysicalSlots) || ConditionCityPhysicalSlots.IsDefault ||
            ConditionCityPhysicalSlots.Any(city => city is < 0 or >= 52) || ConditionForcePhysicalSlots.IsDefault ||
            ConditionForcePhysicalSlots.Length != 13 || ConditionForcePhysicalSlots.Any(force => force is < 0 or > 51) ||
            DialogueRoles is { } dialogue && (dialogue.ForcePhysicalSlot is < 0 or > 51 ||
                dialogue.PersonIds.IsDefault || dialogue.PersonIds.Length != 6 ||
                dialogue.PersonIds.Any(person => person is < 0 or > 6000)))
            throw new InvalidDataException("The ordinary Story execution cursor is incomplete or invalid.");
        if (PersonListContinuation is { } list && (Frames.IsEmpty || list.NextPersonIndex < 0 ||
            list.NextPersonIndex > CandidatePersonPhysicalSlots.Length ||
            list.AwaitingDeathCompletion && list.NextPersonIndex == 0))
            throw new InvalidDataException("The ordinary Story person-list continuation is invalid.");
        if (ForceRankContinuation is { } rank)
        {
            rank.Validate();
            if (Frames.IsEmpty || PersonListContinuation is not null || rank.OperationIndex != Frames[^1].Cursor)
                throw new InvalidDataException("The ordinary Story rank continuation does not own its waiting leaf.");
        }
        if (SinglePersonDeathContinuation is { } death && (Frames.IsEmpty || PersonListContinuation is not null ||
            ForceRankContinuation is not null || death.PersonPhysicalSlot is < 0 or >= 1851))
            throw new InvalidDataException("The ordinary Story single-person death continuation is invalid.");
        if (ConditionAreaPhysicalSlots is { } areas && (areas.IsDefault || areas.Any(area => area is < 0 or > 500)))
            throw new InvalidDataException("The ordinary Story area selections are invalid.");
        if (SelectedCityPhysicalSlots is { } cities && (cities.IsDefault || cities.Length != 5 ||
            cities.Any(city => city?.PhysicalSlot is < 0 or > 51)))
            throw new InvalidDataException("The ordinary Story target City selections are invalid.");
        if (ConditionForceListPhysicalSlots is { } forces && (forces.IsDefault || forces.Any(force => force is < 0 or > 51)))
            throw new InvalidDataException("The ordinary Story Force list is invalid.");
        if (MoveCityPhysicalSlots is { } moveCities && (moveCities.IsDefault || moveCities.Length != 2 ||
            moveCities.Any(city => city?.PhysicalSlot is < 0 or > 51)))
            throw new InvalidDataException("The ordinary Story move City selections are invalid.");
        MessageContext.Validate();
    }

    private static bool People(ImmutableArray<int> people) =>
        !people.IsDefault && people.All(person => person is >= 0 and < 1851);
}
