using San14.Core;

namespace San14.Data;

public sealed record OwnedArmyOrdinaryCityReturnResult(GameState? State,
    IReadOnlyList<string> SourceEffectOrder, string? UnresolvedOwner)
{
    public IReadOnlyList<PersonAssignmentPrecallResult> DistrictTransferAssociations { get; init; } = [];
}

/// <summary>Actual owned ordinary29F220 city return followed by deferred
/// numeric28E6D0 cleanup. No native Army/manager presentation receiver is
/// created by this client. This adapter consumes current saved inputs and
/// reports the first source branch that lacks an owner.</summary>
public sealed class OwnedArmyOrdinaryCityReturnStage(ArmyCampaignRuleAdapter package,
    OwnedArmyGovernorComparatorInputs governorInputs) : IPlayableCityEntryStateRules
{
    public const string Revision = "san14:ce-owned-army-ordinary-city-return-20261007";
    public const string TransportRevision = "san14:ce-owned-transport-city-enter-20261008";
    public bool RequiresCurrentState(GameState state, UnitState unit, CityState city) =>
        unit.NativeArmyOrigin is not null;
    public bool? IsEntryPositionAllowed(GameState state, UnitState unit, CityState city) =>
        unit.NativeArmyOrigin is not null && unit.Mission?.Kind == UnitMissionKind.Transport
            ? package.ReadTransportCityEntryPosition(state, unit, city).Eligible
            : city.HexId == unit.HexId;
    public PlayableCityEntryStateResolution? Return(GameState state, UnitState unit, CityState city)
    {
        var result = TryApply(state,unit.Id,city.Id);
        return result.State is { } next ? new(next,
            unit.Mission?.Kind == UnitMissionKind.Transport ? TransportRevision : Revision) : null;
    }
    public OwnedArmyOrdinaryCityReturnResult TryApply(GameState state, int unitId, int cityId)
    {
        if (!state.Units.TryGetValue(unitId, out var unit) || !state.Cities.TryGetValue(cityId, out var city) ||
            unit.NativeArmyOrigin?.NativeSlotIndex is not int physical ||
            state.NativeArmyLifecycle is not { ArmyGroups: not null, ArmyCautions: not null } ||
            (city.HexId != unit.HexId && IsEntryPositionAllowed(state, unit, city) != true) || city.ForceId != unit.ForceId)
            return new(null, [], "current.owned_ordinary_city_entry");
        try
        {
            var world = package.CaptureOwnedArmyReturnWorld(state);
            var result = ArmyOrdinaryReturnRuntime.PrepareCurrentResourceOutputs(new(physical), new(BaseKind.City, cityId), 0,
                world.Context, world.Pool, new Queries(state, governorInputs), new(false, false), new(package, state, new(false)),
                unit.Mission?.Kind == UnitMissionKind.Transport ? unit.NativeTacticRuntime?.Flags26 : null);
            if (result.Packet is not { } packet) return new(null, [], result.UnresolvedOwner);
            var returned = world.CommitPrepared(state, packet);
            if (returned is null) return new(null, packet.SourceEffectOrder, "current.ordinary_return_publication");
            returned = OwnedArmyLifecycleDetachedInputs.PublishCurrent(returned, world.Context);
            var cleaned = package.ConsumeArmyLifecyclePending(returned);
            return new(cleaned, packet.SourceEffectOrder, cleaned is null ? "current.deferred_cleanup_owner" : null)
            { DistrictTransferAssociations = cleaned is null ? [] : packet.RulerTailAssociationUpdates };
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
    }
    private sealed class Queries(GameState state, OwnedArmyGovernorComparatorInputs inputs)
        : IArmyReturnGovernorCurrentQueries
    {
        public ArmyReturnGovernorComparatorFields? ReadCurrentComparatorFields(int personPhysical) =>
            inputs.ReadCurrentComparatorFields(state, personPhysical);
    }
}
