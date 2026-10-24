using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2A1980 choice1 calls2EA480(Army,0,0,0), not a player order or the
    //automatic low-resource caller's param3=1 report policy.
    private GameState FinishTransportReturnCommand(GameState state, SourceWorldContext current,
        UnitState image, FacilityOperation operation)
    {
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw MaintenanceMissing("transport_finish.receiver");
        if (MaintenanceKnown(ArmySavedCommandKnowledge.ReadControl(state, image), "transport_finish.control140") != 0)
            throw MaintenanceMissing("transport_finish.stored_command");
        var actor = new ArmyUnitReference(physical);
        ushort commander = MaintenanceKnown(current.ReadArmyUnitCommanderId(actor), "transport_finish.Commander12");
        var person = current.ReadPersonLookupReference(commander <= 6000 ? commander : 0)
            ?? throw MaintenanceMissing("transport_finish.Person_pointer");
        ushort home = person.PhysicalSlot is int slot && MaintenanceKnown(current.ReadPersonNativeValidity(slot), "transport_finish.Person_validity")
            ? MaintenanceKnown(current.ReadPersonFirstLocation(slot), "transport_finish.PersonE2") : (ushort)0;
        byte kind = home is >= 52 and <= 61 ? (byte)6 : (byte)5;
        ushort identity = kind == 6 ? (ushort)(home - 51) : home <= 51 ? home : (ushort)0;
        byte action = kind == 6 ? (byte)12 : (byte)11;
        var target = kind == 6 ? current.ReadGateTableReference(identity) : current.ReadCityTableReference(identity);
        if (target is null) throw MaintenanceMissing("transport_finish.Home_pointer");
        ushort link = MaintenanceKnown(current.ReadBaseObjectLink(target), "transport_finish.Home4E");
        var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0)
            ?? throw MaintenanceMissing("transport_finish.Home_Object");
        ushort goal = MaintenanceKnown(current.ReadObjectHexIndex(obj), "transport_finish.Home_Object10");
        if (goal >= 48400) throw MaintenanceMissing("transport_finish.Home_projection");
        var receiver = state.ReadArmyLifecycle()?.Receivers.FindUnit(image.Id)
            ?? throw MaintenanceMissing("transport_finish.receiver_owner");
        var runtime = image.NativeTacticRuntime ?? throw MaintenanceMissing("transport_finish.runtime");
        var mission = image.Mission is { Kind: UnitMissionKind.Transport } transport ? transport
            : throw MaintenanceMissing("transport_finish.mission");
        byte choice = MaintenanceKnown(TransportRouteSourceAdapter.ReadChoice(state, image), "transport_finish.Choice41");
        ushort[] points = Enumerable.Repeat((ushort)48400, 5).ToArray();
        var changed = image with
        {
            FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(action), DestinationHexId = goal,
            Mission = mission with { DestinationCityId = kind == 5 ? identity : null },
            TransportRoute = TransportRouteSourceAdapter.Decode(points),
            March = (image.March ?? throw MaintenanceMissing("transport_finish.March")) with { NextStep = ArmyMarchDestination.None },
            OverrideTarget = runtime.Abnormal25 == 0 ? ArmyOverrideTargetState.Cleared : image.OverrideTarget
        };
        var working = OwnedArmyLifecycleTargets.Write(state, receiver.ReceiverId, new(kind, identity));
        current.SetArmyLifecycleTargetFields(physical, new(kind, identity));
        working = working with { Units = new(working.Units) { [image.Id] = changed },
            ResourceState = ArmyConfigurationSourceAdapter.WriteAction(
                TransportRouteSourceAdapter.Write(working.ResourceState, image.Id, points, choice), image.Id, action) };
        if (runtime.Abnormal25 == 0)
        {
            current.ClearArmyCommandOverride(physical);
            var lifecycle = working.ReadArmyLifecycle()!;
            working = working.WithArmyLifecycle(lifecycle with
                { Receivers = lifecycle.Receivers.With(receiver with { OverrideTarget = changed.OverrideTarget }) });
            working = ArmyOverrideTargetSourceAdapter.ReconcileOwner(working, changed);
        }
        //No ChangedList or city debit is part of2EA480. Keep cargo, flags26
        //and raw41; only the actual route/cache writes follow the command.
        working = FRoute(working, image.Id, true, current, false);
        changed = working.Units[image.Id];
        return changed.March?.NextStep == ArmyMarchDestination.None
            ? StopTransportRoute(working, current, changed, operation, maintenanceCaller: false) : working;
    }

    //304A20's real type2 holding shape is raw13/9/current with five empty
    //transport words. It is unrelated to mode3's normalized Stay1 packet.
    private GameState HoldTransportPositionAfterCommand(GameState state, SourceWorldContext current,
        UnitState image, FacilityOperation operation)
    {
        if (MaintenanceKnown(ArmySavedCommandKnowledge.ReadControl(state, image), "transport_hold.control140") != 0)
            throw MaintenanceMissing("transport_hold.stored_command");
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw MaintenanceMissing("transport_hold.receiver");
        ushort at = MaintenanceKnown(ArmyMarchSourceAdapter.ReadCurrentHex(state, image), "transport_hold.current2A");
        var receiver = state.ReadArmyLifecycle()?.Receivers.FindUnit(image.Id) ?? throw MaintenanceMissing("transport_hold.owner");
        var mission = image.Mission is { Kind: UnitMissionKind.Transport } transport ? transport
            : throw MaintenanceMissing("transport_hold.mission");
        byte choice = MaintenanceKnown(TransportRouteSourceAdapter.ReadChoice(state, image), "transport_hold.Choice41");
        ushort[] points = Enumerable.Repeat((ushort)48400, 5).ToArray();
        var held = image with
        {
            FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(13), DestinationHexId = at,
            Mission = mission with { DestinationCityId = null }, TransportRoute = TransportRouteSourceAdapter.Decode(points),
            March = (image.March ?? throw MaintenanceMissing("transport_hold.March")) with { NextStep = ArmyMarchDestination.None }
        };
        var working = OwnedArmyLifecycleTargets.Write(state, receiver.ReceiverId, new(9, at));
        current.SetArmyLifecycleTargetFields(physical, new(9, at));
        working = working with { Units = new(working.Units) { [image.Id] = held },
            ResourceState = ArmyConfigurationSourceAdapter.WriteAction(
                TransportRouteSourceAdapter.Write(working.ResourceState, image.Id, points, choice), image.Id, 13) };
        if (MaintenanceKnown(ArmyOverrideTargetSourceAdapter.Read(working, held).Kind3C, "transport_hold.override3C") != 255)
            return working;
        var runtime = held.NativeTacticRuntime ?? throw MaintenanceMissing("transport_hold.runtime");
        return runtime.Abnormal23 == 0 && runtime.Abnormal24 == 0 && runtime.Abnormal25 == 0
            ? AppendArmyCommandSpeech(working, current, held, operation, CombatSpeechEvent.ArmyHoldingPosition) : working;
    }

    private GameState StopTransportRoute(GameState state, SourceWorldContext current,
        UnitState image, FacilityOperation operation, bool maintenanceCaller)
    {
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw MaintenanceMissing("transport_stop.receiver");
        if (!OwnedArmyDestructorFlags.SetSourceBit(current, physical, 3, true))
            throw MaintenanceMissing("transport_stop.flags150");
        var working = state.WithArmyLifecycle(OwnedArmyDestructorFlags.PublishCurrent(state.ReadArmyLifecycle()!, current));
        if (!IsPlayerRulerLedDistrict(working, current, image)) return working;
        var actor = new ArmyUnitReference(physical);
        byte action = MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadAction(working, image), "transport_stop.action37");
        var formatted = FormatHistoryReport(working, current, 0x3231,
            new(Army0: actor, String0: ReadHistoryDataText(0, 259 + action),
                String1: ReadHistoryChoiceName(working, 2), String2: ReadHistoryDataText(1, 0x283)));
        working = formatted.State;
        int hex, day;
        if (maintenanceCaller)
        {
            day = operation.ReadCampaignDay(working);
            hex = MaintenanceKnown(current.ReadArmyUnitRawHex2A(actor), "transport_stop.Hex2A");
        }
        else
        {
            hex = MaintenanceKnown(current.ReadArmyUnitRawHex2A(actor), "transport_stop.Hex2A");
            day = operation.ReadCampaignDay(working);
        }
        var effects = new List<CampaignEffectRequest>();
        working = WriteHistory(working, current, new ReportCommand(5, ReadFacilityHistoryDistrict(current, actor), hex,
            day, formatted.Text, CampaignHistoryWriter.Supplemental(working), 5012, MarkBit4: 1), effects);
        return effects.Count == 0 ? working : working with { CampaignEffectRequests = (working.CampaignEffectRequests
            ?? throw MaintenanceMissing("transport_stop.effect_owner"))
            .Append(CampaignEffectSource.ArmyCommandMaintenance, operation.Revision, effects) };
    }
}
