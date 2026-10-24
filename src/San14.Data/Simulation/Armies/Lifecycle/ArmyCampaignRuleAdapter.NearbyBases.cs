using San14.Core;

namespace San14.Data;

public sealed record BaseNearbyEnemyRefresh(int LocationId, bool HasNearbyEnemyArmy, int? EnemyArmyPhysical);
public sealed record BaseNearbyEnemyRefreshResult(GameState? State, BaseNearbyEnemyRefresh[] Receipts, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The second call of source stage26. Reads the actual Base
    /// registry after retirement and overwrites only its nearby-enemy bit.
    /// It does not infer enemies from the asymmetric route hostility query.</summary>
    public BaseNearbyEnemyRefreshResult TryRefreshBaseNearbyArmies(GameState state) =>
        TryRefreshBaseNearbyArmiesCurrent(state, null);

    internal BaseNearbyEnemyRefreshResult RefreshBaseNearbyArmiesCurrent(GameState state, SourceWorldContext current)
        => TryRefreshBaseNearbyArmiesCurrent(state, current);

    private BaseNearbyEnemyRefreshResult TryRefreshBaseNearbyArmiesCurrent(GameState state, SourceWorldContext? borrowed)
    {
        try
        {
            var current = borrowed ?? CaptureWritableTransportRouteCurrent(state);
            var cities = new Dictionary<int, CityState>(state.Cities);
            var gates = new Dictionary<int, GateState>(state.Gates);
            bool? unassignedCity = state.UnassignedCityHasNearbyEnemyArmy;
            bool? unassignedGate = state.UnassignedGateHasNearbyEnemyArmy;
            var receipts = new List<BaseNearbyEnemyRefresh>();
            foreach (var entry in current.ReadRegisteredBases() ?? throw OccupationMissing("base_nearby_registry"))
            {
                var receiver = entry ?? throw OccupationMissing("base_nearby_receiver");
                int location = OccupationKnown(current.ReadBaseObjectLink(receiver), "base_nearby_location");
                var center = CurrentGateMapQueries.FindHex(receiver, current);
                int physical = OccupationKnown(center.PhysicalSlot, "base_nearby_Hex");
                int near = location < 52 ? 2 : 1;
                int? hostile = null;
                foreach (int radius in new[] { near, near + 1 })
                {
                    foreach (int h in TacticRemakeCurrentContext.NativeHexOrder(physical, radius).Skip(1))
                    {
                        var hex = current.ReadHexTableReference(h) ?? throw OccupationMissing("base_nearby_candidate_Hex");
                        int slot = unchecked((short)OccupationKnown(current.ReadHexRaw1C(hex), "base_nearby_occupant"));
                        if (slot is < 1 or > 500) continue;
                        var army = current.ReadArmyUnitTableReference(slot) ?? throw OccupationMissing("base_nearby_Army");
                        if (!OccupationKnown(current.ReadArmyUnitNativeValidity(army), "base_nearby_Army_validity") ||
                            OccupationKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, slot), "base_nearby_Army_excluded")) continue;
                        int force = OccupationKnown(current.ReadBaseForceSlot(receiver), "base_nearby_Force");
                        var own = current.ReadForceTableReference(force <= 51 ? force : 0) ?? throw OccupationMissing("base_nearby_Force_pointer");
                        var other = current.ReadArmyUnitForceReference(army) ?? throw OccupationMissing("base_nearby_Army_Force");
                        if (!OccupationKnown(ArmyMarchDevelopmentSource.ReadFriendlyRelation(state, own, other), "base_nearby_relation"))
                        { hostile = slot; break; }
                    }
                    if (hostile.HasValue) break;
                }
                //No test of the previous bit is required: source2FFA20 first
                //clears it even when an older save has not owned this value.
                if (receiver.PhysicalSlot == 0 && receiver.Kind == BaseKind.City)
                    unassignedCity = hostile.HasValue;
                else if (receiver.PhysicalSlot == 0 && receiver.Kind == BaseKind.Gate)
                    unassignedGate = hostile.HasValue;
                else if (receiver.Kind == BaseKind.City && cities.TryGetValue(location, out var city))
                    cities[location] = city with { HasNearbyEnemyArmy = hostile.HasValue };
                else if (receiver.Kind == BaseKind.Gate && gates.TryGetValue(location, out var gate))
                    gates[location] = gate with { HasNearbyEnemyArmy = hostile.HasValue };
                else throw OccupationMissing("base_nearby_domain_owner");
                receipts.Add(new(location, hostile.HasValue, hostile));
            }
            return new(state with { Cities = cities, Gates = gates,
                UnassignedCityHasNearbyEnemyArmy = unassignedCity,
                UnassignedGateHasNearbyEnemyArmy = unassignedGate }, receipts.ToArray(), null);
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
    }
}
