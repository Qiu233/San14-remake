using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //A saved Object operand retains its original lookup, including0. Read it
    //against the current directory when reached; the lookup can be rebound
    //after the snapshot, so the old projected receiver is not its authority.
    private static ArmyTargetSourceFields ReadSavedArmyCommandTarget(GameState state, SourceWorldContext current,
        ArmyReceiverId receiver, ArmyCommandSnapshot saved)
    {
        if (saved.Target.Kind == ArmyTargetKind.MapObjectReceiver)
        {
            ushort alias = (state.ResourceState as CampaignSourceKnowledge)?.FindArmySavedTargetLookupAlias(receiver)
                ?? throw MaintenanceMissing("saved_Object_lookup_alias");
            return new(11, alias);
        }
        var arena = state.ReadArmyLifecycle() ?? throw MaintenanceMissing("saved_target_arena");
        return ArmyTargetSourceAdapter.Encode(saved.Target, null, new(arena), current.CurrentMapObjectDirectory);
    }

    //The entry retains control140 before this prefix. Its invalid-override
    //branch is independent from the three abnormal counters' later gates.
    private GameState MaintainArmyOverridePrefix(GameState state, SourceWorldContext current,
        UnitState image, FacilityOperation operation, byte control)
    {
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw MaintenanceMissing("override_Army_receiver");
        var runtime = image.NativeTacticRuntime ?? throw MaintenanceMissing("override_runtime");
        var fields = ArmyOverrideTargetSourceAdapter.Read(state, image);
        byte kind = MaintenanceKnown(fields.Kind3C, "override_kind3C");
        bool invalid = false;
        if (kind is 5 or 6 or 11 or 27)
        {
            ushort identity = MaintenanceKnown(fields.Identity3E, "override_identity3E");
            int targetForce;
            if (kind is 5 or 6)
            {
                var target = kind == 5 ? current.ReadCityTableReference(identity <= 51 ? identity : 0)
                    : current.ReadGateTableReference(identity <= 10 ? identity : 0);
                if (target is null) throw MaintenanceMissing("override_Base_pointer");
                invalid = !MaintenanceKnown(current.ReadBaseNativeValidity(target), "override_Base_validity");
                targetForce = invalid ? 0 : MaintenanceKnown(current.ReadBaseForceSlot(target), "override_Base_Force");
            }
            else if (kind == 11)
            {
                var target = current.ReadObjectTableReference(identity <= 3000 ? identity : 0)
                    ?? throw MaintenanceMissing("override_Object_pointer");
                invalid = !MaintenanceKnown(current.ReadObjectNativeValidity(target), "override_Object_validity");
                targetForce = invalid ? 0 : MaintenanceKnown(current.ReadObjectForceSlot13(target), "override_Object_Force");
            }
            else
            {
                var target = current.ReadArmyUnitTableReference(identity <= 500 ? identity : 0)
                    ?? throw MaintenanceMissing("override_Army_pointer");
                int slot = MaintenanceKnown(target.PhysicalSlot, "override_Army_physical");
                invalid = !MaintenanceKnown(current.ReadArmyUnitNativeValidity(target), "override_Army_validity") ||
                    MaintenanceKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, slot), "override_Army_excluded");
                targetForce = invalid ? 0 : current.ReadArmyUnitRawForceSlot(target);
            }
            if (!invalid)
                invalid = !MaintenanceKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(state,
                    current.ReadArmyUnitRawForceSlot(new(physical)), targetForce), "override_relation");
        }
        bool savedClear = control != 0 && runtime.Abnormal25 == 0;
        if (!savedClear && !invalid) return state;

        int upper = MaintenanceKnown(ArmyDestructionParameterSeed.ReadCurrent(state,
            SimulationParameterKeys.Tactics.AbnormalCounterUpperBound), "override_abnormal_cap");
        byte counter = upper < 0 ? unchecked((byte)upper) : (byte)0;
        current.SetTacticAbnormalRawByte(new(RegistryKind.Army, physical), 0x25, counter);
        image = image with { NativeTacticRuntime = runtime with { Abnormal25 = counter } };
        var working = state with { Units = new(state.Units) { [image.Id] = image } };
        if (counter == 0)
        {
            image = image with { OverrideTarget = ArmyOverrideTargetState.Cleared,
                March = (image.March ?? throw MaintenanceMissing("override_March")) with { NextStep = ArmyMarchDestination.None } };
            working = working with { Units = new(working.Units) { [image.Id] = image } };
            var arena = working.ReadArmyLifecycle()!;
            var receiver = arena.Receivers.Find(arena.Slots[physical].ReceiverId)
                ?? throw MaintenanceMissing("override_receiver");
            working = working.WithArmyLifecycle(arena with { Receivers = arena.Receivers.With(receiver with
                { OverrideTarget = ArmyOverrideTargetState.Cleared }) });
            working = ArmyOverrideTargetSourceAdapter.ReconcileOwner(working, image);
            current.ClearArmyCommandOverride(physical);
            working = FRoute(working, image.Id, true, current, false);
            image = working.Units[image.Id];
        }
        if (MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadAction(working, image), "override_action37") == 13)
        {
            working = HoldArmyPositionAfterCommand(working, current, image, operation);
            image = working.Units[image.Id];
        }
        //The original saved&&25==0 short circuit leaves its report flag0,
        //even when the old override was invalid. Do not issue C35C there.
        if (!savedClear && invalid)
        {
            working = AppendArmyCommandSpeech(working, current, image, operation, CombatSpeechEvent.ArmyOrderFinished);
            if (IsPlayerRulerLedDistrict(working, current, image))
                working = AddMaintenanceReport(working, current, image, operation, 0xC35C);
        }
        return working;
    }

    private GameState RewriteSavedArmyCommandAfterTargetChange(GameState state, SourceWorldContext current,
        UnitState image, byte retainedControl)
    {
        if (retainedControl == 0) return state;
        var arena = state.ReadArmyLifecycle() ?? throw MaintenanceMissing("saved_rewrite_arena");
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw MaintenanceMissing("saved_rewrite_Army_receiver");
        var receiver = arena.Receivers.Find(arena.Slots[physical].ReceiverId)
            ?? throw MaintenanceMissing("saved_rewrite_receiver");
        var saved = receiver.SavedCommand is { Kind: ArmySavedCommandKind.Stored, Command: { } command }
            ? command : throw MaintenanceMissing("saved_rewrite_complete_command");
        bool invalid = false;
        if (saved.Action.Id is 7 or 17)
        {
            var fields = ReadSavedArmyCommandTarget(state, current, receiver.ReceiverId, saved);
            ushort identity = MaintenanceKnown(fields.Identity3A, "saved_rewrite_Army_identity");
            var target = current.ReadArmyUnitTableReference(identity <= 500 ? identity : 0)
                ?? throw MaintenanceMissing("saved_rewrite_Army_pointer");
            int targetPhysical = MaintenanceKnown(target.PhysicalSlot, "saved_rewrite_Army_physical");
            invalid = !MaintenanceKnown(current.ReadArmyUnitNativeValidity(target), "saved_rewrite_Army_validity") ||
                MaintenanceKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, targetPhysical), "saved_rewrite_Army_excluded");
            if (!invalid)
            {
                var grouping = new ArmyMapGroupingCurrentQueries(state, current, _scenario, _governorInputs,
                    Seed.Language, SimulationRuleIdentity.Version);
                invalid = MaintenanceKnown(grouping.IsAtHome2C96E0(targetPhysical), "saved_rewrite_Army_at_home") || identity == 0 ||
                    !MaintenanceKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(state,
                        current.ReadArmyUnitRawForceSlot(new(physical)), current.ReadArmyUnitRawForceSlot(target)), "saved_rewrite_Army_relation");
            }
        }
        else if (saved.Action.Id is 6 or 9 or 10)
        {
            var fields = ReadSavedArmyCommandTarget(state, current, receiver.ReceiverId, saved);
            ushort identity = MaintenanceKnown(fields.Identity3A, "saved_rewrite_Object_identity");
            var target = current.ReadObjectTableReference(identity <= 3000 ? identity : 0)
                ?? throw MaintenanceMissing("saved_rewrite_Object_pointer");
            invalid = !MaintenanceKnown(current.ReadObjectNativeValidity(target), "saved_rewrite_Object_validity") || identity == 0 ||
                !MaintenanceKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(state,
                    current.ReadArmyUnitRawForceSlot(new(physical)),
                    MaintenanceKnown(current.ReadObjectForceSlot13(target), "saved_rewrite_Object_Force")), "saved_rewrite_Object_relation");
        }
        if (!invalid) return state;
        byte choice = MaintenanceKnown(image.NativeCommandChoice41, "saved_rewrite_Choice41");
        ArmyCommandSnapshot replacement;
        if (choice == 1)
        {
            var home = current.FindArmyUnitCurrentBase(new(physical));
            ushort location = MaintenanceKnown(current.ReadBaseObjectLink(home), "saved_rewrite_Home4E");
            int slot = MaintenanceKnown(home.PhysicalSlot, "saved_rewrite_Home_physical");
            if (location < 52)
            {
                replacement = new(new(11), new(ArmyTargetKind.City, slot == 0
                    ? new(ArmyTargetDestinationKind.UnavailableBase, null, null)
                    : new(ArmyTargetDestinationKind.City, slot, null)));
            }
            else
            {
                replacement = new(new(12), new(ArmyTargetKind.Gate, slot == 0
                    ? new(ArmyTargetDestinationKind.UnavailableBase, null, null)
                    : new(ArmyTargetDestinationKind.Gate, null, null) { GateId = slot + 51 }));
            }
        }
        else if (choice == 2)
        {
            ushort at = MaintenanceKnown(current.ReadArmyUnitRawHex2A(new(physical)), "saved_rewrite_Hex2A");
            replacement = new(new(13), new(ArmyTargetKind.Hex, new(ArmyTargetDestinationKind.Hex, null, null) { HexId = at }));
        }
        else return state;
        byte nextControl = checked((byte)replacement.Action.Id);
        var slots = arena.Source.Slots.ToArray();
        slots[physical] = slots[physical] with { CommandControl140 = nextControl };
        return (state with { Units = new(state.Units) { [image.Id] = image with { NativeCommandControl140 = nextControl } },
                ResourceState = CampaignSourceKnowledge.From(state.ResourceState).WithArmySavedTargetLookupAlias(receiver.ReceiverId, null) })
            .WithArmyLifecycle(arena with { Source = arena.Source with { Slots = slots },
                Receivers = arena.Receivers.With(receiver with { SavedCommand = ArmySavedCommandState.Store(replacement) }) });
    }
}
