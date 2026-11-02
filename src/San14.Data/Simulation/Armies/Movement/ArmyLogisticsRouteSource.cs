using San14.Core;

namespace San14.Data;

public sealed record ArmyLogisticsRouteResult(int[]? Hexes, int? Cost, int Expanded,
    string? UnresolvedOwner);

/// <summary>The current logistics route from Army position to its commander's
/// actual Home Base. Flags4 select logistics passability and raw edge costs;
/// the wrapper's scratch slot2 does not change the movement rule into mode1.</summary>
internal static class ArmyLogisticsRouteSource
{
    internal static ArmyLogisticsRouteResult Read(GameState state, SourceWorldContext current, UnitState army)
    {
        try
        {
            int physical = Need(army.NativeArmyOrigin?.NativeSlotIndex, "army_receiver");
            var receiver = new ArmyUnitReference(physical);
            if (!Need(current.ReadArmyUnitNativeValidity(receiver), "army_validity")) return new([], 0, 0, null);
            ushort commander = Need(current.ReadArmyUnitCommanderId(receiver), "commander");
            var person = current.ReadPersonLookupReference(commander <= 6000 ? commander : 0)
                ?? throw Missing("commander_pointer");
            int personPhysical = Need(person.PhysicalSlot, "commander_receiver");
            ushort home = Need(current.ReadPersonNativeValidity(personPhysical), "commander_validity")
                ? Need(current.ReadPersonFirstLocation(personPhysical), "commander_home") : (ushort)0;
            var basis = home is >= 1 and <= 51
                ? current.ReadCityTableReference(home) : home is >= 52 and <= 61
                ? current.ReadGateTableReference(home - 51) : current.ReadCityTableReference(0);
            if (basis is null) throw Missing("home_pointer");
            if (!Need(current.ReadBaseNativeValidity(basis), "home_validity")) return new([], 0, 0, null);
            ushort link = Need(current.ReadBaseObjectLink(basis), "home_object");
            var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0) ?? throw Missing("home_object_pointer");
            if (!Need(current.ReadObjectNativeValidity(obj), "home_object_validity")) return new([], 0, 0, null);
            ushort goal = Need(current.ReadObjectHexIndex(obj), "home_hex");
            ushort start = Need(current.ReadArmyUnitRawHex2A(receiver), "army_hex");
            if (ArmyMarchSourceAdapter.DecodeDestination(start).Kind == ArmyMarchDestinationKind.None ||
                ArmyMarchSourceAdapter.DecodeDestination(goal).Kind == ArmyMarchDestinationKind.None ||
                !current.ContainsMapHex(start) || !current.ContainsMapHex(goal)) return new([], 0, 0, null);
            // This uses the same tree ordering and cumulative edge-cost return
            // as 183C00/1845B0, with a distinct logistics query descriptor.
            var query = ArmyRouteSearch.LogisticsQueries(state, current, army, start, goal, physical);
            int[] route = ArmyRouteSearch.Search(query, out int expanded, out int cost);
            return new(route, cost, expanded, null);
        }
        catch (ArmyRouteSearch.MissingOwner missing) { return new(null, null, 0, missing.Message); }
        catch (InvalidDataException missing) { return new(null, null, 0, missing.Message); }
    }

    private static T Need<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static InvalidDataException Missing(string name) => new("food_upkeep.logistics_route." + name);
}

internal static partial class ArmyRouteSearch
{
    internal static Queries LogisticsQueries(GameState state, SourceWorldContext current,
        UnitState army, int start, int home, int receiver) =>
        new(state, current, new LiveMovement(state, current, army), start, home, 5, 0, receiver, 4);
}
