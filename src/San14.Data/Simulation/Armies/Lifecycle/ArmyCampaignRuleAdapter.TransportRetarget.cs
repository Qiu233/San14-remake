using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPlayableTransportRetargetRules
{
    public const string TransportRetargetRevision = "san14:ce-7F6870-transport-existing-city-target-20261008";

    public PlayableTransportCreationResolution? RetargetTransport(GameState state, UnitState unit, CityState city)
    {
        if (unit.NativeArmyOrigin?.NativeSlotIndex is not int physical || physical is < 1 or > 500 ||
            state.NativeArmyLifecycle is not { Indexes.ChangedPendingUnitIds: not null } arena || physical >= arena.Slots.Length ||
            arena.Slots[physical] is not { ActiveUnitId: var id, Commander12: var commander, Status10: var status } ||
            id != unit.Id || commander != unit.OfficerId || unit.NativeTransport is not { } transport ||
            unit.NativeArmyConfiguration is not { } configuration || unit.NativeTacticRuntime is not { } runtime ||
            unit.Mission is not { Kind: UnitMissionKind.Transport } mission || city.ForceId != unit.ForceId)
            return null;
        var geometry = _definitions.ReadCityEntryGeometry(city.Id);
        if (geometry.Goal != city.HexId) return null;
        var prepared = TransportArmyRetargetSource.PrepareCity(new(physical,
            commander != 0 && status is not null ? status != 0 : null, status, configuration.Action37,
            unit.NativeCommandControl140, runtime.Abnormal25, runtime.Flags26, transport.Choice41),
            new(city.Id, geometry.Valid, transport.Waypoints2C.Select(x => (int)x).ToArray()));
        if (prepared.Receipt is not { } receipt) return null;
        var staged = unit with
        {
            DestinationHexId = city.HexId!.Value, Mission = mission with { DestinationCityId = city.Id },
            NativeArmyConfiguration = configuration with { Action37 = receipt.Action37 },
            NativeTacticRuntime = runtime with { Flags26 = receipt.Flags26 },
            NativeTransport = transport with { Waypoints2C = receipt.Waypoints2C.ToArray(), Choice41 = receipt.Choice41 },
            March = (unit.March ?? new(null, null, null)) with { NextStep = ArmyMarchSourceAdapter.DecodeDestination(receipt.NextHex48) },
            OverrideTarget = receipt.ClearOverride3C3E ? ArmyOverrideTargetState.Cleared : unit.OverrideTarget
        };
        if (arena.Targets is null) return null;
        var targetState = OwnedArmyLifecycleTargets.Write(state, arena.Slots[physical].ReceiverId,
            new(receipt.TargetKind38, receipt.TargetIdentity3A));
        var submitted = state with { Units = new(state.Units) { [unit.Id] = staged },
            NativeArmyLifecycle = targetState.NativeArmyLifecycle, ResourceState = targetState.ResourceState };
        SourceWorldContext current;
        try { current = CaptureWritableTransportRouteCurrent(submitted); }
        catch (InvalidDataException) { return null; }
        var registered = ArmyChangedListsSource.Append(submitted, receipt.ChangedArmyList18PhysicalSlot, current);
        if (registered.State is not { } result) return null;
        return new(result, TransportRetargetRevision);
    }
}
