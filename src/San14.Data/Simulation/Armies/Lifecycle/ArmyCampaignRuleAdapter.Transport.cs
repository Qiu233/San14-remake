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
        => CreateTransportCore(state, order.OfficerId, order.SourceCityId, projection,
            current => TransportArmyPacketSource.TryComposeCity(state, current,
                new(order.OfficerId, order.SourceCityId, order.DestinationCityId, order.Troops, order.Gold, order.Supplies,
                    order.Arrival == TransportArrivalDisposition.ReturnToSource)));

    private PlayableTransportCreationResolution? CreateTransportCore(GameState state,
        int officerId, int sourceCityId, UnitState projection,
        Func<SourceWorldContext, TransportArmyPacketResult> composePacket,
        Action<string>? unresolved = null)
    {
        state = ArmyIdentityAllocation.Initialize(state);
        if (state.ArmyIdentities!.Candidate != projection.Id) return Missing("current.transport_identity_candidate");
        if (projection.Mission?.Kind != UnitMissionKind.Transport || state.NativeClock is not { } clock ||
            state.ReadArmyLifecycle() is not { } arena ||
            !state.Officers.TryGetValue(officerId, out var person) || person.Deployed ||
            OfficerBehaviorSourceAdapter.EncodeFlags(person.Behavior) is not ushort flags ||
            _definitions.ResolveOfficerSource(person.Id) is null ||
            person.LocationState is not { } location || state.Units.ContainsKey(projection.Id)) return Missing("current.transport_creation_inputs");
        try
        {
            var world = CaptureOwnedArmyReturnWorld(state);
            var packet = composePacket(world.Context);
            if (packet.Packet is not { } selected) return Missing(packet.UnresolvedOwner ?? "current.transport_packet");
            if (selected.CommandOrder is not null && projection.ForceId != selected.ForcePhysical)
                return Missing("current.transport_Force_domain_projection");
            if (selected.CommandOrder is not null && (selected.Dwords[11] == 5
                ? projection.Mission.DestinationCityId != selected.Dwords[12]
                : selected.Dwords[11] == 9
                    ? projection.DestinationHexId != selected.Dwords[12]
                    : selected.Dwords[11] == 6 &&
                        (!state.Gates.TryGetValue(selected.Dwords[12] + 51, out var targetGate) ||
                         targetGate.HexId != projection.DestinationHexId)))
                return Missing("current.transport_target_domain_projection");
            var prefix = TransportArmyCreationSource.FromLoadedSource(_creation.FormationWriterSource)
                .Prepare(state, world.Context, selected);
            if (prefix.Receipt is not { } receipt) return Missing(prefix.UnresolvedOwner ?? "current.transport_creation_prefix");
            if (receipt.GovernorUpdateReached) return Missing("current.transport_governor_update");
            if (receipt.Fields.HomeSequence28 is null || receipt.FieldOwnersMissing.Count != 0)
                return Missing(receipt.FieldOwnersMissing.FirstOrDefault() ?? "current.transport_HomeSequence28");
            var fields = receipt.Fields; var stocks = receipt.Resources;
            if (fields.CurrentHex2A != projection.HexId) return Missing("current.transport_source_Hex_projection");
            int? commandCost = ArmyDestructionParameterSeed.ReadCurrent(state, SimulationParameterKeys.ArmyDestruction.TransportCommandCost);
            int? primary = OwnedArmyLifecycleAuxiliaries.CurrentPlayerPrimaryDistrict(state, world.Context);
            if (commandCost is null || primary is not int districtId ||
                !state.Districts.TryGetValue(districtId, out var district) || district.OrdersRemaining is not int remaining)
                return Missing("current.transport_command_books");
            //The first player-command leaf uses its actual primary district.
            //The delegated wrapper is separate from this first-player leaf.
            if (state.Cities[sourceCityId].DistrictId != districtId)
                return Missing("current.transport_delegated_source_wrapper");
            if (remaining - CommandBookAvailability.Reserved(state, districtId) < commandCost.Value)
                return Missing("transport.insufficient_orders");
            var created = projection with
            {
                Troops = fields.Troops16, Supplies = fields.Supplies60, MoraleState = new(fields.Morale1A, fields.Morale1A),
                WoundedTroops = fields.Wounded18, Formations = ArmyConfigurationSourceAdapter.DecodeFormations(fields.Formation1B, fields.Formation1B, fields.Formation1C), TacticIds = [],
                NativeArmyOrigin = new(TransportCreationRevision, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language))
                    { NativeSlotIndex = fields.NativeSlot },
                FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(fields.Action37),
                TraitRoster = ArmyTraitSourceAdapter.Decode(new ushort[36]),
                NativeTacticRuntime = new(2, fields.Flags26, 0, 0, 0, 0, 0, 0, new sbyte[6], new byte[6], null, clock.UpdateOrdinal),
                NativeSavedRegionHex152 = fields.SavedHex152,
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
            if (registered is null) return Missing("current.transport_creation_registration");
            //2269E0/type2 creation has just written the actual word14. Give
            //the newly created march its own current credit; this is not an
            //old-save fallback from a source header after later movement.
            if (registered.Lifecycle.Slots[fields.NativeSlot].Priority14 is not ushort initialCredit)
                return Missing("current.transport_initial_credit14");
            created = created with { March = created.March! with { MovementCredit = initialCredit } };
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
                ResourceState = ArmyConfigurationSourceAdapter.Write(
                    TransportRouteSourceAdapter.Write(registered.ResourceState, created.Id, fields.Waypoints2C, fields.Choice41),
                    created.Id, fields.Formation1B, fields.Formation1C, fields.Action37, fields.Category40) }).WithArmyLifecycle(registered.Lifecycle);
            var currentRoute = CaptureWritableTransportRouteCurrent(result);
            var route = TransportArmyCreationRouteSource.Prepare(result, currentRoute, created);
            if (route.Receipt is not { } initialRoute) return Missing(route.UnresolvedOwner ?? "current.transport_initial_route");
            var routed = created with { March = created.March! with { NextStep =
                ArmyMarchSourceAdapter.DecodeDestination(initialRoute.WrapperNextHex48Write ?? fields.InitialNextHex48) } };
            result = (result with { Units = new(result.Units) { [created.Id] = routed } }).WithArmyLifecycle(result.ReadArmyLifecycle()! with {
                    Indexes = currentRoute.ReadOwnedCurrentListPool()!.ExportOwnedState() });
            return new(ArmyIdentityAllocation.Commit(result, created.Id), TransportCreationRevision);
        }
        catch (InvalidDataException error) { return Missing(error.Message); }
        PlayableTransportCreationResolution? Missing(string owner) { unresolved?.Invoke(owner); return null; }
    }
}
