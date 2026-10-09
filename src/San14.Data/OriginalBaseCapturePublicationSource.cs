using San14.Core;

namespace San14.Data;

/// <summary>Project this operation's actual Base/Area/Hex writes. Ordinary
/// Gate capture changes Area35 and selected footprint/rings; it is not an
/// all-Area takeover and never derives native owners from a Core force count.</summary>
internal static class OriginalBaseCapturePublicationSource
{
    internal static GameState Publish(GameState state, OriginalEventInitializationContext current,
        WorldIndexContext pool)
    {
        var working = OriginalOwnedArmyReturnCurrentFields.PublishCurrent(state, current);
        working = OriginalOwnedArmyLifecycleDetachedInputs.PublishCurrent(working, current);
        working = OriginalPersonAssignmentPrecallInputs.PublishCurrent(working, current);
        working = OriginalBaseCaptureCurrentInputs.PublishCurrent(working, current);
        working = OriginalCaptureBaseThreatInputs.PublishCurrent(working, current);
        var areas = new Dictionary<int, AreaState>(working.Areas);
        foreach (var area in working.Areas.Values)
        {
            int city = K(current.ReadAreaCitySlot(new(area.Id)), "Area35");
            if (city is < 1 or > 51 || !working.Cities.ContainsKey(city)) throw Missing("ordinary_Area35_city");
            int force = K(current.ReadAreaCurrentForceSlot(new(area.Id)), "Area_Hex14");
            areas[area.Id] = area with { CityId = city, ForceId = force == 0 ? null : force };
        }
        var cities = new Dictionary<int, CityState>(working.Cities);
        foreach (var city in working.Cities.Values)
        {
            int force = K(current.ReadObjectForceSlot13(new(city.Id)), "City_Object13");
            cities[city.Id] = city with { ForceId = force == 0 ? null : force,
                Durability = K(current.ReadObjectRawField14(new(city.Id)), "City_Object14"),
                AreaIds = areas.Values.Where(row => row.CityId == city.Id).Select(row => row.Id).OrderBy(id => id).ToArray() };
        }
        var gates = new Dictionary<int, GateState>(working.Gates);
        foreach (var gate in working.Gates.Values)
        {
            var target = new OriginalBaseReference(OriginalBaseKind.Gate, gate.Id - 51);
            int force = K(current.ReadBaseForceSlot(target), "Gate_B0");
            int district = K(current.ReadBaseDistrictSlot(target), "Gate78");
            int governor = K(current.ReadArmyReturnBaseGovernor2E(target), "Gate2E");
            gates[gate.Id] = gate with { ForceId = force == 0 ? null : force, DistrictId = district == 0 ? null : district,
                GovernorId = governor == 0 ? null : governor,
                Durability = K(current.ReadObjectRawField14(new(gate.Id)), "Gate_Object14") };
        }
        var territory = new Dictionary<int, TerritoryState>(working.Territory);
        foreach (var cell in working.Territory.Values)
        {
            int force = K(current.ReadHexForceSlot(new(cell.Id)), "Hex14");
            if ((cell.ForceId ?? 0) != force) territory[cell.Id] = cell with { ForceId = force == 0 ? null : force };
        }
        var forceFields = working.NativeForceUnregisterCurrent;
        if (forceFields is not null)
        {
            var windows = new Dictionary<int, byte?[]>(forceFields.DistrictWindows10);
            foreach (int slot in windows.Keys)
                windows[slot] = Enumerable.Range(0x10, 28).Select(at => current.ReadDistrictRawByte(slot, at)).ToArray();
            forceFields = forceFields with { DistrictWindows10 = windows };
        }
        return working with { Cities = cities, Gates = gates, Areas = areas, Territory = territory,
            ArmyMapOccupancy = current.ExportArmyMapOccupancy(), NativeForceUnregisterCurrent = forceFields, PendingCombatSettlements = current.OwnedDestructionQueue, NativeArmyLifecycle = working.NativeArmyLifecycle! with
            { Indexes = pool.ExportOwnedState() } };
    }
    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("current.Base_capture_publication." + field);
}
