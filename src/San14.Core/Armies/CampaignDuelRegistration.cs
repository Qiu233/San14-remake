namespace San14.Core;

/// <summary>Business registration within the caller's private campaign
/// transaction. The real aftermath consumer must finish Registered before
/// this state can continue ordinary campaign stages.</summary>
public static class CampaignDuelRegistration
{
    public static GameState Register(GameState state, CampaignDuelPair pair, CampaignDuelResult result,
        NativeMapSeasonDate date, long calendarCycleOrdinal)
    {
        var service = state.CampaignDuelService ?? throw Missing("service");
        if (service.Active != false || service.Busy != false || service.Registered is not null)
            throw Missing("inactive_registration_boundary");
        var history = service.History switch
        {
            CampaignDuelHistoryKnowledge.KnownEmpty => service.HistoricalPairs ?? [],
            CampaignDuelHistoryKnowledge.KnownEntries => service.HistoricalPairs ?? throw Missing("history_entries"),
            _ => throw Missing("history_knowledge")
        };
        if (pair.ActorOfficerId != result.Actor.OfficerId || pair.OpponentOfficerId != result.Opponent.OfficerId)
            throw Missing("selected_participant_identity");
        int world = state.CampaignEventWorld?.ScenarioId ?? throw Missing("scenario_identity");
        byte count = state.DuelOpportunityCount ?? throw Missing("opportunity_count");
        long sequence = checked(service.LastSequence + 1);
        var people = new Dictionary<int, OfficerState>(state.Officers);
        Apply(result.Actor); Apply(result.Opponent);
        return state with
        {
            Officers = people,
            DuelOpportunityCount = world is 0 or 151 ? unchecked((byte)(count + 1)) : (byte)Math.Min(count + 1, 3),
            CampaignDuelService = service with
            {
                Active = true, Busy = true, History = CampaignDuelHistoryKnowledge.KnownEntries,
                HistoricalPairs = [.. history, new(pair.ActorOfficerId, pair.OpponentOfficerId)],
                LastSequence = sequence, Registered = new(sequence, calendarCycleOrdinal, date, pair, result)
            }
        };

        void Apply(CampaignDuelParticipant side)
        {
            var person = people.GetValueOrDefault(side.OfficerId) ?? throw Missing("participant");
            var condition = person.DuelCondition ?? throw Missing("participant_readiness_owner");
            if (condition.Readiness != side.ReadinessBefore) throw Missing("participant_readiness_lifecycle");
            if (side.WriteReadiness) people[person.Id] = person with
            { DuelCondition = condition with { Readiness = side.ReadinessAfter } };
        }
    }

    /// <summary>37BD50 after the actual phase21 business callbacks have all
    /// returned. Keeps pending display data rather than fabricating an App ACK.</summary>
    public static GameState CompleteAftermath(GameState state, long sequence,
        CampaignDuelAftermathPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        var service = state.CampaignDuelService ?? throw Missing("completion_service");
        var registered = service.Registered ?? throw Missing("completion_registered_result");
        if (service.Active != true || service.Busy != true || registered.Sequence != sequence ||
            registered.AftermathPresentation is not null)
            throw Missing("completion_lifecycle");
        return state with { CampaignDuelService = service with
        {
            Active = false, Busy = false, Registered = null,
            CompletedPresentations = [.. service.CompletedPresentations,
                registered with { AftermathPresentation = presentation }]
        } };
    }

    private static InvalidDataException Missing(string owner) => new("current.DuelRegistration_" + owner);
}
