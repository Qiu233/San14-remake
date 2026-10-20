using San14.Core;

namespace San14.Data;

public sealed record NormalCombatRefreshResult(GameState? State, NormalCombatMixedProjection? Projection, string? UnresolvedOwner);
public sealed record StrategyCombatEventScanResult(GameState? State, NormalCombatMixedProjection? Combat,
    CampaignEventConditionScan? Events, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Explicit strategy-entry refresh. Accepted pairs, source flags
    /// and unconsumed effects publish together, without running App admission.</summary>
    public NormalCombatRefreshResult TryRefreshNormalCombatEngagements(GameState state)
    {
        try
        {
            var previous = state.NormalCombatRefresh ?? throw NormalCombatMixedProducer.Missing("saved_refresh_owner");
            if (previous.Generation == long.MaxValue) throw NormalCombatMixedProducer.Missing("refresh_generation_exhausted");
            var current = CaptureWritableTransportRouteCurrent(state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            OwnedArmyDestructionCurrent.BindOwnedCurrentContext(state, current);
            CaptureBaseThreatInputs.BindCurrent(state, current);
            PersonReturnCommands.BindCurrent(state, current);
            current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
            var tactics = Context(current);
            var queries = new ArmyMapGroupingCurrentQueries(state, current, _scenario, _governorInputs,
                Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
            var projection = new NormalCombatMixedProducer(state, current, tactics, queries).Generate();
            var presentation = new NormalCombatPresentationCapture(state, current, queries, tactics);
            foreach (var actor in projection.Actors) presentation.ActorMessages(actor);
            var working = NormalCombatAssociationTail.Apply(state, current, queries, projection, presentation);
            long generation = previous.Generation + 1;
            var effects = presentation.Effects.Select(effect => ConvertEffect(state, effect)).ToArray();
            var effectsOwner = state.CampaignEffectRequests ?? throw NormalCombatMixedProducer.Missing("campaign_effect_requests");
            var engagements = projection.OrderedPairs.Select(pair => new CombatEngagementPair(
                new(ParticipantKind(pair.Actor.Kind), pair.Actor.ForceSlot),
                new(ParticipantKind(pair.Target.Kind), pair.Target.ForceSlot))).ToArray();
            return new(working with { NormalCombatRefresh = new(generation, engagements),
                CampaignEffectRequests = effectsOwner.Append(CampaignEffectSource.NormalCombatRefresh, generation, effects) }, projection, null);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    /// <summary>The strategy boundary refreshes before the actual ordered event
    /// scan. A later unknown condition discards both; it never publishes a partial
    /// manager, contact flag or presentation batch. Event execution is separate.</summary>
    public StrategyCombatEventScanResult TryRefreshCombatAndScanStrategyEvents(GameState state,
        NativeArmyProgressStageMoment? moment = null)
    {
        var refreshed = TryRefreshNormalCombatEngagements(state);
        if (refreshed.State is not { } working) return new(null, null, null, refreshed.UnresolvedOwner);
        try
        {
            var scan = ScanOrdinaryCampaignEventConditions(working, moment);
            return new(scan.State, refreshed.Projection, scan, null);
        }
        catch (InvalidDataException error) { return new(null, null, null, error.Message); }
    }

    private static CombatParticipantKind ParticipantKind(int native) => native switch
    {
        27 => CombatParticipantKind.Army, 5 => CombatParticipantKind.City,
        6 => CombatParticipantKind.Gate, 11 => CombatParticipantKind.FieldObject,
        _ => throw NormalCombatMixedProducer.Missing("participant_kind")
    };
    private static CampaignEffectRequest ConvertEffect(GameState state, CapturedCombatEffect effect)
    {
        if (effect.PersonLine is { } speech)
        {
            var kind = speech.EntityKind == 1 ? CombatParticipantKind.Army :
                speech.EntityId <= 51 ? CombatParticipantKind.City : CombatParticipantKind.Gate;
            var action = speech.Action switch
            {
                null => CombatSpeechEvent.UnresolvedGarrisonContact,
                0x11 => CombatSpeechEvent.GarrisonUnderPressure, 0x27 => CombatSpeechEvent.GarrisonFormationContact,
                0x28 => CombatSpeechEvent.GarrisonContact, 0x23 => CombatSpeechEvent.UnexpectedContact,
                0x24 => CombatSpeechEvent.EngageChargingArmy, 0x26 => CombatSpeechEvent.EngageTarget,
                0x25 => CombatSpeechEvent.ReplyToEngagement, _ => throw NormalCombatMixedProducer.Missing("speech_action")
            };
            return new(new(kind, kind == CombatParticipantKind.Army ? EntityId(27, speech.EntityId) : speech.EntityId,
                speech.OfficerId, action, speech.HexId,
                speech.Action == 0x26 ? ParticipantKind(speech.Argument1) : null,
                speech.Action == 0x26 ? EntityId(speech.Argument1, speech.Argument2) : null,
                speech.Action is 0x23 or 0x24 or 0x25 ? speech.Argument1 : null,
                speech.AdmissionForce, speech.AdmissionColor) { GarrisonChoice = speech.GarrisonChoice }, null);
        }
        var report = effect.Report ?? throw NormalCombatMixedProducer.Missing("effect_payload");
        return new(null, new(report.DistrictId, report.HexId, report.CampaignDay, report.OfficerId,
            report.ArmyName, report.TargetName, ParticipantKind(report.TargetKind), EntityId(report.TargetKind, report.TargetId),
            report.ForceSlot, report.ForceColor, report.KingdomNameIndex, report.RulerId));

        int EntityId(int kind, int physical)
        {
            if (kind == 6) return physical + 51;
            if (kind != 27) return physical;
            var receiver = state.ReadArmyLifecycle()?.ReceiverSlots().ElementAtOrDefault(physical)
                ?? throw NormalCombatMixedProducer.Missing("effect_Army_receiver");
            return receiver.ActiveUnitId ?? receiver.PendingImage?.Id
                ?? throw NormalCombatMixedProducer.Missing("effect_Army_identity");
        }
    }
}
