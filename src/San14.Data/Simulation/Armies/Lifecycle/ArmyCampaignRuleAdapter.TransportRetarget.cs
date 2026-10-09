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
            NativeTransport = transport with { Waypoints2C = receipt.Waypoints2C.ToArray(),
                Choice41 = receipt.Choice41, CurrentNextHex48 = receipt.NextHex48 },
            NativeOverrideTarget = receipt.ClearOverride3C3E ? new(0xFF, 0) : unit.NativeOverrideTarget
        };
        var targets = arena.Targets38?.ToArray();
        if (targets is not { Length: 501 }) return null;
        targets[physical] = new(receipt.TargetKind38, receipt.TargetIdentity3A);
        var submitted = state with { Units = new(state.Units) { [unit.Id] = staged },
            NativeArmyLifecycle = arena with { Targets38 = targets } };
        var registered = ArmyChangedListsSource.Append(submitted, receipt.ChangedArmyList18PhysicalSlot);
        if (registered.State is not { } result) return null;
        return new(result, TransportRetargetRevision);
    }
}
