using San14.Core;

namespace San14.Data;

/// <summary>Actual business writes and still-pending presentation operands.
/// Returning this value does not clear the active campaign service.</summary>
public sealed record DuelCampaignAftermathResult(GameState State,
    CampaignDuelInjuryNotice[] Injuries, CampaignDuelArmyNotice[] ArmyNotices,
    CampaignEffectRequest[] TraitRefreshes);

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState CompleteDuelAftermathDuringArmyOperation(GameState state,
        SourceWorldContext current, NativeArmyProgressStageMoment moment,
        DuelAftermathContinuation? resumed = null, ArmyUnitReference? originalLoserArmy = null)
    {
        long sequence = state.CampaignDuelService?.Registered?.Sequence
            ?? throw DuelMissing("aftermath_completion_registration");
        //Existing relation owners provide the exact kin/likes arguments to
        //the reached history formatter. This binding occurs only for a duel.
        _personRelationInputs.BindCurrent(state, current);
        var effects = new List<CampaignEffectRequest>();
        var applied = ApplyRegisteredDuelAftermathCore(state, current, moment, effects, resumed, originalLoserArmy);
        var pending = applied.State.CampaignEffectRequests ?? throw DuelMissing("aftermath_effect_owner");
        if (effects.Count != 0)
            pending = pending.Append(CampaignEffectSource.ArmyBattleDefeat, moment.CalendarCycleOrdinal, effects);
        if (applied.TraitRefreshes.Length != 0)
            pending = pending.Append(CampaignEffectSource.ArmyTraitMaintenance, moment.CalendarCycleOrdinal, applied.TraitRefreshes);
        var published = applied.State with { CampaignEffectRequests = pending };
        if (published.DuelAftermathContinuation is not null) return published;
        return CampaignDuelRegistration.CompleteAftermath(published, sequence,
            new(applied.Injuries, applied.ArmyNotices));
    }

    /// <summary>377590's real business prefix, within the caller's detached
    /// stage5 operation. Unknown callbacks reject that whole operation; no
    /// saved world, count, service gate or pending presentation is patched.</summary>
    public DuelCampaignAftermathResult ApplyRegisteredDuelAftermathDuringArmyOperation(
        GameState state, SourceWorldContext current, NativeArmyProgressStageMoment moment,
        List<CampaignEffectRequest> effects)
        => ApplyRegisteredDuelAftermathCore(state, current, moment, effects, null, null);

    private DuelCampaignAftermathResult ApplyRegisteredDuelAftermathCore(GameState state,
        SourceWorldContext current, NativeArmyProgressStageMoment moment, List<CampaignEffectRequest> effects,
        DuelAftermathContinuation? resumed, ArmyUnitReference? originalLoserArmy)
    {
        var service = state.CampaignDuelService ?? throw DuelMissing("aftermath_service");
        var registered = service.Registered ?? throw DuelMissing("aftermath_registered_result");
        if (service.Active != true || service.Busy != true ||
            registered.CalendarCycleOrdinal != moment.CalendarCycleOrdinal ||
            registered.Date != moment.PreWriterThirdDate)
            throw DuelMissing("aftermath_operation_lifecycle");
        var result = registered.Result;
        int actor = registered.Pair.ActorOfficerId, opponent = registered.Pair.OpponentOfficerId;
        var injuries = resumed?.Injuries.ToList() ?? new List<CampaignDuelInjuryNotice>();
        var notices = new List<CampaignDuelArmyNotice>();
        var traitRefreshes = new List<CampaignEffectRequest>();

        //379790 dispatches the ACTOR outcome at result+10, independently of
        //the player-relative outcome at result+0 (including AI vs AI).
        switch (result.Actor.Outcome)
        {
            case CampaignDuelOutcome.Draw:
                state = ApplyDuelDrawAftermath(state, current, actor, opponent, moment, effects, injuries);
                state = ApplyDuelRewardDuringArmyOperation(state, current, actor, 2, opponent);
                state = ApplyDuelRewardDuringArmyOperation(state, current, opponent, 2, actor);
                break;
            case CampaignDuelOutcome.Won:
                if (resumed is null)
                {
                    state = ApplyDuelWinnerAftermath(state, current, actor, opponent, moment, effects, injuries);
                    state = ApplyDuelLoserAftermath(state, current, actor, opponent,
                        result.Aftermath, result.PlayerOutcome, moment, effects, injuries, traitRefreshes);
                }
                else if (resumed.ResumePoint == DuelAftermathResumePoint.AfterMoraleRouting)
                    state = CompleteDuelLoserMoraleTail(state, current, actor, opponent,
                        originalLoserArmy ?? throw DuelMissing("resume_original_loser_Army"), moment, injuries);
                if (state.DuelAftermathContinuation is not null)
                    return new(state, injuries.ToArray(), [], traitRefreshes.ToArray());
                state = ApplyDuelRewardDuringArmyOperation(state, current, actor, 0, opponent);
                state = ApplyDuelRewardDuringArmyOperation(state, current, opponent, 1, actor);
                break;
            case CampaignDuelOutcome.Lost:
                if (resumed is null)
                {
                    state = ApplyDuelWinnerAftermath(state, current, opponent, actor, moment, effects, injuries);
                    state = ApplyDuelLoserAftermath(state, current, opponent, actor,
                        result.Aftermath, result.PlayerOutcome, moment, effects, injuries, traitRefreshes);
                }
                else if (resumed.ResumePoint == DuelAftermathResumePoint.AfterMoraleRouting)
                    state = CompleteDuelLoserMoraleTail(state, current, opponent, actor,
                        originalLoserArmy ?? throw DuelMissing("resume_original_loser_Army"), moment, injuries);
                if (state.DuelAftermathContinuation is not null)
                    return new(state, injuries.ToArray(), [], traitRefreshes.ToArray());
                state = ApplyDuelRewardDuringArmyOperation(state, current, opponent, 0, actor);
                state = ApplyDuelRewardDuringArmyOperation(state, current, actor, 1, opponent);
                break;
            case CampaignDuelOutcome.Undetermined:
                break; //379790's real unmatched return.
            default:
                throw DuelMissing("aftermath_actor_outcome");
        }
        //37A710 is called AFTER all business/rewards, not at registration.
        if (result.Actor.Outcome is CampaignDuelOutcome.Won or CampaignDuelOutcome.Draw)
            AppendUnitNotice(actor, opponent, result.Actor.Outcome);
        if (result.Opponent.Outcome == CampaignDuelOutcome.Won)
            AppendUnitNotice(opponent, actor, CampaignDuelOutcome.Won);
        return new(state, injuries.ToArray(), notices.ToArray(), traitRefreshes.ToArray());

        void AppendUnitNotice(int personId, int otherId, CampaignDuelOutcome outcome)
        {
            int person = DuelAftermathPerson(current, personId);
            if (!DuelAftermathNeed(current.ReadPersonNativeValidity(person), "unit_notice_Person_validity")) return;
            int other = DuelAftermathPerson(current, otherId);
            if (!DuelAftermathNeed(current.ReadPersonNativeValidity(other), "unit_notice_opponent_validity")) return;
            var army = DuelReportArmy(current, person);
            if (!DuelAftermathNeed(current.ReadArmyUnitNativeValidity(army), "unit_notice_Army_validity")) return;
            int unitId = ArmyDestructionQueue.ResolveUnitId(state, army.PhysicalSlot)
                ?? throw DuelMissing("aftermath_unit_notice_Army_identity");
            int commander = DuelAftermathNeed(current.ReadArmyUnitCommanderId(army), "unit_notice_commander");
            var position = PersonReturnCommands.ResolvePersonPosition(state, current, person, moment.PreWriterThirdDate);
            int hex = position.PhysicalSlot ?? 48400;
            notices.Add(new(unitId, commander, hex,
                DuelAftermathNeed(current.ReadPersonIdentity(other), "unit_notice_opponent_identity"), outcome, result.Aftermath));
        }
    }

    private GameState ApplyDuelDrawAftermath(GameState state, SourceWorldContext current,
        int actorId, int opponentId, NativeArmyProgressStageMoment moment,
        List<CampaignEffectRequest> effects, List<CampaignDuelInjuryNotice> injuries)
    {
        int actor = DuelAftermathPerson(current, actorId), opponent = DuelAftermathPerson(current, opponentId);
        if (!DuelAftermathNeed(current.ReadPersonNativeValidity(actor), "draw_actor_validity") ||
            !DuelAftermathNeed(current.ReadPersonNativeValidity(opponent), "draw_opponent_validity")) return state;
        if (DuelAftermathPersonIsPlayer(state, current, actor, false))
            state = AppendDuelOutcomeHistory(state, current, 2, -1, actorId, opponentId, moment, effects);
        else if (DuelAftermathPersonIsPlayer(state, current, opponent, false))
            state = AppendDuelOutcomeHistory(state, current, 2, -1, opponentId, actorId, moment, effects);
        if (DuelAftermathHealth(current, actor) < 2 && DuelAftermathReadiness(state, actorId) <=
            DuelCampaignAftermathParameters.Read(state, "draw-injury-readiness-threshold"))
            state = ApplyDuelInjury(state, current, actor, opponent, moment, injuries);
        if (DuelAftermathHealth(current, opponent) < 2 && DuelAftermathReadiness(state, opponentId) <=
            DuelCampaignAftermathParameters.Read(state, "draw-injury-readiness-threshold"))
            state = ApplyDuelInjury(state, current, opponent, actor, moment, injuries);
        state = RestoreDuelReadinessDuringArmyOperation(state, current, actorId);
        state = RestoreDuelReadinessDuringArmyOperation(state, current, opponentId);
        if (DuelAftermathPersonIsPlayer(state, current, actor, true) ||
            DuelAftermathPersonIsPlayer(state, current, opponent, true))
        {
            state = AppendDuelMoraleNotice(state, current, 2, actorId, moment, effects);
            state = AppendDuelMoraleNotice(state, current, 2, opponentId, moment, effects);
        }
        var firstArmy = DuelReportArmy(current, actor);
        if (DuelAftermathNeed(current.ReadArmyUnitNativeValidity(firstArmy), "draw_first_Army_validity"))
        {
            state = ApplyDuelDirectMorale(state, current, firstArmy,
                DuelCampaignAftermathParameters.Read(state, "draw-morale-gain"));
            //This second lookup really belongs inside the first-valid branch.
            var secondArmy = DuelReportArmy(current, opponent);
            if (DuelAftermathNeed(current.ReadArmyUnitNativeValidity(secondArmy), "draw_second_Army_validity"))
                state = ApplyDuelDirectMorale(state, current, secondArmy,
                    DuelCampaignAftermathParameters.Read(state, "draw-morale-gain"));
        }
        return state;
    }

    private GameState ApplyDuelWinnerAftermath(GameState state, SourceWorldContext current,
        int winnerId, int loserId, NativeArmyProgressStageMoment moment,
        List<CampaignEffectRequest> effects, List<CampaignDuelInjuryNotice> injuries)
    {
        int winner = DuelAftermathPerson(current, winnerId), loser = DuelAftermathPerson(current, loserId);
        if (!DuelAftermathNeed(current.ReadPersonNativeValidity(winner), "winner_validity") ||
            !DuelAftermathNeed(current.ReadPersonNativeValidity(loser), "winner_opponent_validity")) return state;
        if (DuelAftermathHealth(current, winner) < 2 && DuelAftermathReadiness(state, winnerId) <=
            DuelCampaignAftermathParameters.Read(state, "winner-injury-readiness-threshold"))
            state = ApplyDuelInjury(state, current, winner, loser, moment, injuries);
        state = RestoreDuelReadinessDuringArmyOperation(state, current, winnerId);
        if (DuelAftermathPersonIsPlayer(state, current, winner, true) ||
            DuelAftermathPersonIsPlayer(state, current, loser, true))
            state = AppendDuelMoraleNotice(state, current, 0, winnerId, moment, effects);
        var army = DuelReportArmy(current, winner);
        if (DuelAftermathNeed(current.ReadArmyUnitNativeValidity(army), "winner_Army_validity"))
            state = ApplyDuelDirectMorale(state, current, army,
                DuelCampaignAftermathParameters.Read(state, "winner-morale-gain"));
        return state;
    }

    private GameState ApplyDuelInjury(GameState state, SourceWorldContext current,
        int person, int other, NativeArmyProgressStageMoment moment, List<CampaignDuelInjuryNotice> injuries)
    {
        var applied = PersonArmyInjurySource.ApplyDuel(state, current, person, new(other),
            CreateArmyDestructionAbilities(current), FSampler(state, moment));
        if (applied.NativeReturn is null)
            throw DuelMissing("aftermath_injury_27D900:" + applied.UnresolvedOwner);
        if (applied.Receipt is { } receipt &&
            (receipt.PlayerDistrictNotice || receipt.ArmyNoticeKind is not null))
        {
            int? unitId = null, hex = null;
            if (receipt.ArmyNoticeKind is not null)
            {
                if (receipt.ArmyNoticeKind != 13 || receipt.ArmyNoticePhysical is not int armySlot)
                    throw DuelMissing("aftermath_injury_Army_notice_contract");
                unitId = ArmyDestructionQueue.ResolveUnitId(state, armySlot)
                    ?? throw DuelMissing("aftermath_injury_Army_notice_identity");
                var position = PersonReturnCommands.ResolvePersonPosition(state, current, person, moment.PreWriterThirdDate);
                hex = position.PhysicalSlot ?? 48400;
            }
            injuries.Add(new(receipt.PersonId, checked((byte)receipt.BeforeHealthStateId),
                checked((byte)receipt.AfterHealthStateId), receipt.PlayerDistrictNotice, unitId, hex));
        }
        int id = DuelAftermathNeed(current.ReadPersonIdentity(person), "injury_Person_identity");
        var officer = state.Officers.GetValueOrDefault(id) ?? throw DuelMissing("aftermath_injury_Person_owner");
        var health = current.ReadOfficerHealth(id) ?? throw DuelMissing("aftermath_injury_health_owner");
        return officer.Health == health ? state : state with
        { Officers = new(state.Officers) { [id] = officer with { Health = health } } };
    }

    private static int DuelAftermathHealth(SourceWorldContext current, int person) =>
        DuelAftermathNeed(OfficerHealthSourceAdapter.ReadByte(current, person), "health");

    private static byte DuelAftermathReadiness(GameState state, int id) =>
        DuelAftermathNeed(state.Officers.GetValueOrDefault(id)?.DuelCondition?.Readiness, "readiness");

    private static bool DuelAftermathPersonIsPlayer(GameState state, SourceWorldContext current,
        int person, bool explicitValidity)
    {
        var force = current.ReadPersonForce(person) ?? throw DuelMissing("aftermath_Person_Force");
        if (explicitValidity && !DuelAftermathNeed(current.ReadForceNativeValidity(force), "player_Force_validity")) return false;
        return DuelForceIsPlayer(state, current, force);
    }

    private static GameState ApplyDuelDirectMorale(GameState state, SourceWorldContext current,
        ArmyUnitReference army, int adjustment)
    {
        //223990's first cap clamps the SUM, the second caps the raw getter,
        //and its third independent Force lookup belongs to the final store.
        int clamp = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(army), current);
        int getter = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(army), current);
        byte raw = DuelAftermathNeed(current.ReadArmyReturnResourceFields(army)?.Morale1A, "direct_morale_raw");
        int requested = unchecked(Math.Min((int)raw, getter) + adjustment);
        requested = requested < 0 ? 0 : Math.Min(requested, clamp);
        int setter = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(army), current);
        byte after = ArmyMoraleSource.SetByte2419A0(requested, setter);
        int physical = DuelAftermathNeed(army.PhysicalSlot, "direct_morale_receiver");
        current.WriteArmyReturnMorale(physical, after);
        int id = ArmyDestructionQueue.ResolveUnitId(state, physical) ?? throw DuelMissing("aftermath_direct_morale_Army_identity");
        var image = state.Units.GetValueOrDefault(id) ?? throw DuelMissing("aftermath_direct_morale_active_Army");
        return state with { Units = new(state.Units) { [id] = ArmyMoraleSourceAdapter.WithCurrent(image, after) } };
    }
}
