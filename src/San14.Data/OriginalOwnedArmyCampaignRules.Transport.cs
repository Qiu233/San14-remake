using San14.Core;

namespace San14.Data;

public sealed partial class OriginalOwnedArmyCampaignRules : IPlayableTransportCreationRules
{
    public const string TransportCreationRevision = "san14:ce-owned-2D8FE0-city-transport-20261008";

    /// <summary>Actual City type2 creator, registration and CTroops publication.
    /// The actual2009B0 initial route48 tail shares the candidate's pool.
    /// Full source movement cadence and warning callbacks remain separate.</summary>
    public PlayableTransportCreationResolution? CreateTransport(GameState state,
        DispatchTransportOrder order, UnitState projection)
    {
        if (projection.Mission?.Kind != UnitMissionKind.Transport || state.NativeClock is not { } clock ||
            state.NativeArmyLifecycle is not { } arena ||
            !state.Officers.TryGetValue(order.OfficerId, out var person) || person.Deployed ||
            person.NativeAbilityRuntime is not { Flags196: ushort flags } ability ||
            person.NativeLocationRuntime is not { } location || state.Units.ContainsKey(projection.Id)) return null;
        try
        {
            var world = CaptureOwnedArmyReturnWorld(state);
            var packet = OriginalTransportArmyPacketSource.TryComposeCity(state, world.Context,
                new(order.OfficerId, order.SourceCityId, order.DestinationCityId, order.Troops, order.Gold, order.Supplies,
                    order.Arrival == TransportArrivalDisposition.ReturnToSource));
            if (packet.Packet is not { } selected) return null;
            var prefix = OriginalTransportArmyCreationSource.FromLoadedSource(_creation.FormationWriterSource)
                .Prepare(state, world.Context, selected);
            if (prefix.Receipt is not { GovernorUpdateReached: false } receipt ||
                receipt.Fields.HomeSequence28 is null || receipt.FieldOwnersMissing.Count != 0) return null;
            var fields = receipt.Fields; var stocks = receipt.Resources;
            if (fields.CurrentHex2A != projection.HexId) return null;
            int? commandCost = OriginalArmyDestructionParameterSeed.ReadCurrentTc(state, 0x1A4947C);
            int? primary = OriginalOwnedArmyLifecycleAuxiliaries.CurrentPlayerPrimaryDistrict(state, world.Context);
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
                NativeArmyOrigin = new(TransportCreationRevision, Seed.Language, Seed.Source.Sha256)
                    { NativeSlotIndex = fields.NativeSlot },
                NativeArmyConfiguration = new(fields.Formation1B, fields.Formation1C, fields.Action37, fields.Category40)
                    { TraitSlots64 = new ushort[36] },
                NativeTacticRuntime = new(2, fields.Flags26, 0, 0, 0, 0, 0, 0, new sbyte[6], new byte[6], null, clock.UpdateOrdinal),
                NativeMorale1A = fields.Morale1A, NativeSavedRegionHex152 = fields.SavedHex152,
                NativeCurrentHex2A = fields.CurrentHex2A, NativeDirection36 = arena.Slots[fields.NativeSlot].Direction36,
                NativeMapUiByte13C = fields.MapUiByte13C,
                NativeCommandControl140 = arena.Slots[fields.NativeSlot].CommandControl140,
                NativeRouteHold56 = fields.Field56, NativeRouteGoal1F8 = arena.Slots[fields.NativeSlot].RouteGoal1F8,
                NativeOverrideTarget = fields.OverrideTarget,
                NativeReturnResources = new(fields.Gold5C, fields.Supplies60),
                NativeHomeSequence28 = fields.HomeSequence28,
                NativeTransport = new(fields.Waypoints2C.ToArray(), fields.Choice41, fields.InitialNextHex48, null)
            };
            var registered = OriginalOwnedArmyLifecycleCurrent.PrepareTransportCreationAdmission(state, created,
                _definitions, fields.TargetKind38, fields.TargetIdentity3A);
            if (registered is null) return null;
            CityState source = state.Cities[stocks.SourceCityPhysical];
            var result = state with
            {
                Cities = new(state.Cities) { [source.Id] = source with
                    { Troops = stocks.TroopsAfter, Gold = stocks.GoldAfter, Supplies = stocks.SuppliesAfter } },
                Officers = new(state.Officers) { [person.Id] = person with { Deployed = true,
                    NativeAbilityRuntime = ability with { Flags196 = (ushort)(flags | 1) },
                    NativeLocationRuntime = location with { Second11C = fields.CommanderSecondE4 } } },
                Districts = new(state.Districts) { [districtId] = district with
                    { OrdersRemaining = Math.Clamp(unchecked(remaining - commandCost.Value), 0, 200) } },
                Units = new(state.Units) { [created.Id] = created }, NativeArmyLifecycle = registered
            };
            var currentRoute = CaptureWritableTransportRouteCurrent(result);
            var route = OriginalTransportArmyCreationRouteSource.Prepare(result, currentRoute, created);
            if (route.Receipt is not { } initialRoute) return null;
            var routed = created with { NativeTransport = created.NativeTransport! with {
                WrapperNextHex48 = initialRoute.WrapperNextHex48Write,
                CurrentNextHex48 = initialRoute.WrapperNextHex48Write ?? fields.InitialNextHex48 } };
            result = result with { Units = new(result.Units) { [created.Id] = routed },
                NativeArmyLifecycle = result.NativeArmyLifecycle! with {
                    Pool = currentRoute.ReadOwnedCurrentListPool()!.ExportOwnedState() } };
            StateValidator.Validate(result);
            return new(result, TransportCreationRevision);
        }
        catch (InvalidDataException) { return null; }
    }
}
