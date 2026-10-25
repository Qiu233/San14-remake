using San14.Core;

namespace San14.Data;

public sealed record ArmyCause4Receipt(int PhysicalSlot, int UnitId, int PersonPhysical,
    IReadOnlyList<string> SourceEffectOrder, ArmyDestructionTrackingWrite[] TrackingWrites)
{
    /// <summary>The actual resource packet was applied to current and saved
    /// owners. Pending70 admission alone does not establish this; final receiver
    /// retirement remains the enclosing operation's responsibility.</summary>
    public bool ResourceStageCompleted { get; init; }
    public BaseReference? VictimHome { get; init; }
    public PersonNormalArmyRewardReceipt? WinnerReward { get; init; }
    public int? WinnerForce164Gain { get; init; }
}
public sealed record ArmyCause4Result(GameState? State, ArmyCause4Receipt? Receipt, string? UnresolvedOwner);

public sealed partial class OwnedArmyDestructionStage
{
    /// <summary>265250's actual current free5/Home0/District0 entry into
    ///259360(null,null,mode0,cause4). No secondary combat tuple and no request
    ///to make soldiers zero. The enclosing freedom operation owns final Person
    ///placement and28E cleanup; this is an operation-local state draft.</summary>
    public ArmyCause4Result ApplyFreeCommanderCause4Current(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int physical)
        => ApplyCause4Current(state, current, pool, physical, new(null), null, true);

    /// <summary>The same real free5/Home0/District0 call while its enclosing
    /// capture/freedom operation retains live shared-pool temporaries.</summary>
    internal ArmyCause4Result ApplyFreeCommanderCause4WithinOperationCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int physical,
        NativeMapSeasonDate? sourceDate, List<CampaignEffectRequest> feedback)
        => ApplyCause4Current(state, current, pool, physical, new(null), sourceDate, true,
            withinOperation: true, feedback: feedback);

    /// <summary>2851B0's real2C96E0 HomeArmy branch. The Base still has its
    /// old Force/District: employed Commander, valid friendly Home, current
    /// position at that Home, and valid winner Army. Leaves the actual Army
    /// in pending70 for the later26 window; does not change employment/Home.</summary>
    public ArmyCause4Result ApplyEmployedHomeCommanderCause4Current(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int physical,
        ArmyUnitReference winnerArmy, NativeMapSeasonDate? operationThirdDate = null)
        => ApplyCause4Current(state, current, pool, physical, winnerArmy, operationThirdDate, false);

    /// <summary>295EE0 argument4: a still-employed commander, null winner,
    /// mode0/cause4, before the enclosing death resets the Person.</summary>
    internal ArmyCause4Result ApplyDeathCommanderCause4Current(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int physical,
        NativeMapSeasonDate date, List<CampaignEffectRequest> feedback)
        => ApplyCause4Current(state, current, pool, physical, new(null), date, false,
            withinOperation: true, feedback: feedback, deathCommander: true);

    private ArmyCause4Result ApplyCause4Current(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int physical,
        ArmyUnitReference winner, NativeMapSeasonDate? sourceDate, bool freeCommander,
        bool withinOperation = false, List<CampaignEffectRequest>? feedback = null,
        bool deathCommander = false)
    {
        try
        {
            var arena = state.ReadArmyLifecycle() ?? throw Missing("current.cause4_saved_arena");
            var slot = arena.Receivers.Find(arena.Slots[physical].ReceiverId)
                ?? throw Missing("current.cause4_Army_receiver");
            if (slot.ActiveUnitId is not int id || !state.Units.TryGetValue(id, out var army) ||
                army.NativeTacticRuntime is null || !ArmyConfigurationSourceAdapter.HasConfiguration(army))
                throw Missing("current.cause4_live_Army_image");
            var victim = new ArmyUnitReference(physical);
            if (!Known(current.ReadArmyUnitNativeValidity(victim), "cause4_Army_validity")) throw Missing("branch.cause4_invalid_Army");
            if (ArmyPending70Admission.ContainsCurrentPointer(new(RegistryKind.Army, physical), new Pending(current)))
                return new(state, new(physical, id, CommanderPhysical(current, victim), ["259360.pending70_early_return"], []), null);
            int commander = CommanderPhysical(current, victim);
            if (!Known(current.ReadPersonNativeValidity(commander), "cause4_Commander_validity"))
                throw Missing("branch.cause4_invalid_Commander");
            var force = current.ReadArmyUnitForceReference(victim);
            var winnerBase = new BaseReference(BaseKind.City, null);
            var captured = ArmyDestructionResources.CaptureOwners(current, victim, winner, winnerBase);
            if (freeCommander)
            {
                if (current.ReadPersonStatus(commander) != 5 || current.ReadPersonFirstLocation(commander) != 0 ||
                    current.ReadPersonDistrictSlot(commander) != 0)
                    throw Missing("branch.cause4_actual_free_Commander_prefix");
                if (force.PhysicalSlot != 0) throw Missing("current.cause4_free_Commander_Force0");
                if (Known(current.ReadBaseNativeValidity(captured.VictimHome), "cause4_Home_validity"))
                    throw Missing("branch.cause4_free_Commander_has_valid_Home");
            }
            else
            {
                if (current.ReadPersonStatus(commander) is not (>= 1 and <= 4) || force.PhysicalSlot is not (>= 1 and <= 45) ||
                    !Known(current.ReadForceNativeValidity(force), "cause4_employed_Force_validity"))
                    throw Missing("branch.cause4_actual_employed_Commander");
                if (!deathCommander && (!Known(current.ReadArmyUnitNativeValidity(winner), "cause4_winner_Army_validity") || winner.PhysicalSlot == physical))
                    throw Missing("branch.cause4_employed_winner_Army");
                if (!deathCommander && (!Known(current.ReadBaseNativeValidity(captured.VictimHome), "cause4_employed_Home_validity") ||
                    current.ReadBaseForceSlot(captured.VictimHome) != force.PhysicalSlot ||
                    !current.IsArmyUnitMapWriteSkipped(victim)))
                    throw Missing("branch.cause4_actual_friendly_at_Home");
            }
            var effects = new List<string>();
            if (!OwnedArmyDestructorFlags.SetSourceBit(current, physical, 1, true)) throw Missing("current.cause4_Army150");
            effects.Add("25944F.2409E0_Army150_bit1");
            _ = CachedEffect(current, victim, 0x36); //Actual pre-cleanup2C8DA0.
            if (withinOperation)
            {
                var traits = new ArmyReturnTraitCleanup.Current(current, pool);
                for (int index = 0; index < 9; index++) traits.ClearCommanderSlot(victim, index);
                for (int index = 0; index < 36; index++) traits.ClearReceivedSlot(victim, index);
            }
            else
            {
                var traits = ArmyReturnTraitCleanup.ApplyCurrent(victim, current, pool, new(false, false));
                if (traits.Packet is null) throw Missing(traits.UnresolvedOwner ?? "current.cause4_trait_cleanup");
            }
            effects.Add("259526.nine2A2340_then36_2A2540");
            _ = PersonalityImmunity(state, current, commander);
            _ = NearbyProtection(state, current, victim, force, sourceDate ?? state.NativeMapSeason?.ThirdDate);
            effects.Add("2595xx.Personality_and_protector_actual_temporaries");
            var resources = ArmyDestructionResources.Prepare(state, current, victim,
                winner, winnerBase, captured);
            if (resources.Packet is not { } resource) throw Missing(resources.UnresolvedOwner ?? "current.cause4_resources");
            ArmyDestructionResources.ApplyContext(resource);
            state = ArmyDestructionResources.PublishResources(state, resource);
            state = package.ApplyArmyDestructionLoyalty(state, current, commander, force,
                projectOwnedRoles: withinOperation);
            effects.Add("259Fxx.resources_then_actual_232500_232CB0_Loyalty");
            //Cause4 skips injury/death/capture/morale wave and kind14 reports.
            //The free5 leaf makes232460 false. The employed at-home leaf
            //really calls2EA9A0, whose valid friendly same-Hex branch only
            //writes E4 and creates no Return receiver/value temporaries.
            if (deathCommander)
            {
                state = ReturnAbsorptionCommander(state, current, pool, commander,
                    sourceDate ?? throw Missing("current.death_cause4_Return_date"),
                    feedback ?? throw Missing("current.death_cause4_feedback"));
                effects.Add("25B7CE.232460_then2EA9A0_death_current_Return");
            }
            else if (!freeCommander)
            {
                var home = BaseLookup.Find(Known(current.ReadPersonFirstLocation(commander), "cause4_return_PersonE2"), current);
                if (!Known(current.ReadBaseNativeValidity(home), "cause4_return_Home_validity") ||
                    current.ReadBaseForceSlot(home) != force.PhysicalSlot)
                    throw Missing("branch.cause4_return_changed_Home265250");
                var position = PersonReturnCommands.ResolvePersonPosition(state, current, commander,
                    sourceDate ?? state.NativeMapSeason?.ThirdDate ?? throw Missing("current.cause4_return_World34_date"));
                if (position.PhysicalSlot != current.ReadBaseCurrentHexIndex(home))
                    throw Missing("branch.cause4_return_remote_Command");
                current.WritePersonSecondLocation(commander, Math.Min(Known(current.ReadBaseObjectLink(home), "cause4_return_Base4E"), (ushort)1061));
                effects.Add("25B7CE.232460_then2EA9A0_same_Home_E4");
            }
            if (withinOperation)
            {
                state = ReleaseCaptives(state, current, physical, sourceDate, null,
                    feedback ?? throw Missing("current.cause4_operation_feedback"));
                effects.Add("25B89x.230C70_actual_captive_iterator_and2E7890");
            }
            else
            {
                RequireEmptyCause4Captives(current, physical);
                effects.Add("25B89x.230C70_actual_empty_captive_temporary");
            }
            var tracking = ArmyDestructionTrackingTail.ApplyCurrent(state, current, physical);
            state = tracking.State;
            NearbyPersonalityTail(state, current, victim, force, captured.WinnerForce);
            effects.Add("25B9xx.manager60_tracking_and_current_Force_nearby_tail");
            int hex = Known(current.ReadArmyUnitRawHex2A(victim), "cause4_Army2A");
            var originalHex = current.ReadHexTableReference(hex < 48400 ? hex : 0) ?? throw Missing("current.cause4_Hex_pointer");
            if (originalHex.PhysicalSlot is int actualHex) current.SetHexRaw1C(actualHex, 0);
            if (ArmyPending70Admission.EnqueueCurrentPointer(new(RegistryKind.Army, physical), new Pending(current)) ==
                ArmyPending70AdmissionOutcome.KnownAllocationFailure) throw Missing("branch.cause4_pending70_allocation_failure");
            effects.Add("25C171.Hex1C_clear→25C180.pending70_admission");
            PersonNormalArmyRewardReceipt? winnerReward = null;
            int? forceGain = null;
            if (!freeCommander && !deathCommander)
            {
                //Cause4 does not skip these late winner writers. Publish the
                //victim pending image to this same operation before2B9290.
                state = PublishCurrent(state, current, pool, [physical], current.OwnedDestructionQueue
                    ?? throw Missing("current.cause4_manager140_owner"));
                int winnerCommanderId = Known(current.ReadArmyUnitCommanderId(winner), "cause4_winner_Commander12");
                var reward = package.TryApplyNativeNormalPersonRewardDuringArmyOperation(state, winnerCommanderId, 2, 0);
                if (reward.Outcome == PersonNormalArmyRewardOutcome.CurrentInputsUnresolved)
                    throw Missing("current.cause4_winner_2B9290:" + reward.MissingPhase);
                state = reward.State; winnerReward = reward.Receipt;
                effects.Add("25C1xx.2B9290_winner_kind2_reward");
                var experience = ArmyDestructionForce164.Prepare(state, current, victim, winner, winnerBase);
                var packet = experience.Packet ?? throw Missing(experience.UnresolvedOwner ?? "current.cause4_winner_Force164");
                ArmyDestructionForce164.ApplyContext(packet);
                state = ArmyDestructionForce164.PublishCurrent(state, packet); forceGain = packet.RequestedGain;
                effects.Add("25C2xx.258A60_winner_Force164");
                var target = current.ReadArmyTargetState(winner.PhysicalSlot!.Value)
                    ?? throw Missing("current.cause4_winner_Target38");
                if (Known(target.Kind, "cause4_winner_Target38") == ArmyTargetKind.ArmyReceiver &&
                    current.TargetsArmyReceiver(target.Destination ?? throw Missing("current.cause4_winner_Target3A"), physical))
                    if (!OwnedArmyDestructorFlags.SetSourceBit(current, winner.PhysicalSlot.Value, 2, true))
                        throw Missing("current.cause4_winner_Army150");
                effects.Add("25C3xx.winner_Target38_3A_flag2");
            }
            foreach (var other in ArmyMovementManagerLifecycle.ReadCurrentPointerOrder(current))
            {
                int index = Known(other.PhysicalSlot, "cause4_target_clear_Army");
                if (!Known(current.ReadArmyUnitNativeValidity(new(index)), "cause4_target_clear_validity")) continue;
                var savedLifecycle = state.ReadArmyLifecycle()!;
                var saved = savedLifecycle.Receivers.Find(savedLifecycle.Slots[index].ReceiverId)
                    ?? throw Missing("current.cause4_target_clear_receiver");
                var image = saved.ActiveUnitId is int active ? state.Units.GetValueOrDefault(active) : saved.PendingImage;
                if (Known(ArmyConfigurationSourceAdapter.ReadAction(state, image), "cause4_target_clear_Action37") != 7) continue;
                var target = current.ReadArmyTargetState(index) ?? throw Missing("current.cause4_target38");
                if (Known(target.Kind, "cause4_target38") == ArmyTargetKind.ArmyReceiver &&
                    current.TargetsArmyReceiver(target.Destination ?? throw Missing("current.cause4_target3A"), physical))
                    current.ClearArmyTargetToPlaceholder(index);
            }
            effects.Add("25C4xx.manager60_target38_3A_clear");
            GameState result;
            if (withinOperation)
                result = PublishCurrent(state, current, pool, [physical], current.OwnedDestructionQueue
                    ?? throw Missing("current.cause4_manager140_owner"), projectOwnedRoles: true);
            else
            {
                result = ArmyReturnImagePublication.Publish(state, current, [physical]);
                var savedArena = OwnedArmyDestructorFlags.PublishCurrent(result.ReadArmyLifecycle()!, current)
                    with { Indexes = pool.ExportOwnedState() };
                result = (result with { ArmyMapOccupancy = current.ExportArmyMapOccupancy() }).WithArmyLifecycle(savedArena);
                result = OwnedArmyLifecycleTargets.PublishCurrent(result, current);
            }
            return new(result, new(physical, id, commander, effects, tracking.Writes)
                { ResourceStageCompleted = true, VictimHome = captured.VictimHome,
                    WinnerReward = winnerReward, WinnerForce164Gain = forceGain }, null);
        }
        catch (InvalidDataException unknown) { return new(null, null, unknown.Message); }
    }

    //The existing cause4 wrappers do not yet own the nonempty2E7890 report
    //and remote-return context. Preserve their original real230C70 slice:
    //allocation suppression and an actually empty chain can continue;
    //a reached nonempty chain still requires the genuine release consumer.
    private static void RequireEmptyCause4Captives(SourceWorldContext current, int army)
    {
        var temporary = current.CreateNearbyTemporary();
        try
        {
            CollectCaptivesCurrent(current, army, temporary);
            if (Known(current.ReadTemporaryHexListCount(temporary), "230C70_count") != 0)
                throw Missing("branch.cause4_captive_release2E7890");
        }
        finally { current.ClearNearbyTemporary(temporary, true); }
    }
}
