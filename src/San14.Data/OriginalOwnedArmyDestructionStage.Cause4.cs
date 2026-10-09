using San14.Core;

namespace San14.Data;

public sealed record OriginalArmyCause4Receipt(int PhysicalSlot, int UnitId, int PersonPhysical,
    IReadOnlyList<string> SourceEffectOrder, OriginalArmyDestructionTrackingWrite[] TrackingWrites)
{
    public OriginalBaseReference? VictimHome { get; init; }
    public OriginalPersonNormalArmyRewardReceipt? WinnerReward { get; init; }
    public int? WinnerForce164Gain { get; init; }
}
public sealed record OriginalArmyCause4Result(GameState? State, OriginalArmyCause4Receipt? Receipt, string? UnresolvedOwner);

public sealed partial class OriginalOwnedArmyDestructionStage
{
    /// <summary>265250's actual current free5/Home0/District0 entry into
    ///259360(null,null,mode0,cause4). No secondary combat tuple and no request
    ///to make soldiers zero. The enclosing freedom operation owns final Person
    ///placement and28E cleanup; this is an operation-local state draft.</summary>
    public OriginalArmyCause4Result ApplyFreeCommanderCause4Current(GameState state,
        OriginalEventInitializationContext current, WorldIndexContext pool, int physical)
        => ApplyCause4Current(state, current, pool, physical, new(null), null, true);

    /// <summary>2851B0's real2C96E0 HomeArmy branch. The Base still has its
    /// old Force/District: employed Commander, valid friendly Home, current
    /// position at that Home, and valid winner Army. Leaves the actual Army
    /// in pending70 for the later26 window; does not change employment/Home.</summary>
    public OriginalArmyCause4Result ApplyEmployedHomeCommanderCause4Current(GameState state,
        OriginalEventInitializationContext current, WorldIndexContext pool, int physical,
        OriginalArmyUnitReference winnerArmy, NativeMapSeasonDate? operationThirdDate = null)
        => ApplyCause4Current(state, current, pool, physical, winnerArmy, operationThirdDate, false);

    private OriginalArmyCause4Result ApplyCause4Current(GameState state,
        OriginalEventInitializationContext current, WorldIndexContext pool, int physical,
        OriginalArmyUnitReference winner, NativeMapSeasonDate? sourceDate, bool freeCommander)
    {
        try
        {
            var arena = state.NativeArmyLifecycle ?? throw Missing("current.cause4_saved_arena");
            var slot = arena.Slots[physical];
            if (slot.ActiveUnitId is not int id || !state.Units.TryGetValue(id, out var army) ||
                army.NativeTacticRuntime is null || army.NativeArmyConfiguration is null)
                throw Missing("current.cause4_live_Army_image");
            var victim = new OriginalArmyUnitReference(physical);
            if (!Known(current.ReadArmyUnitNativeValidity(victim), "cause4_Army_validity")) throw Missing("branch.cause4_invalid_Army");
            if (OriginalArmyPending70Admission.ContainsCurrentPointer(new(OriginalRegistryKind.Army, physical), new Pending(current)))
                return new(state, new(physical, id, CommanderPhysical(current, victim), ["259360.pending70_early_return"], []), null);
            int commander = CommanderPhysical(current, victim);
            if (!Known(current.ReadPersonNativeValidity(commander), "cause4_Commander_validity"))
                throw Missing("branch.cause4_invalid_Commander");
            var force = current.ReadArmyUnitForceReference(victim);
            var winnerBase = new OriginalBaseReference(OriginalBaseKind.City, null);
            var captured = OriginalArmyDestructionResources.CaptureOwners(current, victim, winner, winnerBase);
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
                if (!Known(current.ReadArmyUnitNativeValidity(winner), "cause4_winner_Army_validity") || winner.PhysicalSlot == physical)
                    throw Missing("branch.cause4_employed_winner_Army");
                if (!Known(current.ReadBaseNativeValidity(captured.VictimHome), "cause4_employed_Home_validity") ||
                    current.ReadBaseForceSlot(captured.VictimHome) != force.PhysicalSlot ||
                    !current.IsArmyUnitMapWriteSkipped(victim))
                    throw Missing("branch.cause4_actual_friendly_at_Home");
            }
            var effects = new List<string>();
            if (!OriginalOwnedArmyDestructorFlags.SetSourceBit(current, physical, 1, true)) throw Missing("current.cause4_Army150");
            effects.Add("25944F.2409E0_Army150_bit1");
            _ = CachedEffect(current, victim, 0x36); //Actual pre-cleanup2C8DA0.
            var traits = OriginalArmyReturnTraitCleanup.ApplyCurrent(victim, current, pool, new(false, false));
            if (traits.Packet is null) throw Missing(traits.UnresolvedOwner ?? "current.cause4_trait_cleanup");
            effects.Add("259526.nine2A2340_then36_2A2540");
            _ = PersonalityImmunity(state, current, commander);
            _ = NearbyProtection(state, current, victim, force, sourceDate ?? state.NativeMapSeason?.ThirdDate);
            effects.Add("2595xx.Personality_and_protector_actual_temporaries");
            var resources = OriginalArmyDestructionResources.Prepare(state, current, victim,
                winner, winnerBase, captured);
            if (resources.Packet is not { } resource) throw Missing(resources.UnresolvedOwner ?? "current.cause4_resources");
            OriginalArmyDestructionResources.ApplyContext(resource);
            state = OriginalArmyDestructionResources.PublishResources(state, resource);
            state = package.ApplyArmyDestructionLoyalty(state, current, commander, force);
            effects.Add("259Fxx.resources_then_actual_232500_232CB0_Loyalty");
            //Cause4 skips injury/death/capture/morale wave and kind14 reports.
            //The free5 leaf makes232460 false. The employed at-home leaf
            //really calls2EA9A0, whose valid friendly same-Hex branch only
            //writes E4 and creates no Return receiver/value temporaries.
            if (!freeCommander)
            {
                var home = OriginalBaseLookup.Find(Known(current.ReadPersonFirstLocation(commander), "cause4_return_PersonE2"), current);
                if (!Known(current.ReadBaseNativeValidity(home), "cause4_return_Home_validity") ||
                    current.ReadBaseForceSlot(home) != force.PhysicalSlot)
                    throw Missing("branch.cause4_return_changed_Home265250");
                var position = OriginalPersonReturnCommands.ResolvePersonPosition(state, current, commander,
                    sourceDate ?? state.NativeMapSeason?.ThirdDate ?? throw Missing("current.cause4_return_World34_date"));
                if (position.PhysicalSlot != current.ReadBaseCurrentHexIndex(home))
                    throw Missing("branch.cause4_return_remote_Command");
                current.WritePersonSecondLocation(commander, Math.Min(Known(current.ReadBaseObjectLink(home), "cause4_return_Base4E"), (ushort)1061));
                effects.Add("25B7CE.232460_then2EA9A0_same_Home_E4");
            }
            RequireNoCaptives(current, physical);
            effects.Add("25B89x.230C70_actual_empty_captive_temporary");
            var tracking = OriginalArmyDestructionTrackingTail.ApplyCurrent(state, current, physical);
            state = tracking.State;
            NearbyPersonalityTail(state, current, victim, force, captured.WinnerForce);
            effects.Add("25B9xx.manager60_tracking_and_current_Force_nearby_tail");
            int hex = Known(current.ReadArmyUnitRawHex2A(victim), "cause4_Army2A");
            var originalHex = current.ReadHexTableReference(hex < 48400 ? hex : 0) ?? throw Missing("current.cause4_Hex_pointer");
            if (originalHex.PhysicalSlot is int actualHex) current.SetHexRaw1C(actualHex, 0);
            if (OriginalArmyPending70Admission.EnqueueCurrentPointer(new(OriginalRegistryKind.Army, physical), new Pending(current)) ==
                OriginalArmyPending70AdmissionOutcome.KnownAllocationFailure) throw Missing("branch.cause4_pending70_allocation_failure");
            effects.Add("25C171.Hex1C_clear→25C180.pending70_admission");
            OriginalPersonNormalArmyRewardReceipt? winnerReward = null;
            int? forceGain = null;
            if (!freeCommander)
            {
                //Cause4 does not skip these late winner writers. Publish the
                //victim pending image to this same operation before2B9290.
                state = PublishCurrent(state, current, pool, [physical], current.OwnedDestructionQueue
                    ?? throw Missing("current.cause4_manager140_owner"));
                int winnerCommanderId = Known(current.ReadArmyUnitCommanderId(winner), "cause4_winner_Commander12");
                var reward = package.TryApplyNativeNormalPersonRewardDuringArmyOperation(state, winnerCommanderId, 2, 0);
                if (reward.Outcome == OriginalPersonNormalArmyRewardOutcome.CurrentInputsUnresolved)
                    throw Missing("current.cause4_winner_2B9290:" + reward.MissingPhase);
                state = reward.State; winnerReward = reward.Receipt;
                effects.Add("25C1xx.2B9290_winner_kind2_reward");
                var experience = OriginalArmyDestructionForce164.Prepare(state, current, victim, winner, winnerBase);
                var packet = experience.Packet ?? throw Missing(experience.UnresolvedOwner ?? "current.cause4_winner_Force164");
                OriginalArmyDestructionForce164.ApplyContext(packet);
                state = OriginalArmyDestructionForce164.PublishCurrent(state, packet); forceGain = packet.RequestedGain;
                effects.Add("25C2xx.258A60_winner_Force164");
                var target = current.ReadArmyLifecycleTargetFields(winner.PhysicalSlot!.Value)
                    ?? throw Missing("current.cause4_winner_Target38");
                if (Known(target.Kind38, "cause4_winner_Target38") == 0x1B && Known(target.Identity3A, "cause4_winner_Target3A") == physical)
                    if (!OriginalOwnedArmyDestructorFlags.SetSourceBit(current, winner.PhysicalSlot.Value, 2, true))
                        throw Missing("current.cause4_winner_Army150");
                effects.Add("25C3xx.winner_Target38_3A_flag2");
            }
            foreach (var other in OriginalArmyMovementManagerLifecycle.ReadCurrentPointerOrder(current))
            {
                int index = Known(other.PhysicalSlot, "cause4_target_clear_Army");
                if (!Known(current.ReadArmyUnitNativeValidity(new(index)), "cause4_target_clear_validity")) continue;
                var saved = state.NativeArmyLifecycle!.Slots[index];
                var image = saved.ActiveUnitId is int active ? state.Units.GetValueOrDefault(active) : saved.PendingImage;
                if (Known(image?.NativeArmyConfiguration?.Action37, "cause4_target_clear_Action37") != 7) continue;
                var target = current.ReadArmyLifecycleTargetFields(index) ?? throw Missing("current.cause4_target38");
                if (Known(target.Kind38, "cause4_target38") == 0x1B && Known(target.Identity3A, "cause4_target3A") == physical)
                    current.SetArmyLifecycleTargetFields(index, new(0x1B, 0));
            }
            effects.Add("25C4xx.manager60_target38_3A_clear");
            var slots = state.NativeArmyLifecycle!.Slots.ToArray();
            var units = new Dictionary<int, UnitState>(state.Units);
            for (int i = 0; i < slots.Length; i++)
            {
                var old = slots[i]; var image = old.ActiveUnitId is int active ? units.GetValueOrDefault(active) : old.PendingImage;
                var fields = current.ReadArmyReturnTraitFields(new(i));
                if (image is not null && fields is not null)
                    image = image with { NativeArmyConfiguration = image.NativeArmyConfiguration! with { TraitSlots64 = fields.Traits64.ToArray() } };
                if (image is not null && current.ReadArmyReturnResourceFields(new(i)) is { } quantity)
                    image = image with { NativeMorale1A = quantity.Morale1A, WoundedTroops = quantity.Wounded18 };
                if (i == physical)
                {
                    if (image is null) throw Missing("current.cause4_retired_image");
                    units.Remove(id); slots[i] = old with { ActiveUnitId = null, PendingImage = image,
                        AuxiliaryAC = fields?.PersonsAC.ToArray(), AuxiliaryF4 = fields?.AuxiliaryF4.ToArray() };
                }
                else if (image is not null)
                {
                    if (old.ActiveUnitId is int activeId) units[activeId] = image;
                    slots[i] = old with { PendingImage = old.PendingImage is null ? null : image,
                        AuxiliaryAC = fields?.PersonsAC.ToArray() ?? old.AuxiliaryAC, AuxiliaryF4 = fields?.AuxiliaryF4.ToArray() ?? old.AuxiliaryF4 };
                }
            }
            var savedArena = OriginalOwnedArmyLifecycleTargets.PublishCurrent(
                OriginalOwnedArmyDestructorFlags.PublishCurrent(state.NativeArmyLifecycle!, current), current) with
            { Slots = slots, Indexes = pool.ExportOwnedState() };
            var result = state with { Units = units, Officers = new(state.Officers) { [army.OfficerId] = state.Officers[army.OfficerId] with { Deployed = false } },
                NativeArmyLifecycle = savedArena, ArmyMapOccupancy = current.ExportArmyMapOccupancy() };
            return new(result, new(physical, id, commander, effects, tracking.Writes)
                { VictimHome = captured.VictimHome, WinnerReward = winnerReward, WinnerForce164Gain = forceGain }, null);
        }
        catch (InvalidDataException unknown) { return new(null, null, unknown.Message); }
    }
}
