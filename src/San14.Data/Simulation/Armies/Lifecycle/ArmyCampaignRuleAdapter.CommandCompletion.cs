using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The reached action1 no-path completion, followed by the current
    /// ordinary command policy. Other completion callers retain their own gates.</summary>
    private GameState FinishNoPathArmyCommand(GameState state, SourceWorldContext current,
        UnitState image, NativeArmyProgressStageMoment moment)
    {
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw MaintenanceMissing("completion_receiver");
        //2A11A0(0): action1 has no3C speech orC360 report. The source still
        //reads and clears150 bit2 before selecting the following command.
        if (!OwnedArmyDestructorFlags.SetSourceBit(current, physical, 2, false))
            throw MaintenanceMissing("no_path_flags150");
        var working = state.WithArmyLifecycle(OwnedArmyDestructorFlags.PublishCurrent(state.ReadArmyLifecycle()!, current));
        if (MaintenanceKnown(image.NativeMapUiByte13C, "completion_MapUi13C") != 0)
            throw MaintenanceMissing("completion_nonzero_MapUi13C");

        //22C990 returns the current Force-table receiver, including its actual
        //physical0 fallback. There is no extra Force native-validity gate.
        int force = MaintenanceKnown(current.ReadForcePhysicalSlot(current.ReadArmyUnitForceReference(new(physical))), "completion_Force");
        if (force is < 1 or > 45) throw MaintenanceMissing("completion_special_Force");
        byte choice = MaintenanceKnown(image.NativeCommandChoice41, "completion_Choice41");
        if (choice == 0) return working;
        if (choice != 2) throw MaintenanceMissing("completion_non_stay_Choice41");
        return HoldArmyPositionAfterCommand(working, current, image, moment);
    }

    /// <summary>304A20 at the proved native-valid, control0 entry. It overwrites
    ///the five intermediate points but preserves credit, override and route cache.</summary>
    private GameState HoldArmyPositionAfterCommand(GameState state, SourceWorldContext current,
        UnitState image, NativeArmyProgressStageMoment moment)
    {
        if (MaintenanceKnown(ArmySavedCommandKnowledge.ReadControl(state, image), "holding_control140") != 0)
            throw MaintenanceMissing("holding_saved_command");
        ushort at = MaintenanceKnown(ArmyMarchSourceAdapter.ReadCurrentHex(state, image), "holding_current2A");
        var march = image.March ?? throw MaintenanceMissing("holding_march");
        var held = image with { FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(13),
            March = march with { Target = ArmyMarchDestination.None,
                FollowingWaypoints = new([]), NextStep = ArmyMarchDestination.None } };
        var receiver = state.ReadArmyLifecycle()!.Receivers.FindUnit(image.Id)
            ?? throw MaintenanceMissing("holding_target_receiver");
        var working = OwnedArmyLifecycleTargets.Write(state, receiver.ReceiverId, new(9, at));
        working = working with { Units = new(working.Units) { [image.Id] = held },
            ResourceState = ArmyConfigurationSourceAdapter.WriteAction(working.ResourceState, image.Id, 13) };

        //3D does not test150 bit2, player affiliation or a district. Preserve
        //the original short circuit and do not consume an unused speech owner.
        byte kind = MaintenanceKnown(ArmyOverrideTargetSourceAdapter.Read(working, held).Kind3C, "holding_override3C");
        if (kind != 255) return working;
        var runtime = held.NativeTacticRuntime ?? throw MaintenanceMissing("holding_runtime");
        if (runtime.Abnormal23 != 0 || runtime.Abnormal24 != 0 || runtime.Abnormal25 != 0) return working;
        int physical = held.NativeArmyOrigin!.NativeSlotIndex!.Value;
        ushort commander = MaintenanceKnown(current.ReadArmyUnitCommanderId(new(physical)), "holding_commander12");
        int person = (current.ReadPersonLookupReference(commander <= 6000 ? commander : 0)
            ?? throw MaintenanceMissing("holding_Person_pointer")).PhysicalSlot
            ?? throw MaintenanceMissing("holding_Person_pointer");
        if (working.PersonnelCommands is null) throw MaintenanceMissing("holding_personnel_owner");
        PersonReturnCommands.BindCurrent(working, current);
        int hex = PersonReturnCommands.ResolvePersonPosition(working, current, person, moment.PreWriterThirdDate).PhysicalSlot ?? 48400;
        //The position getter can initialize a journey's missing deadline. Keep
        //that owner in this same transaction; pool export remains at the caller.
        working = working with { PersonnelCommands = current.ReadOwnedPersonCommandManager()
            ?? throw MaintenanceMissing("holding_personnel_owner") };
        int identity = MaintenanceKnown(current.ReadPersonIdentity(person), "holding_Person_identity10");
        var speech = new CombatSpeechRequest(CombatParticipantKind.Army, held.Id, identity,
            CombatSpeechEvent.ArmyHoldingPosition, hex, null, null, null, null, null);
        return working with { CampaignEffectRequests = (working.CampaignEffectRequests ?? throw MaintenanceMissing("holding_effect_owner"))
            .Append(CampaignEffectSource.ArmyCommandMaintenance, moment.CalendarCycleOrdinal, [new(speech, null)]) };
    }
}
