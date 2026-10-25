using San14.Core;

namespace San14.Data;

public sealed partial class OwnedArmyDestructionStage
{
    /// <summary>2A2760's employed Commander call to259360(null,null,0,4).
    /// The enclosing force operation still owns its Base/District temporaries,
    /// the original friendly Home and final phase26 retirement.</summary>
    internal ArmyCause4Result ApplyAbsorptionCommanderCause4Current(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int physical,
        NativeMapSeasonDate operationDate, List<CampaignEffectRequest> feedback)
    {
        try
        {
            var arena = state.ReadArmyLifecycle() ?? throw Missing("current.absorption_cause4_saved_arena");
            var slot = arena.ReceiverSlots()[physical];
            if (slot.ActiveUnitId is not int id || !state.Units.TryGetValue(id, out var army) ||
                army.NativeTacticRuntime is null || !ArmyConfigurationSourceAdapter.HasConfiguration(army))
                throw Missing("current.absorption_cause4_live_Army_image");
            var victim = new ArmyUnitReference(physical);
            if (!Known(current.ReadArmyUnitNativeValidity(victim), "absorption_cause4_Army_validity"))
                throw Missing("branch.absorption_cause4_invalid_Army");
            int commander = CommanderPhysical(current, victim);
            if (ArmyPending70Admission.ContainsCurrentPointer(new(RegistryKind.Army, physical), new Pending(current)))
                return new(state, new(physical, id, commander, ["259360.pending70_early_return"], []), null);
            if (!Known(current.ReadPersonNativeValidity(commander), "absorption_cause4_Commander_validity") ||
                current.ReadPersonStatus(commander) is not (>= 1 and <= 4))
                throw Missing("branch.absorption_cause4_employed_Commander");
            var force = current.ReadArmyUnitForceReference(victim);
            if (force.PhysicalSlot is not (>= 1 and <= 45) ||
                !Known(current.ReadForceNativeValidity(force), "absorption_cause4_Force_validity"))
                throw Missing("branch.absorption_cause4_ordinary_Force");
            var winner = new ArmyUnitReference(null);
            var winnerBase = new BaseReference(BaseKind.City, null);
            var captured = ArmyDestructionResources.CaptureOwners(current, victim, winner, winnerBase);
            int hex = Known(current.ReadArmyUnitRawHex2A(victim), "absorption_cause4_Army2A");
            var effects = new List<string>();
            if (!OwnedArmyDestructorFlags.SetSourceBit(current, physical, 1, true))
                throw Missing("current.absorption_cause4_Army150");
            effects.Add("25944F.2409E0_Army150_bit1");
            _ = CachedEffect(current, victim, 0x36);
            var traits = new ArmyReturnTraitCleanup.Current(current, pool);
            for (int index = 0; index < 9; index++) traits.ClearCommanderSlot(victim, index);
            for (int index = 0; index < 36; index++) traits.ClearReceivedSlot(victim, index);
            effects.Add("259526.nine2A2340_then36_2A2540");
            _ = PersonalityImmunity(state, current, commander);
            _ = NearbyProtection(state, current, victim, force, operationDate);
            effects.Add("2595xx.Personality_and_protector_actual_temporaries");
            var resources = ArmyDestructionResources.Prepare(state, current, victim, winner, winnerBase, captured);
            var resource = resources.Packet ?? throw Missing(resources.UnresolvedOwner ?? "current.absorption_cause4_resources");
            ArmyDestructionResources.ApplyContext(resource);
            state = ArmyDestructionResources.PublishResources(state, resource);
            state = package.ApplyArmyDestructionLoyalty(state, current, commander, force, projectOwnedRoles: true);
            effects.Add("259Fxx.resources_then_actual_232500_232CB0_Loyalty");

            //Cause4 omits injury/death/capture/morale waves and kind14, but
            //still executes the employed Person's real2EA9A0 before pending70.
            state = ReturnAbsorptionCommander(state, current, pool, commander, operationDate, feedback);
            effects.Add("25B7CE.232460_then2EA9A0_current_Return");
            state = ReleaseCaptives(state, current, physical, operationDate, null, feedback);
            effects.Add("25B89x.230C70_actual_captive_iterator_and2E7890");
            var tracking = ArmyDestructionTrackingTail.ApplyCurrent(state, current, physical);
            state = tracking.State;
            NearbyPersonalityTail(state, current, victim, force, captured.WinnerForce);
            effects.Add("25B9xx.manager60_tracking_and_current_Force_nearby_tail");
            var originalHex = current.ReadHexTableReference(hex < 48400 ? hex : 0)
                ?? throw Missing("current.absorption_cause4_Hex_pointer");
            if (originalHex.PhysicalSlot is int actualHex) current.SetHexRaw1C(actualHex, 0);
            if (ArmyPending70Admission.EnqueueCurrentPointer(new(RegistryKind.Army, physical), new Pending(current)) ==
                ArmyPending70AdmissionOutcome.KnownAllocationFailure)
                throw Missing("branch.absorption_cause4_pending70_allocation_failure");
            effects.Add("25C171.Hex1C_clear_then25C180.pending70_admission");

            //No winner reward/Force164/target flag branch exists for the
            //two actual null pointers supplied by2A2760.
            foreach (var other in ArmyMovementManagerLifecycle.ReadCurrentPointerOrder(current))
            {
                int index = Known(other.PhysicalSlot, "absorption_cause4_target_clear_Army");
                if (!Known(current.ReadArmyUnitNativeValidity(new(index)), "absorption_cause4_target_clear_validity")) continue;
                var saved = state.ReadArmyLifecycle()!.ReceiverSlots()[index];
                var image = saved.ActiveUnitId is int active ? state.Units.GetValueOrDefault(active) : saved.PendingImage;
                if (Known(ArmyConfigurationSourceAdapter.ReadAction(state, image), "absorption_cause4_Action37") != 7) continue;
                var target = current.ReadArmyTargetState(index) ?? throw Missing("current.absorption_cause4_Target38");
                if (Known(target.Kind, "absorption_cause4_Target38") == ArmyTargetKind.ArmyReceiver &&
                    current.TargetsArmyReceiver(target.Destination ?? throw Missing("current.absorption_cause4_Target3A"), physical))
                    current.ClearArmyTargetToPlaceholder(index);
            }
            effects.Add("25C4xx.manager60_target38_3A_clear");
            state = PublishCurrent(state, current, pool, [physical], current.OwnedDestructionQueue
                ?? throw Missing("current.absorption_cause4_manager140_owner"), projectOwnedRoles: true);
            return new(state, new(physical, id, commander, effects, tracking.Writes)
                { ResourceStageCompleted = true, VictimHome = captured.VictimHome }, null);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    private GameState ReturnAbsorptionCommander(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int commander, NativeMapSeasonDate operationDate,
        List<CampaignEffectRequest> feedback)
    {
        var force = current.ReadPersonForce(commander) ?? throw Missing("current.absorption_return_Person_Force");
        bool free = !Known(current.ReadForceNativeValidity(force), "absorption_return_Force_validity");
        var home = new BaseReference(BaseKind.City, null);
        if (!free)
        {
            home = BaseLookup.Find(Known(current.ReadPersonFirstLocation(commander), "absorption_return_PersonE2"), current);
            free = !Known(current.ReadBaseNativeValidity(home), "absorption_return_Home_validity");
            if (!free)
                free = Known(current.ReadBaseForceSlot(home), "absorption_return_Home_Force") !=
                    Known(current.ReadPersonOldForceSlot(commander), "absorption_return_Person_Force22CF60");
        }
        if (free)
        {
            TacticSourceSamplerWorld Sampler() => package.ReadArmyDestructionSampler(state, current)
                ?? throw Missing("current.absorption_return_freedom_World450_45C");
            return package.ApplyCaptiveFreedomCurrent(state, current, pool, commander, operationDate,
                () => Sampler().Day450, Sampler, feedback);
        }
        var position = PersonReturnCommands.ResolvePersonPosition(state, current, commander, operationDate);
        if (current.ReadOwnedPersonCommandManager() is { } positionedOwner)
            state = state with { PersonnelCommands = positionedOwner };
        if (position.PhysicalSlot == current.ReadBaseCurrentHexIndex(home))
        {
            current.WritePersonSecondLocation(commander,
                Math.Min(Known(current.ReadBaseObjectLink(home), "absorption_return_Home4E"), (ushort)1061));
            return state;
        }
        var prepared = PersonReturnCommands.Prepare(state, current, commander, operationDate);
        var packet = prepared.Packet ?? throw Missing(prepared.UnresolvedOwner ?? "current.absorption_return_Prepare");
        var applied = PersonReturnCommands.Apply(state, current, packet);
        return applied.State ?? throw Missing(applied.UnresolvedOwner ?? "current.absorption_return_Apply");
    }
}
