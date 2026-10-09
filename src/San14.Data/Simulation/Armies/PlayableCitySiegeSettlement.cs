using San14.Core;

namespace San14.Data;

/// <summary>Uses original Base common application in the playable period.
/// Ordinary Gate and non-last City fatal contacts use their complete source capture consumer;
/// other captures retain their existing selected provider. Encounter cadence
/// and casualty requests remain the finite adapter, not native stage13.</summary>
public sealed class PlayableCitySiegeSettlement(ArmyCampaignRuleAdapter campaign)
    : IPlayableSiegeSettlementRules
{
    public const string RuleId = "ce-City-178EC0-nonfatal-period-application-v1";
    public const string GateRuleId = "ce-Gate-178EC0-nonfatal-period-application-v1";
    public const string GateCaptureRuleId = "ce-Gate-178EC0-2851B0-29F220-period-capture-v1";
    public const string CityCaptureRuleId = "ce-City-178EC0-2851B0-29F220-period-capture-v1";

    public PlayableSiegeSettlementResult Apply(GameState state, UnitState attacker,
        int locationId, PlayableSiegeResult damage, int period)
    {
        bool sourceDeath = damage.RemainingAttackers == 0 && attacker.NativeArmyOrigin is not null;
        state.Cities.TryGetValue(locationId, out CityState? city);
        GateState? gate = city is null ? state.Gates.GetValueOrDefault(locationId) : null;
        int? owner = city?.ForceId ?? gate?.ForceId;
        int beforeTroops = city?.Troops ?? gate?.Troops ?? 0;
        if ((city is not null || gate is not null) && state.NativeArmySimulation is not null && attacker.NativeArmyOrigin?.NativeSlotIndex is not null &&
            owner is >= 1 and <= 45 && beforeTroops > 0 && damage.RemainingDefenders == 0 && damage.RemainingAttackers > 0 && damage.Captured)
        {
            if (gate is not null) return ApplyFatalBase(state, attacker, gate.Id, gate.Troops, gate.Durability, false, damage);
            //The final-City Force lifecycle has its own existing provider.
            //Select by the current native City pointer table, then let the
            //complete source consumer validate the same owner again.
            var definitions = campaign.CaptureOwnedArmyReturnWorld(state).Context;
            int cityCount = 0;
            for (int slot = 1; slot <= 51; slot++)
            {
                var reference = definitions?.ReadCityTableReference(slot);
                var currentOwner = reference is { } at ? definitions!.ReadBaseForceSlot(at) : null;
                if (currentOwner is null)
                    return new(PlayableSiegeSettlementOutcome.Unresolved, state, damage)
                        { UnresolvedOwner = "playable.current_City_owner_table" };
                if (currentOwner == owner) cityCount++;
            }
            if (cityCount > 1) return ApplyFatalBase(state, attacker, city!.Id, city.Troops, city.Durability, true, damage);
        }
        //Fatal Base/capture remains a distinct source path. A reached source
        //Army death cannot fall back to Core retirement.
        if ((city is null && gate is null) || state.NativeArmySimulation is null ||
            city is not null && city.NativeReturnRuntime is null ||
            attacker.NativeArmyOrigin?.NativeSlotIndex is null ||
            owner is not >= 1 or > 45 || beforeTroops <= 0 ||
            damage.RemainingDefenders <= 0 || damage.Captured)
            return new(sourceDeath ? PlayableSiegeSettlementOutcome.Unresolved :
                PlayableSiegeSettlementOutcome.NotApplicable, state, damage);
        //The playable encounter runs after the native waiting clock's full
        //period. Catch up the existing exact calendar writer before reading its
        //current-day sampler; do not backfill a source stage13 capture. Main's
        //usual post-period catch-up then has nothing left to advance.
        GameState current = campaign.AdvancePlayableCalendar(state);
        GameState appliedState;
        if (city is not null)
        {
            var applied = campaign.TryApplyNativeNormalCityApplication(current, attacker.Id, locationId, damage.DefenderLoss);
            if (applied.Outcome != NormalCityApplicationOutcome.CommittedNonfatal || applied.Receipt is null)
                return new(PlayableSiegeSettlementOutcome.Unresolved, state, damage);
            appliedState = applied.State;
        }
        else
        {
            var applied = campaign.TryApplyNativeNormalGateApplication(current, attacker.Id, locationId, damage.DefenderLoss);
            if (applied.Outcome != NormalCityApplicationOutcome.CommittedNonfatal || applied.Receipt is null)
                return new(PlayableSiegeSettlementOutcome.Unresolved, state, damage);
            appliedState = applied.State;
        }
        string ruleId = city is not null ? RuleId : GateRuleId;
        int remaining = city is not null ? appliedState.Cities[locationId].Troops : appliedState.Gates[locationId].Troops;
        PlayableSiegeResult actual = damage with { DefenderLoss = beforeTroops - remaining, RemainingDefenders = remaining };
        if (!sourceDeath)
            return new(PlayableSiegeSettlementOutcome.Committed, appliedState, actual, ruleId)
                { Reports = current.PeriodReports.Skip(state.PeriodReports.Length).ToArray() };
        //The City application validates its live attacker. Only after that
        //operation succeeds is the zero-ready Army draft consumed by19/26.
        GameState draft = appliedState with { Units = new(appliedState.Units)
            { [attacker.Id] = appliedState.Units[attacker.Id] with { Troops = 0 } } };
        var destroyed = new PlayableArmyDestructionSettlement(campaign).TryDestroy(draft,
            attacker, null, locationId);
        return destroyed.State is { } result
            ? new(PlayableSiegeSettlementOutcome.Committed, result, actual, ruleId)
                { Reports = [.. current.PeriodReports.Skip(state.PeriodReports.Length), .. destroyed.Reports] }
            : new(PlayableSiegeSettlementOutcome.Unresolved, state, damage);
    }

    private PlayableSiegeSettlementResult ApplyFatalBase(GameState state, UnitState attacker, int locationId,
        int beforeTroops, int? durability, bool city, PlayableSiegeResult damage)
    {
        var current = campaign.AdvancePlayableCalendar(state);
        //The Base common application validates the live attacker first. Its
        //separate returned resource image then carries this encounter's
        //surviving ready soldiers into actual29F220 once.
        var applied = campaign.TryApplyNativeNormalBaseFatalApplication(current, attacker.Id, locationId, damage.DefenderLoss);
        if (applied is not { Outcome: NormalCityApplicationOutcome.CommittedFatalApplication, Receipt.Admission.AdmissionKind: CombatSettlementAdmissionKind.Accepted or CombatSettlementAdmissionKind.Duplicate })
            return new(PlayableSiegeSettlementOutcome.Unresolved, state, damage) { UnresolvedOwner = applied.UnresolvedOwner ?? "playable.Base_fatal_admission" };
        var draft = applied.State with { Units = new(applied.State.Units)
            { [attacker.Id] = applied.State.Units[attacker.Id] with { Troops = damage.RemainingAttackers } } };
        var operationDate = current.NativeNormalArmyCombatCalendar?.LastAdvanceDiagnostics.LastOrDefault()?.PreWriterThirdDate;
        var consumed = new OwnedArmyDestructionStage(campaign).TryConsumePhase19(draft, captureWorld38Date: operationDate);
        if (consumed.State is not { } captured)
            return new(PlayableSiegeSettlementOutcome.Unresolved, state, damage) { UnresolvedOwner = consumed.UnresolvedOwner ?? "playable.Base_capture_consumer" };
        bool completeReceipt = city
            ? consumed.CityReceipts.Any(x => x.LocationId == locationId && !x.NativeSkipped)
            : consumed.BaseReceipts.Any(x => x.LocationId == locationId && !x.NativeSkipped);
        if (!completeReceipt || captured.Units.ContainsKey(attacker.Id))
            return new(PlayableSiegeSettlementOutcome.Unresolved, state, damage) { UnresolvedOwner = "playable.Base_capture_not_completed" };
        if (campaign.ConsumeArmyLifecyclePending(captured) is not { } cleaned)
            return new(PlayableSiegeSettlementOutcome.Unresolved, state, damage) { UnresolvedOwner = "playable.Base_capture_pending26" };
        //178EC0 is the troop common body; capture's source wall floor is a
        //separate writer. The provisional encounter's wall fraction does not
        //also debit the source wall before/after26DA10.
        var actual = damage with { DefenderLoss = beforeTroops, RemainingDefenders = 0,
            DurabilityLoss = 0, RemainingDurability = durability, Captured = true };
        return new(PlayableSiegeSettlementOutcome.Committed, cleaned, actual, city ? CityCaptureRuleId : GateCaptureRuleId)
            { BaseCaptureHandled = true, Reports = [.. current.PeriodReports.Skip(state.PeriodReports.Length), .. consumed.Reports] };
    }
}
