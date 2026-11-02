using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState ApplyDuelLoserAftermath(GameState state, SourceWorldContext current,
        int winnerId, int loserId, CampaignDuelAftermath aftermath, CampaignDuelOutcome playerOutcome,
        NativeArmyProgressStageMoment moment, List<CampaignEffectRequest> effects,
        List<CampaignDuelInjuryNotice> injuries, List<CampaignEffectRequest> traitRefreshes)
    {
        int winner = DuelAftermathPerson(current, winnerId), loser = DuelAftermathPerson(current, loserId);
        if (!DuelAftermathNeed(current.ReadPersonNativeValidity(winner), "loser_winner_validity") ||
            !DuelAftermathNeed(current.ReadPersonNativeValidity(loser), "loser_validity")) return state;
        if (playerOutcome == CampaignDuelOutcome.Won)
            state = AppendDuelOutcomeHistory(state, current, 0, (int)aftermath, winnerId, loserId, moment, effects);
        else if (playerOutcome == CampaignDuelOutcome.Lost)
            state = AppendDuelOutcomeHistory(state, current, 1, (int)aftermath, loserId, winnerId, moment, effects);
        var loserArmy = DuelReportArmy(current, loser);
        var winnerArmy = DuelReportArmy(current, winner);
        if (!DuelAftermathNeed(current.ReadArmyUnitNativeValidity(loserArmy), "loser_Army_validity") ||
            !DuelAftermathNeed(current.ReadArmyUnitNativeValidity(winnerArmy), "loser_winner_Army_validity")) return state;
        if (aftermath != CampaignDuelAftermath.None)
        {
            if (aftermath is CampaignDuelAftermath.Death or CampaignDuelAftermath.Capture)
            {
                int victimId = ArmyDestructionQueue.ResolveUnitId(state, loserArmy.PhysicalSlot)
                    ?? throw DuelMissing("aftermath_defeat_victim_identity");
                int winningId = ArmyDestructionQueue.ResolveUnitId(state, winnerArmy.PhysicalSlot)
                    ?? throw DuelMissing("aftermath_defeat_winner_identity");
                var request = new UnitDefeatRequest(victimId, winningId, null)
                { Cause = aftermath == CampaignDuelAftermath.Death ? UnitDefeatCause.DuelDeath : UnitDefeatCause.DuelCapture };
                var queue = current.OwnedDestructionQueue ?? state.PendingCombatSettlements
                    ?? throw DuelMissing("aftermath_defeat_manager140");
                var admitted = CombatSettlementRules.Admit(queue, request, ArmyDestructionQueue.BindCurrent(state, current));
                if (admitted.Kind is null) throw DuelMissing("aftermath_defeat_admission:" + admitted.MissingInput);
                if (admitted.Kind == CombatSettlementAdmissionKind.Full)
                {
                    return SettleImmediateDefeat(state, request,
                        forcedCapture: request.Cause == UnitDefeatCause.DuelCapture,
                        resumePoint: DuelAftermathResumePoint.AfterDefeat);
                }
                current.BindOwnedDestructionQueue(admitted.Queue, state);
                return state with { PendingCombatSettlements = admitted.Queue };
            }
            return state; //The real unmatched aftermath branch.
        }
        if (DuelAftermathPersonIsPlayer(state, current, winner, true) ||
            DuelAftermathPersonIsPlayer(state, current, loser, true))
            state = AppendDuelMoraleNotice(state, current, 1, loserId, moment, effects);
        int trait = 0;
        if (DuelHasTrait(current, winner, 61)) trait = 5;
        else if (DuelHasTrait(current, winner, 62)) trait = 10;
        else if (DuelHasTrait(current, winner, 144))
            trait = DuelTrait144Age(state, current, winner, moment) > 60 ? 10 : 5;
        int strength = Math.Clamp(DuelAftermathStrength(state, current, winner) / 10, 0, 10);
        int difficultyCorrection = 0;
        var force = current.ReadArmyUnitForceReference(loserArmy) ?? throw DuelMissing("aftermath_loser_Force");
        if (DuelAftermathNeed(current.ReadForceNativeValidity(force), "loser_Force_validity") &&
            DuelForceIsPlayer(state, current, force))
        {
            int selector = DuelAftermathNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "loser_settings_selector");
            int difficulty = DuelAftermathNeed(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x134 : 0x28),
                "loser_difficulty");
            difficultyCorrection = difficulty switch { 0 => 20, 1 => 10, _ => 0 };
        }
        int loss = Math.Clamp(unchecked(DuelCampaignAftermathParameters.Read(state, "loser-morale-loss") -
            difficultyCorrection + strength + trait), 30, 70);
        force = current.ReadArmyUnitForceReference(loserArmy) ?? throw DuelMissing("aftermath_loser_modifier_Force");
        if (DuelAftermathNeed(current.ReadForceNativeValidity(force), "loser_modifier_Force_validity"))
            loss = unchecked(loss - DuelAftermathNeed(current.ReadBaseForceModifier(force, 42), "loser_Force_effect42"));
        ushort rawHex = DuelAftermathNeed(current.ReadArmyUnitRawHex2A(winnerArmy), "loser_winner_Hex2A");
        var source = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0)
            ?? throw DuelMissing("aftermath_loser_winner_Hex_pointer");
        var applied = ArmyMoraleZeroRoutingCurrent.ApplyImmediateFromHex(state, current, loserArmy,
            unchecked(-loss), source, TrapMobility(), Immediate);
        state = applied.State ?? throw DuelMissing("aftermath_loser_morale:" + applied.UnresolvedOwner);
        if (state.ArmyDefeatContinuation is not null) return state;
        return CompleteDuelLoserMoraleTail(state, current, winnerId, loserId, loserArmy, moment, injuries);

        GameState Immediate(GameState value, UnitDefeatRequest request)
            => SettleImmediateDefeat(value, request, forcedCapture: false,
                resumePoint: DuelAftermathResumePoint.AfterMoraleRouting);

        GameState SettleImmediateDefeat(GameState value, UnitDefeatRequest request, bool forcedCapture,
            DuelAftermathResumePoint resumePoint)
        {
            var pool = current.ReadOwnedCurrentListPool() ?? throw DuelMissing("aftermath_immediate_pool");
            if (current.ReadOwnedPersonCommandManager() is null)
            {
                if (value.PersonnelCommands is null) throw DuelMissing("aftermath_immediate_personnel_manager");
                PersonReturnCommands.BindCurrent(value, current);
            }
            value = value with { PersonnelCommands = current.ReadOwnedPersonCommandManager()! };
            BindActorlessDestructionInputs(value, current);
            var changed = new HashSet<int>();
            var stage = new OwnedArmyDestructionStage(this);
            var sampler = FSampler(value, moment);
            //The real registered result, not a particular host's normal-period
            //preparation, owns this stage5 aftermath. The same operation must
            //retain that result before an immediate defeat can ask for input.
            bool allowPause = moment is { SchedulerStageIndex: (byte)NativeArmyProgressStage.DuelOpportunity,
                    Boundary: NativeArmyProgressStageBoundary.StageEntry } &&
                value.CampaignDuelService is { Active: true, Busy: true, Registered: { } registered } &&
                registered.Date == moment.PreWriterThirdDate && registered.CalendarCycleOrdinal == moment.CalendarCycleOrdinal;
            value = forcedCapture
                ? stage.ApplyForcedDuelCaptureCurrent(value, current, pool, request, sampler,
                    moment.PreWriterThirdDate, effects, Refresh, person => changed.Add(person),
                    allowPlayerRulerPause: allowPause).State
                : stage.ApplyBattleDefeatCurrent(value, current, pool, request, sampler,
                    moment.PreWriterThirdDate, effects, Refresh, person => changed.Add(person),
                    allowPlayerRulerPause: allowPause);
            void Refresh(ArmyUnitReference army) => traitRefreshes.Add(new(null, null)
            { ArmyTraitViewRefresh = new(ArmyDestructionQueue.ResolveUnitId(value, army.PhysicalSlot)
                ?? throw DuelMissing("aftermath_immediate_trait_Army")) });
            if (value.ArmyDefeatContinuation is not null)
            {
                long sequence = value.CampaignDuelService?.Registered?.Sequence
                    ?? throw DuelMissing("aftermath_immediate_registered_sequence");
                value = value with { DuelAftermathContinuation = new(sequence, resumePoint, injuries.ToImmutableArray()) };
            }
            if (changed.Count == 0) return value;
            var people = new Dictionary<int, OfficerState>(value.Officers);
            foreach (int physical in changed)
            {
                int id = DuelAftermathNeed(current.ReadPersonIdentity(physical), "immediate_changed_identity");
                var officer = people.GetValueOrDefault(id) ?? throw DuelMissing("aftermath_immediate_changed_Person");
                var activation = OfficerTraitActivationSourceAdapter.Read(current, physical);
                if (activation is not { DurationSlotCount: 9, ActiveSlots: not null })
                    throw DuelMissing("aftermath_immediate_changed_activation");
                people[id] = officer with { TraitActivation = activation };
            }
            return value with { Officers = people };
        }
    }

    //Resume2709A0 after its actual immediate destruction, using the original
    //Army pointer. The loser's E4 may now name a captor, Base or free Area;
    //looking up that new position would change which morale byte is tested.
    internal GameState CompleteDuelLoserMoraleTail(GameState state, SourceWorldContext current,
        int winnerId, int loserId, ArmyUnitReference originalLoserArmy,
        NativeArmyProgressStageMoment moment, List<CampaignDuelInjuryNotice> injuries)
    {
        int winner = DuelAftermathPerson(current, winnerId), loser = DuelAftermathPerson(current, loserId);
        if (DuelReportMorale(current, originalLoserArmy) > 0 && DuelAftermathHealth(current, loser) < 3 &&
            DuelAftermathReadiness(state, loserId) <
                DuelCampaignAftermathParameters.Read(state, "loser-injury-readiness-threshold"))
            state = ApplyDuelInjury(state, current, loser, winner, moment, injuries);
        return state;
    }
}
