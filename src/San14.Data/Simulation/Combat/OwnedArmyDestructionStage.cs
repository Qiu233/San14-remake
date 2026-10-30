using San14.Core;

namespace San14.Data;

public sealed record OwnedArmyDestructionReceipt(int PhysicalSlot, int UnitId, int CommanderId, int ForceId,
    int PreviousHex, int HomeCity, PersonArmyInjuryReceipt? Injury, IReadOnlyList<string> SourceEffectOrder)
{
    public bool CommanderReclaimed { get; init; }
    public SettlementBase? VictimHome { get; init; }
    public int? NativeReportKind => CommanderReclaimed ? null : 14; //25B850→1B4AB0, non-reclaimed Person.
    public int? WinnerUnitId { get; init; }
    public int? WinnerCommanderId { get; init; }
    public int? WinnerBaseLocation { get; init; }
    public int? NativeWinnerReportKind => WinnerUnitId is null ? null : 10;
    public PersonNormalArmyRewardReceipt? WinnerReward { get; init; }
    public int? WinnerForce164Gain { get; init; }
    public PersonArmyCaptureReceipt? Capture { get; init; }
    public RulerCaptureReleaseReceipt? RulerRelease { get; init; }
    public PersonArmyCaptureWholeReceipt? Custody { get; init; }
    public ForcedDuelCaptureAnnouncement? ForcedDuelCaptureAnnouncement { get; init; }
    public ArmyDestructionTrackingWrite[] TrackingWrites { get; init; } = [];
    public PeriodReportEntry Report => new("field_destroyed", ForceId, HomeCity > 0 ? HomeCity : null, CommanderId, 0, PreviousHex, PreviousHex)
        { RuleId = OwnedArmyDestructionStage.Revision };
}
public sealed record OwnedArmyDestructionPhaseResult(GameState? State,
    IReadOnlyList<OwnedArmyDestructionReceipt> Receipts, string? UnresolvedOwner)
{
    public OwnedGateCaptureReceipt[] BaseReceipts { get; init; } = [];
    public OwnedCityCaptureReceipt[] CityReceipts { get; init; } = [];
    public PeriodReportEntry[]? SourceReports { get; init; }
    public IReadOnlyList<PeriodReportEntry> Reports => State is null ? [] :
        SourceReports ?? [.. Receipts.Select(x => x.Report), .. BaseReceipts.SelectMany(x => x.Reports), .. CityReceipts.SelectMany(x => x.Reports)];
}

/// <summary>450830's current request traversal and the ordinary259360 branch
/// with an ordinary employed Person and its still friendly original home. Supports a
/// valid winner Army or ordinary City winner and actual remote Return command creation. This is
/// destruction, not29F220 city return.
/// The separate26th phase owns282580 cleanup; no live zero-soldier projection
/// is published. Ordinary custody, AI ruler treatment and the cause1 death
/// call share this operation's current world and pool. Player ruler treatment
/// retains its distinct interactive continuation boundary.</summary>
public sealed partial class OwnedArmyDestructionStage(ArmyCampaignRuleAdapter package)
{
    public const string Revision = "san14:ce-owned-ordinary-Army-destruction-20261007";
    //Historical callers used the hexadecimal source case label as a name.
    //The actual scheduler index is decimal25; keep this API for existing callers.
    public OwnedArmyDestructionPhaseResult TryConsumePhase19(GameState state,
        TacticSourceSamplerWorld? sampler = null, NativeMapSeasonDate? captureWorld38Date = null)
        => TryConsumeDeferredDefeats(state, sampler, captureWorld38Date);

    public OwnedArmyDestructionPhaseResult TryConsumeDeferredDefeats(GameState state,
        TacticSourceSamplerWorld? sampler = null, NativeMapSeasonDate? captureWorld38Date = null)
        => TryConsumeDeferredDefeatsCore(state, sampler, captureWorld38Date, null, null, null);

    internal OwnedArmyDestructionPhaseResult TryConsumeDeferredDefeatsWithGrouping(GameState state,
        TacticSourceSamplerWorld? sampler, NativeMapSeasonDate? captureWorld38Date, ArmyMapGroupingOwner groups)
        => TryConsumeDeferredDefeatsCore(state, sampler, captureWorld38Date, null, null, groups);

    internal OwnedArmyDestructionPhaseResult TryConsumeDeferredDefeatsCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, TacticSourceSamplerWorld? sampler,
        NativeMapSeasonDate? captureWorld38Date, ArmyMapGroupingOwner? groups = null)
        => TryConsumeDeferredDefeatsCore(state, sampler, captureWorld38Date, current, pool, groups);

    private OwnedArmyDestructionPhaseResult TryConsumeDeferredDefeatsCore(GameState state,
        TacticSourceSamplerWorld? sampler, NativeMapSeasonDate? captureWorld38Date,
        SourceWorldContext? borrowedCurrent, WorldIndexContext? borrowedPool, ArmyMapGroupingOwner? groups)
    {
        var receipts = new List<OwnedArmyDestructionReceipt>();
        var captures = new List<OwnedGateCaptureReceipt>();
        var cityCaptures = new List<OwnedCityCaptureReceipt>();
        var reports = new List<PeriodReportEntry>();
        var reportIntents = new List<CampaignEffectRequest>();
        //The legacy parameter name describes one consumer of World34's final
        //byte38. The same actual pre-writer ThirdDate also owns Return dates.
        var sourceDate = captureWorld38Date ?? state.NativeMapSeason?.ThirdDate;
        if (state.PendingCombatSettlements is not { } queue)
            return new(null, receipts, "current.manager140_destruction_queue");
        try
        {
            var world = borrowedCurrent is null ? package.CaptureOwnedArmyReturnWorld(state)
                : package.BorrowOwnedArmyReturnWorld(state, borrowedCurrent, borrowedPool!);
            var current = world.Context;
            var working = state;
            var destroyed = new HashSet<int>();
            OwnedArmyDestructionPhaseResult CompleteCaptureTerminal(GameState terminal)
            {
                terminal = PublishCurrent(terminal, current, world.Pool, destroyed,
                    current.OwnedDestructionQueue ?? queue);
                if (reportIntents.Count != 0)
                    terminal = terminal with { CampaignEffectRequests = (terminal.CampaignEffectRequests
                        ?? throw Missing("capture_terminal_effect_owner")).Append(CampaignEffectSource.ArmyBattleDefeat,
                            terminal.NativeClock?.UpdateOrdinal ?? throw Missing("capture_terminal_effect_revision"), reportIntents) };
                return new(terminal, receipts, null) { BaseReceipts = captures.ToArray(),
                    CityReceipts = cityCaptures.ToArray(), SourceReports = reports.ToArray() };
            }
            // Read the current queue after every callback. A callback can append
            // another request which must settle in this same phase.
            OwnedArmyDestructionPhaseResult PauseEmperorCapture(int cursor)
            {
                if (working.CityCaptureEmperorTail is { } cityTail)
                    working = working with { CityCaptureEmperorTail = cityTail with { SettlementIndex = cursor } };
                if (working.BaseWinnerEmperorTail is { } winnerTail)
                    working = working with { BaseWinnerEmperorTail = winnerTail with { SettlementIndex = cursor } };
                working = BaseCapturePublicationSource.Publish(working, current, world.Pool, projectOwnedRoles: true);
                if (reportIntents.Count != 0)
                    working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                        ?? throw Missing("Emperor_report_owner")).Append(CampaignEffectSource.ArmyBattleDefeat,
                            working.NativeClock?.UpdateOrdinal ?? throw Missing("Emperor_report_revision"), reportIntents) };
                return new(working, receipts, null) { BaseReceipts = captures.ToArray(), CityReceipts = cityCaptures.ToArray(),
                    SourceReports = reports.ToArray() };
            }
            for (int cursor = working.ArmyDefeatContinuation?.SettlementIndex ?? working.BaseCaptureContinuation?.SettlementIndex ?? working.CityCaptureEmperorTail?.SettlementIndex ?? working.BaseWinnerEmperorTail?.SettlementIndex ?? 0; cursor < queue.Requests.Count; cursor++)
            {
                current.BindOwnedDestructionQueue(queue, working);
                var request = queue.Requests[cursor];
                if (request is ExpiredCombatSettlement) continue;
                if (request is BaseCaptureRequest capture)
                {
                    var target = ArmyDestructionQueue.ResolveBaseReference(working, current, capture.Target);
                    if (working.BaseCaptureContinuation is { } occupation)
                    {
                        if (occupation.SettlementIndex != cursor || occupation.Operation.Request != capture)
                            throw Missing("Base_capture_settlement_cursor");
                    }
                    else if (!Known(current.ReadBaseNativeValidity(target), "request_Base_validity")) continue;
                    working = PublishCurrent(working, current, world.Pool, destroyed, queue);
                    if (capture.HasNoWinner)
                    {
                        var neutral = package.ApplyNoWinnerBaseCaptureCurrent(working, current, capture,
                            sourceDate, sampler, reportIntents, groups ?? package.CreateCaptureMapGroupingOwner());
                        working = neutral.State;
                        world = borrowedCurrent is null ? package.CaptureOwnedArmyReturnWorld(working)
                            : package.BorrowOwnedArmyReturnWorld(working, borrowedCurrent, borrowedPool!);
                        current = world.Context;
                        destroyed.Clear();
                        queue = current.OwnedDestructionQueue ?? throw Missing("current.neutral_Base_postcallback_settlements");
                        if (neutral.NativeTerminal)
                        {
                            working = PublishCurrent(working, current, world.Pool, destroyed, queue);
                            if (reportIntents.Count != 0)
                                working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                                    ?? throw Missing("neutral_terminal_effect_owner")).Append(CampaignEffectSource.ArmyBattleDefeat,
                                        working.NativeClock?.UpdateOrdinal ?? throw Missing("neutral_terminal_effect_revision"), reportIntents) };
                            return new(working, receipts, null) { BaseReceipts = captures.ToArray(),
                                CityReceipts = cityCaptures.ToArray(), SourceReports = reports.ToArray() };
                        }
                        continue;
                    }
                    if (capture.WinnerUnitId is null && capture.WinnerDistrictId is not null)
                    {
                        //1745B0 also admits a genuine Base source's District.
                        //Its2851B0 operation has no Army-return receipt.
                        working = package.ApplyBaseWinnerCaptureCurrent(working, current, capture,
                            sourceDate ?? throw Missing("Base_source_capture_date"),
                            sampler ?? package.ReadArmyDestructionSampler(working, current)
                                ?? throw Missing("Base_source_capture_sampler"), reportIntents,
                            groups ?? package.CreateCaptureMapGroupingOwner());
                        if (working.NativePlayableTerminal is not null || working.CampaignTurnStop is not null)
                            return CompleteCaptureTerminal(working);
                        if (AutomaticEmperorCaptureLifecycle.HasCaptureTail(working))
                            return PauseEmperorCapture(cursor);
                        world = borrowedCurrent is null ? package.CaptureOwnedArmyReturnWorld(working)
                            : package.BorrowOwnedArmyReturnWorld(working, borrowedCurrent, borrowedPool!);
                        current = world.Context;
                        destroyed.Clear();
                        queue = current.OwnedDestructionQueue ?? throw Missing("current.Base_postcallback_settlements");
                        continue;
                    }
                    if (target.Kind == BaseKind.Gate)
                    {
                        var captured = new OwnedGateCaptureStage(package).TryApplyCurrent(working, current, capture, sourceDate, sampler, groups);
                        working = captured.State ?? throw Missing(captured.UnresolvedOwner ?? "Gate2851B0");
                        if (captured.NativeTerminal) return CompleteCaptureTerminal(working);
                        if (working.BaseCaptureContinuation is null && !AutomaticEmperorCaptureLifecycle.HasCaptureTail(working))
                        {
                            var completedCapture = captured.Receipt ?? throw Missing("Gate_capture_completed_receipt");
                            captures.Add(completedCapture); reports.AddRange(completedCapture.Reports);
                        }
                    }
                    else
                    {
                        var captured = new OwnedCityCaptureStage(package).TryApplyCurrent(working, current, capture, sourceDate, sampler, groups);
                        working = captured.State ?? throw Missing(captured.UnresolvedOwner ?? "City2851B0");
                        if (captured.NativeTerminal) return CompleteCaptureTerminal(working);
                        if (working.BaseCaptureContinuation is null && !AutomaticEmperorCaptureLifecycle.HasCaptureTail(working))
                        {
                            var completedCapture = captured.Receipt ?? throw Missing("City_capture_completed_receipt");
                            cityCaptures.Add(completedCapture); reports.AddRange(completedCapture.Reports);
                        }
                    }
                    if (AutomaticEmperorCaptureLifecycle.HasCaptureTail(working))
                        return PauseEmperorCapture(cursor);
                    if (working.BaseCaptureContinuation is { } pendingOccupation)
                    {
                        //The person prefix has released its temporary lists.
                        //Publish its real current writes and this queue position,
                        //without claiming the not-yet-executed territory tail.
                        working = working with { BaseCaptureContinuation = pendingOccupation with { SettlementIndex = cursor } };
                        working = BaseCapturePublicationSource.Publish(working, current, world.Pool, projectOwnedRoles: true);
                        if (reportIntents.Count != 0)
                            working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                                ?? throw Missing("report_pending_owner")).Append(CampaignEffectSource.ArmyBattleDefeat,
                                    working.NativeClock?.UpdateOrdinal ?? throw Missing("report_source_revision"), reportIntents) };
                        return new(working, receipts, null) { BaseReceipts = captures.ToArray(), CityReceipts = cityCaptures.ToArray(),
                            SourceReports = reports.ToArray() };
                    }
                    world = borrowedCurrent is null ? package.CaptureOwnedArmyReturnWorld(working)
                        : package.BorrowOwnedArmyReturnWorld(working, borrowedCurrent, borrowedPool!); current = world.Context;
                    destroyed.Clear(); queue = current.OwnedDestructionQueue ?? throw Missing("current.Base_postcallback_settlements");
                    continue;
                }
                var defeat = (UnitDefeatRequest)request;
                if (working.ArmyDefeatContinuation is { } suspended)
                {
                    if (suspended.SettlementIndex != cursor || suspended.Defeat != defeat)
                        throw Missing("ruler_treatment_settlement_cursor");
                    var resumed = ResumeRulerTreatmentTail(working, current, world.Pool, suspended, sourceDate, reportIntents);
                    working = resumed.State;
                    if (resumed.Receipt is { } resumedReceipt)
                    { destroyed.Add(resumedReceipt.PhysicalSlot); receipts.Add(resumedReceipt); reports.Add(resumedReceipt.Report); }
                    queue = current.OwnedDestructionQueue ?? throw Missing("resumed_operation_settlements");
                    continue;
                }
                if (ArmyDestructionQueue.ResolveUnitSlot(working, defeat.VictimUnitId) is not int physical) continue;
                if (current.ReadArmyUnitNativeValidity(new(physical)) is not bool valid) throw Missing("current.victim_validity");
                if (!valid || ArmyPending70Admission.ContainsCurrentPointer(new(RegistryKind.Army, physical), new Pending(current))) continue;
                bool duelDeath = defeat.Cause == UnitDefeatCause.DuelDeath;
                bool forcedCapture = defeat.Cause == UnitDefeatCause.DuelCapture;
                if (!forcedCapture && !duelDeath && defeat.Cause != UnitDefeatCause.OrdinaryBattle)
                    throw Missing("current.destruction_cause");
                var receipt = DestroyCurrent(working, current, world.Pool, defeat, physical,
                    sampler ?? package.ReadArmyDestructionSampler(working, current), sourceDate, reportIntents,
                    forcedDuelCapture: forcedCapture, duelDeath: duelDeath, allowPlayerRulerPause: true);
                working = receipt.State;
                if (working.ArmyDefeatContinuation is { } pendingDecision)
                {
                    working = working with { ArmyDefeatContinuation = pendingDecision with { SettlementIndex = cursor } };
                    working = PublishCurrent(working, current, world.Pool, destroyed, queue, projectOwnedRoles: true);
                    if (reportIntents.Count != 0)
                        working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                            ?? throw Missing("report_pending_owner")).Append(CampaignEffectSource.ArmyBattleDefeat,
                                working.NativeClock?.UpdateOrdinal ?? throw Missing("report_source_revision"), reportIntents) };
                    return new(working, receipts, null) { BaseReceipts = captures.ToArray(), CityReceipts = cityCaptures.ToArray(),
                        SourceReports = reports.ToArray() };
                }
                if (receipt.Receipt is { } completed)
                {
                    destroyed.Add(physical); receipts.Add(completed);
                    //Cause2 still reaches the real kind14 Army notification
                    //after2E0FC0; mode0 only suppresses the morale report.
                    reports.Add(completed.Report);
                }
                queue = current.OwnedDestructionQueue ?? throw Missing("current.operation_settlements");
            }
            queue = CombatSettlementQueue.Create();
            current.BindOwnedDestructionQueue(queue, working);
            var result = PublishCurrent(working, current, world.Pool, destroyed, queue);
            if (reportIntents.Count != 0) result = result with { CampaignEffectRequests =
                (result.CampaignEffectRequests ?? throw Missing("report_pending_owner"))
                    .Append(CampaignEffectSource.ArmyBattleDefeat,
                        result.NativeClock?.UpdateOrdinal ?? throw Missing("report_source_revision"), reportIntents) };
            return new(result, receipts, null) { BaseReceipts = captures.ToArray(), CityReceipts = cityCaptures.ToArray(), SourceReports = reports.ToArray() };
        }
        catch (InvalidDataException error) { return new(null, receipts, error.Message); }
    }

    private (GameState State, OwnedArmyDestructionReceipt? Receipt) DestroyCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool,
        UnitDefeatRequest request, int physical, TacticSourceSamplerWorld? sampler,
        NativeMapSeasonDate? sourceDate, List<CampaignEffectRequest> feedback,
        Action<ArmyUnitReference>? refresh = null, Action<int>? personChanged = null,
        bool projectOwnedRoles = false, bool forcedDuelCapture = false, bool duelDeath = false,
        bool allowPlayerRulerPause = false, ArmyDefeatContinuation? resumed = null)
    {
        if (request.WinnerUnitId == request.VictimUnitId)
            throw Missing("branch.ordinary_null_winner_mode1_cause0");
        var arena = state.ReadArmyLifecycle()!;
        var slot = arena.ReceiverSlots()[physical];
        if (slot.ActiveUnitId is not int id || !state.Units.TryGetValue(id, out var army) ||
            !ArmyConfigurationSourceAdapter.HasConfiguration(army) || army.NativeTacticRuntime is null)
            throw Missing("current.actual_battle_defeat_Army_image");
        var victim = new ArmyUnitReference(physical);
        var winner = new ArmyUnitReference(request.WinnerUnitId is int winnerIdForMapping
            ? ArmyDestructionQueue.ResolveUnitSlot(state, winnerIdForMapping) : null);
        bool winnerValid = Known(current.ReadArmyUnitNativeValidity(winner), "winner_Army_validity");
        if (!winnerValid) winner = new(null);
        var winnerBase = ResolveWinnerBase(state, current, request.WinnerBase, winnerValid);
        bool winnerBaseValid = Known(current.ReadBaseNativeValidity(winnerBase), "winner_Base_validity");
        int? winnerPhysical = winner.PhysicalSlot;
        int? winnerCommander = winnerValid ? Known(current.ReadArmyUnitCommanderId(winner), "winner_Commander12") : null;
        var winnerPerson = winnerCommander is int winnerId
            ? (winnerId <= 6000 ? current.ReadPersonLookupReference(winnerId) : current.ReadTransferPersonTableReference(0))
                ?? throw Missing("current.winner_Commander_pointer")
            : new TransferPersonReference(null);
        ushort commanderId = Known(current.ReadArmyUnitCommanderId(victim), "Army12");
        int commander = (commanderId <= 6000 ? current.ReadPersonLookupReference(commanderId) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot
            ?? throw Missing("current.Commander_pointer");
        var force = resumed is null ? current.ReadArmyUnitForceReference(victim) : new ForceReference(resumed.VictimForceId);
        if (!state.Officers.TryGetValue(commanderId, out var person) || resumed is null &&
            (current.ReadPersonNativeValidity(commander) != true || force.PhysicalSlot is not (>= 46 and <= 51) &&
                current.ReadPersonStatus(commander) is not (>= 1 and <= 4)))
            throw Missing("branch.current_employed_Commander");
        if (resumed is null && (force.PhysicalSlot is not (>= 1 and <= 51) || current.ReadForceNativeValidity(force) != true ||
            force.PhysicalSlot <= 45 && current.ReadPersonStatus(commander) == 1 &&
                current.ReadForceRecord(force.PhysicalSlot.Value)?.RawRuler10 != commanderId))
            throw Missing("branch.current_Force");
        bool specialCommanderReclaim = resumed?.SpecialCommanderReclaim ??
            ReadSpecialDefeatReclaimCurrent(current, force, commander);
        bool? reclaimDefeatedCommander = resumed?.ReclaimDefeatedCommander;
        var captured = resumed is null ? ArmyDestructionResources.CaptureOwners(current, victim, winner, winnerBase) :
            new ArmyDestructionResourceOwners(ResolveDefeatHome(state, current, resumed), new(resumed.WinnerForceId));
        if (!specialCommanderReclaim &&
            (captured.VictimHome is not { Kind: BaseKind.City or BaseKind.Gate, PhysicalSlot: > 0 } ||
             resumed is null && (current.ReadBaseNativeValidity(captured.VictimHome) != true ||
                current.ReadBaseForceSlot(captured.VictimHome) != force.PhysicalSlot)))
            throw Missing("branch.same_friendly_original_home");
        int hex = resumed?.HexId ?? Known(current.ReadArmyUnitRawHex2A(victim), "Army2A");
        bool atHome = !specialCommanderReclaim && current.ReadBaseCurrentHexIndex(captured.VictimHome) == hex;
        var effects = new List<string>();
        PersonArmyInjuryReceipt? injuryReceipt = null;
        int captureImmunity = resumed?.CaptureImmunity ?? 0;
        if (resumed is null)
        {
            if (!OwnedArmyDestructorFlags.SetSourceBit(current, physical, 1, true)) throw Missing("current.Army150");
            effects.Add("25944F.2409E0_Army150_bit1");
            //2C8DA0 is called before any cached trait cleanup, even when the later
            //known-null winner branch does not need capture immunity.
            captureImmunity = CachedEffect(current, victim, 0x36);
            if (refresh is null)
            {
                var cleanup = ArmyReturnTraitCleanup.ApplyCurrent(victim, current, pool, new(false, false));
                if (cleanup.Packet is null) throw Missing(cleanup.UnresolvedOwner ?? "current.trait_cleanup");
            }
            else
            {
                var cleanup = new ArmyReturnTraitCleanup.Current(current, pool, refresh, personChanged);
                for (int i = 0; i < 9; i++) cleanup.ClearCommanderSlot(victim, i);
                for (int i = 0; i < 36; i++) cleanup.ClearReceivedSlot(victim, i);
            }
            effects.Add("259526.nine2A2340_then36_2A2540");
            bool immune = PersonalityImmunity(state, current, commander);
            effects.Add("2595A0.2307D0_Personality_temporary");
            bool protectedNearby = NearbyProtection(state, current, victim, force, sourceDate);
            effects.Add("259729.2A78D0_protector_temporary");
            if (duelDeath)
            {
                var death = package.ApplyDuelDeathCurrent(state, current, pool, commander, winnerPerson);
                state = death.State;
                if (death.NativeReturn is null)
                {
                    if (!allowPlayerRulerPause) throw Missing("player_successor_immediate_stage_owner");
                    return (state with { ArmyDefeatContinuation = Checkpoint(ArmyDefeatResumePoint.AfterPersonDeath) }, null);
                }
                //295EE0 can retire the victim's Force, including this Army. The
                //original259360 caller returns immediately in that case.
                if (death.NativeReturn != 0) return (state, null);
                effects.Add("259C32.duel_death295EE0_cause1");
            }
            else if (!immune && !protectedNearby)
            {
                var injury = forcedDuelCapture
                    ? PersonArmyInjurySource.ApplyForcedCapture(state, current, commander, winnerPerson, package.CreateArmyDestructionAbilities(current), sampler)
                    : PersonArmyInjurySource.Apply(state, current, commander, winnerPerson, package.CreateArmyDestructionAbilities(current), sampler,
                        sourceDate, suppressDeath: Math.Min(Known(current.ReadArmyReturnResourceFields(victim)?.Morale1A,
                            "injury_current_Army1A"), BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(victim), current)) == 0);
                if (injury.Death is { } injuryDeath)
                {
                    var death = package.ApplyBattleInjuryDeathCurrent(state, current, pool, injuryDeath);
                    state = death.State;
                    if (death.NativeReturn is null)
                    {
                        if (!allowPlayerRulerPause) throw Missing("battle_injury_successor_immediate_stage_owner");
                        return (state with { ArmyDefeatContinuation = Checkpoint(ArmyDefeatResumePoint.AfterPersonDeath) }, null);
                    }
                    if (death.NativeReturn != 0) return (state, null);
                    effects.Add("259C32.battle_injury_death295EE0");
                }
                else if (injury.NativeReturn is null) throw Missing(injury.UnresolvedOwner ?? "current.27D900");
                injuryReceipt = injury.Receipt;
            }
            if (!duelDeath)
                effects.Add(forcedDuelCapture ? "259C32.forced_capture27D900_type2_current" : "259C32.ordinary27D900_current");
        }
        var resource = ArmyDestructionResources.Prepare(state, current, victim, winner, winnerBase, captured);
        if (resource.Packet is null) throw Missing(resource.UnresolvedOwner ?? "current.resources259360");
        ArmyDestructionResources.ApplyContext(resource.Packet);
        state = ArmyDestructionResources.PublishResources(state, resource.Packet);
        effects.Add("259Fxx.259360_resource_writes");
        //Loyalty implementation is supplied below once its real source inputs
        //have closed. It is not bypassed merely because the Person is ruler.
        state = package.ApplyArmyDestructionLoyalty(state, current, commander, force, projectOwnedRoles);
        effects.Add("25A834.Person_E8_loyalty_writer");
        //25AA7D: ordinary/cause2 destruction emits the valid winner's10
        //before morale/capture. The separate cause4 kernel omits this call.
        if (winnerValid)
        {
            state = QueueDestructionArmySpeechCurrent(state, current, winner, 10, sourceDate);
            effects.Add("25AA7D.1B4AB0_winner_action10_ArmyMapSpeech");
        }
        if (!forcedDuelCapture)
            state = CaptureMoraleDefeatReport(state, current, victim, winner, checked((ushort)hex), sampler, feedback);
        var wave = ArmyDestructionMoraleWave.Apply(state, current, victim,
            new(new(commander), force, captured.WinnerForce, specialCommanderReclaim), winner, winnerBase);
        if (wave.EffectCount is null) throw Missing(wave.UnresolvedOwner ?? "current.ruler_morale_wave");
        effects.Add("25B1xx.current_rank_office_morale_wave");
        PersonArmyCaptureReceipt? captureReceipt = null;
        RulerCaptureReleaseReceipt? releaseReceipt = null;
        PersonArmyCaptureWholeReceipt? custodyReceipt = null;
        ForcedDuelCaptureAnnouncement? captureAnnouncement = null;
        bool captureAnnouncementAlreadyQueued = false;
        bool returnedByCapture = false;
        //The native local1418 initialization occurs here, after injury/death
        //has returned; a prior AfterPersonDeath checkpoint keeps it unknown.
        reclaimDefeatedCommander = false;
        bool personAlive = Known(current.ReadPersonNativeValidity(commander), "capture_Person_validity") &&
            Known(current.ReadPersonStatus(commander), "capture_PersonE6") != 9;
        if (!duelDeath && personAlive && captureImmunity == 0 && winnerValid)
        {
            bool capturedPerson;
            if (forcedDuelCapture)
            {
                //259360 cause2 skips27E870 and enters2E0FC0(...,0,1)
                //directly. Preserve its source-time player announcement as
                //pending App work before the actual custody callbacks.
                capturedPerson = true;
                captureAnnouncement = package.CaptureForcedDuelCaptureAnnouncement(state, current, commander, captured.WinnerForce, next => state = next);
                effects.Add("25B75x.forced_duel_capture_direct_2E0FC0_flags0_1");
            }
            else
            {
                var capture = PersonArmyCaptureSource.Evaluate(state, current, commander, winnerPerson,
                    package.CreateArmyDestructionAbilities(current), sourceDate, sampler, new(false));
                if (capture.Captured is not bool actualCapture) throw Missing(capture.UnresolvedOwner ?? "current.27E870_capture");
                capturedPerson = actualCapture;
                captureReceipt = capture.Receipt;
                effects.Add("25B75x.27E870_capture_predicate");
            }
            if (capturedPerson)
            {
                //2E0FC0's actual employed ruler/Army-position branch calls
                //2F86F0 and returns1 BEFORE task/appointment/District custody
                //callbacks. A real AI release can therefore close this early
                //return without pretending that normal captivity has run.
                bool ordinaryPerson = Known(OfficerBehaviorRules.UsesOrdinaryPersonRules(current.ReadPersonBehavior(commander)), "captured_Person15E");
                if (!ordinaryPerson &&
                    Known(current.ReadPersonStatus(commander), "special_captured_PersonE6") != 5)
                {
                    //For bit14,2E0FC0 clears the ordinary caller's report flag.
                    //Both(1,1) and forced(0,1) retain the same34AA/SE notice.
                    //The forced prefix already formatted it exactly once.
                    if (!forcedDuelCapture)
                        captureAnnouncement = package.CaptureForcedDuelCaptureAnnouncement(state, current,
                            commander, captured.WinnerForce, next => state = next);
                    if (captureAnnouncement is { } announced)
                    {
                        var notice = BeginHiredCaptureAnnouncementCurrent(state, current, pool, announced,
                            () => Checkpoint(ArmyDefeatResumePoint.AfterSpecialCaptureAnnouncement),
                            feedback, allowPlayerRulerPause);
                        state = notice.State;
                        captureAnnouncementAlreadyQueued = true;
                        if (notice.AwaitingAnnouncement) return (state, null);
                    }
                    state = package.ApplyCapturedSpecialPersonReleaseCurrent(state, current, pool,
                        commander, sourceDate, sampler, feedback);
                    returnedByCapture = true;
                    effects.Add("25B75x.2E0FC0_special_265250_pending_hire_release");
                }
                else if (!ordinaryPerson)
                    throw Missing("branch.2E0FC0_captured_Person_custody");
                else if (current.ReadPersonStatus(commander) == 1)
                {
                    if (PersonPositionRepair.DecodeSecondLocation(Known(current.ReadPersonSecondLocation(commander), "captured_PersonE4")).Kind != 27)
                        throw Missing("branch.2E0FC0_captured_Person_custody");
                    var decision = RulerCaptureReleaseSource.Decide(state, current, commander, captured.WinnerForce, sampler);
                    if (decision.Decision == RulerCaptureDecision.PlayerDecisionRequired)
                    {
                        if (!allowPlayerRulerPause || state.NativePeriodContinuation is null)
                            throw Missing("player_ruler_treatment_deferred_stage25_owner");
                        var prepared = package.PrepareCapturedRulerTreatmentCurrent(state, current,
                            decision.Receipt ?? throw Missing("player_ruler_treatment_context"));
                        state = prepared.State;
                        if (captureAnnouncement is { } announced)
                            feedback.Add(new(null, null) { DuelCaptureAnnouncement = new(announced.OfficerId,
                                announced.OfficerNameAtSource, announced.MessageRoot, announced.SoundId)
                                { TextAtSource = announced.TextAtSource, Speaker = announced.Speaker } });
                        return (state with { ArmyDefeatContinuation = Checkpoint(ArmyDefeatResumePoint.AfterRulerTreatment) with
                            { PendingCapturedRulerTreatment = prepared.Request } }, null);
                    }
                    else if (decision.Decision == RulerCaptureDecision.ExecuteRequired)
                    {
                        state = package.ApplyRulerCaptureExecutionCurrent(state, current, pool, decision,
                            sourceDate ?? throw Missing("current.Lord_execute_World34_date"), feedback);
                        returnedByCapture = true;
                        effects.Add("25B75x.2E0FC0_2F86F0_Lord_AI_execute");
                    }
                    else if (decision.Decision == RulerCaptureDecision.Release)
                    {
                        var released = package.ApplyRulerCaptureReleaseCurrent(state, current, pool, decision,
                            sourceDate ?? throw Missing("current.Lord_return_World34_date"), feedback);
                        state = released.State ?? throw Missing(released.UnresolvedOwner ?? "current.Lord_release");
                        releaseReceipt = released.Receipt; returnedByCapture = true;
                        effects.Add("25B75x.2E0FC0_2F86F0_Lord_AI_release_early_return");
                    }
                    else
                        throw Missing(decision.UnresolvedOwner ?? (decision.Decision == RulerCaptureDecision.PlayerDecisionRequired
                            ? "branch.Lord_player_806D00_decision" : "branch.Lord_execute_27F820_295EE0"));
                }
                else
                {
                    var custody = package.ApplyOrdinaryWinnerArmyCapture(state, current, pool, commander, winner, sourceDate);
                    state = custody.State ?? throw Missing(custody.UnresolvedOwner ?? "current.2E0FC0_ordinary_custody");
                    custodyReceipt = custody.Receipt; returnedByCapture = true;
                    effects.Add("25B75x.2E0FC0_ordinary_capture_callbacks_and_custody");
                }
            }
        }
        personAlive = Known(current.ReadPersonNativeValidity(commander), "return_Person_validity") &&
            Known(current.ReadPersonStatus(commander), "return_PersonE6") != 9;
        //local1418 is first evaluated in the source return/capture segment.
        //AfterPersonDeath continuations retain null until this point.
        reclaimDefeatedCommander = !duelDeath && !returnedByCapture && specialCommanderReclaim;
        if (!duelDeath && personAlive && !returnedByCapture && !specialCommanderReclaim && atHome)
        {
            current.WritePersonSecondLocation(commander, Math.Min(Known(current.ReadBaseObjectLink(captured.VictimHome), "Base4E"), (ushort)1061));
            effects.Add("25B7CE.2EA9A0_same_home_secondE4");
        }
        else if (!duelDeath && personAlive && !returnedByCapture && !specialCommanderReclaim)
        {
            //The actual remote source creates its Return receiver before the
            //Army enters pending70. Its current second-location still points
            //to the live victim and supplies Army2A to231670; do not first
            //replace the Person location with its home.
            var date = sourceDate ?? throw Missing("current.remote_return_World34_date");
            var prepared = PersonReturnCommands.Prepare(state, current, commander, date);
            if (prepared.Packet is not { } returnPacket) throw Missing(prepared.UnresolvedOwner ?? "current.remote_return_prepare");
            var returned = PersonReturnCommands.Apply(state, current, returnPacket);
            state = returned.State ?? throw Missing(returned.UnresolvedOwner ?? "current.remote_return_create");
            effects.Add("25B7CE.2EA9A0_1F24F0_1F68B0_remote_return_command");
        }
        return CompleteDestructionTail(state, current, pool, Checkpoint(ArmyDefeatResumePoint.AfterRulerTreatment),
            sourceDate, feedback, projectOwnedRoles, effects, injuryReceipt, captureReceipt, releaseReceipt,
            custodyReceipt, captureAnnouncement, captureAnnouncementAlreadyQueued);

        ArmyDefeatContinuation Checkpoint(ArmyDefeatResumePoint point) => CreateDefeatCheckpoint(state,
            current, request, physical, commanderId, force, captured, hex, winner, winnerCommander,
            winnerBaseValid ? winnerBase : null, sampler, point, captureImmunity, specialCommanderReclaim, reclaimDefeatedCommander);
    }

    private (GameState State, OwnedArmyDestructionReceipt? Receipt) CompleteDestructionTail(GameState state,
        SourceWorldContext current, WorldIndexContext pool, ArmyDefeatContinuation checkpoint,
        NativeMapSeasonDate? sourceDate, List<CampaignEffectRequest> feedback, bool projectOwnedRoles,
        List<string>? sourceEffects = null, PersonArmyInjuryReceipt? injuryReceipt = null,
        PersonArmyCaptureReceipt? captureReceipt = null, RulerCaptureReleaseReceipt? releaseReceipt = null,
        PersonArmyCaptureWholeReceipt? custodyReceipt = null, ForcedDuelCaptureAnnouncement? captureAnnouncement = null,
        bool captureAnnouncementAlreadyQueued = false)
    {
        int physical = checkpoint.ArmyPhysicalSlot, id = checkpoint.Defeat.VictimUnitId,
            commanderId = checkpoint.CommanderOfficerId, hex = checkpoint.HexId, home = checkpoint.HomeCityId;
        int commander = current.ReadPersonLookupReference(commanderId)?.PhysicalSlot
            ?? throw Missing("resume_Commander_pointer");
        var victim = new ArmyUnitReference(physical);
        var winner = new ArmyUnitReference(checkpoint.WinnerArmyPhysicalSlot);
        int? winnerPhysical = checkpoint.WinnerArmyPhysicalSlot, winnerCommander = checkpoint.WinnerOfficerId;
        var winnerBase = checkpoint.WinnerBase is { } savedBase
            ? ArmyDestructionQueue.ResolveBaseReference(state, current, savedBase) : new BaseReference(BaseKind.City, null);
        bool winnerBaseValid = checkpoint.WinnerBase is not null;
        var force = new ForceReference(checkpoint.VictimForceId);
        var captured = new ArmyDestructionResourceOwners(ResolveDefeatHome(state, current, checkpoint), new(checkpoint.WinnerForceId));
        TacticSourceSamplerWorld? sampler = checkpoint.SampleDay is int day && checkpoint.SampleSalt is int salt
            ? new(day, salt) : null;
        var effects = sourceEffects ?? new List<string>();
        //The source return segment has evaluated local1418 before this tail.
        //Only its true special reclaim path suppresses14; ordinary capture
        //still retains its native speech after Return/custody.
        bool reclaimCommander = checkpoint.ReclaimDefeatedCommander
            ?? throw Missing("resume_special_reclaim_not_evaluated");
        if (!reclaimCommander && Known(current.ReadPersonNativeValidity(commander), "retreat_speech_Person_validity") &&
            Known(current.ReadPersonStatus(commander), "retreat_speech_PersonE6") != 9)
        {
            state = QueueDestructionArmySpeechCurrent(state, current, victim, 14, sourceDate, hex);
            effects.Add("25B850.1B4AB0_report_kind14_ArmyMapSpeech");
        }
        state = ReleaseCaptives(state, current, physical, sourceDate, sampler, feedback);
        effects.Add("25B89x.230C70_actual_captive_iterator");
        var tracking = ArmyDestructionTrackingTail.ApplyCurrent(state, current, physical);
        state = tracking.State;
        state = NearbyPersonalityTail(state, current, victim, force, captured.WinnerForce, sourceDate, feedback, sampler);
        effects.Add("25BAxx.current_manager60_and_nearby_tail");
        var originalHex = current.ReadHexTableReference(hex < 48400 ? hex : 0) ?? throw Missing("current.original_Hex_pointer");
        if (originalHex.PhysicalSlot is int actualHex) current.SetHexRaw1C(actualHex, 0);
        effects.Add("25C171.226870_Hex1C_clear_before_pending");
        var admitted = ArmyPending70Admission.EnqueueCurrentPointer(new(RegistryKind.Army, physical), new Pending(current));
        if (admitted == ArmyPending70AdmissionOutcome.KnownAllocationFailure) throw Missing("branch.pending70_allocation_failure");
        effects.Add("25C180.31F370_pending70");
        if (reclaimCommander)
        {
            state = ReclaimSpecialDefeatedCommanderCurrent(state, current, pool, commander);
            effects.Add("25C180.Person_manager_unregister_then226480_reset");
        }
        PersonNormalArmyRewardReceipt? winnerReward = null;
        int? forceGain = null;
        int? winnerUnitId = null;
        if (winnerPhysical is int winnerSlot)
        {
            winnerUnitId = state.ReadArmyLifecycle()!.ReceiverSlots()[winnerSlot].ActiveUnitId;
            //The original victim has entered pending70 before2B9290. Publish
            //that current image only to this enclosing operation's draft;
            //phase26 still owns header reset/unregistration.
            state = PublishCurrent(state, current, pool, [physical], current.OwnedDestructionQueue!, projectOwnedRoles);
            var reward = package.TryApplyNativeNormalPersonRewardDuringArmyOperation(state, winnerCommander!.Value, 2, 0);
            if (reward.Outcome == PersonNormalArmyRewardOutcome.CurrentInputsUnresolved)
                throw Missing("current.winner_2B9290:" + reward.MissingPhase);
            state = reward.State; winnerReward = reward.Receipt;
            effects.Add("25C1xx.2B9290_winner_kind2_reward");
        }
        if (winnerPhysical is not null || winnerBaseValid)
        {
            var experience = ArmyDestructionForce164.Prepare(state, current, victim, winner, winnerBase);
            if (experience.Packet is not { } experiencePacket) throw Missing(experience.UnresolvedOwner ?? "current.winner_Force164");
            ArmyDestructionForce164.ApplyContext(experiencePacket);
            state = ArmyDestructionForce164.PublishCurrent(state, experiencePacket);
            forceGain = experiencePacket.RequestedGain;
            effects.Add("25C2xx.258A60_winner_Force164");
        }
        if (winnerPhysical is int winnerTargetSlot)
        {
            var target = current.ReadArmyTargetState(winnerTargetSlot) ?? throw Missing("current.winner_Target38");
            if (Known(target.Kind, "winner_Target38") == ArmyTargetKind.ArmyReceiver &&
                current.TargetsArmyReceiver(target.Destination ?? throw Missing("current.winner_Target3A"), physical))
                if (!OwnedArmyDestructorFlags.SetSourceBit(current, winnerTargetSlot, 2, true)) throw Missing("current.winner_Army150");
            effects.Add("25C3xx.winner_Target38_3A_flag2");
        }
        foreach (var other in ArmyMovementManagerLifecycle.ReadCurrentPointerOrder(current))
        {
            int index = Known(other.PhysicalSlot, "target_clear_Army");
            if (!Known(current.ReadArmyUnitNativeValidity(new(index)), "target_clear_Army_validity")) continue;
            var saved = state.ReadArmyLifecycle()!.ReceiverSlots()[index];
            var image = saved.ActiveUnitId is int active ? state.Units.GetValueOrDefault(active) : saved.PendingImage;
            if (Known(ArmyConfigurationSourceAdapter.ReadAction(state, image), "target_clear_Army37") != 7) continue;
            var target = current.ReadArmyTargetState(index) ?? throw Missing("current.target_clear_Army38");
            if (Known(target.Kind, "target_clear_Army38") == ArmyTargetKind.ArmyReceiver &&
                current.TargetsArmyReceiver(target.Destination ?? throw Missing("current.target_clear_Army3A"), physical))
                current.ClearArmyTargetToPlaceholder(index);
        }
        effects.Add("25C4xx.current_manager60_target_clear");
        if (!captureAnnouncementAlreadyQueued && captureAnnouncement is { } announcement)
            feedback.Add(new(null, null) { DuelCaptureAnnouncement = new(announcement.OfficerId,
                announcement.OfficerNameAtSource, announcement.MessageRoot, announcement.SoundId)
                { TextAtSource = announcement.TextAtSource, Speaker = announcement.Speaker } });
        return (state, new(physical, id, commanderId, checkpoint.VictimForceId, hex, home, injuryReceipt, effects)
            { CommanderReclaimed = reclaimCommander, VictimHome = checkpoint.VictimHome,
                WinnerUnitId = winnerUnitId, WinnerCommanderId = winnerCommander,
                WinnerBaseLocation = winnerBaseValid ? Known(current.ReadBaseObjectLink(winnerBase), "winner_Base4E") : null,
                WinnerReward = winnerReward,
                WinnerForce164Gain = forceGain, Capture = captureReceipt, RulerRelease = releaseReceipt, Custody = custodyReceipt,
                ForcedDuelCaptureAnnouncement = captureAnnouncement,
                TrackingWrites = tracking.Writes });
    }

    private static GameState PublishCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, HashSet<int> destroyed, CombatSettlementQueue queue,
        bool projectOwnedRoles = false)
    {
        var result = OwnedArmyReturnCurrentFields.PublishCurrent(state, current);
        result = OwnedArmyLifecycleDetachedInputs.PublishCurrent(result, current);
        result = ArmyReturnImagePublication.Publish(result, current, destroyed);
        var arena = OwnedArmyDestructorFlags.PublishCurrent(result.ReadArmyLifecycle()!, current)
            with { Indexes = projectOwnedRoles ? pool.ProjectOwnedRoles() : pool.ExportOwnedState() };
        return OwnedArmyLifecycleTargets.PublishCurrent((result with {
                ArmyMapOccupancy = current.ExportArmyMapOccupancy(),
                PendingCombatSettlements = queue,
                PersonnelCommands = current.ReadOwnedPersonCommandManager() }).WithArmyLifecycle(arena), current);
    }

    private static bool PersonalityImmunity(GameState state, SourceWorldContext current, int commander)
    {
        var temporary = current.CreatePersonInjuryTemporary();
        bool immune = false;
        try
        {
            current.ClearPersonInjuryTemporary(temporary, false);
            for (int i = 0; i < 9; i++)
            {
                var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, commander, i) ?? throw Missing("current.230730_trait_pointer");
                if (Known(current.ReadSkillPersonalityValidity(trait), "Personality_validity")) current.AppendPersonInjuryTemporary(temporary, trait);
            }
            var node = current.ReadSkillTemporaryHead(temporary) ?? throw Missing("current.Personality_temporary_head");
            while (node.Identity is not null)
            {
                var trait = current.ReadSkillTemporaryPayload(node) ?? throw Missing("current.Personality_temporary_payload");
                if (FirstEffect(current, trait, 0x35) > 0 && Radius(state, current, trait) == 0) immune = true;
                node = current.ReadHexListNodeNext(node) ?? throw Missing("current.Personality_temporary_next");
            }
        }
        finally { current.ClearPersonInjuryTemporary(temporary, true); }
        return immune;
    }
    private GameState ReleaseCaptives(GameState state, SourceWorldContext current, int army,
        NativeMapSeasonDate? sourceDate, TacticSourceSamplerWorld? sampler,
        List<CampaignEffectRequest> feedback)
    {
        var temporary = current.CreateNearbyTemporary();
        try
        {
            CollectCaptivesCurrent(current, army, temporary);
            //230C70's actual allocated chain, not a replacement list of all
            //matching people. A suppressed node allocation stays suppressed.
            var captive = current.ReadRegistryHead(new(temporary.Identity)) ?? throw Missing("current.captive_head");
            while (captive.Identity is not null)
            {
                var entry = current.ReadRegistryNodePayload(captive) ?? throw Missing("current.captive_payload");
                if (entry.Kind != RegistryKind.Person || entry.PhysicalSlot is not int person)
                    throw Missing("current.captive_Person_pointer");
                state = package.ApplyCaptiveReleaseCurrent(state, current, person, sourceDate,
                    () => sampler?.Day450 ?? package.ReadArmyDestructionSampler(state, current)?.Day450
                        ?? throw Missing("current.captive_report_World450"), feedback, sampler);
                captive = current.ReadRegistryNodeNext(captive) ?? throw Missing("current.captive_next");
            }
            return state;
        }
        finally { current.ClearNearbyTemporary(temporary, true); }
    }
    //230C70 shares only collection, never the callers' distinct consumers.
    internal static void CollectCaptivesCurrent(SourceWorldContext current, int army, HexListHandle temporary)
    {
        current.ClearNearbyTemporary(temporary, false);
        var descriptor = current.ReadRegistryDescriptor(RegistryKind.Person) ?? throw Missing("current.Person100_descriptor");
        var node = current.ReadRegistryHead(descriptor) ?? throw Missing("current.Person100_head");
        while (node.Identity is not null)
        {
            var person = current.ReadRegistryNodePayload(node) ?? throw Missing("current.Person100_payload");
            if (person.Kind != RegistryKind.Person || person.PhysicalSlot is not int physical) throw Missing("current.Person100_typed_pointer");
            if (Known(current.ReadPersonStatus(physical), "PersonE6") == 6 && Known(current.ReadPersonSecondLocation(physical), "PersonE4") == army + 61)
            {
                var appended = current.AllocateRegistryNode(new(temporary.Identity)) ?? throw Missing("current.230C70_node");
                if (appended.Identity is not null) current.WriteRegistryNodePayload(appended, person);
            }
            node = current.ReadRegistryNodeNext(node) ?? throw Missing("current.Person100_next");
        }
    }

    private static int CachedEffect(SourceWorldContext current, ArmyUnitReference army, byte effect)
    {
        var fields = current.ReadArmyReturnTraitFields(army) ?? throw Missing("current.Army_cached36");
        foreach (ushort key in fields.Traits64)
        {
            if (key is < 1 or > 355) continue;
            var trait = current.ReadPersonalityTableReference(key) ?? throw Missing("current.Personality_pointer");
            if (!Known(current.ReadSkillPersonalityValidity(trait), "Personality_validity")) continue;
            if (Known(current.ReadPersonalityByte(trait, 0xB6), "PersonalityB6") == effect || Known(current.ReadPersonalityByte(trait, 0xBA), "PersonalityBA") == effect) return 1;
        }
        return 0;
    }
    private static int FirstEffect(SourceWorldContext current, PersonalityReference trait, byte effect)
    {
        foreach (int at in new[] { 0xB6, 0xBA })
            if (Known(current.ReadPersonalityByte(trait, at), "Personality_effect_code") == effect)
                return Math.Clamp((int)unchecked((short)Known(current.ReadPersonalityWord(trait, at + 2), "Personality_effect_value")), -100, 2000);
        return 0;
    }
    private static int Radius(GameState state, SourceWorldContext current, PersonalityReference trait)
    {
        byte kind = Known(current.ReadPersonalityByte(trait, 0xA4), "Personality_categoryA4");
        if (kind is 2 or 3 or 0x17 or 0x18) return 1;
        if (kind is not (9 or 10 or 0x15 or 0x16 or 0x1F or 0x20 or 0x27 or 0x28)) return 0;
        if (trait.PhysicalSlot is >= 256 and <= 355)
        {
            int value = 0;
            for (int i = 0; i < 4; i++) value |= Known(current.ReadPersonalityByte(trait, 0xA8 + i), "Personality_radiusA8") << (i * 8);
            if (value >= 1) return value;
        }
        return Known(ArmyDestructionParameterSeed.ReadCurrent(state, SimulationParameterKeys.ArmyDestruction.PersonalityDefaultRadius), "Personality_default_radius");
    }
    private static T Known<T>(T? value, string field) where T : struct => value ?? throw Missing("current." + field);
    private static InvalidDataException Missing(string field) => new(field);
    private sealed class Pending(SourceWorldContext current) : IArmyPending70AdmissionState
    {
        public RegistryListHandle? ReadPending70Descriptor() => current.ReadArmyReturnPending70Descriptor();
        public RegistryListNodeReference? ReadPending70Head(RegistryListHandle descriptor) => current.ReadRegistryHead(descriptor);
        public RegistryObjectReference? ReadPending70Payload(RegistryListNodeReference node) => current.ReadRegistryNodePayload(node);
        public RegistryListNodeReference? ReadPending70Next(RegistryListNodeReference node) => current.ReadRegistryNodeNext(node);
        public bool? ReadPending70LockEnabled() => current.ReadRegistryLockEnabled();
        public void EnterPending70Lock() => current.EnterRegistryLock();
        public void ExitPending70Lock() => current.ExitRegistryLock();
        public RegistryListNodeReference? AppendPending70Node(RegistryListHandle descriptor) => current.AllocateRegistryNode(descriptor);
        public void WritePending70Payload(RegistryListNodeReference node, RegistryObjectReference receiver) => current.WriteRegistryNodePayload(node, receiver);
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal IEffectiveOfficerAbilities CreateArmyDestructionAbilities(SourceWorldContext current) =>
        new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
    internal TacticSourceSamplerWorld? ReadArmyDestructionSampler(GameState state, SourceWorldContext current) =>
        Context(current).ReadCurrentSamplerWorld(state);
    internal GameState ApplyArmyDestructionLoyalty(GameState state, SourceWorldContext current,
        int personPhysical, ForceReference capturedVictimForce, bool projectOwnedRoles = false)
    {
        var result = ArmyDestructionLoyalty.Prepare(state, current, personPhysical, _governorInputs, capturedVictimForce);
        if (result.Packet is not { } packet) throw new InvalidDataException(result.UnresolvedOwner ?? "current.destruction_loyalty");
        ArmyDestructionLoyalty.ApplyContext(packet);
        return ArmyDestructionLoyalty.PublishCurrent(state, packet, projectOwnedRoles);
    }
}
