using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The reached action1 no-path completion, followed by the current
    /// ordinary command policy. Other completion callers retain their own gates.</summary>
    private GameState FinishNoPathArmyCommand(GameState state, SourceWorldContext current,
        UnitState image, NativeArmyProgressStageMoment moment)
        => FinishNoPathArmyCommand(state, current, image, (FacilityOperation)moment);

    private GameState FinishNoPathArmyCommand(GameState state, SourceWorldContext current,
        UnitState image, FacilityOperation operation)
        => FinishArmyCommand(state, current, image, operation, reportCompletion: false);

    /// <summary>2A11A0(1), reached when a command's actual target is no longer
    /// usable. The source-time speech/report precedes its clear and next policy.</summary>
    private GameState FinishInvalidArmyCommand(GameState state, SourceWorldContext current,
        UnitState image, FacilityOperation operation)
        => FinishArmyCommand(state, current, image, operation, reportCompletion: true);

    private GameState FinishArmyCommand(GameState state, SourceWorldContext current,
        UnitState image, FacilityOperation operation, bool reportCompletion)
    {
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw MaintenanceMissing("completion_receiver");
        byte flags = MaintenanceKnown(current.ReadArmyUnitFlags150(new(physical)), "completion_flags150");
        var working = state;
        if ((flags & 4) == 0)
        {
            byte action = MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadAction(working, image), "completion_action37");
            if (action > 1 && (action < 11 || action is 14 or 16 or 17))
            {
                //3C is independent of param2. Only C360 is conditional on
                //param2 and the actual player's ruler-led District predicate.
                working = AppendArmyCommandSpeech(working, current, image, operation, CombatSpeechEvent.ArmyOrderFinished);
                if (reportCompletion && IsPlayerRulerLedDistrict(working, current, image))
                    working = AddMaintenanceReport(working, current, image, operation, 0xC360);
            }
        }
        //2409E0 follows the source-time3C/C360 prefix even when bit2 was set.
        if (!OwnedArmyDestructorFlags.SetSourceBit(current, physical, 2, false))
            throw MaintenanceMissing("no_path_flags150");
        working = working.WithArmyLifecycle(OwnedArmyDestructorFlags.PublishCurrent(working.ReadArmyLifecycle()!, current));
        if (MaintenanceKnown(image.NativeMapUiByte13C, "completion_MapUi13C") != 0)
            return CompleteCampaignExpeditionArmyCommand(working, current, image, operation);

        return CompleteArmyCommandPolicyCurrent(working, current, working.Units[image.Id], operation);
    }

    /// <summary>304A20 clears the actual valid receiver's five points even
    ///while a saved command prevents its action/main target from changing.</summary>
    private GameState HoldArmyPositionAfterCommand(GameState state, SourceWorldContext current,
        UnitState image, NativeArmyProgressStageMoment moment)
        => HoldArmyPositionAfterCommand(state, current, image, (FacilityOperation)moment);

    private GameState HoldArmyPositionAfterCommand(GameState state, SourceWorldContext current,
        UnitState image, FacilityOperation operation)
    {
        bool nativeValid = MaintenanceKnown(current.ReadArmyUnitNativeValidity(new(image.NativeArmyOrigin!.NativeSlotIndex)), "holding_Army_validity");
        bool canReplace = nativeValid &&
            MaintenanceKnown(ArmySavedCommandKnowledge.ReadControl(state, image), "holding_control140") == 0;
        ushort at = MaintenanceKnown(ArmyMarchSourceAdapter.ReadCurrentHex(state, image), "holding_current2A");
        var march = image.March ?? throw MaintenanceMissing("holding_march");
        var held = image with { FieldAttackPolicy = canReplace ? ArmyConfigurationSourceAdapter.DecodePolicy(13) : image.FieldAttackPolicy,
            March = march with { Target = nativeValid ? ArmyMarchDestination.None : march.Target,
                FollowingWaypoints = nativeValid ? new([]) : march.FollowingWaypoints, NextStep = ArmyMarchDestination.None } };
        var receiver = state.ReadArmyLifecycle()!.Receivers.FindUnit(image.Id)
            ?? throw MaintenanceMissing("holding_target_receiver");
        var working = state;
        if (canReplace)
        {
            working = OwnedArmyLifecycleTargets.Write(working, receiver.ReceiverId, new(9, at));
            current.SetArmyLifecycleTargetFields(held.NativeArmyOrigin!.NativeSlotIndex!.Value, new(9, at));
            working = working with { ResourceState = ArmyConfigurationSourceAdapter.WriteAction(working.ResourceState, image.Id, 13) };
        }
        working = working with { Units = new(working.Units) { [image.Id] = held } };

        //3D does not test150 bit2, player affiliation or a district. Preserve
        //the original short circuit and do not consume an unused speech owner.
        byte kind = MaintenanceKnown(ArmyOverrideTargetSourceAdapter.Read(working, held).Kind3C, "holding_override3C");
        if (kind != 255) return working;
        var runtime = held.NativeTacticRuntime ?? throw MaintenanceMissing("holding_runtime");
        if (runtime.Abnormal23 != 0 || runtime.Abnormal24 != 0 || runtime.Abnormal25 != 0) return working;
        return AppendArmyCommandSpeech(working, current, held, operation, CombatSpeechEvent.ArmyHoldingPosition);
    }

    private GameState AppendArmyCommandSpeech(GameState state, SourceWorldContext current, UnitState image,
        FacilityOperation operation, CombatSpeechEvent speechEvent, ArmyBaseArrivalSpeechContext? arrival = null)
    {
        int physical = image.NativeArmyOrigin!.NativeSlotIndex!.Value;
        ushort commander = MaintenanceKnown(current.ReadArmyUnitCommanderId(new(physical)), "command_speech_commander12");
        int person = (current.ReadPersonLookupReference(commander <= 6000 ? commander : 0)
            ?? throw MaintenanceMissing("command_speech_Person_pointer")).PhysicalSlot
            ?? throw MaintenanceMissing("command_speech_Person_pointer");
        if (state.PersonnelCommands is null) throw MaintenanceMissing("command_speech_personnel_owner");
        PersonReturnCommands.BindCurrent(state, current);
        int hex = PersonReturnCommands.ResolvePersonPosition(state, current, person, operation.ReadDate()).PhysicalSlot ?? 48400;
        //The position getter can initialize a journey's missing deadline. Keep
        //that owner in this same transaction; pool export remains at the caller.
        var working = state with { PersonnelCommands = current.ReadOwnedPersonCommandManager()
            ?? throw MaintenanceMissing("command_speech_personnel_owner") };
        int identity = MaintenanceKnown(current.ReadPersonIdentity(person), "command_speech_Person_identity10");
        var speech = new CombatSpeechRequest(CombatParticipantKind.Army, image.Id, identity,
            speechEvent, hex, null, null, null, null, null) { BaseArrival = arrival,
                CommandOwnedLayout = speechEvent is CombatSpeechEvent.ArmyEnteredBase or CombatSpeechEvent.ArmyDestinationReachedHolding
                    ? CaptureArmyCommandSpeechLayoutCurrent(working,current,identity) : null };
        return working with { CampaignEffectRequests = (working.CampaignEffectRequests ?? throw MaintenanceMissing("command_speech_effect_owner"))
            .Append(arrival is null ? CampaignEffectSource.ArmyCommandMaintenance : CampaignEffectSource.ArmyMovement,
                operation.Revision, [new(speech, null)]) };
    }
}
