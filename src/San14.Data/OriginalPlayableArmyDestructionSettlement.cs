using San14.Core;

namespace San14.Data;

/// <summary>The playable period supplies casualty requests. Nonfatal ordinary
/// strikes share the real178EC0 body and Force/personality tail; a reached
/// death uses the saved source request queue,259360 and28E6D0 once.</summary>
public sealed class OriginalPlayableArmyDestructionSettlement(OriginalOwnedArmyCampaignRules campaign)
    : IPlayableFieldSettlementRules
{
    public const string RuleId = "ce-Army-1745B0-450830-259360-28E6D0-period-v1";
    public const string NonfatalRuleId = "ce-Army-external-178EC0-Force-personality-period-v1";

    public PlayableFieldSettlementResult Apply(GameState state, UnitState attacker,
        UnitState defender, PlayableFieldCombatResult damage, int period)
    {
        if (attacker.NativeArmyOrigin is null && defender.NativeArmyOrigin is null)
            return new(PlayableSiegeSettlementOutcome.NotApplicable, state, damage);
        if (damage.RemainingAttackers != 0 && damage.RemainingDefenders != 0)
            return ApplyNonfatal(state, attacker, defender, damage);
        if (attacker.NativeArmyOrigin?.NativeSlotIndex is null ||
            defender.NativeArmyOrigin?.NativeSlotIndex is null)
            return new(PlayableSiegeSettlementOutcome.Unresolved, state, damage);
        GameState current = campaign.AdvancePlayableCalendar(state);
        //A transport killed by the forward strike first loses its cargo in
        //178EC0. The later259360 destructor must see that emptied receiver,
        //and the removed Army cannot supply the prototype reverse request.
        if (damage.RemainingDefenders == 0 && attacker.NativeTacticRuntime?.Status10 == 1 &&
            defender.NativeTacticRuntime?.Status10 == 2)
        {
            var fatal = campaign.TryApplyExternalTransportArmyDestruction(current, attacker.Id, defender.Id, damage.DefenderLoss);
            if (fatal.Outcome != OriginalTransportArmyFatalApplicationOutcome.Committed || fatal.Receipt is not { } receipt)
                return new(PlayableSiegeSettlementOutcome.Unresolved, state, damage, fatal.MissingOwner);
            var actual = damage with { DefenderLoss = defender.Troops, RemainingDefenders = 0,
                AttackerLoss = attacker.Troops - receipt.SurvivingAttackerTroops, RemainingAttackers = receipt.SurvivingAttackerTroops };
            PlayableBattleValidation.Validate(attacker.Troops, defender.Troops, actual);
            return new(PlayableSiegeSettlementOutcome.Committed, fatal.State, actual,
                OriginalOwnedArmyCampaignRules.TransportFatalApplicationRevision)
                { Reports = [.. current.PeriodReports.Skip(state.PeriodReports.Length), .. fatal.Reports] };
        }
        if (damage.RemainingAttackers == 0 && damage.RemainingDefenders > 0 &&
            attacker.NativeTacticRuntime?.Status10 == 1 && defender.NativeTacticRuntime?.Status10 == 2)
            return ApplyLiveTransportReverseFatal(state, current, attacker, defender, damage);
        //Outside the actual forward transport-fatal branch above, simultaneous
        //prototype death still lacks a source strike order and winner.
        if (damage.RemainingAttackers == 0 && damage.RemainingDefenders == 0)
            return new(PlayableSiegeSettlementOutcome.Unresolved, state, damage);
        var quantities = new Dictionary<int, UnitState>(current.Units)
        {
            [attacker.Id] = current.Units[attacker.Id] with { Troops = damage.RemainingAttackers },
            [defender.Id] = current.Units[defender.Id] with { Troops = damage.RemainingDefenders }
        };
        UnitState victim = damage.RemainingAttackers == 0 ? attacker : defender;
        UnitState winner = damage.RemainingAttackers == 0 ? defender : attacker;
        var destroyed = TryDestroy(current with { Units = quantities }, victim,
            winner.NativeArmyOrigin!.NativeSlotIndex, null);
        return destroyed.State is { } result
            ? new(PlayableSiegeSettlementOutcome.Committed, result, damage, RuleId)
                { Reports = [.. current.PeriodReports.Skip(state.PeriodReports.Length), .. destroyed.Reports] }
            : new(PlayableSiegeSettlementOutcome.Unresolved, state, damage);
    }

    private PlayableFieldSettlementResult ApplyLiveTransportReverseFatal(GameState input, GameState current,
        UnitState attacker, UnitState transport, PlayableFieldCombatResult damage)
    {
        //The ordinary forward hit is nonfatal but still consumes real cargo.
        //The live transport then originates the external reverse request.
        //Both operations stay one private contact candidate until26 succeeds.
        var forward = campaign.TryApplyExternalNormalArmyApplication(current, attacker.Id, transport.Id, damage.DefenderLoss);
        if (forward.Outcome is OriginalExternalArmyNormalApplicationOutcome.CurrentInputsUnresolved or
            OriginalExternalArmyNormalApplicationOutcome.DestructionRequired)
            return Unknown(forward.MissingOwner);
        var reverse = campaign.TryApplyExternalSingleArmyDestruction(forward.State, transport.Id, attacker.Id, damage.AttackerLoss);
        if (reverse.Outcome != OriginalSingleArmyFatalApplicationOutcome.Committed || reverse.Receipt is not { } receipt)
            return Unknown(reverse.MissingOwner);
        var actual = damage with { AttackerLoss = attacker.Troops, RemainingAttackers = 0,
            DefenderLoss = transport.Troops - receipt.SurvivingAttackerTroops, RemainingDefenders = receipt.SurvivingAttackerTroops };
        PlayableBattleValidation.Validate(attacker.Troops, transport.Troops, actual);
        return new(PlayableSiegeSettlementOutcome.Committed, reverse.State, actual,
            OriginalOwnedArmyCampaignRules.SingleFatalApplicationRevision)
            { Reports = [.. current.PeriodReports.Skip(input.PeriodReports.Length), .. reverse.Reports] };

        PlayableFieldSettlementResult Unknown(string? owner) => new(PlayableSiegeSettlementOutcome.Unresolved, input, damage, owner);
    }

    private PlayableFieldSettlementResult ApplyNonfatal(GameState state, UnitState attacker,
        UnitState defender, PlayableFieldCombatResult damage)
    {
        if (attacker.NativeArmyOrigin?.NativeSlotIndex is null || defender.NativeArmyOrigin?.NativeSlotIndex is null)
            return Unknown("external-normal.current_native_Army_identity");
        GameState current = campaign.AdvancePlayableCalendar(state);
        //These are the existing period adapter's two requests, not a source
        //CAttackUtil pair order. Reverse reads actual forward writes; neither
        //request nor any prefix escapes before the complete contact succeeds.
        var forward = campaign.TryApplyExternalNormalArmyApplication(current, attacker.Id, defender.Id, damage.DefenderLoss);
        if (forward.Outcome is OriginalExternalArmyNormalApplicationOutcome.CurrentInputsUnresolved or
                OriginalExternalArmyNormalApplicationOutcome.DestructionRequired)
            return Unknown(forward.MissingOwner);
        var reverse = campaign.TryApplyExternalNormalArmyApplication(forward.State, defender.Id, attacker.Id, damage.AttackerLoss);
        if (reverse.Outcome is OriginalExternalArmyNormalApplicationOutcome.CurrentInputsUnresolved or
                OriginalExternalArmyNormalApplicationOutcome.DestructionRequired)
            return Unknown(reverse.MissingOwner);
        if (!reverse.State.Units.TryGetValue(attacker.Id, out var currentAttacker) ||
            !reverse.State.Units.TryGetValue(defender.Id, out var currentDefender))
            return Unknown("external-normal.nonfatal_Army_projection");
        var applied = damage with { AttackerLoss = attacker.Troops - currentAttacker.Troops,
            DefenderLoss = defender.Troops - currentDefender.Troops,
            RemainingAttackers = currentAttacker.Troops, RemainingDefenders = currentDefender.Troops };
        PlayableBattleValidation.Validate(attacker.Troops, defender.Troops, applied);
        return new(PlayableSiegeSettlementOutcome.Committed, reverse.State, applied, NonfatalRuleId)
            { Reports = current.PeriodReports.Skip(state.PeriodReports.Length).ToArray() };

        PlayableFieldSettlementResult Unknown(string? owner) =>
            new(PlayableSiegeSettlementOutcome.Unresolved, state, damage, owner);
    }

    internal OriginalOwnedArmyDestructionPhaseResult TryDestroy(GameState draft,
        UnitState victim, int? winnerArmySlot, int? winnerBaseLocation)
    {
        if (victim.NativeArmyOrigin?.NativeSlotIndex is not int physical ||
            campaign.CaptureCurrentDefinitions(draft) is not { } definitions)
            return new(null, [], "playable.death_current_native_identity");
        var admitted = OriginalOwnedArmyDestructionCurrent.AdmitOrdinaryArmy1745B0(draft,
            definitions, physical, winnerArmySlot, winnerBaseLocation);
        if (!admitted.IsPending || admitted.State is not { } queued)
            return new(null, [], admitted.UnresolvedOwner ?? "playable.death_request_not_queued");
        var destroyed = new OriginalOwnedArmyDestructionStage(campaign).TryConsumePhase19(queued);
        if (destroyed.State is not { } consumed || consumed.Units.ContainsKey(victim.Id) ||
            !destroyed.Receipts.Any(receipt => receipt.UnitId == victim.Id))
            return new(null, [], destroyed.UnresolvedOwner ?? "playable.death_victim_not_consumed");
        return campaign.ConsumeArmyLifecyclePending(consumed) is { } reset
            ? destroyed with { State = reset }
            : new(null, [], "playable.death_pending26_unresolved");
    }
}
