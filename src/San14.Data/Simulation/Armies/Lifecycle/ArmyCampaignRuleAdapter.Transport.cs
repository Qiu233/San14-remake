using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPlayableTransportCreationRules
{
    public const string TransportCreationRevision = "san14:ce-owned-2D8FE0-city-transport-20261008";

    /// <summary>Actual City type2 creator, registration and CTroops publication.
    /// The actual2009B0 initial route48 tail shares the candidate's pool.
    /// Full source movement cadence and warning callbacks remain separate.</summary>
    public PlayableTransportCreationResolution? CreateTransport(GameState state,
        DispatchTransportOrder order, UnitState projection)
    {
        state = ArmyIdentityAllocation.Initialize(state);
        if (state.ArmyIdentities!.Candidate != projection.Id) return null;
        if (projection.Mission?.Kind != UnitMissionKind.Transport || state.NativeClock is not { } clock ||
            state.ReadArmyLifecycle() is not { } arena ||
            !state.Officers.TryGetValue(order.OfficerId, out var person) || person.Deployed ||
            OfficerBehaviorSourceAdapter.EncodeFlags(person.Behavior) is not ushort flags ||
            _definitions.ResolveOfficerSource(person.Id) is null ||
            person.LocationState is not { } location || state.Units.ContainsKey(projection.Id)) return null;
        try
        {
            var world = CaptureOwnedArmyReturnWorld(state);
            var packet = TransportArmyPacketSource.TryComposeCity(state, world.Context,
                new(order.OfficerId, order.SourceCityId, order.DestinationCityId, order.Troops, order.Gold, order.Supplies,
                    order.Arrival == TransportArrivalDisposition.ReturnToSource));
            if (packet.Packet is not { } selected) return null;
            var prefix = TransportArmyCreationSource.FromLoadedSource(_creation.FormationWriterSource)
                .Prepare(state, world.Context, selected);
            if (prefix.Receipt is not { GovernorUpdateReached: false } receipt ||
                receipt.Fields.HomeSequence28 is null || receipt.FieldOwnersMissing.Count != 0) return null;
            var fields = receipt.Fields; var stocks = receipt.Resources;
            if (fields.CurrentHex2A != projection.HexId) return null;
            int? commandCost = ArmyDestructionParameterSeed.ReadCurrent(state, SimulationParameterKeys.ArmyDestruction.TransportCommandCost);
            int? primary = OwnedArmyLifecycleAuxiliaries.CurrentPlayerPrimaryDistrict(state, world.Context);
            if (commandCost is null || primary is not int districtId ||
                !state.Districts.TryGetValue(districtId, out var district) || district.OrdersRemaining is not int remaining)
                return null;
            //The first player-command leaf uses its actual primary district.
            //Core's existing source-district reservation check applies only
            //when it names that same receiver. Delegated wrapper is separate.
            if (state.Cities[order.SourceCityId].DistrictId != districtId ||
                remaining - CommandBookAvailability.Reserved(state, districtId) < commandCost.Value) return null;
            var created = projection with
            {
                Troops = fields.Troops16, Supplies = fields.Supplies60, Morale = fields.Morale1A,
                WoundedTroops = fields.Wounded18, FormationId = fields.Formation1B, TacticIds = [],
                NativeArmyOrigin = new(TransportCreationRevision, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language))
                    { NativeSlotIndex = fields.NativeSlot },
                NativeArmyConfiguration = new(fields.Formation1B, fields.Formation1C, fields.Action37, fields.Category40),
                TraitRoster = ArmyTraitSourceAdapter.Decode(new ushort[36]),
                NativeTacticRuntime = new(2, fields.Flags26, 0, 0, 0, 0, 0, 0, new sbyte[6], new byte[6], null, clock.UpdateOrdinal),
                NativeMorale1A = fields.Morale1A, NativeSavedRegionHex152 = fields.SavedHex152,
                PositionKnowledge = ArmyPositionKnowledge.Confirmed, NativeDirection36 = arena.Slots[fields.NativeSlot].Direction36,
                NativeMapUiByte13C = fields.MapUiByte13C,
                NativeCommandControl140 = arena.Slots[fields.NativeSlot].CommandControl140,
                NativeRouteHold56 = fields.Field56, NativeRouteGoal1F8 = arena.Slots[fields.NativeSlot].RouteGoal1F8,
                OverrideTarget = fields.OverrideTarget,
                CarriedStores = new(fields.Gold5C, fields.Supplies60),
                NativeHomeSequence28 = fields.HomeSequence28,
                TransportRoute = TransportRouteSourceAdapter.Decode(fields.Waypoints2C),
                March = new(null, null, ArmyMarchSourceAdapter.DecodeDestination(fields.InitialNextHex48))
            };
            var registered = OwnedArmyLifecycleCurrent.PrepareTransportCreationAdmission(state, created,
                _definitions, fields.TargetKind38, fields.TargetIdentity3A);
            if (registered is null) return null;
            CityState source = state.Cities[stocks.SourceCityPhysical];
            var result = (state with { Cities = new(state.Cities) { [source.Id] = source with
                    { Troops = stocks.TroopsAfter, Gold = stocks.GoldAfter, Supplies = stocks.SuppliesAfter } },
                Officers = new(state.Officers) { [person.Id] = person with { Deployed = true,
                    Behavior = OfficerBehaviorSourceAdapter.WithSourceFlags(person.Behavior, OfficerBehaviorSourceAdapter.MarkReturn(flags)),
                    LocationState = location with { Position = OfficerLocationSourceAdapter.DecodePosition(fields.CommanderSecondE4,
                        new ArmyReceiverSourceDirectory(registered.Lifecycle)) } } },
                Districts = new(state.Districts) { [districtId] = district with
                    { OrdersRemaining = Math.Clamp(unchecked(remaining - commandCost.Value), 0, 200) } },
                Units = new(state.Units) { [created.Id] = created },
                ArmyMapOccupancy = registered.Occupancy,
                ResourceState = TransportRouteSourceAdapter.Write(registered.ResourceState, created.Id,
                    fields.Waypoints2C, fields.Choice41) }).WithArmyLifecycle(registered.Lifecycle);
            var currentRoute = CaptureWritableTransportRouteCurrent(result);
            var route = TransportArmyCreationRouteSource.Prepare(result, currentRoute, created);
            if (route.Receipt is not { } initialRoute) return null;
            var routed = created with { March = created.March! with { NextStep =
                ArmyMarchSourceAdapter.DecodeDestination(initialRoute.WrapperNextHex48Write ?? fields.InitialNextHex48) } };
            result = (result with { Units = new(result.Units) { [created.Id] = routed } }).WithArmyLifecycle(result.ReadArmyLifecycle()! with {
                    Indexes = currentRoute.ReadOwnedCurrentListPool()!.ExportOwnedState() });
            return new(ArmyIdentityAllocation.Commit(result, created.Id), TransportCreationRevision);
        }
        catch (InvalidDataException) { return null; }
    }
}
