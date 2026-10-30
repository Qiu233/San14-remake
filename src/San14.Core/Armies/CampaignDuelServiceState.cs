namespace San14.Core;

public enum CampaignDuelHistoryKnowledge { Unknown, KnownEmpty, KnownEntries }

public enum CampaignDuelOutcome { Undetermined = -1, Won = 0, Lost = 1, Draw = 2 }
public enum CampaignDuelAftermath { NotEvaluated = -1, None = 0, Death = 1, Capture = 2 }
public enum CampaignDuelRoundAction { Unprepared = -1, Ordinary = 0, Critical = 1, Evaded = 2, Counter = 3 }

/// <summary>The two current commanders are an unordered historical pair.
/// Army identities can retire and are not the identity of this entry.</summary>
public sealed record CampaignDuelHistoricalPair(int FirstOfficerId, int SecondOfficerId)
{
    //The historical query tests membership twice. Even a repeated query ID
    //matches when it occurs on either side of the saved pair.
    public bool Matches(int first, int second) => (FirstOfficerId == first || SecondOfficerId == first) &&
        (FirstOfficerId == second || SecondOfficerId == second);
}

public sealed record CampaignDuelPair(int ActorUnitId, int OpponentUnitId, int? AssistUnitId,
    int ActorOfficerId, int OpponentOfficerId, int? AssistOfficerId,
    // Null preserves an unknown presentation preference. Numerical duel
    // selection and aftermath do not depend on this display-only choice.
    int PlayerSide, int? PresentationSetting);

public sealed record CampaignDuelRound(int Number, int Damage, CampaignDuelRoundAction Action);
public sealed record CampaignDuelParticipant(int OfficerId, CampaignDuelOutcome Outcome,
    byte ReadinessBefore, byte ReadinessAfter, bool WriteReadiness,
    int PreparedInitialReadiness, int PreparedFinalReadiness,
    int[] ReadinessTrajectory, CampaignDuelRound[] Rounds)
{
    public CampaignDuelParticipant Copy() => this with
    { ReadinessTrajectory = ReadinessTrajectory.ToArray(), Rounds = Rounds.ToArray() };
}

/// <summary>The numerical duel is prepared once. Actual readiness writes
/// differ from the presentation's prepared values for an instant defeat.</summary>
public sealed record CampaignDuelResult(CampaignDuelOutcome PlayerOutcome, bool InstantDefeat,
    CampaignDuelAftermath Aftermath, int? FirstStrikerOfficerId, int? WinnerOfficerId,
    int? LoserOfficerId, CampaignDuelParticipant Actor, CampaignDuelParticipant Opponent)
{
    public CampaignDuelResult Copy() => this with { Actor = Actor.Copy(), Opponent = Opponent.Copy() };
}

/// <summary>Registered numerical result awaiting the real campaign aftermath.
/// Presentation timing and skip input never select its winner or aftermath.</summary>
public sealed partial record CampaignRegisteredDuel(long Sequence, long CalendarCycleOrdinal,
    NativeMapSeasonDate Date, CampaignDuelPair Pair, CampaignDuelResult Result)
{
    //Null preserves unknown presentation knowledge in a historical142 save.
    //Real completed aftermath always supplies its actual pending operands.
    public CampaignDuelAftermathPresentation? AftermathPresentation { get; init; }
    public CampaignRegisteredDuel Copy() => this with
    { Result = Result.Copy(), AftermathPresentation = AftermathPresentation?.Copy() };
}

/// <summary>The campaign owns pair history and a registered duel's actual
/// business continuation. A missing list preserves unknown history.</summary>
public sealed record CampaignDuelServiceState(bool? Active, bool? Busy,
    CampaignDuelHistoryKnowledge History)
{
    public CampaignDuelHistoricalPair[]? HistoricalPairs { get; init; }
    public long LastSequence { get; init; }
    public CampaignRegisteredDuel? Registered { get; init; }
    public CampaignRegisteredDuel[] CompletedPresentations { get; init; } = [];

    public CampaignDuelServiceState Copy() => this with
    {
        HistoricalPairs = HistoricalPairs?.ToArray(), Registered = Registered?.Copy(),
        CompletedPresentations = CompletedPresentations.Select(value => value.Copy()).ToArray()
    };

    public static void Validate(GameState state)
    {
        if (state.CampaignDuelService is not { } service) return;
        if (!Enum.IsDefined(service.History) || service.LastSequence < 0 ||
            service.CompletedPresentations is null ||
            service.History == CampaignDuelHistoryKnowledge.Unknown && service.HistoricalPairs is not null ||
            service.History == CampaignDuelHistoryKnowledge.KnownEmpty && service.HistoricalPairs is { Length: > 0 } ||
            service.History == CampaignDuelHistoryKnowledge.KnownEntries && service.HistoricalPairs is not { Length: > 0 })
            throw new InvalidDataException("Campaign duel history knowledge is invalid.");
        foreach (var pair in service.HistoricalPairs ?? [])
            if (pair is null || pair.FirstOfficerId == pair.SecondOfficerId ||
                !state.Officers.ContainsKey(pair.FirstOfficerId) || !state.Officers.ContainsKey(pair.SecondOfficerId))
                throw new InvalidDataException("Campaign duel historical pair is invalid.");
        if (service.Registered is { } registered)
        {
            if (service.Active != true || service.Busy != true)
                throw new InvalidDataException("Registered duel lost its active campaign gate.");
            ValidateDuel(state, registered, service.LastSequence);
        }
        long previous = 0;
        foreach (var completed in service.CompletedPresentations)
        {
            ValidateDuel(state, completed, service.LastSequence);
            if (completed.Sequence <= previous || completed.Sequence == service.Registered?.Sequence)
                throw new InvalidDataException("Campaign duel presentation order is invalid.");
            previous = completed.Sequence;
        }
    }

    // Save/input validation only. Ordinary history queries do not invoke it.
    private static void ValidateDuel(GameState state, CampaignRegisteredDuel duel, long lastSequence)
    {
        if (duel is null || duel.Pair is not { } pair || duel.Result is not { } result ||
            duel.Sequence < 1 || duel.Sequence > lastSequence || duel.CalendarCycleOrdinal < 0 || duel.Date is null ||
            pair.ActorOfficerId == pair.OpponentOfficerId || pair.ActorUnitId == pair.OpponentUnitId ||
            pair.PlayerSide is < -1 or > 1 ||
            pair.AssistUnitId.HasValue != pair.AssistOfficerId.HasValue ||
            !state.Officers.ContainsKey(pair.ActorOfficerId) || !state.Officers.ContainsKey(pair.OpponentOfficerId) ||
            pair.AssistOfficerId is int assist && !state.Officers.ContainsKey(assist) ||
            result.Actor is null || result.Opponent is null || result.Actor.OfficerId != pair.ActorOfficerId ||
            result.Opponent.OfficerId != pair.OpponentOfficerId || !Enum.IsDefined(result.PlayerOutcome) ||
            !Enum.IsDefined(result.Aftermath))
            throw new InvalidDataException("Campaign registered duel is invalid.");
        ValidateParticipant(result.Actor); ValidateParticipant(result.Opponent);
        duel.AftermathPresentation?.Validate(state);
        duel.PresentationSnapshot?.Validate(pair);
        foreach (int? id in new[] { result.FirstStrikerOfficerId, result.WinnerOfficerId, result.LoserOfficerId })
            if (id.HasValue && id != pair.ActorOfficerId && id != pair.OpponentOfficerId)
                throw new InvalidDataException("Campaign duel result names another participant.");
    }

    private static void ValidateParticipant(CampaignDuelParticipant side)
    {
        if (!Enum.IsDefined(side.Outcome) || side.ReadinessTrajectory is not { Length: 6 } ||
            side.Rounds is not { Length: 5 } ||
            side.PreparedInitialReadiness is < 0 or > 255 || side.PreparedFinalReadiness is < 0 or > 255 ||
            side.ReadinessTrajectory.Any(value => value is < 0 or > 255))
            throw new InvalidDataException("Campaign duel participant result is invalid.");
        for (int index = 0; index < side.Rounds.Length; index++)
            if (side.Rounds[index] is not { } round || round.Number != index + 1 ||
                round.Damage < 0 || !Enum.IsDefined(round.Action))
                throw new InvalidDataException("Campaign duel round is invalid.");
    }
}

public sealed partial record GameState
{
    public CampaignDuelServiceState? CampaignDuelService { get; init; }
    public byte? DuelOpportunityCount { get; init; }
}
